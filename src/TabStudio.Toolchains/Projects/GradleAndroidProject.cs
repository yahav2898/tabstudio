using System.Text.RegularExpressions;
using TabStudio.Toolchains.Processes;

namespace TabStudio.Toolchains.Projects;

/// <summary>Identifies Android Gradle roots, including Kotlin DSL and version-catalog plugin aliases.</summary>
public static partial class GradleAndroidProject
{
    /// <summary>The SDK's isolated Gradle launcher inside the tablet's Linux environment.</summary>
    public const string LinuxGradleCommand = "/usr/local/bin/tabstudio-android-gradle";
    private static readonly string[] CapacitorFiles = ["capacitor.config.ts", "capacitor.config.json", "capacitor.config.js"];
    /// <summary>Whether a settings.gradle(.kts) root includes an Android application plugin.</summary>
    public static bool IsProject(string directory)
    {
        if (!File.Exists(Path.Combine(directory, "settings.gradle")) && !File.Exists(Path.Combine(directory, "settings.gradle.kts")))
        {
            return false;
        }

        try
        {
            var files = Directory.EnumerateFiles(directory, "*.gradle*")
                .Concat(Directory.EnumerateDirectories(directory)
                    .Where(d => !Path.GetFileName(d).StartsWith('.') && Path.GetFileName(d) is not ("build" or "node_modules"))
                    .SelectMany(d => Directory.EnumerateFiles(d, "*.gradle*")))
                .Append(Path.Combine(directory, "gradle", "libs.versions.toml"));
            return files.Where(File.Exists).Any(file =>
                new FileInfo(file).Length <= 1_000_000 && ApplicationPlugin().IsMatch(WithoutComments(File.ReadAllText(file))));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Whether this is a Capacitor web project whose Android folder can be created on the tablet.</summary>
    public static bool IsCapacitor(string directory) => File.Exists(Path.Combine(directory, "package.json"))
        && CapacitorFiles.Any(f => File.Exists(Path.Combine(directory, f)));

    /// <summary>Creates a Gradle build, using the repository's wrapper when present and ARM AAPT2 on tablets.</summary>
    public static ProcessStartSpec Build(string directory, bool linuxEnvironment)
    {
        var args = new List<string>();
        var wrapper = File.Exists(Path.Combine(directory, "gradlew"));
        if (wrapper)
        {
            args.Add("gradlew");
        }

        args.AddRange(["--no-daemon", "--console=plain", "--max-workers=2", "assembleDebug"]);
        var environment = new Dictionary<string, string?>();
        if (linuxEnvironment)
        {
            args.Add("-Pandroid.aapt2FromMavenOverride=/opt/tabstudio/android/sdk/build-tools/35.0.0/aapt2");
            args.Add("-Dorg.gradle.native=false");
            args.Add("-Dorg.gradle.vfs.watch=false");
            environment["JAVA_HOME"] = "/usr/lib/jvm/java-17-openjdk";
            environment["ANDROID_HOME"] = "/opt/tabstudio/android/sdk";
            environment["ANDROID_SDK_ROOT"] = "/opt/tabstudio/android/sdk";
        }

        return new ProcessStartSpec { Program = wrapper ? "sh" : linuxEnvironment ? LinuxGradleCommand : "gradle", Arguments = args, WorkingDirectory = directory, Environment = environment };
    }

    /// <summary>Finds APK outputs without looking in dependency caches or choosing an unrelated APK.</summary>
    public static IReadOnlyList<string> DebugApks(string directory) => Directory.EnumerateDirectories(directory)
        .Select(d => Path.Combine(d, "build", "outputs", "apk"))
        .Where(Directory.Exists)
        .SelectMany(d => Directory.EnumerateFiles(d, "*.apk", SearchOption.AllDirectories))
        .Where(p => p.Contains("debug", StringComparison.OrdinalIgnoreCase) && !p.EndsWith("-androidTest.apk", StringComparison.OrdinalIgnoreCase))
        .Order(StringComparer.Ordinal).ToList();

    // Preserve quoted strings (including URLs); remove only actual comments.
    private static string WithoutComments(string text) => Comments().Replace(text, m => m.Value.StartsWith('/') ? string.Empty : m.Value);

    [GeneratedRegex("['\"]com\\.android\\.application['\"]", RegexOptions.CultureInvariant, 100)]
    private static partial Regex ApplicationPlugin();

    [GeneratedRegex("\"(?:\\\\.|[^\"\\\\])*\"|'(?:\\\\.|[^'\\\\])*'|//[^\\r\\n]*|/\\*[\\s\\S]*?\\*/", RegexOptions.CultureInvariant, 100)]
    private static partial Regex Comments();
}
