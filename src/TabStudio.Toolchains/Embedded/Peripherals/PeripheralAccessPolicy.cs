using System.Text.Json;

namespace TabStudio.Toolchains.Embedded.Peripherals;

/// <summary>An explicit selected-device register allowlist. SVD metadata alone never grants write permission.</summary>
public sealed class PeripheralAccessPolicy
{
    private static readonly string[] ConfigurationTokens = ["FLASH", "EFUSE", "FUSE", "OTP", "OPTION", "OPTCR", "OPTKEY", "RDP", "UICR", "NVMC", "NVMCTRL", "EEPROM", "OBR", "OBKEY"];
    private readonly Dictionary<string, (ulong Address, int Size)> _ordinaryRegisters;

    private PeripheralAccessPolicy(string device, Dictionary<string, (ulong Address, int Size)> ordinaryRegisters)
    {
        Device = device;
        _ordinaryRegisters = ordinaryRegisters;
    }

    /// <summary>The exact SVD device name for which the map was reviewed.</summary>
    public string Device { get; }

    /// <summary>Reads a bounded explicit ordinaryRegisters list, including each full peripheral/register name, numeric address and sizeBits.</summary>
    public static PeripheralAccessPolicy Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (json.Length > 256 * 1024) throw new InvalidDataException("Peripheral access map exceeds its size bound.");
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
        var root = document.RootElement;
        var device = root.GetProperty("device").GetString();
        if (string.IsNullOrWhiteSpace(device)) throw new InvalidDataException("The peripheral access map must name the actual selected SVD device.");
        var entries = root.GetProperty("ordinaryRegisters");
        if (entries.ValueKind != JsonValueKind.Array || entries.GetArrayLength() > 4096) throw new InvalidDataException("Invalid ordinary-register allowlist.");
        var registers = new Dictionary<string, (ulong Address, int Size)>(StringComparer.Ordinal);
        foreach (var entry in entries.EnumerateArray())
        {
            var name = entry.GetProperty("name").GetString();
            var address = entry.GetProperty("address").GetUInt64();
            var size = entry.GetProperty("sizeBits").GetInt32();
            if (string.IsNullOrWhiteSpace(name) || size is not (8 or 16 or 32 or 64) || address % (ulong)(size / 8) != 0)
                throw new InvalidDataException("An ordinary-register entry needs a full name and an aligned 8/16/32/64-bit address.");
            if (IsConfigurationRegister(name)) throw new InvalidDataException("Configuration, fuse and flash-controller registers cannot be ordinary-register write targets.");
            if (!registers.TryAdd(name, (address, size))) throw new InvalidDataException("Duplicate ordinary-register allowlist entry.");
        }
        return new(device, registers);
    }

    /// <summary>Refuses unknown/mismatched targets and every flash/configuration-controller name, even if manually added to a map.</summary>
    public void AuthorizeOrdinaryWrite(SvdDevice device, SvdPeripheral peripheral, SvdRegister register)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(peripheral);
        ArgumentNullException.ThrowIfNull(register);
        var name = peripheral.Name + "." + register.Name;
        if (Device != device.Name || IsConfigurationRegister(name) || !_ordinaryRegisters.TryGetValue(name, out var expected) ||
            expected.Address != register.Address || expected.Size != register.SizeBits)
        {
            throw new InvalidOperationException("This exact device/register/address is not in the reviewed ordinary-register map. Configuration writes use a separate target-specific safety flow.");
        }
    }

    private static bool IsConfigurationRegister(string name)
    {
        var upper = name.ToUpperInvariant();
        return ConfigurationTokens.Any(token => upper.Contains(token, StringComparison.Ordinal));
    }
}
