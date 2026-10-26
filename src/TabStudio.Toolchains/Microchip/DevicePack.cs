using System.Xml;
using System.Xml.Linq;

namespace TabStudio.Toolchains.Microchip;

/// <summary>A locally installed Microchip device-family pack, identified by its PDSC descriptor.</summary>
/// <param name="Vendor">Vendor from the descriptor.</param>
/// <param name="Name">Pack name.</param>
/// <param name="Version">Pack version.</param>
/// <param name="Directory">Installed directory containing the descriptor.</param>
/// <param name="Devices">Devices explicitly listed by this pack.</param>
public sealed record DevicePack(string Vendor, string Name, string Version, string Directory, IReadOnlySet<string> Devices)
{
    /// <summary>Reads a pack's PDSC file. Device support is read from the descriptor, never inferred from its family name.</summary>
    /// <param name="descriptor">PDSC path.</param>
    public static DevicePack Load(string descriptor)
    {
        ArgumentException.ThrowIfNullOrEmpty(descriptor);
        using var stream = File.OpenRead(descriptor);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 32_000_000,
        });
        var root = XDocument.Load(reader).Root;
        if (root?.Name.LocalName != "package")
        {
            throw new InvalidDataException("Expected a device pack's PDSC package descriptor.");
        }

        var vendor = root.Elements().FirstOrDefault(e => e.Name.LocalName == "vendor")?.Value.Trim() ?? string.Empty;
        var name = root.Elements().FirstOrDefault(e => e.Name.LocalName == "name")?.Value.Trim() ?? string.Empty;
        var version = (string?)root.Descendants().FirstOrDefault(e => e.Name.LocalName == "release")?.Attribute("version") ?? string.Empty;
        if (vendor.Length == 0 || name.Length == 0 || !System.Version.TryParse(version, out _))
        {
            throw new InvalidDataException("The device pack descriptor is missing its vendor, name or numeric version.");
        }

        var devices = root.Descendants().Where(e => e.Name.LocalName == "device")
            .Select(e => (string?)e.Attribute("Dname")).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new DevicePack(vendor, name, version, Path.GetDirectoryName(Path.GetFullPath(descriptor))!, devices);
    }

    /// <summary>The XC compiler's device-support directory passed to -mdfp.</summary>
    /// <param name="compiler">XC8 or XC32.</param>
    public string CompilerDirectory(string compiler)
    {
        ArgumentException.ThrowIfNullOrEmpty(compiler);
        var folder = compiler.ToUpperInvariant() switch
        {
            "XC8" => "xc8",
            "XC32" => "xc32",
            _ => throw new ArgumentException("Device packs are resolved for XC8 or XC32.", nameof(compiler)),
        };
        var path = Path.Combine(Directory, folder);
        return System.IO.Directory.Exists(path) ? path : throw new InvalidDataException($"{Vendor}.{Name} {Version} has no {compiler} support directory.");
    }

    /// <summary>Resolves a device, honoring an exact MPLAB pack reference when present.</summary>
    /// <param name="packs">Installed packs.</param>
    /// <param name="device">The exact device name.</param>
    /// <param name="requested">The project's explicit pack reference, or null to select the newest supporting pack.</param>
    public static DevicePack? Resolve(IEnumerable<DevicePack> packs, string device, MplabPackReference? requested = null)
    {
        ArgumentNullException.ThrowIfNull(packs);
        ArgumentException.ThrowIfNullOrEmpty(device);
        return packs.Where(p => p.Devices.Contains(device)
                && (requested is null || (p.Vendor.Equals(requested.Vendor, StringComparison.OrdinalIgnoreCase)
                    && p.Name.Equals(requested.Name, StringComparison.OrdinalIgnoreCase) && p.Version == requested.Version)))
            .OrderByDescending(p => System.Version.Parse(p.Version)).FirstOrDefault();
    }
}
