using System.Buffers.Binary;

namespace TabStudio.Toolchains.Embedded.Analysis;

/// <summary>The four core registers used by read-only exception-frame capture.</summary>
public enum CortexFaultRegister
{
    /// <summary>Live LR, which must still contain a supported EXC_RETURN value.</summary>
    Link,
    /// <summary>Main stack pointer.</summary>
    MainStack,
    /// <summary>Process stack pointer.</summary>
    ProcessStack,
    /// <summary>Live program counter in the top halted frame.</summary>
    ProgramCounter,
}

/// <summary>Read-only access to one unchanged halt epoch. Implementations must reject resume/reconnect races.</summary>
public interface ICortexFaultReader
{
    /// <summary>Reads exactly the requested bytes, or fails rather than returning partial data.</summary>
    Task<byte[]> ReadMemoryAsync(uint address, int count, CancellationToken cancellationToken);

    /// <summary>Reads a live top-frame core register without running any target function.</summary>
    Task<uint> ReadRegisterAsync(CortexFaultRegister registerKind, CancellationToken cancellationToken);
}

/// <summary>A proven exception-frame layout, or the explicit reason no frame can be recovered.</summary>
/// <param name="BasicFrameAddress">Address of r0, or null when the frame is not established.</param>
/// <param name="FrameBytes">Complete hardware frame extent, including reserved floating-point state.</param>
/// <param name="HasFloatingPointContext">Whether EXC_RETURN bit 4 indicates an extended frame.</param>
/// <param name="Reason">Evidence or refusal reason.</param>
public sealed record CortexExceptionFramePlan(uint? BasicFrameAddress, int FrameBytes, bool HasFloatingPointContext, string Reason);

/// <summary>Captured fault evidence from one halted Cortex-M core.</summary>
/// <param name="Snapshot">Fault status and any verified basic exception frame.</param>
/// <param name="CpuId">Actual CPUID read from the selected target.</param>
/// <param name="LinkRegister">Captured LR, or null when register evaluation was unavailable.</param>
/// <param name="MainStackPointer">Captured MSP, when available.</param>
/// <param name="ProcessStackPointer">Captured PSP, when available.</param>
/// <param name="ProgramCounter">Captured live PC, when available.</param>
/// <param name="Frame">Frame-selection evidence.</param>
/// <param name="ActiveException">Actual ICSR.VECTACTIVE exception number at capture time.</param>
public sealed record CortexFaultCaptureResult(CortexFaultSnapshot Snapshot, uint CpuId, uint? LinkRegister,
    uint? MainStackPointer, uint? ProcessStackPointer, uint? ProgramCounter, CortexExceptionFramePlan Frame,
    uint ActiveException = 0);

/// <summary>Captures latched fault status without clearing it or changing target memory.</summary>
public static class CortexFaultCapture
{
    /// <summary>Models with an implemented CFSR and explicitly supported exception layouts.</summary>
    public static bool IsSupportedModel(string? model) => model is "cortex-m-v7" or "cortex-m-v8-mainline";

    /// <summary>Selects MSP/PSP only at proven exception entry. Core words start at SP for both basic and
    /// extended frames; the 18 floating-point words follow them and are deliberately not decoded.
    /// Security-transition/additional-state contexts and unknown EXC_RETURN values are refused.</summary>
    public static CortexExceptionFramePlan SelectFrame(string model, uint linkRegister, uint mainStack,
        uint processStack, bool atFaultVectorEntry)
    {
        if (!IsSupportedModel(model)) throw new NotSupportedException("Select a supported Cortex-M fault model explicitly; Cortex-M0/M0+ have no CFSR.");
        var known = linkRegister is 0xFFFFFFF1 or 0xFFFFFFF9 or 0xFFFFFFFD or 0xFFFFFFE1 or 0xFFFFFFE9 or 0xFFFFFFED;
        if (model == "cortex-m-v8-mainline")
            known |= linkRegister is 0xFFFFFFB0 or 0xFFFFFFB8 or 0xFFFFFFBC or 0xFFFFFFA0 or 0xFFFFFFA8 or 0xFFFFFFAC;
        if (!known) return new(null, 0, false, "LR is not a supported same-security EXC_RETURN value; no stack frame was guessed.");
        if (!atFaultVectorEntry) return new(null, 0, false, "PC is not the active fault vector entry; a handler prologue may have moved SP. Status registers were captured without guessing a frame.");
        var stack = (linkRegister & 4) != 0 ? processStack : mainStack;
        var floatingPoint = (linkRegister & 16) == 0;
        var extent = floatingPoint ? 104 : 32;
        if (stack == 0 || (stack & 3) != 0 || (ulong)stack + (uint)extent > uint.MaxValue)
            return new(null, 0, floatingPoint, "The selected stack pointer is null, unaligned or overflows its frame extent.");
        return new(stack, extent, floatingPoint, floatingPoint
            ? "Extended frame: core r0..xPSR read at SP; the following 72 bytes of FP/reserved state are excluded."
            : "Basic frame: core r0..xPSR read at the EXC_RETURN-selected stack pointer.");
    }

    /// <summary>Reads CPUID/ICSR/VTOR, CFSR/HFSR/MMFAR/BFAR and live registers. A frame is read only when
    /// the halted PC equals the active fault vector entry and EXC_RETURN has a supported layout.</summary>
    public static async Task<CortexFaultCaptureResult> CaptureAsync(ICortexFaultReader reader, string model,
        CancellationToken cancellationToken = default) =>
        await CaptureCoreAsync(reader, model, automatic: false, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Manual capture unexpectedly omitted fault status.");

    /// <summary>Captures an actual active HardFault/MemManage/BusFault/UsageFault only. A nonfault ICSR returns
    /// null before reading latched CFSR/HFSR, so old flags cannot produce an automatic crash report.</summary>
    public static Task<CortexFaultCaptureResult?> CaptureAutomaticAsync(ICortexFaultReader reader, string model,
        CancellationToken cancellationToken = default) => CaptureCoreAsync(reader, model, automatic: true, cancellationToken);

    private static async Task<CortexFaultCaptureResult?> CaptureCoreAsync(ICortexFaultReader reader, string model, bool automatic,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        if (!IsSupportedModel(model)) throw new NotSupportedException("Set FaultModel to cortex-m-v7 or cortex-m-v8-mainline for the actual supported core before capturing fault status.");
        var core = await reader.ReadMemoryAsync(0xE000ED00, 12, cancellationToken).ConfigureAwait(false);
        if (core.Length != 12) throw new InvalidDataException("Incomplete CPUID/ICSR/VTOR read.");
        var cpuId = Word(core, 0);
        var part = (cpuId >> 4) & 0xfff;
        var supportedPart = model == "cortex-m-v7" ? part is 0xC23 or 0xC24 or 0xC27 : part is 0xD21 or 0xD31 or 0xD22 or 0xD23;
        if ((cpuId >> 24) != 0x41 || !supportedPart)
            throw new NotSupportedException($"CPUID 0x{cpuId:X8} does not match the explicitly supported {model} cores. No CFSR access was attempted.");
        var active = Word(core, 4) & 0x1ff;
        if (automatic && active is not (>= 3 and <= 6)) return null;
        var status = await reader.ReadMemoryAsync(0xE000ED28, 20, cancellationToken).ConfigureAwait(false);
        if (status.Length != 20) throw new InvalidDataException("Incomplete CFSR/HFSR/MMFAR/BFAR read.");
        var snapshot = new CortexFaultSnapshot(Word(status, 0), Word(status, 4), Word(status, 12), Word(status, 16));
        uint? lr = null, msp = null, psp = null, pc = null;
        var plan = new CortexExceptionFramePlan(null, 0, false, "Core registers were unavailable; only actual fault status was captured.");
        try
        {
            lr = await reader.ReadRegisterAsync(CortexFaultRegister.Link, cancellationToken).ConfigureAwait(false);
            msp = await reader.ReadRegisterAsync(CortexFaultRegister.MainStack, cancellationToken).ConfigureAwait(false);
            psp = await reader.ReadRegisterAsync(CortexFaultRegister.ProcessStack, cancellationToken).ConfigureAwait(false);
            pc = await reader.ReadRegisterAsync(CortexFaultRegister.ProgramCounter, cancellationToken).ConfigureAwait(false);
            var atEntry = false;
            if (active is >= 3 and <= 6)
            {
                var vector = checked(Word(core, 8) + active * 4);
                var entry = await reader.ReadMemoryAsync(vector, 4, cancellationToken).ConfigureAwait(false);
                if (entry.Length != 4) throw new InvalidDataException("Incomplete active-fault vector read.");
                var handler = Word(entry, 0);
                atEntry = (handler & 1) != 0 && (pc.Value & ~1u) == (handler & ~1u);
            }
            plan = SelectFrame(model, lr.Value, msp.Value, psp.Value, atEntry);
            const uint frameErrors = (1u << 3) | (1u << 4) | (1u << 11) | (1u << 12);
            if ((snapshot.Cfsr & frameErrors) != 0)
                plan = new(null, 0, plan.HasFloatingPointContext, "Stacking/unstacking fault flags make the exception frame unreliable; no stack memory was decoded.");
            if (plan.BasicFrameAddress is uint address)
            {
                var bytes = await reader.ReadMemoryAsync(address, 32, cancellationToken).ConfigureAwait(false);
                if (bytes.Length != 32) throw new InvalidDataException("Incomplete basic exception frame.");
                var words = Enumerable.Range(0, 8).Select(index => Word(bytes, index * 4)).ToArray();
                snapshot = snapshot with { StackedWords = words, StackPointer = address };
            }
        }
        catch (NotSupportedException error)
        {
            plan = new(null, 0, false, "Core register evaluation unavailable: " + error.Message + " Only captured status is reported.");
        }
        return new(snapshot, cpuId, lr, msp, psp, pc, plan, active);
    }

    private static uint Word(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
}
