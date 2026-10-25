using System.Globalization;
using Android.Content;
using Android.OS;
using Android.Provider;
using TabStudio.App.Platform;
using TabStudio.Link.Networking;

namespace TabStudio.Android;

/// <summary>
/// This device as other TabStudio Link devices see it: the name the user gave it in Android's settings, or its maker
/// and model, and whether it is a phone or a tablet by its smallest screen width.
/// </summary>
/// <param name="context">The application context.</param>
public sealed class AndroidDeviceIdentity(Context context) : IDeviceIdentity
{
    /// <summary>The smallest width, in density-independent pixels, from which Android itself calls a device a tablet.</summary>
    private const int TabletWidthDp = 600;

    /// <inheritdoc />
    public string Name { get; } = ReadName(context);

    /// <inheritdoc />
    public string Kind => context.Resources?.Configuration?.SmallestScreenWidthDp >= TabletWidthDp ? LinkDeviceKinds.Tablet : LinkDeviceKinds.Phone;

    private static string ReadName(Context context)
    {
        var name = Settings.Global.GetString(context.ContentResolver, Settings.Global.DeviceName);
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name.Trim();
        }

        var model = string.IsNullOrWhiteSpace(Build.Model) ? "Android device" : Build.Model.Trim();
        var maker = Build.Manufacturer?.Trim();
        if (string.IsNullOrEmpty(maker) || model.StartsWith(maker, StringComparison.OrdinalIgnoreCase))
        {
            return model;
        }

        return $"{char.ToUpper(maker[0], CultureInfo.InvariantCulture)}{maker[1..]} {model}";
    }
}
