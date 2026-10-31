using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace TabStudio.Containers.Runtime;

/// <summary>
/// A container's output: the most recent lines in memory for the Containers window and <c>docker logs</c>, the rest
/// in a file next to the container (bounded, like Docker's json-file driver with a size limit), and live followers.
/// </summary>
public sealed class ContainerLog : IDisposable
{
    private const int MemoryLines = 2000;
    private const long FileLimit = 8 * 1024 * 1024;
    private readonly Lock _gate = new();
    private readonly Queue<ContainerLogEntry> _recent = new();
    private readonly List<Channel<ContainerLogEntry>> _followers = [];
    private readonly string _path;
    private StreamWriter? _file;
    private Func<string, string> _redact = text => text;

    /// <summary>Opens a container's log.</summary>
    /// <param name="path">Its file.</param>
    public ContainerLog(string path)
    {
        _path = path;
        if (File.Exists(path))
        {
            foreach (var line in File.ReadLines(path).TakeLast(MemoryLines))
            {
                try
                {
                    if (JsonSerializer.Deserialize<ContainerLogEntry>(line) is { } entry) _recent.Enqueue(entry);
                }
                catch (JsonException)
                {
                }
            }
        }
    }

    /// <summary>Hides secrets, such as the container's passwords, in what is shown.</summary>
    public void SetRedaction(Func<string, string> redact) => _redact = redact;

    /// <summary>Raised for every new entry.</summary>
    public event EventHandler<ContainerLogEntry>? Written;

    /// <summary>The most recent entries.</summary>
    public IReadOnlyList<ContainerLogEntry> Recent(int? tail = null)
    {
        lock (_gate)
        {
            return tail is { } count ? [.. _recent.TakeLast(count)] : [.. _recent];
        }
    }

    /// <summary>Adds an entry.</summary>
    public void Write(int stream, string text)
    {
        var entry = new ContainerLogEntry(DateTimeOffset.UtcNow, stream, _redact(text));
        Channel<ContainerLogEntry>[] followers;
        lock (_gate)
        {
            _recent.Enqueue(entry);
            while (_recent.Count > MemoryLines) _recent.Dequeue();
            try
            {
                if (_file is null || _file.BaseStream.Length > FileLimit)
                {
                    _file?.Dispose();
                    if (File.Exists(_path) && new FileInfo(_path).Length > FileLimit) File.Move(_path, _path + ".1", overwrite: true);
                    _file = new StreamWriter(new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
                }

                _file.WriteLine(JsonSerializer.Serialize(entry));
            }
            catch (IOException)
            {
                // The memory copy still has it; a full disk must not stop the container.
            }

            followers = [.. _followers];
        }

        foreach (var follower in followers) follower.Writer.TryWrite(entry);
        Written?.Invoke(this, entry);
    }

    /// <summary>Follows the log: the recent entries (or the last <paramref name="tail"/>), then new ones until cancelled.</summary>
    public async IAsyncEnumerable<ContainerLogEntry> FollowAsync(int? tail, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<ContainerLogEntry>(new UnboundedChannelOptions { SingleReader = true });
        IReadOnlyList<ContainerLogEntry> backlog;
        lock (_gate)
        {
            backlog = tail is { } count ? [.. _recent.TakeLast(count)] : [.. _recent];
            _followers.Add(channel);
        }

        try
        {
            foreach (var entry in backlog) yield return entry;
            await foreach (var entry in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false)) yield return entry;
        }
        finally
        {
            lock (_gate) _followers.Remove(channel);
        }
    }

    /// <summary>Ends every follower, as when the container is removed or its process ends for good.</summary>
    public void Complete()
    {
        lock (_gate)
        {
            foreach (var follower in _followers) follower.Writer.TryComplete();
            _followers.Clear();
            _file?.Dispose();
            _file = null;
        }
    }

    /// <inheritdoc />
    public void Dispose() => Complete();

    /// <summary>Reads a stream into the log line by line (or chunk by chunk for a terminal).</summary>
    public async Task PumpAsync(Stream stream, int number, bool terminal, CancellationToken cancellationToken = default)
    {
        try
        {
            if (terminal)
            {
                var buffer = new byte[8192];
                var decoder = Encoding.UTF8.GetDecoder();
                var chars = new char[8192 + 4];
                int read;
                while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    var count = decoder.GetChars(buffer, 0, read, chars, 0);
                    if (count > 0) Write(number, new string(chars, 0, count));
                }

                return;
            }

            using var reader = new StreamReader(stream, Encoding.UTF8);
            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                Write(number, line);
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
        {
        }
    }
}

