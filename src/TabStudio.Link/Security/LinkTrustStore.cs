using System.Text.Json;
using System.Text.Json.Serialization;

namespace TabStudio.Link.Security;

/// <summary>
/// The devices this one is paired with, kept in a JSON file: a host keeps the tablets it serves, a tablet the hosts
/// it links to. Pairing once is enough; later connections are recognized by fingerprint.
/// </summary>
/// <remarks>Thread-safe. Changes are saved immediately; a damaged file reads as no paired devices.</remarks>
public sealed class LinkTrustStore
{
    private readonly string? _filePath;
    private readonly Lock _gate = new();
    private List<LinkPairedDevice> _devices;

    /// <summary>Creates a store.</summary>
    /// <param name="filePath">The file it is kept in, or <see langword="null"/> to keep it in memory only.</param>
    public LinkTrustStore(string? filePath)
    {
        _filePath = filePath;
        _devices = filePath is null ? [] : Load(filePath);
    }

    /// <summary>Raised after a device was paired, updated or forgotten.</summary>
    public event EventHandler? Changed;

    /// <summary>The paired devices, the most recently seen first.</summary>
    public IReadOnlyList<LinkPairedDevice> Devices
    {
        get
        {
            lock (_gate)
            {
                return [.. _devices.OrderByDescending(d => d.LastSeen)];
            }
        }
    }

    /// <summary>Returns a paired device by its fingerprint.</summary>
    /// <param name="fingerprint">The device's certificate fingerprint.</param>
    /// <returns>The device, or <see langword="null"/> when it is not paired.</returns>
    public LinkPairedDevice? Find(LinkFingerprint fingerprint)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);
        lock (_gate)
        {
            return _devices.Find(d => d.Fingerprint == fingerprint);
        }
    }

    /// <summary>Pairs a device, or updates it when it is already paired, and saves.</summary>
    /// <param name="device">The device.</param>
    public void Remember(LinkPairedDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        lock (_gate)
        {
            _devices = [.. _devices.Where(d => d.Fingerprint != device.Fingerprint), device];
            Save();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Forgets a device, which then has to pair again, and saves.</summary>
    /// <param name="fingerprint">The device's fingerprint.</param>
    /// <returns>Whether the device was paired.</returns>
    public bool Forget(LinkFingerprint fingerprint)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);
        bool removed;
        lock (_gate)
        {
            var remaining = _devices.Where(d => d.Fingerprint != fingerprint).ToList();
            removed = remaining.Count != _devices.Count;
            _devices = remaining;
            if (removed)
            {
                Save();
            }
        }

        if (removed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return removed;
    }

    private static List<LinkPairedDevice> Load(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return [];
            }

            var stored = JsonSerializer.Deserialize(File.ReadAllText(filePath), LinkTrustStoreJsonContext.Default.ListStoredDevice) ?? [];
            var devices = new List<LinkPairedDevice>();
            foreach (var device in stored)
            {
                if (LinkFingerprint.TryParse(device.Fingerprint, out var fingerprint))
                {
                    devices.Add(new LinkPairedDevice(device.Name, device.Kind, fingerprint, device.PairedAt, device.LastSeen, device.Addresses ?? [], device.Port));
                }
            }

            return devices;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    /// <summary>Writes the file. The caller holds the gate.</summary>
    private void Save()
    {
        if (_filePath is null)
        {
            return;
        }

        var stored = _devices
            .Select(d => new StoredDevice(d.Name, d.Kind, d.Fingerprint.Value, d.PairedAt, d.LastSeen, d.Addresses, d.Port))
            .ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_filePath))!);
        var temporary = _filePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(stored, LinkTrustStoreJsonContext.Default.ListStoredDevice));
        File.Move(temporary, _filePath, overwrite: true);
    }
}

/// <summary>A paired device as the file stores it.</summary>
internal sealed record StoredDevice(
    string Name,
    string Kind,
    string Fingerprint,
    DateTimeOffset PairedAt,
    DateTimeOffset LastSeen,
    IReadOnlyList<string>? Addresses,
    int Port);

/// <summary>Source-generated JSON for the paired devices file.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(List<StoredDevice>))]
internal sealed partial class LinkTrustStoreJsonContext : JsonSerializerContext
{
}
