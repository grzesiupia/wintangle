using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Wintangle.App.UI.Controls;

/// <summary>
/// Toggle switch (Settings → Startup): a 42×24 pill track with an 18px knob.
/// Off = Border track with the knob at the left; on = Accent track with the
/// knob slid 18px right. The track color swaps via the "Switch" style trigger
/// (DynamicResource — survives theme swaps); the knob slides with a 0.16s
/// ease-out animation driven here.
/// </summary>
public class Switch : ToggleButton
{
    private const double KnobTravel = 18;

    private TranslateTransform? _knobTransform;

    public Switch()
    {
        SetResourceReference(StyleProperty, "Switch");
        Checked += OnToggled;
        Unchecked += OnToggled;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _knobTransform = GetTemplateChild("KnobTranslate") as TranslateTransform;

        // Snap to the current state without animating on first layout.
        if (_knobTransform != null)
        {
            _knobTransform.BeginAnimation(TranslateTransform.XProperty, null);
            _knobTransform.X = IsChecked == true ? KnobTravel : 0;
        }
    }

    private void OnToggled(object sender, RoutedEventArgs e)
    {
        var transform = _knobTransform ?? (GetTemplateChild("KnobTranslate") as TranslateTransform);
        if (transform == null)
        {
            return;
        }

        var animation = new DoubleAnimation(IsChecked == true ? KnobTravel : 0, TimeSpan.FromSeconds(0.16))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        transform.BeginAnimation(TranslateTransform.XProperty, animation);
    }
}
