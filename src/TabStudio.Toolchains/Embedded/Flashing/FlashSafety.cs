using System.Globalization;

namespace TabStudio.Toolchains.Embedded.Flashing;

/// <summary>A half-open byte-address range; callers must obtain the map for the actual target.</summary>
/// <param name="Start">First byte address.</param>
/// <param name="Length">Number of bytes.</param>
public readonly record struct FlashAddressRange(ulong Start, ulong Length)
{
    /// <summary>First address after the range, checked for overflow.</summary>
    public ulong End => checked(Start + Length);

    /// <summary>Whether this non-empty interval contains the whole other interval.</summary>
    public bool Contains(FlashAddressRange other) => Length > 0 && other.Length > 0 && other.Start >= Start && other.End <= End;
}

/// <summary>The specific class of target configuration write.</summary>
public enum TargetConfigurationKind
{
    /// <summary>Rewritable device option bytes.</summary>
    OptionBytes,
    /// <summary>Rewritable fuses, where the selected target documents reversibility.</summary>
    Fuses,
    /// <summary>Readout protection below permanent level two.</summary>
    ReadoutProtection,
    /// <summary>One-time programmable electronic fuses. Always refused.</summary>
    Efuses,
}

/// <summary>A concrete old-to-new configuration change; never inferred from arbitrary command flags.</summary>
/// <param name="DeviceIdentity">Selected device identity, including its serial when available.</param>
/// <param name="Kind">Configuration memory kind.</param>
/// <param name="Register">Exact register or fuse name.</param>
/// <param name="OldValue">Read-back current value.</param>
/// <param name="NewValue">Requested new value.</param>
/// <param name="Reversible">Whether the target documentation says the write is reversible.</param>
/// <param name="Consequence">Specific consequence, including any erase, debug loss or reset.</param>
/// <param name="ReadoutProtectionLevel">Requested level for a readout-protection write.</param>
public sealed record TargetConfigurationChange(string DeviceIdentity, TargetConfigurationKind Kind, string Register,
    ulong OldValue, ulong NewValue, bool Reversible, string Consequence, int? ReadoutProtectionLevel = null);

/// <summary>Checks firmware address boundaries and makes dangerous configuration changes explicit.</summary>
public static class FlashSafety
{
    /// <summary>Refuses every firmware segment outside the explicitly selected application-flash ranges.</summary>
    public static void ValidateFirmware(IReadOnlyList<FlashAddressRange> firmware, IReadOnlyList<FlashAddressRange> allowedApplicationFlash)
    {
        ArgumentNullException.ThrowIfNull(firmware);
        ArgumentNullException.ThrowIfNull(allowedApplicationFlash);
        if (firmware.Count == 0 || allowedApplicationFlash.Count == 0)
        {
            throw new InvalidOperationException("The actual target's application-flash map and firmware segments are required before erase.");
        }
        foreach (var range in firmware)
        {
            if (!allowedApplicationFlash.Any(x => x.Contains(range)))
            {
                throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture,
                    $"Firmware range 0x{range.Start:X}..0x{range.End:X} is outside application flash. Bootloader, option-byte, fuse and protected-memory writes are not upload operations."));
            }
        }
    }

    /// <summary>The precise confirmation text. eFuses and permanent readout protection are refused before a prompt can be shown.</summary>
    public static string RequiredConfirmation(TargetConfigurationChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        ArgumentException.ThrowIfNullOrWhiteSpace(change.DeviceIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(change.Register);
        ArgumentException.ThrowIfNullOrWhiteSpace(change.Consequence);
        if (change.Kind == TargetConfigurationKind.Efuses)
        {
            throw new NotSupportedException("TabStudio refuses eFuse burning.");
        }
        if (change.ReadoutProtectionLevel >= 2 ||
            change.Kind == TargetConfigurationKind.ReadoutProtection && change.NewValue is 2 or 0xCC)
        {
            throw new NotSupportedException("TabStudio refuses permanent readout protection (RDP level 2).");
        }
        if (change.Kind == TargetConfigurationKind.ReadoutProtection && change.ReadoutProtectionLevel is not (0 or 1))
        {
            throw new InvalidOperationException("An explicit reversible readout-protection level is required.");
        }
        return string.Create(CultureInfo.InvariantCulture,
            $"CONFIRM {change.DeviceIdentity}: {change.Kind} {change.Register} 0x{change.OldValue:X} -> 0x{change.NewValue:X}; {(change.Reversible ? "reversible" : "irreversible")}; {change.Consequence}");
    }

    /// <summary>Requires exact, operation-specific text immediately before a configuration change.</summary>
    public static void AuthorizeConfigurationChange(TargetConfigurationChange change, string typedConfirmation)
    {
        var required = RequiredConfirmation(change);
        if (!string.Equals(required, typedConfirmation, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Specific confirmation does not match the selected device and exact configuration change.");
        }
    }
}
