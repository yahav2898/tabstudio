using System.Text.Json;

namespace TabStudio.Toolchains.Catalog;

/// <summary>
/// The toolchain catalog: every toolchain TabStudio knows, as data. It ships inside the app as
/// <c>Catalog/toolchains.json</c>, versioned with the code.
/// </summary>
/// <remarks>
/// <para>
/// The catalog is the one place a new toolchain is added: its packages, how to find and run its projects, its
/// language server and debug adapter, and the patterns that turn its error output into links. Its
/// <see cref="ToolchainDefinition.Listed"/> flag decides whether the Toolchain Store offers it.
/// </para>
/// <para>
/// <see cref="Linux"/> pins the Alpine Linux release toolchains are installed into on Android, and the hash of
/// its root file system for each architecture, so every device sets up the same, verified environment.
/// </para>
/// </remarks>
public sealed record ToolchainCatalog
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly Lazy<ToolchainCatalog> BuiltIn = new(LoadBuiltIn);

    /// <summary>The catalog format; a newer format than this code reads is refused.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>The catalog that ships with TabStudio.</summary>
    public static ToolchainCatalog Default => BuiltIn.Value;

    /// <summary>The format version of this catalog.</summary>
    public int SchemaVersion { get; init; }

    /// <summary>The Linux distribution toolchains are installed into on Android.</summary>
    public required LinuxDistribution Linux { get; init; }

    /// <summary>Every toolchain, in the Store's order.</summary>
    public IReadOnlyList<ToolchainDefinition> Toolchains { get; init; } = [];

    /// <summary>The toolchains the Toolchain Store offers.</summary>
    public IEnumerable<ToolchainDefinition> Listed => Toolchains.Where(t => t.Listed);

    /// <summary>Reads a catalog from JSON.</summary>
    /// <param name="json">The catalog.</param>
    /// <exception cref="InvalidDataException">The JSON is not a catalog this version of TabStudio can read.</exception>
    public static ToolchainCatalog Load(Stream json)
    {
        ArgumentNullException.ThrowIfNull(json);
        ToolchainCatalog? catalog;
        try
        {
            catalog = JsonSerializer.Deserialize<ToolchainCatalog>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"The toolchain catalog is not valid: {ex.Message}", ex);
        }

        if (catalog is null)
        {
            throw new InvalidDataException("The toolchain catalog is empty.");
        }

        if (catalog.SchemaVersion is < 1 or > CurrentSchemaVersion)
        {
            throw new InvalidDataException($"The toolchain catalog has format {catalog.SchemaVersion}; this version of TabStudio reads format {CurrentSchemaVersion}.");
        }

        var duplicate = catalog.Toolchains.GroupBy(t => t.Id, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidDataException($"The toolchain catalog lists '{duplicate.Key}' more than once.");
        }

        return catalog;
    }

    /// <summary>Finds a toolchain by its id.</summary>
    /// <param name="id">The id, such as <c>python</c>.</param>
    public ToolchainDefinition? Find(string id) => Toolchains.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.Ordinal));

    /// <summary>Finds the toolchain that runs a file, by its extension; listed toolchains first.</summary>
    /// <param name="path">The file.</param>
    public ToolchainDefinition? ForFile(string path) =>
        Toolchains.Where(t => t.Id != "android" && t.RunsFile(path)).OrderByDescending(t => t.Listed).FirstOrDefault();

    /// <summary>
    /// Finds the toolchain that installs a language server or debug adapter, by the id its clients know it by, such
    /// as <c>pyright</c> or <c>js-debug</c>: what the Store must install for that tool to run.
    /// </summary>
    /// <param name="toolId">The tool's id (<see cref="ToolCommandDefinition.Id"/>).</param>
    /// <returns>The toolchain, listed ones first, or <see langword="null"/>.</returns>
    public ToolchainDefinition? ForTool(string toolId) =>
        Toolchains
            .Where(t => string.Equals(t.LanguageServer?.Id, toolId, StringComparison.Ordinal) || string.Equals(t.DebugAdapter?.Id, toolId, StringComparison.Ordinal))
            .OrderByDescending(t => t.Listed)
            .FirstOrDefault();

    private static ToolchainCatalog LoadBuiltIn()
    {
        using var stream = typeof(ToolchainCatalog).Assembly.GetManifestResourceStream("TabStudio.Toolchains.Catalog.toolchains.json")
            ?? throw new InvalidOperationException("The built-in toolchain catalog is missing from the assembly.");
        return Load(stream);
    }
}
