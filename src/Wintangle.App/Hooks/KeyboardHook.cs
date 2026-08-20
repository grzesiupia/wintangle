using System.Runtime.InteropServices;
using System.Threading;
using Wintangle.App.Interop;
using Wintangle.App.Services;
using Wintangle.Core.Hotkeys;

namespace Wintangle.App.Hooks;

/// <summary>
/// Global low-level keyboard hook (WH_KEYBOARD_LL) running on a dedicated
/// STA thread with its own message pump.
/// </summary>
/// <remarks>
/// Matched combinations are swallowed (return 1) and dispatched synchronously
/// on the hook thread. Unmatched keys fall through via CallNextHookEx.
/// Shutdown posts WM_QUIT to the hook thread's queue and unhooks.
/// </remarks>
internal sealed class KeyboardHook
{
    /// <summary>
    /// GC root for the native callback: the delegate must stay alive for as
    /// long as the hook is installed.
    /// </summary>
    private static readonly HookApi.LowLevelKeyboardProc s_proc = HookProc;

    /// <summary>Current hook instance; the static proc dispatches through this.</summary>
    private static KeyboardHook? s_instance;

    private readonly HotkeyMatcher _matcher;

    /// <summary>
    /// Set by the hook thread once the install attempt finished (success or
    /// failure), so Start() doesn't block on a fixed timeout.
    /// </summary>
    private readonly ManualResetEventSlim _installSignal = new(false);

    private Thread? _thread;

    // Written by the hook thread, read by the UI thread in Stop().
    private volatile uint _threadId;
    private volatile IntPtr _hook = IntPtr.Zero;

    public KeyboardHook(HotkeyTable table)
    {
        _matcher = new HotkeyMatcher(table);
    }

    /// <summary>Current hotkey table (swappable later for live reconfig).</summary>
    public HotkeyTable Table
    {
        get => _matcher.Table;
        set => _matcher.Table = value;
    }

    /// <summary>
    /// When true, modified key combos are swallowed and raised as
    /// <see cref="KeyCaptured"/> instead of being dispatched (used by the
    /// recording UI in a later phase).
    /// </summary>
    private volatile bool _recordingMode;

    public bool RecordingMode
    {
        get => _recordingMode;
        set => _recordingMode = value;
    }

    /// <summary>Raised (on the hook thread) when a matched hotkey fires.</summary>
    public event Action<HotkeyAction>? ActionMatched;

    /// <summary>Raised (on the hook thread) when a combo is captured in recording mode.</summary>
    public event Action<byte, KeyModifiers>? KeyCaptured;

    /// <summary>
    /// Starts the hook thread. Returns true only when the hook thread confirms
    /// the install succeeded; false on install failure. On failure the app
    /// continues without shortcuts (failure is logged).
    /// </summary>
    public bool Start()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        if (_thread != null)
        {
            return true;
        }

        Volatile.Write(ref s_instance, this);
        _installSignal.Reset();

        _thread = new Thread(ThreadMain)
        {
            IsBackground = true,
            Name = "WintangleKeyboardHook",
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();

        // Wait for the hook thread to signal install outcome (success or
        // failure) — no fixed timeout, since the thread always signals. By
        // the time this returns, _threadId is assigned, so Stop() can always
        // post WM_QUIT.
        _installSignal.Wait();

        // _hook is volatile, so the plain read is already a volatile read.
        return _hook != IntPtr.Zero;
    }

    /// <summary>
    /// Stops the hook: posts WM_QUIT to the hook thread, waits for it to
    /// unwind, then unhooks.
    /// </summary>
    public void Stop()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Volatile.Write(ref s_instance, null);

        if (_threadId != 0)
        {
            HookApi.PostThreadMessageW(_threadId, NativeMethods.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        }

        try
        {
            _thread?.Join(2000);
        }
        catch (Exception ex)
        {
            Log.Warn($"KeyboardHook.Stop join failed: {ex.Message}");
        }

        // _hook is volatile, so the plain read is already a volatile read.
        if (_hook != IntPtr.Zero)
        {
            HookApi.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }

        _thread = null;
    }

    private void ThreadMain()
    {
        // Assign _threadId FIRST: Stop() may run as soon as Start() returns
        // and must see a valid id to post WM_QUIT (volatile write observed
        // through the install-signal happens-before edge).
        _threadId = HookApi.GetCurrentThreadId();

        try
        {
            _hook = HookApi.SetWindowsHookExW(NativeMethods.WH_KEYBOARD_LL, s_proc, HookApi.GetModuleHandleW(null), 0);
        }
        catch (Exception ex)
        {
            Log.Error("Keyboard hook install failed", ex);
        }

        // Signal install result (success or failure) so Start() doesn't wait
        // on a fixed timeout.
        _installSignal.Set();

        if (_hook == IntPtr.Zero)
        {
            Log.Error("low-level keyboard hook not installed; shortcuts disabled");
            return;
        }

        try
        {
            while (HookApi.GetMessageW(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                HookApi.TranslateMessage(ref msg);
                HookApi.DispatchMessageW(ref msg);
            }
        }
        catch (Exception ex)
        {
            Log.Error("Keyboard hook message loop error", ex);
        }
        finally
        {
            if (_hook != IntPtr.Zero)
            {
                HookApi.UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
            }

            Log.Info("keyboard hook thread exited");
        }
    }

    private static IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        var instance = Volatile.Read(ref s_instance);
        if (nCode >= 0 && instance != null)
        {
            return instance.ProcessHookEvent(nCode, wParam, lParam);
        }

        return HookApi.CallNextHookEx(instance?._hook ?? IntPtr.Zero, nCode, wParam, lParam);
    }

    private IntPtr ProcessHookEvent(int nCode, IntPtr wParam, IntPtr lParam)
    {
        var msg = (uint)wParam.ToInt64();

        if (msg is not (NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN
            or NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP))
        {
            return HookApi.CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        var kbd = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

        return msg is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN
            ? ProcessKeyDown(kbd, nCode, wParam, lParam)
            : ProcessKeyUp(kbd, nCode, wParam, lParam);
    }

    private IntPtr ProcessKeyDown(in KBDLLHOOKSTRUCT kbd, int nCode, IntPtr wParam, IntPtr lParam)
    {
        if ((kbd.flags & NativeMethods.LLKHF_INJECTED) != 0)
        {
            return HookApi.CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        var vk = (byte)kbd.vkCode;

        // Modifier-only keys are never hotkeys themselves.
        if (IsModifierKey(vk))
        {
            return HookApi.CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        var mods = GetCurrentModifiers();

        if (RecordingMode)
        {
            if (mods == KeyModifiers.None)
            {
                // Bare Escape cancels recording (raised to the recorder, then
                // swallowed so the focused window doesn't react). Any other
                // bare key falls through — recording only swallows modified
                // combos, so typing is never eaten while waiting.
                if (vk == NativeMethods.VK_ESCAPE)
                {
                    KeyCaptured?.Invoke(vk, mods);
                    return Swallow();
                }

                return HookApi.CallNextHookEx(_hook, nCode, wParam, lParam);
            }

            InjectWinComboBreak(mods);
            KeyCaptured?.Invoke(vk, mods);
            return Swallow();
        }

        var result = _matcher.Process(vk, mods);
        switch (result.Kind)
        {
            case HotkeyMatchResultKind.Matched:
                InjectWinComboBreak(mods);
                ActionMatched?.Invoke(result.Action);
                return Swallow();

            case HotkeyMatchResultKind.RepeatSuppressed:
                // Holding the key: auto-repeat arrives with no Win keyup in
                // between, so no fresh F24 break is needed (the previous one
                // still sits between Win down and Win up). But a rapid
                // re-press (release + re-press within the repeat window) has
                // already produced a Win keyup, so the shell would see a clean
                // Win down/up pair with no key between and pop the Start menu.
                // InjectWinComboBreak is a no-op when Win is not held, so it is
                // safe on both paths.
                InjectWinComboBreak(mods);
                return Swallow();

            default:
                return HookApi.CallNextHookEx(_hook, nCode, wParam, lParam);
        }
    }

    private IntPtr ProcessKeyUp(in KBDLLHOOKSTRUCT kbd, int nCode, IntPtr wParam, IntPtr lParam)
    {
        // All key releases pass through unchanged. The F24 dummy press injected
        // when a Win combo was swallowed sits between the Win keydown and this
        // Win keyup, so the shell never sees a "clean" Win release and does not
        // pop the Start menu. The real Win keyup flows through naturally, so
        // Win never stays logically pressed.
        return HookApi.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    /// <summary>
    /// Injects a dummy VK_F24 keypress (down + up in one SendInput call) so
    /// the shell observes a key event between the Win keydown and Win keyup of
    /// a swallowed Win combo. The Start menu only opens when Win is released
    /// without any intervening key event, so this suppresses the pop while the
    /// genuine Win keyup still reaches the shell. No-op for combos without the
    /// Win modifier.
    /// </summary>
    private static void InjectWinComboBreak(KeyModifiers mods)
    {
        if ((mods & KeyModifiers.Win) == 0)
        {
            return;
        }

        var inputs = new[]
        {
            new INPUT
            {
                type = INPUT.INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = NativeMethods.VK_F24,
                        dwFlags = SendInputFlags.KEYEVENTF_VIRTUALKEY,
                    },
                },
            },
            new INPUT
            {
                type = INPUT.INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = NativeMethods.VK_F24,
                        dwFlags = SendInputFlags.KEYEVENTF_VIRTUALKEY | SendInputFlags.KEYEVENTF_KEYUP,
                    },
                },
            },
        };

        uint injected = HookApi.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (injected == 0)
        {
            Log.Warn($"F24 break injection failed (Win32 error {Marshal.GetLastWin32Error()}).");
        }
    }

    private IntPtr Swallow() => (IntPtr)1;

    private static bool IsModifierKey(byte vk) => vk is
        NativeMethods.VK_SHIFT or NativeMethods.VK_CONTROL or NativeMethods.VK_MENU
        or NativeMethods.VK_LWIN or NativeMethods.VK_RWIN
        or NativeMethods.VK_LSHIFT or NativeMethods.VK_RSHIFT
        or NativeMethods.VK_LCONTROL or NativeMethods.VK_RCONTROL
        or NativeMethods.VK_LMENU or NativeMethods.VK_RMENU;

    private static KeyModifiers GetCurrentModifiers()
    {
        var mods = KeyModifiers.None;

        if (IsKeyDown(NativeMethods.VK_CONTROL))
        {
            mods |= KeyModifiers.Ctrl;
        }

        if (IsKeyDown(NativeMethods.VK_MENU))
        {
            mods |= KeyModifiers.Alt;
        }

        if (IsKeyDown(NativeMethods.VK_LWIN) || IsKeyDown(NativeMethods.VK_RWIN))
        {
            mods |= KeyModifiers.Win;
        }

        if (IsKeyDown(NativeMethods.VK_SHIFT))
        {
            mods |= KeyModifiers.Shift;
        }

        return mods;
    }

    private static bool IsKeyDown(int vk) => (HookApi.GetAsyncKeyState(vk) & 0x8000) != 0;
}
