using System.Text.RegularExpressions;

namespace TabStudio.Toolchains.Projects;

/// <summary>
/// A Swift package, read from its <c>Package.swift</c> without running it: its executable products and targets, and
/// whether it is a Vapor server.
/// </summary>
public static partial class SwiftProject
{
    /// <summary>
    /// The package's executables, in the order <c>Package.swift</c> declares them: its <c>.executable</c> products, else
    /// its <c>.executableTarget</c>s, else, for a package without either, the folders of <c>Sources</c> with a
    /// <c>main.swift</c>.
    /// </summary>
    /// <param name="directory">The package folder.</param>
    public static IReadOnlyList<string> Executables(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        var manifest = Path.Combine(directory, "Package.swift");
        if (!File.Exists(manifest))
        {
            return [];
        }

        var text = File.ReadAllText(manifest);
        var products = ExecutableProduct().Matches(text).Select(m => m.Groups["name"].Value).Distinct(StringComparer.Ordinal).ToList();
        if (products.Count > 0)
        {
            return products;
        }

        var targets = ExecutableTarget().Matches(text).Select(m => m.Groups["name"].Value).Distinct(StringComparer.Ordinal).ToList();
        if (targets.Count > 0)
        {
            return targets;
        }

        var sources = Path.Combine(directory, "Sources");
        return Directory.Exists(sources)
            ? [.. Directory.EnumerateDirectories(sources).Where(d => File.Exists(Path.Combine(d, "main.swift"))).Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal)]
            : [];
    }

    /// <summary>The executable a source file belongs to: the target whose folder under <c>Sources</c> holds it.</summary>
    /// <param name="directory">The package folder.</param>
    /// <param name="file">A source file, or <see langword="null"/>.</param>
    public static string? ExecutableOf(string directory, string? file)
    {
        ArgumentNullException.ThrowIfNull(directory);
        if (file is null)
        {
            return null;
        }

        var sources = Path.Combine(directory, "Sources") + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(file).StartsWith(sources, StringComparison.Ordinal))
        {
            return null;
        }

        var target = Path.GetFullPath(file)[sources.Length..].Split(Path.DirectorySeparatorChar)[0];
        return Executables(directory).FirstOrDefault(e => string.Equals(e, target, StringComparison.Ordinal));
    }

    /// <summary>Whether the package is a Vapor app: it depends on <c>vapor/vapor</c>.</summary>
    /// <param name="directory">The package folder.</param>
    public static bool IsVapor(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        var manifest = Path.Combine(directory, "Package.swift");
        return File.Exists(manifest) && Vapor().IsMatch(File.ReadAllText(manifest));
    }

    /// <summary>Whether the package has test targets, which <c>swift test</c> runs.</summary>
    /// <param name="directory">The package folder.</param>
    public static bool HasTests(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        var manifest = Path.Combine(directory, "Package.swift");
        return File.Exists(manifest) && TestTarget().IsMatch(File.ReadAllText(manifest));
    }

    /// <summary>Where SwiftPM puts a debug build's executable: <c>.build/debug/&lt;name&gt;</c>.</summary>
    /// <param name="directory">The package folder.</param>
    /// <param name="executable">The executable's name.</param>
    public static string DebugExecutable(string directory, string executable)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(executable);
        return Path.Combine(directory, ".build", "debug", executable);
    }

    [GeneratedRegex(@"\.executable\s*\(\s*name\s*:\s*""(?<name>[^""]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex ExecutableProduct();

    [GeneratedRegex(@"\.executableTarget\s*\(\s*name\s*:\s*""(?<name>[^""]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex ExecutableTarget();

    [GeneratedRegex(@"github\.com/vapor/vapor(?:\.git)?""|\.product\s*\(\s*name\s*:\s*""Vapor""", RegexOptions.CultureInvariant)]
    private static partial Regex Vapor();

    [GeneratedRegex(@"\.testTarget\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex TestTarget();
}
