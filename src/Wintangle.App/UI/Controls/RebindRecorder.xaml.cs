using System.Windows;
using System.Windows.Controls;
using Wintangle.App.Hooks;
using Wintangle.Core.Hotkeys;

namespace Wintangle.App.UI.Controls;

/// <summary>
/// Inline hotkey recorder: shows the current combo with a Record button.
/// While recording, the shared keyboard hook swallows modified combos and
/// raises <see cref="KeyboardHook.KeyCaptured"/> on the hook thread — this
/// control marshals captures to the UI thread, validates them, and raises
/// <see cref="ComboCaptured"/> with the new binding.
/// </summary>
/// <remarks>
/// <para>Recording state: a bare Escape cancels; a bare modifier-only key is
/// ignored (the hook filters those before raising anyway); an invalid or
/// duplicate combo shows an inline error and keeps waiting so the user can
/// press another combo or Esc.</para>
/// <para>If the host window closes mid-recording, call <see cref="CancelRecording"/>
/// so <see cref="KeyboardHook.RecordingMode"/> is reset — the hook must never
/// stay armed after the recorder disappears.</para>
/// </remarks>
public partial class RebindRecorder : UserControl
{
    private KeyboardHook? _hook;
    private bool _isRecording;
    private Hotkey _currentHotkey;

    public RebindRecorder()
    {
        InitializeComponent();
        UpdateIdleUi();
    }

    /// <summary>The hook used for recording (assigned by the host).</summary>
    internal KeyboardHook? Hook
    {
        get => _hook;
        set
        {
            if (_hook != null)
            {
                _hook.KeyCaptured -= OnKeyCaptured;
            }

            _hook = value;

            if (_hook != null)
            {
                _hook.KeyCaptured += OnKeyCaptured;
            }
        }
    }

    /// <summary>
    /// Optional host validator (e.g. the duplicate-combo check). Returns an
    /// error message or null. Runs on the UI thread after the Core validation.
    /// </summary>
    public Func<Hotkey, string?>? ValidateCombo { get; set; }

    /// <summary>Raised (UI thread) after a valid combo is captured and applied.</summary>
    public event EventHandler<Hotkey>? ComboCaptured;

    /// <summary>Raised (UI thread) when recording is cancelled (Esc or Cancel).</summary>
    public event EventHandler? RecordingCancelled;

    /// <summary>
    /// Raised (UI thread) just before this recorder arms the shared hook. The
    /// host uses it to cancel any other active recording first — the hook has
    /// a single <see cref="KeyboardHook.RecordingMode"/> flag, so only one
    /// recorder may capture at a time.
    /// </summary>
    public event EventHandler? RecordingStarted;

    /// <summary>The combo currently displayed / last captured.</summary>
    public Hotkey CurrentHotkey => _currentHotkey;

    /// <summary>Replaces the displayed combo (idle state only; ignored while recording).</summary>
    public void SetHotkey(Hotkey hotkey)
    {
        _currentHotkey = hotkey;
        if (!_isRecording)
        {
            UpdateIdleUi();
        }
    }

    /// <summary>
    /// Force-stops recording without applying a combo. Used when the settings
    /// window closes mid-recording so the hook is never left armed.
    /// </summary>
    public void CancelRecording()
    {
        if (!_isRecording)
        {
            return;
        }

        StopRecordingCore(cancel: true);
    }

    private void RecordButton_Click(object sender, RoutedEventArgs e)
    {
        if (_hook == null || _isRecording)
        {
            return;
        }

        // Let the host cancel any other active recording first: the shared
        // RecordingMode flag must not be yanked out from under this recorder.
        RecordingStarted?.Invoke(this, EventArgs.Empty);

        _isRecording = true;
        _hook.RecordingMode = true;
        ShowRecordingUi();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => StopRecordingCore(cancel: true);

    private void OnKeyCaptured(byte vk, KeyModifiers mods)
    {
        // The hook raises on its own thread — marshal to the UI thread.
        if (Dispatcher.CheckAccess())
        {
            HandleCapture(vk, mods);
            return;
        }

        try
        {
            Dispatcher.BeginInvoke(() => HandleCapture(vk, mods));
        }
        catch (InvalidOperationException)
        {
            // Dispatcher can reject work once the app starts shutting down;
            // the recorder is being torn down anyway.
        }
    }

    private void HandleCapture(byte vk, KeyModifiers mods)
    {
        if (!_isRecording)
        {
            return; // stale capture after Cancel/Close
        }

        var hotkey = new Hotkey(vk, mods);

        if (RebindValidator.IsCancel(hotkey))
        {
            StopRecordingCore(cancel: true);
            return;
        }

        // The hook never raises modifier-only keys, but stay defensive.
        if (RebindValidator.IsModifierKey(vk))
        {
            return; // keep waiting
        }

        var error = RebindValidator.Validate(hotkey) ?? ValidateCombo?.Invoke(hotkey);
        if (error != null)
        {
            ShowError(error);
            return; // keep recording: try another combo or press Esc
        }

        _currentHotkey = hotkey;
        StopRecordingCore(cancel: false);
        ComboCaptured?.Invoke(this, hotkey);
    }

    private void StopRecordingCore(bool cancel)
    {
        _isRecording = false;
        if (_hook != null)
        {
            _hook.RecordingMode = false;
        }

        if (cancel)
        {
            RecordingCancelled?.Invoke(this, EventArgs.Empty);
        }

        UpdateIdleUi();
    }

    private void UpdateIdleUi()
    {
        ComboText.Text = HotkeyLabels.Format(_currentHotkey);
        ComboText.Visibility = Visibility.Visible;
        RecordingText.Visibility = Visibility.Collapsed;
        RecordButton.Visibility = Visibility.Visible;
        CancelButton.Visibility = Visibility.Collapsed;
        ErrorText.Visibility = Visibility.Collapsed;
    }

    private void ShowRecordingUi()
    {
        ComboText.Visibility = Visibility.Collapsed;
        RecordingText.Visibility = Visibility.Visible;
        RecordButton.Visibility = Visibility.Collapsed;
        CancelButton.Visibility = Visibility.Visible;
        ErrorText.Visibility = Visibility.Collapsed;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
