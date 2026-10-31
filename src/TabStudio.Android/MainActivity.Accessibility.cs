using Android.Animation;
using Android.Content.Res;
using Avalonia.Threading;
using TabStudio.App.Accessibility;

namespace TabStudio.Android;

/// <summary>
/// Android's accessibility settings, handed to the IDE (docs/design/README.md, "Accessibility"): the font size
/// (Settings &gt; Display &gt; Font size), whether animations are off (Settings &gt; Accessibility &gt; Remove
/// animations, or the developer options' animator scale at off) and the language's layout direction. The activity
/// handles font scale and layout direction changes itself (<c>ConfigurationChanges</c>), so the IDE changes at once
/// rather than being recreated; animations are read again whenever the activity comes back, as after the Settings app.
/// </summary>
public sealed partial class MainActivity
{
    /// <inheritdoc />
    public override void OnConfigurationChanged(Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        ReportAccessibility(newConfig);
    }

    /// <inheritdoc />
    protected override void OnResume()
    {
        base.OnResume();
        ReportAccessibility(Resources?.Configuration);
    }

    private static void ReportAccessibility(Configuration? configuration)
    {
        double fontScale = configuration?.FontScale ?? 1f;
        var rightToLeft = configuration?.LayoutDirection == global::Android.Views.LayoutDirection.Rtl;
        var reduceMotion = !ValueAnimator.AreAnimatorsEnabled();
        Dispatcher.UIThread.Post(() => SystemAccessibility.Current.Update(fontScale, reduceMotion, rightToLeft));
    }
}
