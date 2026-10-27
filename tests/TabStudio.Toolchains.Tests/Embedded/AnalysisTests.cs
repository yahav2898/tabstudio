using System.Buffers.Binary;
using System.Text;
using TabStudio.Toolchains.Embedded.Analysis;

namespace TabStudio.Toolchains.Tests.Embedded;

/// <summary>Exact binary memory accounting, crash evidence and conservative stack bounds.</summary>
public sealed class AnalysisTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Elf_sections_use_load_addresses_and_exclude_debug_data(bool wide, bool bigEndian)
    {
        var report = MemoryAnalyzer.AnalyzeElf(BuildElf(wide, bigEndian),
            [new("FLASH", 0x8000000, 0x10000, false), new("RAM", 0x20000000, 0x1000, true)]);

        Assert.Equal(24ul, report.FlashBytes);
        Assert.Equal(20ul, report.RamBytes);
        Assert.False(report.PlacementInferred);
        Assert.Equal(3, report.Sections.Count);
        Assert.Equal(0x8000010ul, Assert.Single(report.Sections, s => s.Name == ".data").LoadAddress);
        Assert.Null(Assert.Single(report.Sections, s => s.Name == ".bss").LoadAddress);
        Assert.DoesNotContain(report.Sections, s => s.Name == ".debug_info");
    }

    [Fact]
    public void Elf_flags_fallback_is_explicit_and_deltas_are_signed()
    {
        var current = MemoryAnalyzer.AnalyzeElf(BuildElf());
        Assert.True(current.PlacementInferred);
        var previous = current with { FlashBytes = 30, RamBytes = 10 };
        Assert.Equal(new FirmwareMemoryDelta(-6, 10), current.Compare(previous));
    }

    [Fact]
    public void Linux_host_executable_provides_real_elf_tables_without_a_cross_compiler()
    {
        if (!OperatingSystem.IsLinux()) return;
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("No test host executable.");
        var report = MemoryAnalyzer.AnalyzeElf(File.ReadAllBytes(executable));
        Assert.True(report.FlashBytes > 0);
        Assert.NotEmpty(report.Sections);
        Assert.All(report.Sections, section => Assert.True(section.Size > 0));
        Assert.Equal(new FirmwareMemoryDelta(0, 0), report.Compare(report));
    }

    [Fact]
    public void Corrupt_elf_tables_fail_before_allocating_or_indexing()
    {
        Assert.Throws<InvalidDataException>(() => MemoryAnalyzer.AnalyzeElf([0, 1, 2]));
        var image = BuildElf();
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(32), uint.MaxValue);
        Assert.Throws<InvalidDataException>(() => MemoryAnalyzer.AnalyzeElf(image));
        image = BuildElf();
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(128 + 40 + 20), uint.MaxValue);
        Assert.Throws<InvalidDataException>(() => MemoryAnalyzer.AnalyzeElf(image));
    }

    [Fact]
    public void Relocatable_objects_are_not_reported_as_linked_memory()
    {
        var image = BuildElf();
        image[16] = 1;
        Assert.Throws<InvalidDataException>(() => MemoryAnalyzer.AnalyzeElf(image));
    }

    [Fact]
    public void Elf_symbol_aliases_do_not_inflate_totals_and_source_ownership_is_not_guessed()
    {
        var image = BuildElf();
        void W16(int at, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(at), value);
        void W32(int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(at), value);
        W16(48, 8);
        var names = Encoding.UTF8.GetBytes("\0.text\0.data\0.bss\0.debug_info\0.shstrtab\0.strtab\0.symtab\0");
        names.CopyTo(image, 768);
        W32(128 + 5 * 40 + 20, (uint)names.Length);
        var strings = Encoding.UTF8.GetBytes("\0file.c\0foo\0alias\0");
        strings.CopyTo(image, 848);
        W32(128 + 6 * 40, 40); W32(128 + 6 * 40 + 4, 3);
        W32(128 + 6 * 40 + 16, 848); W32(128 + 6 * 40 + 20, (uint)strings.Length);
        W32(128 + 7 * 40, 48); W32(128 + 7 * 40 + 4, 2);
        W32(128 + 7 * 40 + 16, 560); W32(128 + 7 * 40 + 20, 64);
        W32(128 + 7 * 40 + 24, 6); W32(128 + 7 * 40 + 36, 16);
        W32(576, 1); image[588] = 4; W16(590, 0xfff1);
        W32(592, 8); W32(596, 0x8000000); W32(600, 8); image[604] = 2; W16(606, 1);
        W32(608, 12); W32(612, 0x8000000); W32(616, 8); image[620] = 0x12; W16(622, 1);

        var report = MemoryAnalyzer.AnalyzeElf(image);
        Assert.Equal(24ul, report.FlashBytes);
        Assert.Equal("file.c", Assert.Single(report.Symbols, s => s.Name == "foo").SourceFile);
        Assert.Null(Assert.Single(report.Symbols, s => s.Name == "alias").SourceFile);
        Assert.Equal(2, report.Symbols.Count);
    }

    [Fact]
    public void Gnu_map_skips_discarded_sections_output_totals_symbols_and_fill()
    {
        var map = """
            Discarded input sections
             .text.dead 0x00000000 0x20 dead.o
            Linker script and memory map
            .text           0x08000000 0x40
             .text.main     0x08000000 0x20 build/main.o
             .text.long_function_name
                            0x08000020 0x10 libfoo.a(worker.o)
             *fill*         0x08000030 0x10
                            0x08000000 main
            """;

        var rows = MemoryAnalyzer.ParseGnuMap(map);
        Assert.Equal(2, rows.Count);
        Assert.Equal(new FirmwareObjectContribution(".text.main", 0x8000000, 32, "build/main.o"), rows[0]);
        Assert.Equal("libfoo.a(worker.o)", rows[1].ObjectFile);
    }

    [Fact]
    public void Cortex_faults_keep_status_evidence_and_never_guess_source_lines()
    {
        var report = CrashDecoder.DecodeCortexM(new((1u << 24) | (1u << 9) | (1u << 15), 1u << 30,
            0xDEADBEEF, 0, [1, 2, 3, 4, 12, 0x8000021, 0x8000011, 1u << 24]));

        Assert.Contains(report.Findings, f => f.Code == "EMBFAULT017" && f.Message.Contains("Unaligned", StringComparison.Ordinal));
        Assert.Contains(report.Findings, f => f.Code == "EMBFAULT024" && f.Message.Contains("possible", StringComparison.Ordinal));
        Assert.Equal(new ulong[] { 0x8000010, 0x8000020 }, report.ProgramCounters);
        Assert.DoesNotContain(report.Findings, f => f.Address == 0xDEADBEEF);
    }

    [Fact]
    public void Invalid_stacking_does_not_publish_a_fabricated_program_counter()
    {
        var report = CrashDecoder.DecodeCortexM(new(1u << 12, 0, 0, 0,
            [0, 0, 0, 0, 0, 0x8000001, 0x8000011, 1u << 24], 0x1ffffff0, 0x20000000, 0x20001000));
        Assert.Empty(report.ProgramCounters);
        Assert.Contains(report.Findings, f => f.Code == "EMBFAULT023");
    }

    [Fact]
    public void Esp_pairs_extract_pcs_without_using_stack_pointers()
    {
        var report = CrashDecoder.DecodeEspPanic("""
            Guru Meditation Error: Core 0 panic'ed (LoadProhibited). Exception was unhandled.
            Backtrace: 0x400d1234:0x3ffb0010 0x400d5678:0x3ffb0030 |<-CORRUPTED
            """);
        Assert.Equal(new ulong[] { 0x400d1234, 0x400d5678 }, report.ProgramCounters);
        Assert.Equal("LoadProhibited", Assert.Single(report.Findings).Message);
    }

    [Fact]
    public void Avr_reset_flags_preserve_multiple_causes_and_unknown_register_bits()
    {
        var report = CrashDecoder.DecodeAvrMcusr(0x89);
        Assert.Equal(3, report.Findings.Count);
        Assert.Contains(report.Findings, f => f.Message == "Power-on reset");
        Assert.Contains(report.Findings, f => f.Message == "Watchdog reset");
        Assert.Contains(report.Findings, f => f.Code == "EMBAVR006");
    }

    [Fact]
    public void Addr2line_keeps_windows_paths_discriminators_and_omits_unknown_locations()
    {
        var frames = Addr2LineParser.Parse("0x08000010\nFoo::bar()\nC:\\src\\main.cpp:42 (discriminator 1)\n0x08000020\n??\n??:0\n");
        Assert.Equal(new FirmwareSourceFrame(0x8000010, "Foo::bar()", "C:\\src\\main.cpp", 42), Assert.Single(frames));
    }

    [Fact]
    public void Gcc_stack_rows_support_cpp_names_windows_paths_and_dynamic_bounds()
    {
        var usage = StackAnalyzer.ParseGccStackUsage("C:\\firmware\\main.cpp:10:5:void Foo::bar()\t32\tstatic\nmain.c:20:1:worker\t64\tdynamic,bounded\nmain.c:40:1:unbounded\t16\tdynamic\n");
        Assert.Equal("C:\\firmware\\main.cpp", usage[0].File);
        Assert.Equal("void Foo::bar()", usage[0].Function);
        Assert.True(usage[1].Bounded);
        Assert.False(usage[2].Bounded);
        Assert.Throws<InvalidDataException>(() => StackAnalyzer.ParseGccStackUsage("bad row"));
        Assert.Throws<InvalidDataException>(() => StackAnalyzer.ParseGccStackUsage("main.c:1:1:foo\t16\tunknown"));
    }

    [Fact]
    public void Stack_uses_deepest_call_chain_rather_than_sum_of_siblings()
    {
        var usage = StackAnalyzer.ParseGccStackUsage("main.c:1:1:task\t16\tstatic\nmain.c:2:1:a\t24\tstatic\nmain.c:3:1:b\t64\tstatic\n");
        var bounds = StackAnalyzer.Analyze(usage, [new("task", "a"), new("task", "b")], true);
        Assert.Equal(80ul, Assert.Single(bounds, b => b.Function == "task").WorstCaseBytes);
        var task = Assert.Single(StackAnalyzer.CheckTaskBudgets(bounds, [new("Sensor", "task", 96, 32)]));
        Assert.True(task.ExceedsBudget);
        Assert.Equal(112ul, task.RequiredBytes);
    }

    [Theory]
    [InlineData("task", false)]
    [InlineData("missing", true)]
    [InlineData(null, true)]
    public void Unknown_graph_evidence_never_becomes_a_passing_bound(string? target, bool complete)
    {
        var usage = StackAnalyzer.ParseGccStackUsage("main.c:1:1:task\t16\tstatic\n");
        var bounds = StackAnalyzer.Analyze(usage, [new("task", target)], complete);
        var task = Assert.Single(bounds, b => b.Function == "task");
        Assert.Null(task.WorstCaseBytes);
        Assert.NotEmpty(task.Reasons);
        Assert.Null(Assert.Single(StackAnalyzer.CheckTaskBudgets(bounds, [new("Task", "task", 1024)])).RequiredBytes);
    }

    [Fact]
    public void Recursive_and_ambiguous_function_identities_have_unknown_bounds()
    {
        StackUsageEntry[] usage = [new("a.c", 1, 1, "a", 8, true), new("b.c", 1, 1, "b", 8, true), new("c.c", 1, 1, "b", 16, true)];
        var report = StackAnalyzer.Analyze(usage, [new("a", "b"), new("b", "a")], true);
        Assert.All(report, r => Assert.Null(r.WorstCaseBytes));
        Assert.Contains(report.SelectMany(r => r.Reasons), r => r.Contains("Recursive", StringComparison.Ordinal));
        Assert.Contains(report.SelectMany(r => r.Reasons), r => r.Contains("Ambiguous", StringComparison.Ordinal));
    }

    // Minimal independently specified linked ELF. .data VMA is RAM, LMA follows .text in FLASH.
    private static byte[] BuildElf(bool wide = false, bool bigEndian = false)
    {
        var image = new byte[1024];
        image[0] = 0x7f; image[1] = (byte)'E'; image[2] = (byte)'L'; image[3] = (byte)'F';
        image[4] = (byte)(wide ? 2 : 1); image[5] = (byte)(bigEndian ? 2 : 1); image[6] = 1;
        void W16(int at, ushort value)
        {
            if (bigEndian) BinaryPrimitives.WriteUInt16BigEndian(image.AsSpan(at), value);
            else BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(at), value);
        }
        void W32(int at, uint value)
        {
            if (bigEndian) BinaryPrimitives.WriteUInt32BigEndian(image.AsSpan(at), value);
            else BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(at), value);
        }
        void Word(int at, ulong value)
        {
            if (!wide) W32(at, (uint)value);
            else if (bigEndian) BinaryPrimitives.WriteUInt64BigEndian(image.AsSpan(at), value);
            else BinaryPrimitives.WriteUInt64LittleEndian(image.AsSpan(at), value);
        }
        W16(16, 2); W32(20, 1);
        Word(wide ? 32 : 28, wide ? 64ul : 52ul);
        Word(wide ? 40 : 32, wide ? 256ul : 128ul);
        W16(wide ? 52 : 40, (ushort)(wide ? 64 : 52));
        W16(wide ? 54 : 42, (ushort)(wide ? 56 : 32)); W16(wide ? 56 : 44, 2);
        W16(wide ? 58 : 46, (ushort)(wide ? 64 : 40)); W16(wide ? 60 : 48, 6); W16(wide ? 62 : 50, 5);
        void Segment(int index, ulong offset, ulong vma, ulong lma, ulong fileSize, ulong memorySize)
        {
            var p = (wide ? 64 : 52) + index * (wide ? 56 : 32);
            W32(p, 1);
            Word(p + (wide ? 8 : 4), offset);
            Word(p + (wide ? 16 : 8), vma); Word(p + (wide ? 24 : 12), lma);
            Word(p + (wide ? 32 : 16), fileSize); Word(p + (wide ? 40 : 20), memorySize);
        }
        Segment(0, 800, 0x8000000, 0x8000000, 16, 16);
        Segment(1, 816, 0x20000000, 0x8000010, 8, 20);
        void Section(int index, uint name, uint type, ulong flags, ulong address, ulong offset, ulong size)
        {
            var p = (wide ? 256 : 128) + index * (wide ? 64 : 40);
            W32(p, name); W32(p + 4, type);
            Word(p + 8, flags); Word(p + (wide ? 16 : 12), address);
            Word(p + (wide ? 24 : 16), offset); Word(p + (wide ? 32 : 20), size);
        }
        var names = Encoding.UTF8.GetBytes("\0.text\0.data\0.bss\0.debug_info\0.shstrtab\0");
        names.CopyTo(image, 768);
        Section(1, 1, 1, 6, 0x8000000, 800, 16);
        Section(2, 7, 1, 3, 0x20000000, 816, 8);
        Section(3, 13, 8, 3, 0x20000008, 824, 12);
        Section(4, 18, 1, 0, 0, 900, 100);
        Section(5, 30, 3, 0, 0, 768, (ulong)names.Length);
        return image;
    }
}
