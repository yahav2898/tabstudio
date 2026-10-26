namespace TabStudio.Toolchains.Embedded.Peripherals;

/// <summary>A device's CMSIS-SVD register description. It is metadata, never a permission to access a target.</summary>
/// <param name="Name">Device name supplied by the pack.</param>
/// <param name="Peripherals">Peripheral instances and their absolute addresses.</param>
public sealed record SvdDevice(string Name, IReadOnlyList<SvdPeripheral> Peripherals)
{
    /// <summary>CPU byte order from the vendor SVD, or unspecified. Live accesses must not guess it.</summary>
    public string ByteOrder { get; init; } = "unspecified";
}

/// <summary>A peripheral instance with its flattened registers.</summary>
/// <param name="Name">Peripheral instance name.</param>
/// <param name="Description">Vendor description.</param>
/// <param name="BaseAddress">Absolute byte address.</param>
/// <param name="Registers">Registers, with cluster and array names retained.</param>
public sealed record SvdPeripheral(string Name, string Description, ulong BaseAddress, IReadOnlyList<SvdRegister> Registers);

/// <summary>A register with access and side-effect metadata retained for safe editing.</summary>
/// <param name="Name">Register name, including a cluster prefix when applicable.</param>
/// <param name="Description">Vendor description.</param>
/// <param name="Address">Absolute byte address.</param>
/// <param name="SizeBits">Register width in bits.</param>
/// <param name="Access">CMSIS access token such as read-only or read-write.</param>
/// <param name="ResetValue">Optional reset value, not a live read.</param>
/// <param name="ModifiedWriteValues">Write side effect such as oneToClear.</param>
/// <param name="ReadAction">Read side effect such as clear.</param>
/// <param name="Fields">Named bit fields.</param>
public sealed record SvdRegister(string Name, string Description, ulong Address, int SizeBits, string Access,
    ulong? ResetValue, string? ModifiedWriteValues, string? ReadAction, IReadOnlyList<SvdField> Fields);

/// <summary>A named bit field and the vendor's interpretation of its values.</summary>
/// <param name="Name">Field name.</param>
/// <param name="Description">Vendor description.</param>
/// <param name="BitOffset">Least significant bit.</param>
/// <param name="BitWidth">Number of bits.</param>
/// <param name="Access">Effective access inherited from its register.</param>
/// <param name="ModifiedWriteValues">Effective write side effect.</param>
/// <param name="ReadAction">Effective read side effect.</param>
/// <param name="Values">Enumerated values. Wildcard enumerations are retained in the description, not guessed.</param>
public sealed record SvdField(string Name, string Description, int BitOffset, int BitWidth, string Access,
    string? ModifiedWriteValues, string? ReadAction, IReadOnlyList<SvdEnumValue> Values)
{
    /// <summary>Returns the positioned mask, including the full-width 64-bit case.</summary>
    public ulong Mask => (BitWidth == 64 ? ulong.MaxValue : (1UL << BitWidth) - 1) << BitOffset;

    /// <summary>Extracts this field from a live or historical register value.</summary>
    public ulong Decode(ulong registerValue) => (registerValue & Mask) >> BitOffset;
}

/// <summary>A concrete enumerated field value.</summary>
/// <param name="Name">Vendor symbolic name.</param>
/// <param name="Value">Numeric field value.</param>
/// <param name="Description">Vendor description.</param>
public sealed record SvdEnumValue(string Name, ulong Value, string Description);
