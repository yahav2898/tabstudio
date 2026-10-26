using System.Globalization;
using System.Text.RegularExpressions;

namespace TabStudio.Toolchains.Embedded.Analysis;

/// <summary>A stopped Cortex-M core's fault registers and optional basic exception frame.</summary>
/// <param name="Cfsr">Configurable fault status register at 0xE000ED28.</param>
/// <param name="Hfsr">Hard fault status register at 0xE000ED2C.</param>
/// <param name="Mmfar">Memory management fault address, meaningful only with MMARVALID.</param>
/// <param name="Bfar">Bus fault address, meaningful only with BFARVALID.</param>
/// <param name="StackedWords">Basic exception frame: r0-r3, r12, lr, pc, xPSR. An extended FPU frame must first be skipped using EXC_RETURN.</param>
/// <param name="StackPointer">Address from which the basic frame was read.</param>
/// <param name="StackStart">Optional lowest valid stack address from the linker or RTOS.</param>
/// <param name="StackEnd">Optional exclusive upper stack address.</param>
public sealed record CortexFaultSnapshot(uint Cfsr, uint Hfsr, uint Mmfar, uint Bfar,
    IReadOnlyList<uint>? StackedWords = null, uint? StackPointer = null,
    uint? StackStart = null, uint? StackEnd = null);

/// <summary>An evidence-based crash finding; addresses still require symbols from the matching ELF.</summary>
/// <param name="Code">Stable diagnostic code.</param>
/// <param name="Message">Plain-language explanation of observed flags.</param>
/// <param name="Address">Relevant fault or instruction address, if available.</param>
public sealed record CrashFinding(string Code, string Message, ulong? Address = null);

/// <summary>Decoded evidence and addresses to resolve with the exact firmware's addr2line.</summary>
/// <param name="Findings">Observed causes; multiple simultaneous status flags remain visible.</param>
/// <param name="ProgramCounters">Instruction addresses, preserving their captured order.</param>
public sealed record CrashReport(IReadOnlyList<CrashFinding> Findings, IReadOnlyList<ulong> ProgramCounters);

/// <summary>Decodes crash evidence without guessing a source location, task name or reset cause.</summary>
public static partial class CrashDecoder
{
    /// <summary>Decodes ARMv7-M/ARMv8-M CFSR and HFSR. Cortex-M0/M0+ do not implement CFSR.</summary>
    public static CrashReport DecodeCortexM(CortexFaultSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var findings = new List<CrashFinding>();
        var pcs = new List<ulong>();
        void Flag(int bit, string code, string message, ulong? address = null)
        {
            if ((snapshot.Cfsr & (1u << bit)) != 0) findings.Add(new(code, message, address));
        }
        ulong? memoryAddress = (snapshot.Cfsr & (1u << 7)) != 0 ? snapshot.Mmfar : null;
        ulong? busAddress = (snapshot.Cfsr & (1u << 15)) != 0 ? snapshot.Bfar : null;
        Flag(0, "EMBFAULT001", "Instruction access violates the memory protection configuration.");
        Flag(1, "EMBFAULT002", "Data access violates the memory protection configuration.", memoryAddress);
        Flag(3, "EMBFAULT003", "Memory fault while restoring an exception frame; the stack may be invalid.");
        Flag(4, "EMBFAULT004", "Memory fault while saving an exception frame; check the stack bounds.");
        Flag(5, "EMBFAULT005", "Memory fault while lazily saving floating-point state.");
        Flag(8, "EMBFAULT006", "Bus error while fetching an instruction.");
        Flag(9, "EMBFAULT007", "Precise data bus error; the stacked PC identifies the faulting instruction.", busAddress);
        Flag(10, "EMBFAULT008", "Imprecise data bus error; the stacked PC may be later than the faulting write.", busAddress);
        Flag(11, "EMBFAULT009", "Bus error while restoring an exception frame; check the stack memory.");
        Flag(12, "EMBFAULT010", "Bus error while saving an exception frame; check the stack bounds.");
        Flag(13, "EMBFAULT011", "Bus error while lazily saving floating-point state.");
        Flag(16, "EMBFAULT012", "Undefined instruction; check code integrity and the target architecture.");
        Flag(17, "EMBFAULT013", "Invalid execution state; check Thumb state and branch targets.");
        Flag(18, "EMBFAULT014", "Invalid exception return value.");
        Flag(19, "EMBFAULT015", "Coprocessor instruction without an enabled coprocessor; check FPU configuration.");
        Flag(20, "EMBFAULT016", "Hardware stack limit was exceeded (STKOF, where implemented).");
        Flag(24, "EMBFAULT017", "Unaligned memory access trapped.");
        Flag(25, "EMBFAULT018", "Integer division by zero trapped.");
        if ((snapshot.Hfsr & (1u << 1)) != 0)
            findings.Add(new("EMBFAULT019", "Bus error while reading the exception vector table."));
        if ((snapshot.Hfsr & (1u << 30)) != 0)
            findings.Add(new("EMBFAULT020", "A configurable fault escalated to HardFault; inspect the other fault flags."));
        if ((snapshot.Hfsr & (1u << 31)) != 0)
            findings.Add(new("EMBFAULT021", "A debug event escalated to HardFault."));
        // Saving/restoring failed: the captured frame is not a reliable instruction source.
        const uint frameErrors = (1u << 3) | (1u << 4) | (1u << 11) | (1u << 12);
        if (snapshot.StackedWords is { Count: >= 8 } frame && (snapshot.Cfsr & frameErrors) == 0)
        {
            if ((frame[7] & (1u << 24)) != 0)
            {
                pcs.Add(frame[6] & ~1u);
                if (frame[5] != 0 && (frame[5] & 0xFFFFFFE0u) != 0xFFFFFFE0u)
                    pcs.Add(frame[5] & ~1u);
            }
            else
            {
                findings.Add(new("EMBFAULT022", "The exception frame has no Thumb state bit; frame decoding may be invalid."));
            }
        }
        if (snapshot.StackPointer is uint sp && snapshot.StackStart is uint low && snapshot.StackEnd is uint high)
        {
            if (high <= low) throw new ArgumentException("Stack end must be above stack start.", nameof(snapshot));
            if (sp < low || (ulong)sp + 32 > high)
                findings.Add(new("EMBFAULT023", "The captured exception frame lies outside the configured stack bounds.", sp));
        }
        if (memoryAddress == 0 || busAddress == 0)
            findings.Add(new("EMBFAULT024", "The fault address is zero; a null pointer access is possible, but needs instruction-level confirmation.", 0));
        if (findings.Count == 0)
            findings.Add(new("EMBFAULT000", "No supported fault flags were captured; the reset cause is unknown."));
        return new(findings, pcs);
    }

    /// <summary>Extracts ESP-IDF's PC:SP pairs and optional panic reason; SP values are never mistaken for PCs.</summary>
    public static CrashReport DecodeEspPanic(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var pcs = new List<ulong>();
        var findings = new List<CrashFinding>();
        foreach (var line in text.Replace("\r", "", StringComparison.Ordinal).Split('\n'))
        {
            var reason = EspReason().Match(line);
            if (reason.Success) findings.Add(new("EMBESP001", reason.Groups[1].Value.Trim()));
            if (!line.Contains("Backtrace:", StringComparison.OrdinalIgnoreCase)) continue;
            foreach (Match pair in EspPair().Matches(line))
                pcs.Add(ulong.Parse(pair.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        }
        if (findings.Count == 0) findings.Add(new("EMBESP000", "No ESP panic reason was present in the captured output."));
        return new(findings, pcs);
    }

    /// <summary>Decodes the classic AVR MCUSR layout. Other AVR families require their documented reset register.</summary>
    public static CrashReport DecodeAvrMcusr(byte mcusr)
    {
        var findings = new List<CrashFinding>();
        string[] causes = ["Power-on reset", "External reset", "Brown-out reset", "Watchdog reset", "JTAG reset"];
        for (var bit = 0; bit < causes.Length; bit++)
            if ((mcusr & (1 << bit)) != 0) findings.Add(new($"EMBAVR00{bit + 1}", causes[bit]));
        if ((mcusr & 0xE0) != 0) findings.Add(new("EMBAVR006", "MCUSR contains unknown bits; verify the selected AVR's register layout."));
        if (findings.Count == 0) findings.Add(new("EMBAVR000", "Reset flags are clear; firmware may already have cleared MCUSR."));
        return new(findings, Array.Empty<ulong>());
    }

    [GeneratedRegex(@"Guru Meditation Error:.*?\(([^\r\n]+)\)", RegexOptions.CultureInvariant)]
    private static partial Regex EspReason();

    [GeneratedRegex(@"0x([0-9a-fA-F]+):0x[0-9a-fA-F]+", RegexOptions.CultureInvariant)]
    private static partial Regex EspPair();
}
