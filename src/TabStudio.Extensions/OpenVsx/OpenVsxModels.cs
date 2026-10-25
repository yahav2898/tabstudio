using TabStudio.Extensions.Manifests;

namespace TabStudio.Extensions.OpenVsx;

/// <summary>How search results are ordered.</summary>
public enum OpenVsxSort
{
    /// <summary>Best match first, which also weighs downloads and ratings.</summary>
    Relevance,

    /// <summary>Most downloaded first.</summary>
    Downloads,

    /// <summary>Best rated first.</summary>
    Rating,

    /// <summary>Most recently published first.</summary>
    Published,
}

/// <summary>A search of the registry.</summary>
/// <param name="Text">What the user typed; empty lists every extension, which with <see cref="OpenVsxSort.Downloads"/> gives the popular ones.</param>
/// <param name="Category">A category as the registry names them, such as <c>Programming Languages</c> or <c>Themes</c>, or <see langword="null"/>.</param>
/// <param name="Sort">The order.</param>
/// <param name="Offset">How many results to skip, for the next page.</param>
/// <param name="Size">How many results to return, at most 100.</param>
public sealed record OpenVsxQuery(string Text, string? Category = null, OpenVsxSort Sort = OpenVsxSort.Relevance, int Offset = 0, int Size = 30);

/// <summary>An extension in a list of search results.</summary>
/// <param name="Id">The extension's identifier; the registry calls the publisher its namespace.</param>
/// <param name="Version">The latest version.</param>
/// <param name="DisplayName">The name shown to people.</param>
/// <param name="Description">A one-line description.</param>
/// <param name="IconUrl">The icon, or <see langword="null"/>.</param>
/// <param name="AverageRating">The average of its reviews from 0 to 5, or <see langword="null"/> without reviews.</param>
/// <param name="ReviewCount">How many reviews it has.</param>
/// <param name="DownloadCount">How often it was downloaded.</param>
/// <param name="Published">When the latest version was published.</param>
/// <param name="IsVerified">Whether the registry verified that the publisher owns the namespace.</param>
/// <param name="IsDeprecated">Whether the publisher deprecated it.</param>
public sealed record OpenVsxSummary(
    ExtensionIdentifier Id,
    string Version,
    string DisplayName,
    string Description,
    Uri? IconUrl,
    double? AverageRating,
    long ReviewCount,
    long DownloadCount,
    DateTimeOffset? Published,
    bool IsVerified,
    bool IsDeprecated);

/// <summary>A page of search results.</summary>
/// <param name="Offset">How many results were skipped.</param>
/// <param name="TotalSize">How many extensions match in all.</param>
/// <param name="Extensions">This page's extensions.</param>
/// <param name="IsOffline">Whether the registry could not be reached and these are the results it gave last time.</param>
public sealed record OpenVsxSearchResult(int Offset, int TotalSize, IReadOnlyList<OpenVsxSummary> Extensions, bool IsOffline);

/// <summary>The files the registry keeps for an extension version.</summary>
/// <param name="Download">The .vsix package.</param>
/// <param name="Sha256">The package's SHA-256 hash.</param>
/// <param name="Signature">The package's signature archive (<c>.sigzip</c>).</param>
/// <param name="PublicKey">The key the signature was made with, in PEM.</param>
/// <param name="Icon">The icon.</param>
/// <param name="Readme">The README, in Markdown.</param>
/// <param name="Changelog">The change log, in Markdown.</param>
/// <param name="License">The license text.</param>
/// <param name="Manifest">The package.json.</param>
public sealed record OpenVsxFiles(
    Uri? Download,
    Uri? Sha256,
    Uri? Signature,
    Uri? PublicKey,
    Uri? Icon,
    Uri? Readme,
    Uri? Changelog,
    Uri? License,
    Uri? Manifest);

/// <summary>An extension version with everything the registry knows about it.</summary>
public sealed record OpenVsxExtension
{
    /// <summary>The extension's identifier.</summary>
    public required ExtensionIdentifier Id { get; init; }

    /// <summary>This version.</summary>
    public required string Version { get; init; }

    /// <summary>The name shown to people.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The publisher's name as shown to people, such as <c>Microsoft</c> for the namespace <c>ms-vscode</c>.</summary>
    public required string PublisherDisplayName { get; init; }

    /// <summary>A one-line description.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>The files of this version.</summary>
    public required OpenVsxFiles Files { get; init; }

    /// <summary>The average of its reviews from 0 to 5, or <see langword="null"/> without reviews.</summary>
    public double? AverageRating { get; init; }

    /// <summary>How many reviews it has.</summary>
    public long ReviewCount { get; init; }

    /// <summary>How often it was downloaded.</summary>
    public long DownloadCount { get; init; }

    /// <summary>When this version was published.</summary>
    public DateTimeOffset? Published { get; init; }

    /// <summary>Whether the registry verified that the publisher owns the namespace.</summary>
    public bool IsVerified { get; init; }

    /// <summary>Whether the publisher deprecated it.</summary>
    public bool IsDeprecated { get; init; }

    /// <summary>Whether this is a pre-release version.</summary>
    public bool IsPreRelease { get; init; }

    /// <summary>Whether the publisher marks it as a preview.</summary>
    public bool IsPreview { get; init; }

    /// <summary>The VS Code versions it asks for, such as <c>^1.80.0</c>, or <see langword="null"/>.</summary>
    public string? EngineVersion { get; init; }

    /// <summary>Its categories, such as <c>Programming Languages</c>.</summary>
    public IReadOnlyList<string> Categories { get; init; } = [];

    /// <summary>Its search tags.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>The license, such as <c>MIT</c>.</summary>
    public string? License { get; init; }

    /// <summary>The project's home page.</summary>
    public Uri? Homepage { get; init; }

    /// <summary>The source repository.</summary>
    public Uri? Repository { get; init; }

    /// <summary>
    /// The platform this version was built for: <c>universal</c> for most, or one such as <c>linux-arm64</c> for
    /// extensions that ship native programs.
    /// </summary>
    public string TargetPlatform { get; init; } = "universal";

    /// <summary>The account that published this version, or <see langword="null"/>.</summary>
    public string? PublishedBy { get; init; }

    /// <summary>Every published version, newest first.</summary>
    public IReadOnlyList<string> Versions { get; init; } = [];

    /// <summary>Extensions it depends on, as <c>namespace.name</c>.</summary>
    public IReadOnlyList<string> Dependencies { get; init; } = [];

    /// <summary>Extensions it bundles, for an extension pack.</summary>
    public IReadOnlyList<string> BundledExtensions { get; init; } = [];

    /// <summary>What replaces it when it is deprecated, as <c>namespace.name</c>, or <see langword="null"/>.</summary>
    public string? Replacement { get; init; }
}

/// <summary>A package downloaded from the registry and checked against what it published.</summary>
/// <param name="Path">The downloaded .vsix file; the caller deletes it.</param>
/// <param name="Sha256">Its SHA-256 hash, in lowercase hexadecimal.</param>
/// <param name="ChecksumVerified">Whether the hash matched the one the registry published.</param>
/// <param name="SignatureVerified">Whether the registry's signature of the package was verified.</param>
public sealed record OpenVsxDownload(string Path, string Sha256, bool ChecksumVerified, bool SignatureVerified);
