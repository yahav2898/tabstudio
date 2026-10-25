using System.Text.Json;
using System.Text.Json.Serialization;

namespace TabStudio.App.Toolbox;

/// <summary>
/// The UI frameworks the Toolbox knows: one JSON file each, embedded in the app from its Toolbox folder.
/// </summary>
public sealed class ToolboxCatalog
{
    private const string ResourcePrefix = "Toolbox/";

    /// <summary>Creates a catalog of the given frameworks.</summary>
    /// <param name="frameworks">The frameworks.</param>
    public ToolboxCatalog(IEnumerable<ToolboxFramework> frameworks)
    {
        ArgumentNullException.ThrowIfNull(frameworks);
        Frameworks = [.. frameworks];
    }

    /// <summary>The frameworks, such as Avalonia.</summary>
    public IReadOnlyList<ToolboxFramework> Frameworks { get; }

    /// <summary>Reads every framework file embedded in the app.</summary>
    /// <exception cref="InvalidDataException">A file is not a framework description.</exception>
    public static ToolboxCatalog LoadEmbedded()
    {
        var assembly = typeof(ToolboxCatalog).Assembly;
        var frameworks = new List<ToolboxFramework>();
        foreach (var resource in assembly.GetManifestResourceNames().Order(StringComparer.Ordinal))
        {
            if (!resource.StartsWith(ResourcePrefix, StringComparison.Ordinal) || !resource.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(resource)!;
            frameworks.Add(Read(stream, resource));
        }

        return new ToolboxCatalog(frameworks);
    }

    /// <summary>Reads one framework description.</summary>
    /// <param name="stream">The JSON.</param>
    /// <param name="source">Where it comes from, for the error message.</param>
    /// <exception cref="InvalidDataException">The JSON is not a framework description.</exception>
    public static ToolboxFramework Read(Stream stream, string source)
    {
        ArgumentNullException.ThrowIfNull(stream);
        try
        {
            var framework = JsonSerializer.Deserialize(stream, ToolboxJsonContext.Default.ToolboxFramework);
            return framework is { Name.Length: > 0 }
                ? framework
                : throw new InvalidDataException($"{source} does not name its framework.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{source} is not a Toolbox description: {ex.Message}", ex);
        }
    }

    /// <summary>The framework whose Toolbox applies to a file, such as Avalonia's for an <c>.axaml</c> file.</summary>
    /// <param name="path">The file, or <see langword="null"/> for none.</param>
    public ToolboxFramework? ForFile(string? path)
    {
        if (path is null)
        {
            return null;
        }

        foreach (var framework in Frameworks)
        {
            if (framework.AppliesTo(path))
            {
                return framework;
            }
        }

        return null;
    }
}

/// <summary>Reads the Toolbox's JSON files, which have comments for the people who edit them.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true)]
[JsonSerializable(typeof(ToolboxFramework))]
internal sealed partial class ToolboxJsonContext : JsonSerializerContext
{
}
