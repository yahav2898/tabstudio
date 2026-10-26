using System.Xml;
using System.Xml.Linq;
using System.Text.Json;

namespace TabStudio.Toolchains.Microchip;

/// <summary>A device pack explicitly selected by an MPLAB X configuration.</summary>
/// <param name="Vendor">Pack vendor.</param>
/// <param name="Name">Pack name.</param>
/// <param name="Version">Exact requested version.</param>
public sealed record MplabPackReference(string Vendor, string Name, string Version);

/// <summary>One MPLAB X build configuration. Option values remain verbatim, including per-file overrides.</summary>
/// <param name="Name">Configuration name.</param>
/// <param name="Device">Device name, including the PIC prefix.</param>
/// <param name="Compiler">MPLAB language toolchain identifier.</param>
/// <param name="CompilerVersion">Requested compiler version.</param>
/// <param name="Files">Project files, excluding files disabled in this configuration.</param>
/// <param name="Packs">The configuration's exact pack references.</param>
/// <param name="Options">Tool option groups, kept separate so equal property names do not overwrite other tools.</param>
/// <param name="FileOptions">Per-file tool options.</param>
public sealed record MplabConfiguration(
    string Name,
    string Device,
    string Compiler,
    string CompilerVersion,
    IReadOnlyList<string> Files,
    IReadOnlyList<MplabPackReference> Packs,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Options,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> FileOptions);

/// <summary>An MPLAB X project, read without evaluating its code or running a project script.</summary>
/// <param name="Directory">Project directory.</param>
/// <param name="Makefile">The project's declared makefile.</param>
/// <param name="Configurations">Every named configuration.</param>
public sealed record MplabProject(string Directory, string Makefile, IReadOnlyList<MplabConfiguration> Configurations)
{
    /// <summary>The marker used to recognize an MPLAB X project.</summary>
    public const string Marker = "nbproject/configurations.xml";

    /// <summary>The explicitly selected configuration, or MPLAB's default/first configuration.</summary>
    public MplabConfiguration SelectedConfiguration()
    {
        var path = Path.Combine(Directory, ".tabstudio", "pic-build.json");
        if (File.Exists(path))
        {
            if (new FileInfo(path).Length > 65536) throw new InvalidDataException("The PIC build selection is too large.");
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            if (json.RootElement.ValueKind != JsonValueKind.Object || !json.RootElement.TryGetProperty("configuration", out var property) || property.ValueKind != JsonValueKind.String)
                throw new InvalidDataException("Select a named MPLAB configuration in pic-build.json.");
            var name = property.GetString();
            return Configurations.FirstOrDefault(c => c.Name == name) ?? throw new InvalidDataException("Unknown MPLAB configuration: " + name);
        }

        return Configurations.FirstOrDefault(c => c.Name == "default") ?? Configurations[0];
    }

    /// <summary>Loads the project's configuration descriptor.</summary>
    /// <param name="configurationFile">The path of nbproject/configurations.xml.</param>
    public static MplabProject Load(string configurationFile)
    {
        ArgumentException.ThrowIfNullOrEmpty(configurationFile);
        var full = Path.GetFullPath(configurationFile);
        var projectDirectory = Path.GetDirectoryName(Path.GetDirectoryName(full))!;
        using var stream = File.OpenRead(full);
        using var reader = XmlReader.Create(stream, ReaderSettings());
        return Read(XDocument.Load(reader, LoadOptions.SetLineInfo), projectDirectory);
    }

    /// <summary>Reads XML from a configuration descriptor.</summary>
    /// <param name="xml">Descriptor XML.</param>
    /// <param name="projectDirectory">Project directory.</param>
    public static MplabProject Parse(string xml, string projectDirectory)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentException.ThrowIfNullOrEmpty(projectDirectory);
        using var text = new StringReader(xml);
        using var reader = XmlReader.Create(text, ReaderSettings());
        return Read(XDocument.Load(reader, LoadOptions.SetLineInfo), Path.GetFullPath(projectDirectory));
    }

    private static XmlReaderSettings ReaderSettings() => new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16_000_000 };

    private static MplabProject Read(XDocument document, string projectDirectory)
    {
        var root = document.Root;
        if (root?.Name.LocalName != "configurationDescriptor")
        {
            throw new InvalidDataException("Expected an MPLAB X configurationDescriptor.");
        }

        var files = root.Descendants().Where(e => e.Name.LocalName == "itemPath"
            && !e.Ancestors().Any(p => p.Name.LocalName == "logicalFolder" && (string?)p.Attribute("projectFiles") == "false"))
            .Select(e => Normalize(e.Value)).Where(f => f.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
        var configurations = new List<MplabConfiguration>();
        foreach (var conf in root.Elements().Where(e => e.Name.LocalName == "confs").SelectMany(e => e.Elements().Where(c => c.Name.LocalName == "conf")))
        {
            var name = (string?)conf.Attribute("name") ?? string.Empty;
            var tools = Child(conf, "toolsSet");
            var device = Child(tools, "targetDevice")?.Value.Trim() ?? string.Empty;
            var compiler = Child(tools, "languageToolchain")?.Value.Trim() ?? string.Empty;
            if (name.Length == 0 || device.Length == 0 || compiler.Length == 0)
            {
                throw new InvalidDataException($"Incomplete MPLAB X configuration at line {((IXmlLineInfo)conf).LineNumber}: name, targetDevice and languageToolchain are required.");
            }

            if (configurations.Any(c => c.Name == name))
            {
                throw new InvalidDataException($"MPLAB X configuration '{name}' is declared more than once.");
            }

            var items = conf.Elements().Where(e => e.Name.LocalName == "item").ToArray();
            var excluded = items.Where(e => (string?)e.Attribute("ex") is "true" or "1")
                .Select(e => Normalize((string?)e.Attribute("path") ?? string.Empty)).ToHashSet(StringComparer.Ordinal);
            var packs = Child(conf, "packs")?.Elements().Where(e => e.Name.LocalName == "pack")
                .Select(e => new MplabPackReference((string?)e.Attribute("vendor") ?? string.Empty,
                    (string?)e.Attribute("name") ?? string.Empty, (string?)e.Attribute("version") ?? string.Empty)).ToArray() ?? [];
            var overrides = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>>(StringComparer.Ordinal);
            foreach (var item in items.Where(e => !excluded.Contains(Normalize((string?)e.Attribute("path") ?? string.Empty))))
            {
                var path = Normalize((string?)item.Attribute("path") ?? string.Empty);
                if (path.Length > 0)
                {
                    overrides[path] = OptionsOf(item);
                }
            }

            configurations.Add(new MplabConfiguration(name, device, compiler,
                Child(tools, "languageToolchainVersion")?.Value.Trim() ?? string.Empty,
                [.. files.Where(f => !excluded.Contains(f))], packs, OptionsOf(conf), overrides));
        }

        if (configurations.Count == 0)
        {
            throw new InvalidDataException("The MPLAB X project has no build configurations.");
        }

        return new MplabProject(projectDirectory, Normalize(Child(root, "projectmakefile")?.Value ?? "Makefile"), configurations);
    }

    private static Dictionary<string, IReadOnlyDictionary<string, string>> OptionsOf(XElement parent)
    {
        var groups = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        foreach (var group in parent.Elements().Where(e => e.Name.LocalName != "item"))
        {
            var properties = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in group.Elements().Where(e => e.Name.LocalName == "property"))
            {
                if ((string?)property.Attribute("key") is { Length: > 0 } key)
                {
                    properties[key] = (string?)property.Attribute("value") ?? string.Empty;
                }
            }

            if (properties.Count > 0)
            {
                groups[group.Name.LocalName] = properties;
            }
        }

        return groups;
    }

    private static XElement? Child(XElement? parent, string name) => parent?.Elements().FirstOrDefault(e => e.Name.LocalName == name);

    private static string Normalize(string path) => path.Trim().Replace('\\', '/');
}
