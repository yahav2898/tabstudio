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
    public static bool IsVapor(string directory) => IsVaporCore(directory);

    /// <summary>
    /// The Apple framework a Swift project's app is written with, which only Apple's SDKs in Xcode have, on a Mac:
    /// <c>SwiftUI</c>, <c>UIKit</c> or <c>AppKit</c> imported by its sources, or an Xcode project (<c>.xcodeproj</c>) in its
    /// folder or the folder above, as IceCubesApp's packages are; <see langword="null"/> for a package that builds on
    /// Linux, which TabStudio's Swift toolchain is.
    /// </summary>
    /// <param name="directory">The package folder.</param>
    public static string? AppleFramework(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        var sources = Path.Combine(directory, "Sources");
        var scanned = 0;
        if (Directory.Exists(sources))
        {
            foreach (var file in Directory.EnumerateFiles(sources, "*.swift", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, MaxRecursionDepth = 8 }))
            {
                if (++scanned > 400)
                {
                    break;
                }

                string text;
                try
                {
                    text = new FileInfo(file).Length <= 256 * 1024 ? File.ReadAllText(file) : string.Empty;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    continue;
                }

                // An import inside #if canImport(UIKit) or #if os(iOS) is for Apple platforms only: the package builds
                // on Linux without it, as Alamofire does.
                if (AppleImport().Matches(text).FirstOrDefault(m => !IsConditional(text, m.Index)) is { } import)
                {
                    return import.Groups["framework"].Value;
                }
            }
        }

        try
        {
            var parent = Path.GetDirectoryName(Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar));
            return new[] { directory, parent }.OfType<string>().Any(folder => Directory.EnumerateDirectories(folder, "*.xcodeproj").Any()) ? "Xcode" : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The sentence that says why an Apple-platform app does not build here.</summary>
    /// <param name="name">The project's name.</param>
    /// <param name="framework">Its framework, from <see cref="AppleFramework"/>.</param>
    public static string AppleExplanation(string name, string framework) =>
        $"{name} is an Apple-platform app ({framework}), which builds only with Xcode and Apple's SDKs on a Mac; TabStudio's Swift builds and runs Swift packages for Linux.";

    /// <summary>Whether a position in a Swift file is inside an <c>#if</c> block.</summary>
    private static bool IsConditional(string text, int index)
    {
        var depth = 0;
        foreach (Match directive in CompilerDirective().Matches(text[..index]))
        {
            depth += directive.Groups["directive"].Value == "endif" ? -1 : directive.Groups["directive"].Value == "if" ? 1 : 0;
        }

        return depth > 0;
    }

    [GeneratedRegex(@"^\s*#(?<directive>if|endif)\b", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex CompilerDirective();

    [GeneratedRegex(@"^\s*(?:@\w+\s+)*import\s+(?<framework>SwiftUI|UIKit|AppKit|WatchKit)\s*$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex AppleImport();

    private static bool IsVaporCore(string directory)
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
