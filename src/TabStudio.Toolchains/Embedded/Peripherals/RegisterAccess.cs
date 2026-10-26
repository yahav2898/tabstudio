namespace TabStudio.Toolchains.Embedded.Peripherals;

/// <summary>A proposed register write. The debugger must be paused and validate the target address before applying it.</summary>
/// <param name="Address">Absolute byte address.</param>
/// <param name="SizeBits">Register width.</param>
/// <param name="Value">Value to write, never a request to run arbitrary code.</param>
/// <param name="Description">Human-readable operation including the field and its side effects.</param>
public sealed record RegisterWrite(ulong Address, int SizeBits, ulong Value, string Description);

/// <summary>Decodes CMSIS register metadata and refuses unsafe automatic read/modify/write operations.</summary>
public static class RegisterAccess
{
    /// <summary>Describes a register access for hover/completion, with fields and named values.</summary>
    public static string Describe(SvdPeripheral peripheral, SvdRegister register)
    {
        ArgumentNullException.ThrowIfNull(peripheral);
        ArgumentNullException.ThrowIfNull(register);
        var lines = new List<string>
        {
            $"{peripheral.Name}->{register.Name}  0x{register.Address:X}  {register.SizeBits}-bit {register.Access}",
            register.Description
        };
        if (register.ReadAction is { Length: > 0 } readAction) lines.Add($"Reading has side effect: {readAction}.");
        foreach (var field in register.Fields.OrderByDescending(f => f.BitOffset))
        {
            var range = field.BitWidth == 1 ? $"[{field.BitOffset}]" : $"[{field.BitOffset + field.BitWidth - 1}:{field.BitOffset}]";
            lines.Add($"{range} {field.Name}: {field.Description} ({field.Access}" +
                (field.ModifiedWriteValues is null ? ")" : $", write {field.ModifiedWriteValues})"));
            foreach (var value in field.Values) lines.Add($"  {value.Name} = {value.Value}: {value.Description}");
        }
        return string.Join(Environment.NewLine, lines.Where(line => line.Length != 0));
    }

    /// <summary>Creates a positioned numeric mask expression. It does not invent a vendor header's macro spelling.</summary>
    public static string MaskExpression(SvdField field, ulong value)
    {
        ArgumentNullException.ThrowIfNull(field);
        ValidateValue(field, value);
        return $"(0x{value:X}{(field.BitOffset + field.BitWidth > 32 ? "ULL" : "U")} << {field.BitOffset})";
    }

    /// <summary>Plans a plain field assignment, preserving other bits only when SVD marks the whole register safe to read and write.</summary>
    public static RegisterWrite CreateFieldWrite(SvdRegister register, SvdField field, ulong requestedValue, ulong currentValue)
    {
        ValidateMembership(register, field);
        ValidateValue(field, requestedValue);
        if (!CanRead(register.Access) || !CanWrite(register.Access) || !CanWrite(field.Access))
            throw new InvalidOperationException("This register or field does not support ordinary read/write access.");
        if (HasReadSideEffect(register.ReadAction) || register.Fields.Any(f => HasReadSideEffect(f.ReadAction)))
            throw new InvalidOperationException("Reading this register has side effects; automatic read/modify/write is refused.");
        if (HasSideEffect(register.ModifiedWriteValues) || register.Fields.Any(f => HasSideEffect(f.ModifiedWriteValues)))
            throw new InvalidOperationException("This register has write side effects; use an explicitly named field action, not an assignment.");
        if (register.Access.Contains("Once", StringComparison.OrdinalIgnoreCase) || field.Access.Contains("Once", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Write-once registers require a target-specific operation.");
        var widthMask = register.SizeBits == 64 ? ulong.MaxValue : (1UL << register.SizeBits) - 1;
        var updated = ((currentValue & ~field.Mask) | (requestedValue << field.BitOffset)) & widthMask;
        return new(register.Address, register.SizeBits, updated, $"Set {register.Name}.{field.Name} to {requestedValue} (0x{requestedValue:X}).");
    }

    /// <summary>Plans a direct one-to-clear/set/toggle command without reading a register. Mixed write semantics are refused.</summary>
    public static RegisterWrite CreateFieldAction(SvdRegister register, SvdField field, ulong bits)
    {
        ValidateMembership(register, field);
        ValidateValue(field, bits);
        var action = field.ModifiedWriteValues;
        if (action is not ("oneToClear" or "oneToSet" or "oneToToggle"))
            throw new InvalidOperationException("Only explicit oneToClear, oneToSet and oneToToggle actions can be issued without a read.");
        if (!CanWrite(register.Access) || !CanWrite(field.Access) || register.Fields.Any(f => CanWrite(f.Access) && f.ModifiedWriteValues != action))
            throw new InvalidOperationException("Mixed register write semantics require a target-specific operation.");
        // Every writable bit must be described, otherwise a zero in an unspecified ordinary field could change it.
        var described = register.Fields.Aggregate(0UL, (mask, f) => mask | f.Mask);
        var widthMask = register.SizeBits == 64 ? ulong.MaxValue : (1UL << register.SizeBits) - 1;
        if (described != widthMask && register.ModifiedWriteValues != action)
            throw new InvalidOperationException("Unspecified bits lack safe write semantics; direct field action is refused.");
        return new(register.Address, register.SizeBits, bits << field.BitOffset,
            $"{action} {register.Name}.{field.Name}: mask 0x{bits:X}. This is an action, not the resulting register value.");
    }

    private static void ValidateMembership(SvdRegister register, SvdField field)
    {
        ArgumentNullException.ThrowIfNull(register);
        ArgumentNullException.ThrowIfNull(field);
        if (!register.Fields.Contains(field)) throw new ArgumentException("The field does not belong to this register.", nameof(field));
        if (register.SizeBits is < 1 or > 64) throw new ArgumentException("Register width must be between 1 and 64 bits.", nameof(register));
    }

    private static void ValidateValue(SvdField field, ulong value)
    {
        if (field.BitWidth is < 1 or > 64 || field.BitOffset < 0 || field.BitOffset + field.BitWidth > 64)
            throw new ArgumentException("Field range must fit 64 bits.", nameof(field));
        if (field.BitWidth < 64 && value >= 1UL << field.BitWidth)
            throw new ArgumentOutOfRangeException(nameof(value), "Value does not fit the field.");
    }

    private static bool HasSideEffect(string? action) => action is { Length: > 0 } and not "modify";
    private static bool HasReadSideEffect(string? action) => action is { Length: > 0 };
    private static bool CanRead(string access) => access is "read-only" or "read-write" or "read-writeOnce";
    private static bool CanWrite(string access) => access is "write-only" or "read-write" or "writeOnce" or "read-writeOnce";
}
