using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace TabStudio.Toolchains.Embedded.Flashing;

/// <summary>A locally checked, application-only Nordic Secure DFU package. Signature trust is checked by the target bootloader.</summary>
/// <param name="ApplicationRange">Explicitly selected application address and payload length.</param>
/// <param name="ApplicationSha256">SHA-256 of the application BIN.</param>
/// <param name="HasSignature">Whether the init packet carries a signature; this does not assert trust in that signature.</param>
public sealed record NrfDfuApplication(FlashAddressRange ApplicationRange, string ApplicationSha256, bool HasSignature);

/// <summary>Validates Nordic SDK Secure DFU ZIP manifests and protobuf init packets without extracting archive paths.</summary>
/// <remarks>The wire format follows Nordic's dfu-cc.proto. SoftDevice, bootloader, external-application and debug packages are refused.</remarks>
public static class NrfDfuPackage
{
    /// <summary>Checks one application BIN/DAT package, including declared size, SHA-256, duplicate fields and archive bounds.</summary>
    public static NrfDfuApplication Inspect(ReadOnlySpan<byte> archiveBytes, ulong applicationAddress)
    {
        if (archiveBytes.Length is <= 0 or > 128 * 1024 * 1024)
        {
            throw new InvalidDataException("Nordic DFU package must contain 1 byte..128 MiB.");
        }
        using var stream = new MemoryStream(archiveBytes.ToArray(), writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        if (archive.Entries.Count != 3 || archive.Entries.Select(entry => entry.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 3 ||
            archive.Entries.Any(entry => string.IsNullOrWhiteSpace(entry.FullName) || entry.FullName.Contains('/') || entry.FullName.Contains('\\') || entry.FullName.Contains(':') || entry.FullName.Any(char.IsControl)))
        {
            throw new InvalidDataException("Secure DFU requires exactly three flat, uniquely named files: manifest, application BIN and init DAT.");
        }
        var manifestBytes = Read(archive.GetEntry("manifest.json") ?? throw new InvalidDataException("DFU manifest.json is missing."), 65536);
        using var document = JsonDocument.Parse(manifestBytes, new JsonDocumentOptions { MaxDepth = 8 });
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 1 || !root.TryGetProperty("manifest", out var manifest) ||
            manifest.ValueKind != JsonValueKind.Object || manifest.EnumerateObject().Count() != 1 || !manifest.TryGetProperty("application", out var application) || application.ValueKind != JsonValueKind.Object ||
            application.EnumerateObject().Count() != 2 || !application.TryGetProperty("bin_file", out var binName) || binName.ValueKind != JsonValueKind.String ||
            !application.TryGetProperty("dat_file", out var datName) || datName.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException("Only an application-only Nordic Secure DFU manifest is allowed; bootloader/SoftDevice/configuration updates are refused.");
        }
        var binaryEntry = archive.GetEntry(binName.GetString()!) ?? throw new InvalidDataException("DFU application BIN is missing.");
        var initEntry = archive.GetEntry(datName.GetString()!) ?? throw new InvalidDataException("DFU init DAT is missing.");
        if (!binaryEntry.FullName.EndsWith(".bin", StringComparison.OrdinalIgnoreCase) || !initEntry.FullName.EndsWith(".dat", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("DFU application and init files must use BIN and DAT extensions.");
        }
        var binary = Read(binaryEntry, 128 * 1024 * 1024);
        var packet = Fields(Read(initEntry, 65536));
        var isSigned = packet.ContainsKey(2);
        if (packet.ContainsKey(1) == isSigned)
        {
            throw new InvalidDataException("DFU init packet must contain exactly one command or signed command.");
        }
        Dictionary<int, Field> command;
        if (isSigned)
        {
            var signed = Fields(Bytes(packet, 2));
            if (Scalar(signed, 2) > 1 || Bytes(signed, 3).Length != 64)
            {
                throw new InvalidDataException("DFU signature encoding is unsupported.");
            }
            command = Fields(Bytes(signed, 1));
        }
        else
        {
            command = Fields(Bytes(packet, 1));
        }
        if (Scalar(command, 1) != 1 || command.ContainsKey(3))
        {
            throw new InvalidDataException("DFU packet is not an INIT command.");
        }
        var init = Fields(Bytes(command, 2));
        if (Scalar(init, 4) != 0 || Scalar(init, 5) != 0 || Scalar(init, 6) != 0 || Scalar(init, 9) != 0 || Scalar(init, 7) != (ulong)binary.Length)
        {
            throw new InvalidDataException("DFU init packet must describe exactly one non-debug application, without bootloader or SoftDevice bytes.");
        }
        var hash = Fields(Bytes(init, 8));
        var digest = SHA256.HashData(binary);
        var expected = digest.Reverse().ToArray(); // Nordic serializes the SHA-256 digest in little-endian byte order.
        if (Scalar(hash, 1) != 3 || !CryptographicOperations.FixedTimeEquals(Bytes(hash, 2), expected))
        {
            throw new InvalidDataException("DFU application's SHA-256 does not match its init packet.");
        }
        return new(new(applicationAddress, (ulong)binary.Length), Convert.ToHexStringLower(digest), isSigned);
    }

    private static byte[] Read(ZipArchiveEntry entry, int maximumSize)
    {
        if (entry.Length <= 0 || entry.Length > maximumSize)
        {
            throw new InvalidDataException("DFU archive entry exceeds its size bound or is empty.");
        }
        using var input = entry.Open();
        var bytes = new byte[checked((int)entry.Length)];
        input.ReadExactly(bytes);
        if (input.ReadByte() != -1)
        {
            throw new InvalidDataException("DFU archive entry exceeds its declared size.");
        }
        return bytes;
    }

    private sealed record Field(ulong? Scalar, byte[]? Bytes);

    private static Dictionary<int, Field> Fields(ReadOnlySpan<byte> data)
    {
        var result = new Dictionary<int, Field>();
        var offset = 0;
        while (offset < data.Length)
        {
            if (result.Count >= 256)
            {
                throw new InvalidDataException("DFU protobuf contains too many fields.");
            }
            var tag = Varint(data, ref offset);
            var number = checked((int)(tag >> 3));
            if (number <= 0 || number > 536870911)
            {
                throw new InvalidDataException("DFU protobuf field number is invalid.");
            }
            Field field;
            switch (tag & 7)
            {
                case 0:
                    field = new(Varint(data, ref offset), null);
                    break;
                case 2:
                    var length = Varint(data, ref offset);
                    if (length > (ulong)(data.Length - offset))
                    {
                        throw new InvalidDataException("Truncated DFU protobuf field.");
                    }
                    field = new(null, data.Slice(offset, (int)length).ToArray());
                    offset += (int)length;
                    break;
                default:
                    throw new InvalidDataException("Unsupported DFU protobuf wire type.");
            }
            if (!result.TryAdd(number, field))
            {
                throw new InvalidDataException("Duplicate DFU protobuf field is ambiguous.");
            }
        }
        return result;
    }

    private static ulong Varint(ReadOnlySpan<byte> data, ref int offset)
    {
        ulong value = 0;
        for (var shift = 0; shift < 64; shift += 7)
        {
            if (offset >= data.Length)
            {
                throw new InvalidDataException("Truncated DFU protobuf varint.");
            }
            var item = data[offset++];
            if (shift == 63 && item > 1)
            {
                throw new InvalidDataException("DFU protobuf varint overflows.");
            }
            value |= (ulong)(item & 0x7F) << shift;
            if ((item & 0x80) == 0)
            {
                return value;
            }
        }
        throw new InvalidDataException("DFU protobuf varint is too long.");
    }

    private static ulong Scalar(Dictionary<int, Field> fields, int number) => !fields.TryGetValue(number, out var field) ? 0 :
        field.Scalar ?? throw new InvalidDataException("DFU protobuf scalar has the wrong wire type.");

    private static byte[] Bytes(Dictionary<int, Field> fields, int number) => fields.TryGetValue(number, out var field) && field.Bytes is { } bytes ? bytes :
        throw new InvalidDataException("DFU protobuf byte field is missing or has the wrong wire type.");
}
