using System.Text.Json;
using TabStudio.Core.Workspaces;

namespace TabStudio.Packages.Uno;

/// <summary>
/// The package list of a version of <c>Uno.Sdk</c> (<c>targets/netstandard2.0/packages.json</c>): groups of packages
/// that share a version, such as <c>Core</c> for Uno Platform's own packages, and the properties a project sets to
/// choose another version of a group, as Uno.Sdk's <c>ImplicitPackagesResolver</c> reads them.
/// </summary>
public sealed class UnoSdkManifest
{
    /// <summary>The path of the list in the <c>Uno.Sdk</c> package.</summary>
    public static readonly string RelativePath = Path.Combine("targets", "netstandard2.0", "packages.json");

    /// <summary>The project properties that set a group's version, by group, as Uno.Sdk names them.</summary>
    private static readonly Dictionary<string, string> VersionProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Extensions"] = "UnoExtensionsVersion",
        ["Toolkit"] = "UnoToolkitVersion",
        ["Themes"] = "UnoThemesVersion",
        ["CSharpMarkup"] = "UnoCSharpMarkupVersion",
        ["SkiaSharp"] = "SkiaSharpVersion",
        ["SvgSkia"] = "SvgSkiaVersion",
        ["Mvvm"] = "CommunityToolkitMvvmVersion",
        ["Prism"] = "PrismVersion",
        ["UnoFonts"] = "UnoFontsVersion",
        ["MicrosoftLoggingConsole"] = "MicrosoftLoggingVersion",
        ["CoreLogging"] = "UnoCoreLoggingSingletonVersion",
        ["MsalClient"] = "MicrosoftIdentityClientVersion",
    };

    private readonly Dictionary<string, Group> _groupsByPackage;

    private UnoSdkManifest(string sdkVersion, IReadOnlyList<Group> groups)
    {
        SdkVersion = sdkVersion;
        Groups = groups;
        _groupsByPackage = new Dictionary<string, Group>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            foreach (var package in group.Packages)
            {
                _groupsByPackage.TryAdd(package, group);
            }
        }
    }

    /// <summary>The <c>Uno.Sdk</c> version the list comes with.</summary>
    public string SdkVersion { get; }

    /// <summary>The groups, in the list's order.</summary>
    public IReadOnlyList<Group> Groups { get; }

    /// <summary>The Uno Platform version: that of the <c>Core</c> group, which <c>Uno.WinUI</c> is in.</summary>
    public string UnoVersion => Groups.FirstOrDefault(g => g.Name.Equals("Core", StringComparison.OrdinalIgnoreCase))?.Version ?? SdkVersion;

    /// <summary>Reads the list from an extracted <c>Uno.Sdk</c> package.</summary>
    /// <param name="sdkDirectory">The package's folder, such as <c>…/uno.sdk/6.7.30</c>.</param>
    /// <param name="sdkVersion">The package's version.</param>
    /// <exception cref="InvalidDataException">The package has no list, or the list does not read.</exception>
    public static UnoSdkManifest Load(string sdkDirectory, string sdkVersion)
    {
        ArgumentException.ThrowIfNullOrEmpty(sdkDirectory);
        var path = Path.Combine(sdkDirectory, RelativePath);
        if (!File.Exists(path))
        {
            throw new InvalidDataException($"Uno.Sdk {sdkVersion} has no {RelativePath.Replace('\\', '/')}.");
        }

        return Parse(File.ReadAllText(path), sdkVersion);
    }

    /// <summary>Reads a list.</summary>
    /// <param name="json">The text of <c>packages.json</c>.</param>
    /// <param name="sdkVersion">The <c>Uno.Sdk</c> version it comes with.</param>
    /// <exception cref="InvalidDataException">The text is not a package list.</exception>
    public static UnoSdkManifest Parse(string json, string sdkVersion)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using var document = JsonDocument.Parse(json.TrimStart('﻿'));
            var groups = new List<Group>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                var name = element.GetProperty("group").GetString() ?? string.Empty;
                var version = element.GetProperty("version").GetString() ?? string.Empty;
                var packages = element.TryGetProperty("packages", out var list)
                    ? list.EnumerateArray().Select(p => p.GetString()).OfType<string>().ToList()
                    : [];
                var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (element.TryGetProperty("versionOverride", out var byFramework) && byFramework.ValueKind == JsonValueKind.Object)
                {
                    foreach (var entry in byFramework.EnumerateObject())
                    {
                        if (entry.Value.GetString() is { Length: > 0 } overridden)
                        {
                            overrides[entry.Name] = overridden;
                        }
                    }
                }

                groups.Add(new Group(name, version, packages, overrides));
            }

            return new UnoSdkManifest(sdkVersion, groups);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            throw new InvalidDataException($"The package list of Uno.Sdk {sdkVersion} does not read: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// The version of a package for a project: its group's, unless the project sets the group's version property;
    /// for <c>net10.0</c>, the group's override for that framework when it has one.
    /// </summary>
    /// <param name="packageId">The package.</param>
    /// <param name="project">The project, whose properties may set the version.</param>
    /// <returns>The version, or <see langword="null"/> when the list does not have the package.</returns>
    public string? GetVersion(string packageId, ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(packageId);
        ArgumentNullException.ThrowIfNull(project);
        if (!_groupsByPackage.TryGetValue(packageId, out var group))
        {
            return null;
        }

        if (VersionProperties.TryGetValue(group.Name, out var property)
            && project.GetProperty(property) is { Length: > 0 } chosen
            && !chosen.Contains("$(", StringComparison.Ordinal))
        {
            return chosen.Trim();
        }

        return group.Overrides.TryGetValue("net10.0", out var overridden) ? overridden : group.Version;
    }

    /// <summary>A group of packages that share a version.</summary>
    /// <param name="Name">The group, such as <c>Core</c> or <c>Toolkit</c>.</param>
    /// <param name="Version">The packages' version.</param>
    /// <param name="Packages">The package ids.</param>
    /// <param name="Overrides">Versions for particular target frameworks, such as <c>net10.0</c>.</param>
    public sealed record Group(string Name, string Version, IReadOnlyList<string> Packages, IReadOnlyDictionary<string, string> Overrides);
}
