using System.Buffers.Binary;

namespace TabStudio.Toolchains.Embedded.Flashing;

/// <summary>Stops known in-image security configuration from being mistaken for ordinary application bytes.
/// This is an explicit part allowlist, not a complete map of every MCU's security/configuration locations.</summary>
public static class EmbeddedConfigurationGuard
{
    // NXP UM10360 rev2 §32.6/Table568. LPC1751 intentionally excluded: CRP depends on the silicon part ID.
    private static readonly HashSet<string> LpcParts = new(StringComparer.OrdinalIgnoreCase)
    {
        "LPC1769", "LPC1768", "LPC1767", "LPC1766", "LPC1765", "LPC1764", "LPC1763",
        "LPC1759", "LPC1758", "LPC1756", "LPC1754", "LPC1752",
        "LPC1769FBD100", "LPC1768FBD100", "LPC1767FBD100", "LPC1766FBD100", "LPC1765FBD100", "LPC1764FBD100", "LPC1763FBD100",
        "LPC1768FET100", "LPC1759FBD80", "LPC1758FBD80", "LPC1756FBD80", "LPC1754FBD80", "LPC1752FBD80",
    };
    // Actual ordering table from K64P144M120SF5 rev7 p2. No guessed Kinetis-family prefix is assigned a policy.
    private static readonly HashSet<string> K64Parts = new(StringComparer.OrdinalIgnoreCase)
    {
        "MK64FX512VLL12", "MK64FN1M0VLL12", "MK64FX512VDC12", "MK64FN1M0VDC12",
        "MK64FX512VLQ12", "MK64FN1M0VLQ12", "MK64FX512VMD12", "MK64FN1M0VMD12",
    };

    /// <summary>Whether the exact supplied part identifier has a reviewed in-image configuration policy.</summary>
    public static bool HasPolicy(string target) => LpcParts.Contains(target) || K64Parts.Contains(target);

    /// <summary>Checks stable bytes before constructing an OpenOCD/probe-rs application plan.
    /// Non-default configuration is refused: a generic plan has no live old-value read and therefore cannot
    /// supply the exact device-bound authorization required by <see cref="FlashSafety.AuthorizeConfigurationChange"/>.
    /// Unknown parts are not declared configuration-safe by this method.</summary>
    public static void Validate(EmbeddedFlashRequest request, ReadOnlySpan<byte> image)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Protocol is not (EmbeddedFlashProtocol.OpenOcd or EmbeddedFlashProtocol.ProbeRs)) return;
        if (request.Protocol == EmbeddedFlashProtocol.OpenOcd)
        {
            var config = Path.GetFileName(request.TargetConfig?.Replace('\\', '/'));
            if (string.Equals(config, "lpc17xx.cfg", StringComparison.OrdinalIgnoreCase) && !LpcParts.Contains(request.Target)
                || string.Equals(config, "kx.cfg", StringComparison.OrdinalIgnoreCase) && !K64Parts.Contains(request.Target))
            {
                throw new NotSupportedException("This LPC17xx/Kinetis OpenOCD configuration requires an exact part with a reviewed embedded configuration policy. Generic/unknown part identifiers cannot prove CRP/FCF safety; select a documented supported part or use a separately reviewed workflow.");
            }
        }
        var extension = Path.GetExtension(request.FirmwarePath);
        if (LpcParts.Contains(request.Target))
        {
            var bytes = FirmwareImageContent.ReadRange(image, extension, 0x2FC, 4, request.BinaryAddress);
            if (bytes.All(value => value is null)) return;
            if (bytes.Any(value => value is null))
                throw new InvalidOperationException($"{request.Target}: partial CRP word at 0x2FC..0x300 has unknown resulting configuration; provide a complete unprotected CRP word.");
            var value = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Select(value => value!.Value).ToArray());
            // Accept only explicit non-protecting placeholders. Unknown words are not implicitly safe.
            if (value is 0 or uint.MaxValue) return;
            var mode = value switch { 0x12345678 => "CRP1 disables debug/read access", 0x87654321 => "CRP2 restricts recovery to full erase", 0x43218765 => "CRP3 disables normal debug/ISP recovery", _ => "unreviewed CRP/configuration value" };
            throw new InvalidOperationException($"{request.Target}: CRP at 0x2FC = 0x{value:X8} ({mode}). Application upload refuses this configuration image before erase. Use an unprotected image; a separate supported configuration operation would require live old/new values, exact device confirmation and documented reversibility.");
        }
        if (K64Parts.Contains(request.Target))
        {
            var bytes = FirmwareImageContent.ReadRange(image, extension, 0x400, 16, request.BinaryAddress);
            // Missing fields are not FF defaults: sector erase can erase FSEC to FF, which enables security.
            if (bytes.Any(value => value is null))
                throw new InvalidOperationException($"{request.Target}: complete FCF at 0x400..0x410 is required. Sparse/partial updates cannot prove configuration preservation across erase. Include the reviewed unprotected FCF; missing bytes are unknown, not safe erased defaults.");
            for (var i = 0; i < bytes.Length; i++)
            {
                var safe = i == 12 ? (byte)0xFE : (byte)0xFF;
                if (bytes[i] != safe)
                    throw new InvalidOperationException($"{request.Target}: FCF byte 0x{0x400 + i:X} = 0x{bytes[i]!.Value:X2}; the reviewed unsecured/unprotected default is 0x{safe:X2}. Application upload refuses security/protection/option changes before erase. FSEC=FF is secured, not harmless padding. A separate supported operation would require current device values, exact old/new confirmation and documented reversibility.");
            }
        }
    }
}
