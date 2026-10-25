using Android.Views;

namespace TabStudio.Android;

/// <summary>
/// Desktop mode in the activity: the tablet window's pointer events go past the external displays' mouse first,
/// which captures a mouse moving on the tablet for the IDE on the display, and gives it back on a touch.
/// </summary>
public sealed partial class MainActivity
{
    // The external displays outlive an activity that Android recreates, as the app's services do.
    private static AndroidExternalDisplays? _displays;

    /// <inheritdoc />
    public override bool DispatchTouchEvent(MotionEvent? ev) =>
        (ev is not null && _displays?.Mouse.OnTabletEvent(ev) == true) || base.DispatchTouchEvent(ev);

    /// <inheritdoc />
    public override bool DispatchGenericMotionEvent(MotionEvent? ev) =>
        (ev is not null && _displays?.Mouse.OnTabletEvent(ev) == true) || base.DispatchGenericMotionEvent(ev);

    /// <inheritdoc />
    /// <remarks>Android ends pointer capture when the window loses the focus, so the display's mouse hears of it.</remarks>
    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        _displays?.Mouse.OnWindowFocusChanged(hasFocus);
    }
}
