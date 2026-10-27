using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TabStudio.Toolchains.Embedded.Analysis;

/// <summary>A linker memory region. Regions must describe the selected chip rather than a similar board.</summary>
/// <param name="Name">Linker region name.</param>
/// <param name="Origin">First address in the region.</param>
/// <param name="Length">Region size in bytes.</param>
/// <param name="IsRam">Whether the region is volatile RAM.</param>
public sealed record FirmwareMemoryRegion(string Name, ulong Origin, ulong Length, bool IsRam);

/// <summary>An allocated ELF section and its runtime and load placement.</summary>
/// <param name="Name">Section name.</param>
/// <param name="Address">Runtime address.</param>
/// <param name="LoadAddress">Load address from the containing PT_LOAD segment, if file backed.</param>
/// <param name="Size">Runtime size, including zero initialized storage.</param>
/// <param name="FileSize">Bytes stored in the firmware image.</param>
/// <param name="IsRam">RAM classification, inferred from SHF_WRITE when no region was supplied.</param>
/// <param name="Region">Matching linker region, if known.</param>
public sealed record FirmwareSection(string Name, ulong Address, ulong? LoadAddress, ulong Size,
    ulong FileSize, bool IsRam, string? Region);

/// <summary>A sized ELF symbol; aliases may overlap and are not summed to produce memory totals.</summary>
/// <param name="Name">Symbol name, as emitted by the linker.</param>
/// <param name="Address">Runtime address.</param>
/// <param name="Size">Symbol's declared byte size.</param>
/// <param name="Section">Allocated section containing the symbol.</param>
/// <param name="SourceFile">STT_FILE ownership for local symbols, when available.</param>
public sealed record FirmwareSymbol(string Name, ulong Address, ulong Size, string Section, string? SourceFile);

/// <summary>A GNU linker map input contribution. Padding and linker-created data are not attributed to a file.</summary>
/// <param name="Section">Input section.</param>
/// <param name="Address">Runtime address.</param>
/// <param name="Size">Contribution in bytes.</param>
/// <param name="ObjectFile">Object/archive member emitted by the linker.</param>
public sealed record FirmwareObjectContribution(string Section, ulong Address, ulong Size, string ObjectFile);

/// <summary>Memory usage from a linked ELF, independent of debug sections and symbol aliases.</summary>
/// <param name="FlashBytes">Union of allocated file-backed load ranges.</param>
/// <param name="RamBytes">Union of runtime ranges classified as RAM.</param>
/// <param name="Sections">Allocated sections, suitable for a treemap.</param>
/// <param name="Symbols">Sized symbols, suitable for drill down.</param>
/// <param name="PlacementInferred">True when RAM placement used ELF flags instead of chip memory regions.</param>
public sealed record FirmwareMemoryReport(ulong FlashBytes, ulong RamBytes,
    IReadOnlyList<FirmwareSection> Sections, IReadOnlyList<FirmwareSymbol> Symbols, bool PlacementInferred)
{
    /// <summary>Compares two linked builds; use the same memory-region configuration for both.</summary>
    public FirmwareMemoryDelta Compare(FirmwareMemoryReport previous)
    {
        ArgumentNullException.ThrowIfNull(previous);
        return new(checked((long)FlashBytes - (long)previous.FlashBytes),
            checked((long)RamBytes - (long)previous.RamBytes));
    }
}

/// <summary>A signed byte change since a preceding build.</summary>
/// <param name="FlashBytes">Additional or removed flash bytes.</param>
/// <param name="RamBytes">Additional or removed RAM bytes.</param>
public sealed record FirmwareMemoryDelta(long FlashBytes, long RamBytes);

/// <summary>Reads ELF32/ELF64, little or big endian, without starting or loading firmware.</summary>
public static partial class MemoryAnalyzer
{
    private sealed record Section(uint Name, uint Type, ulong Flags, ulong Address, ulong Offset,
        ulong Size, uint Link, ulong EntrySize);
    private sealed record Segment(ulong Offset, ulong Address, ulong Physical, ulong FileSize, ulong MemorySize);

    /// <summary>Analyzes ET_EXEC/ET_DYN allocated sections; malformed tables and overflow are rejected.</summary>
    public static FirmwareMemoryReport AnalyzeElf(ReadOnlySpan<byte> image,
        IReadOnlyList<FirmwareMemoryRegion>? regions = null)
    {
        if (image.Length < 16 || image[0] != 0x7f || image[1] != 'E' || image[2] != 'L' || image[3] != 'F')
            throw new InvalidDataException("Not an ELF image.");
        var wide = image[4] switch { 1 => false, 2 => true, _ => throw new InvalidDataException("Unsupported ELF class.") };
        var little = image[5] switch { 1 => true, 2 => false, _ => throw new InvalidDataException("Unsupported ELF byte order.") };
        if (image[6] != 1) throw new InvalidDataException("Unsupported ELF version.");
        Need(image, 0, wide ? 64u : 52u);
        var type = U16(image, 16, little);
        if (type is not (2 or 3)) throw new InvalidDataException("Memory analysis requires a linked ELF executable.");
        var phoff = wide ? U64(image, 32, little) : U32(image, 28, little);
        var shoff = wide ? U64(image, 40, little) : U32(image, 32, little);
        var phsize = U16(image, wide ? 54u : 42u, little);
        var phcount = U16(image, wide ? 56u : 44u, little);
        var shsize = U16(image, wide ? 58u : 46u, little);
        var shcount = U16(image, wide ? 60u : 48u, little);
        var namesIndex = U16(image, wide ? 62u : 50u, little);
        if (shcount == 0 || namesIndex == 0xffff || phcount == 0xffff)
            throw new InvalidDataException("Extended or absent ELF section tables are not supported.");
        if (shsize < (wide ? 64 : 40) || (phcount > 0 && phsize < (wide ? 56 : 32)))
            throw new InvalidDataException("Truncated ELF entry size.");
        Need(image, shoff, checked((ulong)shsize * shcount));
        if (phcount > 0) Need(image, phoff, checked((ulong)phsize * phcount));
        if (namesIndex >= shcount) throw new InvalidDataException("Invalid section-name table.");
        foreach (var region in regions ?? Array.Empty<FirmwareMemoryRegion>())
        {
            if (region.Length == 0 || region.Origin > ulong.MaxValue - region.Length)
                throw new ArgumentException("Memory regions must have nonzero, non-overflowing ranges.", nameof(regions));
        }
        var sections = new List<Section>(shcount);
        for (var i = 0; i < shcount; i++)
        {
            var p = checked(shoff + (ulong)i * shsize);
            var section = wide
                ? new Section(U32(image, p, little), U32(image, p + 4, little), U64(image, p + 8, little),
                    U64(image, p + 16, little), U64(image, p + 24, little), U64(image, p + 32, little),
                    U32(image, p + 40, little), U64(image, p + 56, little))
                : new Section(U32(image, p, little), U32(image, p + 4, little), U32(image, p + 8, little),
                    U32(image, p + 12, little), U32(image, p + 16, little), U32(image, p + 20, little),
                    U32(image, p + 24, little), U32(image, p + 36, little));
            if (section.Type != 8) Need(image, section.Offset, section.Size);
            if (section.Address > ulong.MaxValue - section.Size) throw new InvalidDataException("Section address overflow.");
            sections.Add(section);
        }
        var segments = new List<Segment>();
        for (var i = 0; i < phcount; i++)
        {
            var p = checked(phoff + (ulong)i * phsize);
            if (U32(image, p, little) != 1) continue;
            var segment = wide
                ? new Segment(U64(image, p + 8, little), U64(image, p + 16, little), U64(image, p + 24, little), U64(image, p + 32, little), U64(image, p + 40, little))
                : new Segment(U32(image, p + 4, little), U32(image, p + 8, little), U32(image, p + 12, little), U32(image, p + 16, little), U32(image, p + 20, little));
            if (segment.Address > ulong.MaxValue - segment.MemorySize || segment.Physical > ulong.MaxValue - segment.MemorySize)
                throw new InvalidDataException("Segment address overflow.");
            if (segment.FileSize > segment.MemorySize) throw new InvalidDataException("ELF load file size exceeds memory size.");
            Need(image, segment.Offset, segment.FileSize);
            segments.Add(segment);
        }
        var names = sections[namesIndex];
        if (names.Type != 3) throw new InvalidDataException("Section-name table is not a string table.");
        var allocated = new Dictionary<int, FirmwareSection>();
        for (var i = 0; i < sections.Count; i++)
        {
            var s = sections[i];
            if ((s.Flags & 2) == 0 || s.Size == 0) continue;
            var name = StringAt(image, names, s.Name);
            var region = regions?.FirstOrDefault(r => s.Address >= r.Origin && s.Address - r.Origin <= r.Length && s.Size <= r.Length - (s.Address - r.Origin));
            var ram = region?.IsRam ?? ((s.Flags & 1) != 0);
            ulong? load = null;
            var fileSize = s.Type == 8 ? 0 : s.Size;
            if (fileSize != 0)
            {
                var segment = segments.FirstOrDefault(p => s.Address >= p.Address && s.Address - p.Address <= p.FileSize
                    && s.Offset >= p.Offset && s.Offset - p.Offset == s.Address - p.Address
                    && fileSize <= p.FileSize - (s.Address - p.Address));
                load = segment is null ? s.Address : checked(segment.Physical + s.Address - segment.Address);
            }
            allocated.Add(i, new(name, s.Address, load, s.Size, fileSize, ram, region?.Name));
        }
        var symbols = new List<FirmwareSymbol>();
        foreach (var table in sections.Where(s => s.Type is 2 or 11))
        {
            if (table.Link >= sections.Count || sections[(int)table.Link].Type != 3)
                throw new InvalidDataException("Invalid symbol string table.");
            var minimum = wide ? 24u : 16u;
            if (table.EntrySize < minimum || table.Size % table.EntrySize != 0)
                throw new InvalidDataException("Invalid symbol entry size.");
            if (table.Size / table.EntrySize > 262_144)
                throw new InvalidDataException("ELF exceeds the 262,144-symbol analysis limit.");
            string? source = null;
            for (ulong at = 0; at < table.Size; at += table.EntrySize)
            {
                var p = checked(table.Offset + at);
                var symbolName = StringAt(image, sections[(int)table.Link], U32(image, p, little));
                var info = image[checked((int)(p + (wide ? 4u : 12u)))];
                var index = U16(image, p + (wide ? 6u : 14u), little);
                var address = wide ? U64(image, p + 8, little) : U32(image, p + 4, little);
                var size = wide ? U64(image, p + 16, little) : U32(image, p + 8, little);
                if ((info & 15) == 4) { source = symbolName; continue; }
                if ((info & 15) is not (1 or 2) || size == 0 || !allocated.TryGetValue(index, out var section)) continue;
                symbols.Add(new(symbolName, address, size, section.Name, (info >> 4) == 0 ? source : null));
            }
        }
        // dynsym may repeat symtab entries. Preserve aliases but not duplicate records.
        return new(Union(allocated.Values.Where(s => s.LoadAddress.HasValue).Select(s => (s.LoadAddress!.Value, s.FileSize))),
            Union(allocated.Values.Where(s => s.IsRam).Select(s => (s.Address, s.Size))),
            allocated.Values.ToArray(), symbols.Distinct().OrderByDescending(s => s.Size).ToArray(), allocated.Values.Any(s => s.Region is null));
    }

    /// <summary>Reads GNU ld map input-section rows. Output-section totals, symbol rows and fill are excluded.</summary>
    public static IReadOnlyList<FirmwareObjectContribution> ParseGnuMap(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = new List<FirmwareObjectContribution>();
        string? pending = null;
        var active = false;
        foreach (var line in text.Replace("\r", "", StringComparison.Ordinal).Split('\n'))
        {
            if (line.StartsWith("Linker script and memory map", StringComparison.Ordinal)) { active = true; continue; }
            if (!active) continue; // discarded sections precede this heading and must never count.
            var match = MapContribution().Match(line);
            if (match.Success)
            {
                var section = match.Groups[1].Value;
                if (section.Length == 0) section = pending ?? "";
                var objectFile = match.Groups[4].Value.Trim();
                if (section.Length > 0 && (objectFile.EndsWith(".o", StringComparison.Ordinal) || objectFile.EndsWith(')')))
                    result.Add(new(section, Hex(match.Groups[2].Value), Hex(match.Groups[3].Value), objectFile));
                pending = null;
            }
            else
            {
                var name = line.Trim();
                pending = line.Length > 0 && char.IsWhiteSpace(line[0]) && name.StartsWith('.') && !name.Contains(' ') ? name : null;
            }
        }
        return result;
    }

    private static ulong Union(IEnumerable<(ulong Address, ulong Size)> ranges)
    {
        ulong total = 0, start = 0, end = 0;
        var any = false;
        foreach (var (address, size) in ranges.OrderBy(r => r.Address))
        {
            var nextEnd = checked(address + size);
            if (!any) { start = address; end = nextEnd; any = true; }
            else if (address <= end) { end = Math.Max(end, nextEnd); }
            else { total = checked(total + end - start); start = address; end = nextEnd; }
        }
        return any ? checked(total + end - start) : 0;
    }

    private static void Need(ReadOnlySpan<byte> image, ulong offset, ulong size)
    {
        if (offset > (ulong)image.Length || size > (ulong)image.Length - offset)
            throw new InvalidDataException("ELF data lies outside the image.");
    }

    private static ushort U16(ReadOnlySpan<byte> image, ulong at, bool little)
    {
        Need(image, at, 2); var bytes = image.Slice((int)at, 2);
        return little ? BinaryPrimitives.ReadUInt16LittleEndian(bytes) : BinaryPrimitives.ReadUInt16BigEndian(bytes);
    }

    private static uint U32(ReadOnlySpan<byte> image, ulong at, bool little)
    {
        Need(image, at, 4); var bytes = image.Slice((int)at, 4);
        return little ? BinaryPrimitives.ReadUInt32LittleEndian(bytes) : BinaryPrimitives.ReadUInt32BigEndian(bytes);
    }

    private static ulong U64(ReadOnlySpan<byte> image, ulong at, bool little)
    {
        Need(image, at, 8); var bytes = image.Slice((int)at, 8);
        return little ? BinaryPrimitives.ReadUInt64LittleEndian(bytes) : BinaryPrimitives.ReadUInt64BigEndian(bytes);
    }

    private static string StringAt(ReadOnlySpan<byte> image, Section table, uint index)
    {
        if (index >= table.Size) throw new InvalidDataException("String index lies outside the ELF table.");
        Need(image, table.Offset, table.Size);
        var bytes = image.Slice(checked((int)(table.Offset + index)), checked((int)(table.Size - index)));
        var length = bytes.IndexOf((byte)0);
        if (length < 0) throw new InvalidDataException("Unterminated ELF string.");
        return Encoding.UTF8.GetString(bytes[..length]);
    }

    private static ulong Hex(string value) => ulong.Parse(value.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^\s+(?:(\.\S+)\s+)?(0x[0-9a-fA-F]+)\s+(0x[0-9a-fA-F]+)\s+(.+?)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex MapContribution();
}
