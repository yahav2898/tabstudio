using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace TabStudio.Core.Workspaces;

/// <summary>
/// Uno Platform and WinUI 3 projects. Uno Platform implements the WinUI API outside Windows, so TabStudio builds both
/// against Uno's assemblies and runs them in the app preview with Uno's Skia renderer: an Uno project as it is, and a
/// WinUI 3 project (Windows App SDK, <c>UseWinUI</c>) with Uno standing in for the Windows App SDK.
/// </summary>
public static class UnoProjects
{
    /// <summary>The MSBuild SDK of Uno Platform's single-project apps and libraries.</summary>
    public const string SdkName = "Uno.Sdk";

    /// <summary>The Windows App SDK package that WinUI 3 projects reference, which Uno replaces on the tablet.</summary>
    public const string WindowsAppSdkPackage = "Microsoft.WindowsAppSDK";

    /// <summary>
    /// The <c>Uno.Sdk</c> version TabStudio uses when a project does not name one, which is the case for every WinUI 3
    /// project. Uno.Sdk's package list gives the versions of Uno's other packages: this one is Uno Platform 6.7.135.
    /// </summary>
    public const string DefaultSdkVersion = "6.7.30";

    /// <summary>Whether a project is built against Uno Platform: an Uno project, or a WinUI 3 project.</summary>
    /// <param name="project">The project.</param>
    public static bool UsesUno(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project.Language == ProjectLanguage.CSharp && project.Kind is ProjectKind.Uno or ProjectKind.WinUI;
    }

    /// <summary>Whether a project is an app that F5 shows in the app preview, rather than a library.</summary>
    /// <param name="project">The project.</param>
    public static bool IsApp(ProjectInfo project) => UsesUno(project) && project.IsExecutable;

    /// <summary>Whether a project is a WinUI 3 project for the Windows App SDK, which TabStudio builds against Uno instead.</summary>
    /// <param name="project">The project.</param>
    public static bool IsWinUI(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project.Kind == ProjectKind.WinUI && project.Language == ProjectLanguage.CSharp;
    }

    /// <summary>Whether a project uses Uno's MSBuild SDK, <c>&lt;Project Sdk="Uno.Sdk"&gt;</c>.</summary>
    /// <param name="project">The project.</param>
    public static bool UsesUnoSdk(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return string.Equals(project.Sdk, SdkName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The features an Uno project turns on in its <c>UnoFeatures</c> property, such as <c>Toolkit</c> or
    /// <c>Material</c>, in lower case, as Uno.Sdk compares them.
    /// </summary>
    /// <param name="project">The project.</param>
    public static IReadOnlySet<string> GetFeatures(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return (project.GetProperty("UnoFeatures") ?? string.Empty)
            .Split([';', '\n', '\r', '\t', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(feature => !feature.StartsWith("$(", StringComparison.Ordinal))
            .Select(feature => feature.ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// The version of <c>Uno.Sdk</c> a project uses: the one in its <c>Sdk</c> attribute (<c>Uno.Sdk/6.7.135</c>), or
    /// the one the nearest <c>global.json</c> names under <c>msbuild-sdks</c>, as the .NET SDK resolves it; for a WinUI 3
    /// project, or when neither names one, <see cref="DefaultSdkVersion"/>.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <returns>The version, and where it came from: the project file, a <c>global.json</c> path, or <see langword="null"/> for the default.</returns>
    public static (string Version, string? Source) FindSdkVersion(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!UsesUnoSdk(project))
        {
            return (DefaultSdkVersion, null);
        }

        if (ReadSdkAttributeVersion(project.FilePath) is { } inProject)
        {
            return (inProject, project.FilePath);
        }

        for (var directory = new DirectoryInfo(project.DirectoryPath); directory is not null; directory = directory.Parent)
        {
            var globalJson = Path.Combine(directory.FullName, "global.json");
            if (File.Exists(globalJson))
            {
                // The .NET SDK reads the nearest global.json only.
                return ReadGlobalJsonVersion(globalJson) is { } version ? (version, globalJson) : (DefaultSdkVersion, null);
            }
        }

        return (DefaultSdkVersion, null);
    }

    /// <summary>Reads <c>msbuild-sdks</c> → <c>Uno.Sdk</c> from a <c>global.json</c>.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The version, or <see langword="null"/> when the file does not name one or cannot be read.</returns>
    public static string? ReadGlobalJsonVersion(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("msbuild-sdks", out var sdks)
                && sdks.ValueKind == JsonValueKind.Object)
            {
                foreach (var sdk in sdks.EnumerateObject())
                {
                    if (string.Equals(sdk.Name, SdkName, StringComparison.OrdinalIgnoreCase) && sdk.Value.ValueKind == JsonValueKind.String)
                    {
                        return sdk.Value.GetString()?.Trim() is { Length: > 0 } version ? version : null;
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // An unreadable global.json leaves the version to the default, as for a project without one.
        }

        return null;
    }

    /// <summary>The version in <c>Sdk="Uno.Sdk/6.7.135"</c>, when the project file states one.</summary>
    private static string? ReadSdkAttributeVersion(string projectPath)
    {
        try
        {
            if (XDocument.Load(projectPath).Root is not { } root)
            {
                return null;
            }

            // <Project Sdk="Uno.Sdk/6.7.135">, or <Sdk Name="Uno.Sdk" Version="6.7.135" />.
            var references = (root.Attribute("Sdk")?.Value ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Concat(root.Elements()
                    .Where(e => e.Name.LocalName == "Sdk")
                    .Select(e => $"{e.Attribute("Name")?.Value}/{e.Attribute("Version")?.Value}"));
            foreach (var part in references)
            {
                var slash = part.IndexOf('/', StringComparison.Ordinal);
                if (slash > 0 && string.Equals(part[..slash], SdkName, StringComparison.OrdinalIgnoreCase) && part[(slash + 1)..].Trim() is { Length: > 0 } version)
                {
                    return version;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException)
        {
            // The parser already reports a project file it cannot read.
        }

        return null;
    }
}
