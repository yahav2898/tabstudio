using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace TabStudio.Containers.Runtime;

/// <summary>
/// Private command transport for Alpine's docker shell shim. It is authenticated on loopback and exposes no Docker
/// Engine API. Requests and output records are base64 framed; repository arguments are never evaluated as shell code.
/// </summary>
public sealed class DockerCommandBridge : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly DockerCommand _command;
    private readonly string _token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
    private readonly Task _accepting;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<long, Task> _clients = new();
    private long _next;

    /// <summary>Starts a private listener; callers install <see cref="ShellScript"/> as /usr/local/bin/docker.</summary>
    public DockerCommandBridge(DockerCommand command)
    {
        _command = command;
        _listener.Start(8);
        _accepting = AcceptAsync();
    }

    /// <summary>A BusyBox-only client, including streamed output, exec stdin and shell exit codes.</summary>
    public string ShellScript => $$"""
        #!/bin/sh
        umask 077
        dir=$(mktemp -d "${TMPDIR:-/tmp}/tabstudio-docker.XXXXXX") || exit 1
        writer= client=
        cleanup() { [ -z "$writer" ] || kill "$writer" 2>/dev/null; [ -z "$client" ] || kill "$client" 2>/dev/null; rm -rf "$dir"; }
        trap cleanup EXIT
        trap 'exit 130' INT TERM
        mkfifo "$dir/in" "$dir/out" || exit 1
        interactive=0
        if [ "$1" = exec ]; then
          for arg in "$@"; do case "$arg" in -i|-it|-ti) interactive=1;; esac; done
        fi
        (
          printf '%s\n' '{{_token}}'
          printf '%s' "$PWD" | base64 | tr -d '\n'; printf '\n'
          for arg in "$@"; do printf 'A'; printf '%s' "$arg" | base64 | tr -d '\n'; printf '\n'; done
          printf '\n'
          [ "$interactive" = 0 ] || exec cat
        ) > "$dir/in" &
        writer=$!
        nc 127.0.0.1 {{((IPEndPoint)_listener.LocalEndpoint).Port}} < "$dir/in" > "$dir/out" &
        client=$!
        while IFS= read -r record; do
          case "$record" in
            O*) printf '%s' "${record#O}" | base64 -d;;
            E*) printf '%s' "${record#E}" | base64 -d >&2;;
            X*) exit "${record#X}";;
          esac
        done < "$dir/out"
        printf '%s\n' 'TabStudio command connection closed.' >&2
        exit 1
        """ + "\n";

    private async Task AcceptAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_stop.Token).ConfigureAwait(false);
                var id = Interlocked.Increment(ref _next);
                var task = HandleAsync(client);
                _clients[id] = task;
                _ = RemoveWhenDoneAsync(id, task);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException) { }
    }

    private async Task RemoveWhenDoneAsync(long id, Task task)
    {
        await task.ConfigureAwait(false);
        _clients.TryRemove(id, out _);
    }

    private async Task HandleAsync(TcpClient client)
    {
        using (client)
        using (var stop = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token))
        using (var writes = new SemaphoreSlim(1, 1))
        {
            var stream = client.GetStream();
            // Slow or unauthenticated connections cannot hold the app indefinitely.
            stop.CancelAfter(TimeSpan.FromSeconds(10));
            try
            {
                var token = await ReadLineAsync(stream, stop.Token).ConfigureAwait(false);
                if (token is null || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(_token))) return;
                var cwd = await ReadLineAsync(stream, stop.Token).ConfigureAwait(false) ?? throw new IOException("Missing working directory.");
                var directory = Encoding.UTF8.GetString(Convert.FromBase64String(cwd));
                var args = new List<string>();
                while (await ReadLineAsync(stream, stop.Token).ConfigureAwait(false) is { Length: > 0 } line)
                {
                    if (args.Count >= 512) throw new IOException("Too many command arguments.");
                    args.Add(Encoding.UTF8.GetString(Convert.FromBase64String(line.StartsWith('A') ? line[1..] : throw new FormatException("Invalid argument frame."))));
                }
                stop.CancelAfter(Timeout.InfiniteTimeSpan);
                async Task Write(ReadOnlyMemory<byte> data, bool error)
                {
                    await writes.WaitAsync(stop.Token).ConfigureAwait(false);
                    try
                    {
                        var frame = Encoding.ASCII.GetBytes((error ? "E" : "O") + Convert.ToBase64String(data.Span) + "\n");
                        await stream.WriteAsync(frame, stop.Token).ConfigureAwait(false);
                    }
                    finally { writes.Release(); }
                }
                var code = await _command.ExecuteAsync(args, directory, stream, Write, stop.Token).ConfigureAwait(false);
                await stream.WriteAsync(Encoding.ASCII.GetBytes("X" + code.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n"), stop.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or FormatException or ObjectDisposedException) { }
        }
    }

    private static async Task<string?> ReadLineAsync(Stream stream, CancellationToken token)
    {
        using var line = new MemoryStream();
        var one = new byte[1];
        while (await stream.ReadAsync(one, token).ConfigureAwait(false) > 0)
        {
            if (one[0] == (byte)'\n') return Encoding.ASCII.GetString(line.ToArray());
            if (line.Length >= 65536) throw new IOException("Command frame is too long.");
            line.WriteByte(one[0]);
        }
        return null;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        _listener.Stop();
        await _accepting.ConfigureAwait(false);
        await Task.WhenAll(_clients.Values).ConfigureAwait(false);
        _stop.Dispose();
    }
}
