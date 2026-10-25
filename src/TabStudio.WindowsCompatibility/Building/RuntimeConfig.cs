using System.Text.Json;
using System.Text.Json.Nodes;

namespace TabStudio.WindowsCompatibility.Building;

/// <summary>
/// The <c>&lt;app&gt;.runtimeconfig.json</c> the .NET host reads to start a Windows desktop app: the Windows Desktop
/// shared framework of the app's .NET version, as the SDK writes it for a framework-dependent app.
/// </summary>
public static class RuntimeConfig
{
    /// <summary>The shared framework Windows Forms and WPF apps run on.</summary>
    public const string WindowsDesktopFramework = "Microsoft.WindowsDesktop.App";

    /// <summary>The file's text.</summary>
    /// <param name="majorVersion">The .NET version the app targets, such as 10; any installed patch of it runs the app.</param>
    public static string Create(int majorVersion)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(majorVersion, 5);
        var root = new JsonObject
        {
            ["runtimeOptions"] = new JsonObject
            {
                ["tfm"] = $"net{majorVersion}.0",
                ["framework"] = new JsonObject
                {
                    ["name"] = WindowsDesktopFramework,
                    ["version"] = $"{majorVersion}.0.0",
                },
                ["configProperties"] = new JsonObject
                {
                    ["System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization"] = false,
                },
            },
        };
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
    }

    /// <summary>The .NET version of a target framework such as <c>net10.0-windows</c>: 10.</summary>
    /// <param name="targetFramework">The target framework.</param>
    public static int MajorVersion(string targetFramework)
    {
        ArgumentNullException.ThrowIfNull(targetFramework);
        var digits = targetFramework.StartsWith("net", StringComparison.OrdinalIgnoreCase) ? targetFramework[3..] : targetFramework;
        var dot = digits.IndexOf('.', StringComparison.Ordinal);
        return int.TryParse(dot < 0 ? digits : digits[..dot], System.Globalization.CultureInfo.InvariantCulture, out var major) && major >= 5 ? major : 10;
    }
}
