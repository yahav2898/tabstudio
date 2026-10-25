using Android.Content;
using Android.Content.Res;

namespace TabStudio.Android;

/// <summary>
/// Whether a keyboard is connected to the device, as Android's configuration says: a keyboard with letters that is
/// not folded away. Desktop mode types on the external display with it, or with the on-screen keyboard without one.
/// </summary>
internal sealed class AndroidKeyboardWatcher : Java.Lang.Object, IComponentCallbacks
{
    /// <summary>Starts watching the application's configuration.</summary>
    /// <param name="context">The application context.</param>
    public AndroidKeyboardWatcher(Context context)
    {
        IsConnected = Connected(context.Resources?.Configuration);
        context.RegisterComponentCallbacks(this);
    }

    /// <summary>Whether a keyboard is connected.</summary>
    public bool IsConnected { get; private set; }

    /// <summary>Raised on the UI thread when a keyboard is connected or disconnected.</summary>
    public event EventHandler? Changed;

    /// <inheritdoc />
    public void OnConfigurationChanged(Configuration? newConfig)
    {
        var connected = Connected(newConfig);
        if (connected != IsConnected)
        {
            IsConnected = connected;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    public void OnLowMemory()
    {
    }

    private static bool Connected(Configuration? configuration) =>
        configuration is { Keyboard: KeyboardType.Qwerty, HardKeyboardHidden: HardKeyboardHidden.No };
}
