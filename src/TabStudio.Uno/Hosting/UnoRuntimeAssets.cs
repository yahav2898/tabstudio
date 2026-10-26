namespace TabStudio.Uno.Hosting;

/// <summary>
/// The assemblies and files an Uno Platform app runs with, from the assemblies its restore selected: Uno's packages
/// compile against reference assemblies in <c>lib/</c> and run with the implementation for a renderer in
/// <c>uno-runtime/</c>, which is Skia's in TabStudio, as for Uno's desktop targets.
/// </summary>
/// <remarks>
/// Uno.Sdk does the same with its <c>ReplaceUnoRuntime</c> target: the copy of an assembly the app runs with comes
/// from <c>uno-runtime/&lt;framework&gt;/skia/</c> of the package whenever there is one. A library that brings files
/// for <c>ms-appx:///</c> addresses, such as a font package, marks its assembly with a <c>.uprimarker</c> file and
/// keeps them in a folder named after the assembly next to it; the build copies that folder to the app's output.
/// </remarks>
public sealed class UnoRuntimeAssets
{
    /// <summary>The renderer whose implementation TabStudio runs.</summary>
    public const string Renderer = "skia";

    private UnoRuntimeAssets(IReadOnlyList<string> assemblies, IReadOnlyList<UnoContentFolder> content, string? unoVersion)
    {
        Assemblies = assemblies;
        Content = content;
        UnoVersion = unoVersion;
    }

    /// <summary>Full paths of the assemblies to run with, Uno's from <c>uno-runtime/</c>.</summary>
    public IReadOnlyList<string> Assemblies { get; }

    /// <summary>The folders of files the app's libraries bring for <c>ms-appx:///</c> addresses.</summary>
    public IReadOnlyList<UnoContentFolder> Content { get; }

    /// <summary>The version of Uno Platform the app runs on, from the <c>Uno.WinUI</c> package; <see langword="null"/> when it has none.</summary>
    public string? UnoVersion { get; }

    /// <summary>The path of <c>Uno.UI.dll</c> the app runs with, or <see langword="null"/> when the app does not use Uno Platform.</summary>
    public string? UnoUIPath => Assemblies.FirstOrDefault(path => string.Equals(Path.GetFileName(path), "Uno.UI.dll", StringComparison.OrdinalIgnoreCase));

    /// <summary>Selects the assemblies and files to run with from a restore's runtime assemblies.</summary>
    /// <param name="runtimeAssemblies">Full paths, as <c>RestoreResult.RuntimeAssemblies</c> lists them.</param>
    public static UnoRuntimeAssets From(IReadOnlyList<string> runtimeAssemblies)
    {
        ArgumentNullException.ThrowIfNull(runtimeAssemblies);
        var assemblies = runtimeAssemblies.Select(RuntimeImplementation).ToList();
        var content = assemblies
            .Select(ContentOf)
            .OfType<UnoContentFolder>()
            .ToList();
        var unoUI = assemblies.FirstOrDefault(path => string.Equals(Path.GetFileName(path), "Uno.UI.dll", StringComparison.OrdinalIgnoreCase));
        return new UnoRuntimeAssets(assemblies, content, unoUI is null ? null : PackageVersionOf(unoUI));
    }

    /// <summary>
    /// The implementation to run for an assembly of a package: the one in <c>uno-runtime/&lt;framework&gt;/skia/</c>
    /// when the package has one, the newest framework's when not the same, and otherwise the assembly itself.
    /// </summary>
    /// <param name="path">The assembly the restore selected, such as <c>…/uno.winui/6.7.135/lib/net10.0/Uno.UI.dll</c>.</param>
    public static string RuntimeImplementation(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var frameworkDirectory = Path.GetDirectoryName(path);
        var libDirectory = frameworkDirectory is null ? null : Path.GetDirectoryName(frameworkDirectory);
        if (frameworkDirectory is null || libDirectory is null
            || !string.Equals(Path.GetFileName(libDirectory), "lib", StringComparison.OrdinalIgnoreCase)
            || Path.GetDirectoryName(libDirectory) is not { } packageDirectory)
        {
            return path;
        }

        var runtimes = Path.Combine(packageDirectory, "uno-runtime");
        if (!Directory.Exists(runtimes))
        {
            return path;
        }

        var name = Path.GetFileName(path);
        var sameFramework = Path.Combine(runtimes, Path.GetFileName(frameworkDirectory), Renderer, name);
        if (File.Exists(sameFramework))
        {
            return sameFramework;
        }

        return Directory.EnumerateDirectories(runtimes)
            .OrderByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(framework => Path.Combine(framework, Renderer, name))
            .FirstOrDefault(File.Exists) ?? path;
    }

    /// <summary>The folder of <c>ms-appx:///</c> files a library brings, when its assembly has a <c>.uprimarker</c>.</summary>
    private static UnoContentFolder? ContentOf(string assemblyPath)
    {
        var directory = Path.GetDirectoryName(assemblyPath);
        var name = Path.GetFileNameWithoutExtension(assemblyPath);
        if (directory is null)
        {
            return null;
        }

        var folder = Path.Combine(directory, name);
        return File.Exists(Path.Combine(directory, name + ".uprimarker")) && Directory.Exists(folder)
            ? new UnoContentFolder(name, folder)
            : null;
    }

    /// <summary>The version folder of a package in the global packages folder: <c>…/uno.winui/6.7.135/…</c>.</summary>
    private static string? PackageVersionOf(string assemblyPath)
    {
        for (var directory = Path.GetDirectoryName(assemblyPath); directory is not null; directory = Path.GetDirectoryName(directory))
        {
            var parent = Path.GetDirectoryName(directory);
            if (parent is not null && string.Equals(Path.GetFileName(parent), "uno.winui", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFileName(directory);
            }
        }

        return null;
    }
}

/// <summary>Files a library brings for <c>ms-appx:///</c> addresses, such as <c>ms-appx:///Uno.Fonts.OpenSans/Fonts/OpenSans.ttf</c>.</summary>
/// <param name="Name">The folder's name in the app's output, which is the library's assembly name.</param>
/// <param name="SourceDirectory">Where its files are in the package.</param>
public sealed record UnoContentFolder(string Name, string SourceDirectory);
