using System.Text;
using System.Text.Json;
using TabStudio.Toolchains.Microchip;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Toolchains.Embedded.Storage;

/// <summary>Block RPC over a fixed Python BOT/SCSI worker borrowing one Android permissioned USB descriptor.</summary>
public sealed class UsbMassStorageBlockDevice : IBlockDevice, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ToolchainProcess _process;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Task<string>? _error;
    private int _requestId;
    private bool _writesEnabled, _faulted, _disposed;

    private UsbMassStorageBlockDevice(ToolchainProcess process)
    {
        _process = process;
        _reader = new(process.Output, Encoding.UTF8, false, 4096, leaveOpen: true);
        _writer = new(process.Input, new UTF8Encoding(false), 4096, leaveOpen: true) { NewLine = "\n", AutoFlush = true };
        if (process.Error is { } error) _error = new StreamReader(error).ReadToEndAsync();
    }

    /// <inheritdoc />
    public int SectorSize { get; private set; }
    /// <inheritdoc />
    public ulong SectorCount { get; private set; }
    /// <summary>SCSI inquiry text supplied by the actual selected device.</summary>
    public string Inquiry { get; private set; } = "";

    /// <summary>Starts the read-only worker. The caller retains the USB connection until this object is disposed.</summary>
    public static async Task<UsbMassStorageBlockDevice> OpenAsync(IToolchainRuntime runtime, IUsbConnection connection,
        string workingDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(connection);
        if (!Uf2BootselUpload.IsSupportedDevice(connection.Device) || connection.FileDescriptor < 3)
            throw new NotSupportedException("Only the selected permissioned RP ROM BOOTSEL device can be opened for mass-storage upload.");
        var process = runtime.Start(new ProcessStartSpec
        {
            Program = "python3", Arguments = ["-u", UsbBridgeInstallation.ToolsDirectory + "/msc_worker.py"],
            WorkingDirectory = workingDirectory,
            InheritedDescriptors = new Dictionary<int, int> { [connection.FileDescriptor] = 3 },
            Environment = new Dictionary<string, string?>
            {
                ["TABSTUDIO_USB_FD"] = "3",
                ["TABSTUDIO_USB_DEVICE"] = JsonSerializer.Serialize(connection.Device, JsonOptions),
            },
        });
        var result = new UsbMassStorageBlockDevice(process);
        try
        {
            using var ready = await result.ReadReplyAsync(cancellationToken).ConfigureAwait(false);
            var root = ready.RootElement;
            if (!root.GetProperty("ok").GetBoolean()) throw new IOException(root.GetProperty("error").GetString());
            result.SectorSize = root.GetProperty("sectorSize").GetInt32();
            result.SectorCount = root.GetProperty("sectorCount").GetUInt64();
            result.Inquiry = root.GetProperty("inquiry").GetString() ?? "";
            if (result.SectorSize != 512 || result.SectorCount is 0 or > uint.MaxValue
                || root.GetProperty("vendorId").GetInt32() != connection.Device.VendorId
                || root.GetProperty("productId").GetInt32() != connection.Device.ProductId)
            {
                throw new InvalidDataException("Mass-storage worker identity/capacity did not match its granted BOOTSEL device.");
            }
            return result;
        }
        catch { await result.DisposeAsync().ConfigureAwait(false); throw; }
    }

    /// <summary>Enables writes only for a plan prepared from this exact worker's validated FAT volume.</summary>
    public async Task EnableWritesAsync(PreparedUf2Upload plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!ReferenceEquals(plan.Volume.BlockDevice, this)) throw new InvalidOperationException("The UF2 plan belongs to another block device.");
        using var reply = await RequestAsync(new { op = "enable", guard = "rp-rom-uf2-validated", family = plan.FamilyId }, cancellationToken).ConfigureAwait(false);
        _writesEnabled = true;
    }

    /// <inheritdoc />
    public async Task<byte[]> ReadBlocksAsync(uint sector, int count, CancellationToken cancellationToken)
    {
        ValidateRange(sector, count);
        var result = new byte[checked(count * 512)];
        for (var offset = 0; offset < count; offset += 64)
        {
            var chunk = Math.Min(64, count - offset);
            using var reply = await RequestAsync(new { op = "read", sector = checked(sector + (uint)offset), count = chunk }, cancellationToken).ConfigureAwait(false);
            byte[] data;
            try { data = Convert.FromBase64String(reply.RootElement.GetProperty("data").GetString() ?? ""); }
            catch (FormatException error) { throw new InvalidDataException("Invalid block-worker base64 payload.", error); }
            if (data.Length != chunk * 512) throw new InvalidDataException("Short SCSI block read.");
            data.CopyTo(result, offset * 512);
        }
        return result;
    }

    /// <inheritdoc />
    public async Task WriteBlocksAsync(uint sector, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        if (!_writesEnabled) throw new InvalidOperationException("The block worker remains read-only until its UF2/volume plan is validated.");
        if (data.Length == 0 || data.Length % 512 != 0 || data.Length > 64 * 512)
            throw new ArgumentException("SCSI writes require 1..64 complete sectors.", nameof(data));
        ValidateRange(sector, data.Length / 512);
        using var reply = await RequestAsync(new { op = "write", sector, data = Convert.ToBase64String(data.Span) }, cancellationToken).ConfigureAwait(false);
    }

    private void ValidateRange(uint sector, int count)
    {
        if (count <= 0 || count > 32768 || (ulong)sector + (uint)count > SectorCount)
            throw new ArgumentOutOfRangeException(nameof(count), "Block transfer exceeds actual capacity or the bounded read limit.");
    }

    private async Task<JsonDocument> RequestAsync<T>(T request, CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_faulted) throw new IOException("The mass-storage worker lost protocol synchronization; reconnect before retrying.");
            var id = ++_requestId;
            var node = JsonSerializer.SerializeToNode(request, JsonOptions) ?? throw new InvalidOperationException("Empty block command.");
            node["id"] = id;
            token.ThrowIfCancellationRequested();
            await _writer.WriteLineAsync(node.ToJsonString().AsMemory(), token).ConfigureAwait(false);
            await _writer.FlushAsync(token).ConfigureAwait(false);
            var reply = await ReadReplyAsync(token).ConfigureAwait(false);
            var root = reply.RootElement;
            if (root.GetProperty("id").GetInt32() != id) { reply.Dispose(); _faulted = true; throw new InvalidDataException("Block-worker reply ID mismatch."); }
            if (!root.GetProperty("ok").GetBoolean())
            {
                var message = root.GetProperty("error").GetString() ?? "SCSI command failed.";
                var disconnected = root.TryGetProperty("disconnected", out var gone) && gone.GetBoolean();
                var transferred = root.TryGetProperty("dataTransferred", out var sent) && sent.GetBoolean();
                if (root.TryGetProperty("fatal", out var fatal) && fatal.GetBoolean()) _faulted = true;
                reply.Dispose();
                if (disconnected) throw new BlockWriteDisconnectedException(message, transferred);
                throw new IOException(message);
            }
            return reply;
        }
        catch (OperationCanceledException) { _faulted = true; _process.Stop(); throw; }
        finally { _gate.Release(); }
    }

    private async Task<JsonDocument> ReadReplyAsync(CancellationToken token)
    {
        var line = await _reader.ReadLineAsync(token).ConfigureAwait(false);
        if (line is null) throw new IOException("The permissioned BOOTSEL worker disconnected without a complete response.");
        if (line.Length > 65536) throw new InvalidDataException("Block-worker reply exceeds its bounded protocol size.");
        return JsonDocument.Parse(line, new JsonDocumentOptions { MaxDepth = 8 });
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _process.CloseInput();
        _process.Stop();
        try { await _process.Completion.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false); }
        catch (TimeoutException) { /* Parent FD remains owned by caller until process cleanup completes. */ }
        _reader.Dispose(); await _writer.DisposeAsync().ConfigureAwait(false); _process.Dispose();
        if (_error is { IsCompletedSuccessfully: true }) _ = _error.Result;
        _gate.Dispose();
    }
}
