using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace TabStudio.Toolchains.Embedded.Peripherals;

/// <summary>Reads bounded, entity-free CMSIS-SVD metadata, resolving named inheritance and register arrays.</summary>
public static class SvdReader
{
    private const int MaximumInstances = 4096;

    /// <summary>Reads peripheral/register/field inheritance, clusters and dim arrays. Rejects invalid or ambiguous metadata.</summary>
    public static SvdDevice Parse(string xml)
    {
        ArgumentNullException.ThrowIfNull(xml);
        using var input = new StringReader(xml);
        using var reader = XmlReader.Create(input, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
            MaxCharactersInDocument = 16 * 1024 * 1024, MaxCharactersFromEntities = 0
        });
        var document = XDocument.Load(reader);
        var device = document.Root ?? throw new FormatException("SVD has no device element.");
        if (device.Name.LocalName != "device")
            throw new FormatException("SVD root must be device.");
        var symbols = new Dictionary<string, XElement>(StringComparer.Ordinal);
        Collect(device.Element("peripherals"), "", symbols);
        var peripherals = new List<SvdPeripheral>();
        var budget = new ExpansionBudget();
        var deviceSize = Integer(device, "size", 32);
        var deviceAccess = Text(device, "access") ?? "read-write";
        foreach (var source in device.Element("peripherals")?.Elements("peripheral") ?? [])
        {
            var name = Required(source, "name");
            var effective = Resolve(source, name, symbols, new HashSet<string>(StringComparer.Ordinal));
            // Inherited child references are relative to the new peripheral instance as well.
            var inheritedSymbols = new Dictionary<string, XElement>(StringComparer.Ordinal);
            Collect(effective.Element("registers"), name, inheritedSymbols);
            foreach (var entry in inheritedSymbols) symbols[entry.Key] = entry.Value;
            var address = Number(Required(effective, "baseAddress"));
            var registers = new List<SvdRegister>();
            AddRegisters(effective.Element("registers"), name, "", address,
                Integer(effective, "size", deviceSize), Text(effective, "access") ?? deviceAccess,
                symbols, registers, budget, 0);
            peripherals.Add(new(name, Description(effective), address, registers));
        }
        return new(Text(device, "name") ?? "Unnamed device", peripherals)
        {
            ByteOrder = device.Element("cpu")?.Element("endian")?.Value.Trim() ?? "unspecified"
        };
    }

    private static void Collect(XElement? container, string prefix, Dictionary<string, XElement> symbols)
    {
        if (container is null) return;
        foreach (var child in container.Elements())
        {
            var name = Text(child, "name");
            var path = name is null ? prefix : Join(prefix, name);
            if (name is not null && !symbols.TryAdd(path, child))
                throw new FormatException($"Duplicate SVD declaration: {path}.");
            foreach (var nested in child.Elements().Where(x => x.Name.LocalName is "registers" or "fields"))
                Collect(nested, path, symbols);
            if (child.Name.LocalName == "cluster") CollectClusterChildren(child, path, symbols);
        }
    }

    private static void CollectClusterChildren(XElement cluster, string prefix, Dictionary<string, XElement> symbols)
    {
        foreach (var child in cluster.Elements().Where(x => x.Name.LocalName is "register" or "cluster"))
        {
            var path = Join(prefix, Required(child, "name"));
            if (!symbols.TryAdd(path, child)) throw new FormatException($"Duplicate SVD declaration: {path}.");
            Collect(child.Element("fields"), path, symbols);
            if (child.Name.LocalName == "cluster") CollectClusterChildren(child, path, symbols);
        }
    }

    private static XElement Resolve(XElement source, string path, IReadOnlyDictionary<string, XElement> symbols, HashSet<string> visiting)
    {
        if (!visiting.Add(path)) throw new FormatException($"Circular SVD derivedFrom at {path}.");
        try
        {
            var derived = source.Attribute("derivedFrom")?.Value;
            if (derived is null) return new XElement(source);
            var parent = path.Contains('.') ? path[..path.LastIndexOf('.')] : "";
            var basePath = derived.Contains('.') ? derived : Join(parent, derived);
            if (!symbols.TryGetValue(basePath, out var basis))
                throw new FormatException($"Unknown SVD derivedFrom '{derived}' at {path}.");
            var merged = Resolve(basis, basePath, symbols, visiting);
            Merge(merged, source);
            merged.Attribute("derivedFrom")?.Remove();
            return merged;
        }
        finally { visiting.Remove(path); }
    }

    private static void Merge(XElement basis, XElement overlay)
    {
        foreach (var attribute in overlay.Attributes()) basis.SetAttributeValue(attribute.Name, attribute.Value);
        foreach (var child in overlay.Elements())
        {
            var name = Text(child, "name");
            var previous = basis.Elements(child.Name).FirstOrDefault(x => name is null || Text(x, "name") == name);
            if (previous is null) basis.Add(new XElement(child));
            else if (child.Name.LocalName is "registers" or "fields" or "register" or "cluster" or "field") Merge(previous, child);
            else previous.ReplaceWith(new XElement(child));
        }
    }

    private static void AddRegisters(XElement? container, string sourcePrefix, string displayPrefix, ulong baseAddress,
        int parentSize, string parentAccess, IReadOnlyDictionary<string, XElement> symbols, List<SvdRegister> result, ExpansionBudget budget, int depth)
    {
        if (container is null) return;
        if (depth > 32) throw new FormatException("SVD cluster nesting exceeds its bound.");
        foreach (var original in container.Elements().Where(x => x.Name.LocalName is "register" or "cluster"))
        {
            var originalName = Required(original, "name");
            var sourcePath = Join(sourcePrefix, originalName);
            var element = Resolve(original, sourcePath, symbols, new HashSet<string>(StringComparer.Ordinal));
            foreach (var (instanceName, increment) in Instances(element))
            {
                if (++budget.Instances > 131072) throw new FormatException("SVD expands to too many cluster/register instances.");
                if (result.Count >= MaximumInstances) throw new FormatException("SVD expands to too many registers.");
                var address = checked(baseAddress + Number(Required(element, "addressOffset")) + increment);
                var name = Join(displayPrefix, instanceName);
                var size = Integer(element, "size", parentSize);
                var access = Text(element, "access") ?? parentAccess;
                if (element.Name.LocalName == "cluster")
                {
                    AddRegisters(element, sourcePath, name, address, size, access, symbols, result, budget, depth + 1);
                    continue;
                }
                if (size is < 1 or > 64) throw new FormatException($"Unsupported register width {size} at {sourcePath}.");
                var modified = Text(element, "modifiedWriteValues");
                var readAction = Text(element, "readAction");
                var fields = new List<SvdField>();
                ulong occupied = 0;
                foreach (var originalField in element.Element("fields")?.Elements("field") ?? [])
                {
                    var fieldPath = Join(sourcePath, Required(originalField, "name"));
                    var field = Resolve(originalField, fieldPath, symbols, new HashSet<string>(StringComparer.Ordinal));
                    var (offset, width) = BitRange(field);
                    foreach (var (fieldName, fieldIncrement) in Instances(field))
                    {
                        if (++budget.Fields > 262144) throw new FormatException("SVD expands to too many fields.");
                        var bit = checked(offset + (int)fieldIncrement);
                        if (width < 1 || width > size || bit < 0 || bit > size - width)
                            throw new FormatException($"Invalid field range at {fieldPath}.");
                        var values = new List<SvdEnumValue>();
                        foreach (var value in field.Elements("enumeratedValues").Elements("enumeratedValue"))
                        {
                            var text = Text(value, "value");
                            if (text is null || text.Contains('x', StringComparison.OrdinalIgnoreCase) && !text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                                continue;
                            values.Add(new(Required(value, "name"), Number(text), Description(value)));
                        }
                        var parsed = new SvdField(fieldName, Description(field), bit, width,
                            Text(field, "access") ?? access, Text(field, "modifiedWriteValues") ?? modified,
                            Text(field, "readAction") ?? readAction, values);
                        if ((occupied & parsed.Mask) != 0) throw new FormatException($"Overlapping SVD fields at {fieldPath}.");
                        occupied |= parsed.Mask;
                        fields.Add(parsed);
                    }
                }
                var reset = Text(element, "resetValue");
                result.Add(new(name, Description(element), address, size, access, reset is null ? null : Number(reset),
                    modified, readAction, fields));
            }
        }
    }

    private static IEnumerable<(string Name, ulong Increment)> Instances(XElement element)
    {
        var name = Required(element, "name");
        var count = Integer(element, "dim", 1);
        if (count is < 1 or > MaximumInstances) throw new FormatException("Invalid SVD dim count.");
        if (count == 1 && Text(element, "dim") is null) { yield return (name, 0); yield break; }
        var step = Number(Required(element, "dimIncrement"));
        var indexText = Text(element, "dimIndex");
        string[] indexes;
        if (indexText is null)
        {
            indexes = Enumerable.Range(0, count).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToArray();
        }
        else if (indexText.Contains(','))
        {
            indexes = indexText.Split(',', StringSplitOptions.TrimEntries);
        }
        else if (indexText.Contains('-'))
        {
            var ends = indexText.Split('-', StringSplitOptions.TrimEntries);
            if (ends.Length != 2) throw new FormatException("Invalid SVD dimIndex range.");
            if (int.TryParse(ends[0], CultureInfo.InvariantCulture, out var start) && int.TryParse(ends[1], CultureInfo.InvariantCulture, out var end))
            {
                if ((long)end - start + 1 != count) throw new FormatException("SVD dimIndex count does not match dim.");
                indexes = Enumerable.Range(start, count).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToArray();
            }
            else if (ends[0].Length == 1 && ends[1].Length == 1 && ends[1][0] - ends[0][0] + 1 == count)
            {
                indexes = Enumerable.Range(ends[0][0], count).Select(i => ((char)i).ToString()).ToArray();
            }
            else
            {
                throw new FormatException("Unsupported SVD dimIndex range.");
            }
        }
        else
        {
            indexes = [indexText];
        }
        if (indexes.Length != count) throw new FormatException("SVD dimIndex count does not match dim.");
        for (var i = 0; i < count; i++)
            yield return (name.Contains("%s", StringComparison.Ordinal) ? name.Replace("%s", indexes[i], StringComparison.Ordinal) : name + indexes[i], checked((ulong)i * step));
    }

    private static (int Offset, int Width) BitRange(XElement field)
    {
        if (Text(field, "bitOffset") is { } offset) return (checked((int)Number(offset)), checked((int)Number(Required(field, "bitWidth"))));
        if (Text(field, "lsb") is { } lsb)
        {
            var low = checked((int)Number(lsb));
            return (low, checked((int)Number(Required(field, "msb"))) - low + 1);
        }
        var range = Required(field, "bitRange").Trim('[', ']').Split(':');
        if (range.Length != 2) throw new FormatException("SVD bitRange must be [msb:lsb].");
        var minimum = checked((int)Number(range[1]));
        return (minimum, checked((int)Number(range[0])) - minimum + 1);
    }

    private static ulong Number(string value)
    {
        var text = value.Trim().Replace("_", "", StringComparison.Ordinal);
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return ulong.Parse(text[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        if (text.StartsWith('#')) return Convert.ToUInt64(text[1..], 2);
        if (text.StartsWith("0b", StringComparison.OrdinalIgnoreCase)) return Convert.ToUInt64(text[2..], 2);
        return ulong.Parse(text, CultureInfo.InvariantCulture);
    }

    private static int Integer(XElement element, string name, int fallback) => Text(element, name) is { } value ? checked((int)Number(value)) : fallback;
    private static string Join(string left, string right) => left.Length == 0 ? right : left + "." + right;
    private static string? Text(XElement element, string name) => element.Element(name)?.Value.Trim();
    private static string Required(XElement element, string name) => Text(element, name) ?? throw new FormatException($"SVD {element.Name.LocalName} is missing {name}.");
    private static string Description(XElement element) => Text(element, "description") ?? "";

    private sealed class ExpansionBudget
    {
        public int Instances { get; set; }
        public int Fields { get; set; }
    }
}
