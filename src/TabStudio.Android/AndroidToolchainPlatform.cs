using Android.Content;
using Android.Net;
using Android.OS;
using TabStudio.Toolchains.Linux;

namespace TabStudio.Android;

/// <summary>
/// What an Android device offers toolchains: the app's native library folder, where the APK's PRoot and its
/// loader were extracted (the one place Android lets an app execute files from), the API level, and the DNS
/// servers of the network the device is on, which the Linux environment's <c>resolv.conf</c> follows.
/// </summary>
internal static class AndroidToolchainPlatform
{
    /// <summary>Describes the device.</summary>
    /// <param name="context">The application context.</param>
    public static ToolchainPlatform Create(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new ToolchainPlatform
        {
            NativeLibraryDirectory = context.ApplicationInfo?.NativeLibraryDir,
            AndroidApiLevel = (int)Build.VERSION.SdkInt,
            DnsServers = () => DnsServers(context),
        };
    }

    /// <summary>The active network's DNS servers; empty when offline, and the environment then uses public ones.</summary>
    private static List<string> DnsServers(Context context)
    {
        try
        {
            if (context.GetSystemService(Context.ConnectivityService) is not ConnectivityManager connectivity
                || connectivity.ActiveNetwork is not { } network
                || connectivity.GetLinkProperties(network) is not { } properties)
            {
                return [];
            }

            // Link-local IPv6 servers need a zone (fe80::1%wlan0) that musl's resolver does not read.
            return [.. properties.DnsServers
                .Select(address => address.HostAddress)
                .OfType<string>()
                .Where(address => address.Length > 0 && !address.Contains('%', StringComparison.Ordinal))];
        }
        catch (Java.Lang.Exception)
        {
            return [];
        }
    }
}
