using System.Text.Json;
using System.Threading.Channels;
using TabStudio.WindowsBridge.Builds;
using TabStudio.WindowsBridge.Debugging;

namespace TabStudio.WindowsBridge.Hosting;

/// <summary>
/// netcoredbg on the PC, tunneled to the tablet's debugger: the Debug Adapter Protocol's bytes travel over TabStudio
/// Link in both directions, with the paths in every message rewritten between the tablet's and the mirror's. The
/// program netcoredbg launches is reported as soon as it runs, so its windows stream like any other.
/// </summary>
internal sealed class BridgeDebugTunnel : IDisposable
{
    private readonly Channel<byte[]> _fromTablet = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });
    private readonly DapPathRewriter _rewriter;
    private readonly Func<byte[], Task> _toTablet;
    private readonly BridgeProcess _adapter;
    private int _closed;

    /// <summary>Starts netcoredbg for a tunnel.</summary>
    /// <param name="id">The tablet's number for the tunnel.</param>
    /// <param name="netcoredbg">The netcoredbg program.</param>
    /// <param name="dotnet">The <c>dotnet</c> program, which netcoredbg runs .NET apps with.</param>
    /// <param name="workingDirectory">The folder to start it in, on the bridge.</param>
    /// <param name="paths">The workspace's paths.</param>
    /// <param name="toTablet">Sends bytes of the adapter's messages to the tablet.</param>
    /// <exception cref="BridgeProcessException">netcoredbg could not be started.</exception>
    public BridgeDebugTunnel(int id, string netcoredbg, string? dotnet, string workingDirectory, BridgePathMapper paths, Func<byte[], Task> toTablet)
    {
        Id = id;
        _rewriter = new DapPathRewriter(paths);
        _toTablet = toTablet;
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (dotnet is not null && Path.GetDirectoryName(dotnet) is { Length: > 0 } dotnetFolder)
        {
            // netcoredbg starts a .NET app's assembly with the dotnet it finds on the PATH: the SDK's own.
            environment["PATH"] = dotnetFolder + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
            environment["DOTNET_ROOT"] = dotnetFolder;
        }

        _adapter = BridgeProcess.Start(new BridgeProcessStart(netcoredbg, ["--interpreter=vscode"], workingDirectory, environment, BinaryStreams: true));
        _ = Task.Run(WriteToAdapterAsync);
        _ = Task.Run(ReadFromAdapterAsync);
        _ = _adapter.Exited.ContinueWith(t => Close(t.IsCompletedSuccessfully && t.Result != 0 ? $"netcoredbg stopped with exit code {t.Result}." : null), TaskScheduler.Default);
    }

    /// <summary>Raised once on the thread pool when the tunnel closed, with why when it was not on purpose.</summary>
    public event EventHandler<string?>? Closed;

    /// <summary>Raised on the thread pool when the debugged program started, with its process id and name.</summary>
    public event EventHandler<(int ProcessId, string Name)>? ProgramStarted;

    /// <summary>The tablet's number for the tunnel.</summary>
    public int Id { get; }

    /// <summary>netcoredbg's process.</summary>
    public int AdapterProcessId => _adapter.Id;

    /// <summary>The program the tablet asked to launch, once it did.</summary>
    public string? ProgramName { get; private set; }

    /// <summary>Passes bytes from the tablet's debugger to netcoredbg, in order.</summary>
    /// <param name="data">The bytes.</param>
    public void FromTablet(byte[] data) => _fromTablet.Writer.TryWrite(data);

    /// <summary>Closes the tunnel and ends netcoredbg, which ends the program it debugs.</summary>
    public void Dispose() => Close(null);

    private void Close(string? reason)
    {
        if (Interlocked.Exchange(ref _closed, 1) == 1)
        {
            return;
        }

        _fromTablet.Writer.TryComplete();
        _adapter.Dispose();
        Closed?.Invoke(this, reason);
    }

    private async Task WriteToAdapterAsync()
    {
        var framer = new DapMessageFramer();
        try
        {
            await foreach (var data in _fromTablet.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                foreach (var message in framer.Append(data))
                {
                    var (json, command) = _rewriter.ToAdapter(message);
                    if (command is "launch" && ProgramName is null)
                    {
                        ProgramName = ReadProgramName(json);
                    }

                    var framed = DapMessageFramer.Frame(json);
                    await _adapter.StandardInput.WriteAsync(framed).ConfigureAwait(false);
                    await _adapter.StandardInput.FlushAsync().ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidDataException or InvalidOperationException)
        {
            Close($"The connection to netcoredbg broke: {ex.Message}");
        }
    }

    private async Task ReadFromAdapterAsync()
    {
        var framer = new DapMessageFramer();
        var buffer = new byte[64 * 1024];
        try
        {
            while (true)
            {
                var read = await _adapter.StandardOutput.ReadAsync(buffer).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                foreach (var message in framer.Append(buffer.AsSpan(0, read)))
                {
                    var (json, name) = _rewriter.ToTablet(message);
                    if (name == "process" && ReadProcessId(json) is { } processId)
                    {
                        ProgramStarted?.Invoke(this, (processId, ProgramName ?? "Program"));
                    }

                    await _toTablet(DapMessageFramer.Frame(json)).ConfigureAwait(false);
                }
            }

            Close(null);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidDataException or InvalidOperationException or TabStudio.Link.LinkConnectionLostException)
        {
            Close($"The connection to netcoredbg broke: {ex.Message}");
        }
    }

    /// <summary>The name of the program a launch request starts: its assembly or executable without the extension.</summary>
    private static string? ReadProgramName(byte[] json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("arguments", out var arguments)
                && arguments.TryGetProperty("program", out var program) && program.ValueKind == JsonValueKind.String
                ? Path.GetFileNameWithoutExtension(program.GetString())
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The program's process from netcoredbg's <c>process</c> event.</summary>
    private static int? ReadProcessId(byte[] json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("body", out var body)
                && body.TryGetProperty("systemProcessId", out var id) && id.TryGetInt32(out var pid) && pid > 0
                ? pid
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
