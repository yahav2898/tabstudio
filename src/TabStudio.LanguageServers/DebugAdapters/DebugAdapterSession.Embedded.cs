using System.Globalization;
using System.Text.Json.Nodes;

namespace TabStudio.LanguageServers.DebugAdapters;

public sealed partial class DebugAdapterSession
{
    /// <summary>Reads a bounded byte range through DAP readMemory. The caller must pause the target and avoid read-sensitive peripheral registers.</summary>
    /// <param name="address">Absolute target address.</param>
    /// <param name="count">Number of bytes, at most one MiB.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<byte[]> ReadMemoryAsync(ulong address, int count, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, 1024 * 1024);
        _ = checked(address + (ulong)count);
        var body = await _connection.SendRequestAsync("readMemory", new JsonObject
        {
            ["memoryReference"] = "0x" + address.ToString("X", CultureInfo.InvariantCulture),
            ["count"] = count
        }, cancellationToken).ConfigureAwait(false);
        if (!body.TryGetProperty("address", out var firstAddress) || firstAddress.GetString() is not { } addressText)
            throw new DebugAdapterException("The adapter did not identify the first returned memory address.");
        var hexadecimal = addressText.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
        if (!ulong.TryParse(hexadecimal ? addressText.AsSpan(2) : addressText.AsSpan(),
            hexadecimal ? NumberStyles.AllowHexSpecifier : NumberStyles.None, CultureInfo.InvariantCulture, out var actualAddress) || actualAddress != address)
        {
            throw new DebugAdapterException("The adapter returned memory at a different address than requested.");
        }
        if (body.TryGetProperty("unreadableBytes", out var unreadable) && unreadable.GetInt32() > 0)
            throw new DebugAdapterException("The target reported unreadable bytes; partial register/memory reads are refused.");
        if (!body.TryGetProperty("data", out var encoded) || encoded.GetString() is not { } text)
            throw new DebugAdapterException("The adapter supplied no memory data.");
        if (text.Length > ((count + 2) / 3) * 4)
            throw new DebugAdapterException("The adapter returned more memory data than requested.");
        byte[] data;
        try { data = Convert.FromBase64String(text); }
        catch (FormatException error) { throw new DebugAdapterException("The adapter returned invalid base64 memory data.", error); }
        if (data.Length != count) throw new DebugAdapterException("The adapter returned a partial memory read.");
        return data;
    }

    /// <summary>Writes a bounded, complete memory range through DAP writeMemory. Callers must enforce target memory-map and configuration-write policy first.</summary>
    /// <param name="address">Absolute target address.</param>
    /// <param name="data">Bytes to write, at most one MiB.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task WriteMemoryAsync(ulong address, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(data.Length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(data.Length, 1024 * 1024);
        _ = checked(address + (ulong)data.Length);
        var body = await _connection.SendRequestAsync("writeMemory", new JsonObject
        {
            ["memoryReference"] = "0x" + address.ToString("X", CultureInfo.InvariantCulture),
            ["data"] = Convert.ToBase64String(data.Span), ["allowPartial"] = false
        }, cancellationToken).ConfigureAwait(false);
        if (!body.TryGetProperty("bytesWritten", out var written) || written.GetInt32() != data.Length)
            throw new DebugAdapterException("The adapter did not confirm a complete memory write. Read back before retrying.");
        if (body.TryGetProperty("offset", out var offset) && offset.GetInt64() != 0)
            throw new DebugAdapterException("The adapter reported a shifted memory write. Read back before retrying.");
    }
}
