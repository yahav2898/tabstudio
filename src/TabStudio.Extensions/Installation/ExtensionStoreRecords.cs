using System.Text.Json.Serialization;

namespace TabStudio.Extensions.Installation;

/// <summary>The list of installed extensions, as <c>extensions.json</c> in the extensions folder keeps it.</summary>
/// <param name="Extensions">One record per installed extension.</param>
internal sealed record InstalledRecords(IReadOnlyList<InstalledRecord> Extensions);

/// <summary>One installed extension in <c>extensions.json</c>.</summary>
/// <param name="Id">The identifier, <c>publisher.name</c>.</param>
/// <param name="Version">The installed version.</param>
/// <param name="Folder">The folder's name inside the extensions folder.</param>
/// <param name="Enabled">Whether its contributions are active.</param>
/// <param name="Origin">Where it came from.</param>
/// <param name="Registry">The registry it was downloaded from, if any.</param>
/// <param name="FileName">The .vsix file it was installed from, if any.</param>
/// <param name="Sha256">The package's hash.</param>
/// <param name="Verification">What was verified at install time.</param>
/// <param name="InstalledAt">When it was installed.</param>
internal sealed record InstalledRecord(
    string Id,
    string Version,
    string Folder,
    bool Enabled,
    ExtensionOrigin Origin,
    string? Registry,
    string? FileName,
    string? Sha256,
    PackageVerification Verification,
    DateTimeOffset InstalledAt);

/// <summary>Source-generated JSON for <c>extensions.json</c>.</summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(InstalledRecords))]
internal sealed partial class ExtensionStoreJsonContext : JsonSerializerContext
{
}
