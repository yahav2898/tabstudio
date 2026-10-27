using System.Text.Json;
using System.Text.Json.Serialization;

namespace TabStudio.Toolchains.Python;

/// <summary>What compiling one Python package needs, from <c>python-build-packages.json</c>.</summary>
/// <param name="Packages">The Alpine packages, such as <c>libpq-dev</c>.</param>
/// <param name="Needs">What they bring, in words, such as "PostgreSQL's pg_config and client headers".</param>
/// <param name="Note">What else helps, such as a release that ships wheels, or <see langword="null"/>.</param>
public sealed record PythonBuildRequirement(IReadOnlyList<string> Packages, string? Needs, string? Note);

/// <summary>
/// The Alpine Linux packages that compiling Python packages needs: the compilers and Python's headers for every
/// build, and the libraries' headers that known packages build against (psycopg2, mysqlclient, lxml, Pillow, cffi,
/// cryptography and others). The table is data, <c>Python/python-build-packages.json</c>, so a package is added
/// there without a code change.
/// </summary>
public sealed class PythonBuildPackages
{
    private static readonly Lazy<PythonBuildPackages> Table = new(Load);
    private readonly Dictionary<string, PythonBuildRequirement> _packages;
    private readonly Dictionary<string, string> _headers;

    private PythonBuildPackages(IReadOnlyList<string> buildTools, Dictionary<string, PythonBuildRequirement> packages, Dictionary<string, string> headers)
    {
        BuildTools = buildTools;
        _packages = packages;
        _headers = headers;
    }

    /// <summary>The table that ships with TabStudio.</summary>
    public static PythonBuildPackages Default => Table.Value;

    /// <summary>What every build needs: <c>build-base</c> (the C compiler, make and the C library's headers) and Python's headers.</summary>
    public IReadOnlyList<string> BuildTools { get; }

    /// <summary>The Python packages the table knows, by normalized name.</summary>
    public IReadOnlyCollection<string> KnownPackages => _packages.Keys;

    /// <summary>What compiling a Python package needs, or <see langword="null"/> when the table does not list it.</summary>
    /// <param name="name">The package's name, in any spelling, such as <c>Pillow</c>.</param>
    public PythonBuildRequirement? For(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _packages.GetValueOrDefault(PythonRequirements.Normalize(name));
    }

    /// <summary>The Alpine package with a C header, such as <c>libffi-dev</c> for <c>ffi.h</c>, or <see langword="null"/>.</summary>
    /// <param name="header">The header as the compiler named it, such as <c>libxml/xmlversion.h</c>.</param>
    public string? ForHeader(string header)
    {
        ArgumentNullException.ThrowIfNull(header);
        return _headers.GetValueOrDefault(header.Trim('<', '>', '"'));
    }

    /// <summary>
    /// The Alpine packages to install before building: the build tools, the packages the table lists for each
    /// failed package, and those with the headers the compiler did not find.
    /// </summary>
    /// <param name="failed">The Python packages that failed to build.</param>
    /// <param name="missingHeaders">The headers the compiler reported missing.</param>
    public IReadOnlyList<string> PackagesFor(IEnumerable<string> failed, IEnumerable<string> missingHeaders)
    {
        ArgumentNullException.ThrowIfNull(failed);
        ArgumentNullException.ThrowIfNull(missingHeaders);
        return
        [
            .. BuildTools
                .Concat(failed.SelectMany(name => For(name)?.Packages ?? []))
                .Concat(missingHeaders.Select(ForHeader).OfType<string>())
                .Distinct(StringComparer.Ordinal),
        ];
    }

    /// <summary>Reads a table in the format of <c>python-build-packages.json</c>.</summary>
    /// <param name="json">The table.</param>
    public static PythonBuildPackages Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var file = JsonSerializer.Deserialize(json, TableJsonContext.Default.TableFile) ?? throw new JsonException("The Python build table is empty.");
        return new PythonBuildPackages(
            file.BuildTools ?? [],
            (file.Packages ?? []).ToDictionary(p => PythonRequirements.Normalize(p.Key), p => new PythonBuildRequirement(p.Value.Packages ?? [], p.Value.Needs, p.Value.Note), StringComparer.Ordinal),
            new Dictionary<string, string>(file.Headers ?? [], StringComparer.Ordinal));
    }

    private static PythonBuildPackages Load()
    {
        using var stream = typeof(PythonBuildPackages).Assembly.GetManifestResourceStream("TabStudio.Toolchains.Python.python-build-packages.json")
            ?? throw new InvalidOperationException("The Python build table is missing from the assembly.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    internal sealed class TableFile
    {
        [JsonPropertyName("buildTools")]
        public List<string>? BuildTools { get; set; }

        [JsonPropertyName("packages")]
        public Dictionary<string, TableEntry>? Packages { get; set; }

        [JsonPropertyName("headers")]
        public Dictionary<string, string>? Headers { get; set; }
    }

    internal sealed class TableEntry
    {
        [JsonPropertyName("packages")]
        public List<string>? Packages { get; set; }

        [JsonPropertyName("needs")]
        public string? Needs { get; set; }

        [JsonPropertyName("note")]
        public string? Note { get; set; }
    }
}

[JsonSourceGenerationOptions(ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true)]
[JsonSerializable(typeof(PythonBuildPackages.TableFile))]
internal sealed partial class TableJsonContext : JsonSerializerContext;
