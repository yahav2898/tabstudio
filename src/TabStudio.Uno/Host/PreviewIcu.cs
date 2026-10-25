#nullable enable

using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace TabStudio.UnoHost;

/// <summary>
/// Gives Uno's text engine ICU, the Unicode library it breaks lines and orders bidirectional text with. An Uno app
/// does it from a module initializer that Uno's generator writes into the app, which only runs for an app in the
/// default load context; TabStudio's apps run in contexts of their own, so the host does it for them.
/// </summary>
/// <remarks>
/// Uno loads the system's ICU on Linux, and on Android from Android 12 (<c>libicu.so</c>). On Android 7 to 11 it does
/// not try, because <c>libicuuc.so</c> is not one of the libraries Android lets apps load by a path. .NET loads that
/// library by its name for its own globalization on every Android version, so the host does the same and hands Uno
/// the library .NET already uses.
/// </remarks>
internal static class PreviewIcu
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Static;

    /// <summary>Initializes ICU for Uno, once for the process.</summary>
    /// <param name="dataAssembly">The assembly whose <c>icudt.dat</c> resource holds ICU's data where Uno brings its own ICU (Windows, macOS).</param>
    /// <returns>A description of the ICU used, for the log.</returns>
    public static string Initialize(Assembly dataAssembly)
    {
        var icu = Type.GetType("Microsoft.UI.Xaml.Documents.UnicodeText+ICU, Uno.UI", throwOnError: true)!;
        if (OperatingSystem.IsAndroid() && OperatingSystem.IsAndroidVersionAtLeast(24) && !OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            var library = NativeLibrary.Load("libicuuc.so");
            var version = Enumerable.Range(50, 51).Reverse().FirstOrDefault(v => NativeLibrary.TryGetExport(library, $"u_getVersion_{v}", out _));
            if (version == 0)
            {
                throw new InvalidOperationException("Android's ICU (libicuuc.so) has no versioned u_getVersion, which Uno Platform's text needs.");
            }

            icu.GetField("_libicuuc", Private)!.SetValue(null, library);
            icu.GetField("_icuVersion", Private)!.SetValue(null, version);
            icu.GetField("_dataAssembly", Private)!.SetValue(null, dataAssembly);
            return $"Android's ICU {version} (libicuuc.so), as .NET loads it";
        }

        try
        {
            icu.GetMethod("SetDataAssembly", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [dataAssembly]);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is { } inner)
        {
            throw new InvalidOperationException($"Uno Platform could not load ICU, which it lays out text with: {inner.Message}", inner);
        }

        var loaded = (int)icu.GetField("_icuVersion", Private)!.GetValue(null)!;
        return $"ICU {loaded} of the system";
    }
}
