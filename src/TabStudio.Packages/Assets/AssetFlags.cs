namespace TabStudio.Packages.Assets;

/// <summary>
/// The kinds of assets a package gives a project, as NuGet's <c>IncludeAssets</c>, <c>ExcludeAssets</c> and
/// <c>PrivateAssets</c> metadata and a <c>.nuspec</c> dependency's <c>include</c> and <c>exclude</c> attributes name them.
/// </summary>
[Flags]
internal enum AssetFlags
{
    /// <summary>Nothing.</summary>
    None = 0,

    /// <summary><c>lib/</c> and <c>ref/</c> assemblies to compile against.</summary>
    Compile = 1,

    /// <summary><c>lib/</c> and <c>runtimes/</c> assemblies to run with.</summary>
    Runtime = 2,

    /// <summary><c>contentFiles/</c>.</summary>
    ContentFiles = 4,

    /// <summary>The package's own <c>build/</c> props and targets.</summary>
    Build = 8,

    /// <summary><c>buildMultiTargeting/</c>.</summary>
    BuildMultitargeting = 16,

    /// <summary><c>buildTransitive/</c>, which flow even where <c>build/</c> does not.</summary>
    BuildTransitive = 32,

    /// <summary><c>runtimes/{rid}/native/</c>.</summary>
    Native = 64,

    /// <summary><c>analyzers/</c>.</summary>
    Analyzers = 128,

    /// <summary>Everything.</summary>
    All = Compile | Runtime | ContentFiles | Build | BuildMultitargeting | BuildTransitive | Native | Analyzers,
}

/// <summary>Reads and combines <see cref="AssetFlags"/> as NuGet does.</summary>
internal static class AssetFlagNames
{
    /// <summary>
    /// What NuGet's default <c>PrivateAssets</c> keeps from flowing past a dependency: content files, analyzers and
    /// <c>build/</c>, but not <c>buildTransitive/</c>. A packed project writes it as <c>exclude="Build,Analyzers"</c>.
    /// </summary>
    public const AssetFlags DefaultSuppressParent = AssetFlags.ContentFiles | AssetFlags.Analyzers | AssetFlags.Build;

    /// <summary>
    /// Parses a list such as <c>compile;runtime</c> or <c>Build,Analyzers</c>, ignoring case and unknown names;
    /// <see langword="null"/> when the text is empty.
    /// </summary>
    /// <param name="text">The metadata or attribute value.</param>
    public static AssetFlags? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var flags = AssetFlags.None;
        foreach (var name in text.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            flags |= name.ToUpperInvariant() switch
            {
                "ALL" => AssetFlags.All,
                "NONE" => AssetFlags.None,
                "COMPILE" => AssetFlags.Compile,
                "RUNTIME" => AssetFlags.Runtime,
                "CONTENTFILES" => AssetFlags.ContentFiles,
                "BUILD" => AssetFlags.Build,
                "BUILDMULTITARGETING" => AssetFlags.BuildMultitargeting,
                "BUILDTRANSITIVE" => AssetFlags.BuildTransitive,
                "NATIVE" => AssetFlags.Native,
                "ANALYZERS" => AssetFlags.Analyzers,
                _ => AssetFlags.None,
            };
        }

        return flags;
    }

    /// <summary>
    /// The assets that do not pass an edge whose included and excluded assets are given, such as
    /// <c>IncludeAssets</c> and <c>ExcludeAssets</c>: what is not included, plus what is excluded.
    /// </summary>
    /// <param name="include">The included assets, or <see langword="null"/> for all.</param>
    /// <param name="exclude">The excluded assets, or <see langword="null"/> for none.</param>
    public static AssetFlags Excluded(string? include, string? exclude) =>
        (AssetFlags.All & ~(Parse(include) ?? AssetFlags.All)) | (Parse(exclude) ?? AssetFlags.None);

    /// <summary>
    /// The assets that do not pass a <c>.nuspec</c> dependency: without an <c>include</c> attribute, NuGet includes
    /// everything but content files, so a package's <c>contentFiles/</c> reach only the projects that reference it
    /// themselves, not those that get it through another package.
    /// </summary>
    /// <param name="include">The dependency's <c>include</c> attribute, or <see langword="null"/>.</param>
    /// <param name="exclude">The dependency's <c>exclude</c> attribute, or <see langword="null"/>.</param>
    public static AssetFlags NuspecExcluded(string? include, string? exclude) =>
        (AssetFlags.All & ~(Parse(include) ?? (AssetFlags.All & ~AssetFlags.ContentFiles))) | (Parse(exclude) ?? AssetFlags.None);
}
