using System.Text.RegularExpressions;
using TabStudio.Toolchains.Processes;

namespace TabStudio.Toolchains.Projects;

/// <summary>Distinguishes Dart packages from Flutter apps sharing the pubspec.yaml marker.</summary>
public static partial class FlutterProject
{
    /// <summary>Whether the package declares a Flutter SDK dependency (not just a name containing Flutter).</summary>
    public static bool UsesFlutter(string directory) => File.Exists(Path.Combine(directory, "pubspec.yaml"))
        && SdkPattern().IsMatch(File.ReadAllText(Path.Combine(directory, "pubspec.yaml")));

    /// <summary>Runs Flutter in the web preview, or a Dart package's console entry point.</summary>
    public static RunPlan? Plan(string directory)
    {
        var flutter = UsesFlutter(directory);
        var entry = flutter ? "lib/main.dart" : File.Exists(Path.Combine(directory, "bin/main.dart")) ? "bin/main.dart" : "lib/main.dart";
        if (!File.Exists(Path.Combine(directory, entry))) return null;
        return new RunPlan(null, new ProcessStartSpec
        {
            Program = flutter ? "flutter" : "dart", WorkingDirectory = directory,
            Arguments = flutter ? ["run", "-d", "web-server", "--web-hostname", "127.0.0.1", "--web-port", "0"] : ["run", entry],
        }, entry);
    }

    [GeneratedRegex("^\\s*sdk\\s*:\\s*['\"]?flutter['\"]?\\s*(?:#.*)?$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex SdkPattern();
}
