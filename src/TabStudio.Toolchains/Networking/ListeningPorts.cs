using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using TabStudio.Toolchains.Processes.Unix;

namespace TabStudio.Toolchains.Networking;

/// <summary>A TCP listener visible in Linux's socket table.</summary>
/// <param name="Port">Its port.</param>
/// <param name="Inode">The socket inode, used to associate it with a run's process tree.</param>
public sealed record ListeningPort(int Port, string Inode);

/// <summary>Finds local TCP listeners without assuming a language, framework or port number.</summary>
public static partial class ListeningPorts
{
    /// <summary>Parses /proc/net/tcp or tcp6, accepting loopback and wildcard LISTEN sockets only.</summary>
    public static IReadOnlyList<ListeningPort> ParseProc(string table)
    {
        ArgumentNullException.ThrowIfNull(table);
        var result = new List<ListeningPort>();
        foreach (var line in table.Split('\n'))
        {
            var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 10 || fields[3] != "0A")
            {
                continue;
            }

            var local = fields[1].Split(':');
            if (local.Length == 2 && IsLocal(local[0])
                && int.TryParse(local[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var port)
                && port is > 0 and <= 65535)
            {
                result.Add(new ListeningPort(port, fields[9]));
            }
        }

        return result;
    }

    /// <summary>Finds localhost HTTP URLs or explicit listening/serving messages in program output.</summary>
    public static IReadOnlyList<int> ParseOutput(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var ports = new HashSet<int>();
        // Bound the input and regex work even when a program prints an enormous line.
        foreach (var pattern in new[] { LocalUrl(), ListenMessage() })
        {
            foreach (Match match in pattern.Matches(text.Length > 8192 ? text[^8192..] : text))
            {
                if (int.TryParse(match.Groups["port"].Value, CultureInfo.InvariantCulture, out var port) && port is > 0 and <= 65535)
                {
                    ports.Add(port);
                }
            }
        }

        return [.. ports.Order()];
    }

    /// <summary>
    /// Reads listeners belonging to the given program or its descendants. PRoot uses the device's network
    /// namespace, so these are the same /proc tables the guest sees. Android may deny access; callers then
    /// use output messages instead. Existing listeners of other programs are never offered.
    /// </summary>
    public static IReadOnlyList<int> ForProcess(int pid)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsAndroid())
        {
            return [];
        }

        try
        {
            var inodes = new HashSet<string>(StringComparer.Ordinal);
            var debuggers = new HashSet<int>();
            foreach (var process in ProcessTree.Descendants(pid).Prepend(pid))
            {
                if (DebuggerPortOf(process) is { } debugger)
                {
                    debuggers.Add(debugger);
                }

                try
                {
                    foreach (var fd in Directory.EnumerateFiles($"/proc/{process}/fd"))
                    {
                        var target = new FileInfo(fd).LinkTarget;
                        if (target is not null && target.StartsWith("socket:[", StringComparison.Ordinal) && target.EndsWith(']'))
                        {
                            inodes.Add(target[8..^1]);
                        }
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // The child exited, or Android hides its descriptors.
                }
            }

            var listeners = new List<ListeningPort>();
            foreach (var name in new[] { "tcp", "tcp6" })
            {
                try
                {
                    listeners.AddRange(ParseProc(File.ReadAllText("/proc/net/" + name)));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Android 10+ usually hides these tables from apps.
                }
            }

            return [.. listeners.Where(p => inodes.Contains(p.Inode) && !debuggers.Contains(p.Port)).Select(p => p.Port).Distinct().Order()];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>
    /// The port of the Node.js inspector a program opens, which is a debugger's WebSocket and never the app: an
    /// <c>--inspect</c>, <c>--inspect-brk</c> or <c>--inspect-wait</c> option on its command line or in
    /// <c>NODE_OPTIONS</c>, with 9229 when it names no port. Nx's <c>serve</c> runs the app this way.
    /// </summary>
    /// <param name="arguments">The program's command line.</param>
    /// <param name="nodeOptions">Its <c>NODE_OPTIONS</c>, if any.</param>
    /// <returns>The port, or <see langword="null"/> when the program opens no inspector.</returns>
    public static int? InspectorPort(IReadOnlyList<string> arguments, string? nodeOptions = null)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Count == 0 || Path.GetFileName(arguments[0]) is not ("node" or "nodejs" or "node.exe"))
        {
            return null;
        }

        var options = arguments.Skip(1)
            .Concat((nodeOptions ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        int? port = null;
        foreach (var option in options)
        {
            if (Inspect().Match(option) is not { Success: true } match)
            {
                continue;
            }

            port = 9229;
            if (match.Groups["port"].Success && int.TryParse(match.Groups["port"].Value, CultureInfo.InvariantCulture, out var given) && given is > 0 and <= 65535)
            {
                port = given;
            }
        }

        return port;
    }

    /// <summary>The inspector port of a running process, from <c>/proc</c>.</summary>
    private static int? DebuggerPortOf(int pid)
    {
        try
        {
            var arguments = File.ReadAllText($"/proc/{pid}/cmdline").Split('\0', StringSplitOptions.RemoveEmptyEntries);
            string? nodeOptions = null;
            try
            {
                nodeOptions = File.ReadAllText($"/proc/{pid}/environ").Split('\0')
                    .FirstOrDefault(v => v.StartsWith("NODE_OPTIONS=", StringComparison.Ordinal))?["NODE_OPTIONS=".Length..];
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Another user's process, or Android hides it.
            }

            return InspectorPort(arguments, nodeOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// The address a local server answers on: <c>http://127.0.0.1:port/</c>, or <c>http://[::1]:port/</c> when only IPv6's
    /// loopback accepts the connection. A dev server bound to "localhost" (Vite, Next.js and others on Node 17 and later)
    /// listens on whichever address localhost resolves to first, which is ::1 on Android's Alpine environment and on
    /// most Linux computers.
    /// </summary>
    /// <param name="port">The port it listens on.</param>
    /// <param name="cancellationToken">Stops the attempt.</param>
    public static async Task<Uri> LoopbackAddressAsync(int port, CancellationToken cancellationToken = default)
    {
        foreach (var (address, host) in new[] { (IPAddress.Loopback, "127.0.0.1"), (IPAddress.IPv6Loopback, "[::1]") })
        {
            if (address.AddressFamily == AddressFamily.InterNetworkV6 && !Socket.OSSupportsIPv6)
            {
                continue;
            }

            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(1));
                using var client = new TcpClient(address.AddressFamily);
                await client.ConnectAsync(address, port, timeout.Token).ConfigureAwait(false);
                return new Uri(string.Create(CultureInfo.InvariantCulture, $"http://{host}:{port}/"));
            }
            catch (Exception ex) when (ex is SocketException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
            {
                // Not listening on this address: try the next one.
            }
        }

        return new Uri(string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}/"));
    }

    private static bool IsLocal(string address) => address.ToUpperInvariant() switch
    {
        "00000000" or "00000000000000000000000000000000" or "00000000000000000000000001000000" => true,
        _ => address.Length == 8 && address.EndsWith("7F", StringComparison.OrdinalIgnoreCase),
    };

    [GeneratedRegex(@"^--inspect(?:-brk|-wait)?(?:=(?:(?:[^:=\s]+|\[[^\]]+\]):)?(?<port>\d{1,5}))?$", RegexOptions.CultureInvariant, 100)]
    private static partial Regex Inspect();

    [GeneratedRegex(@"\bhttp://(?:localhost|127(?:\.\d{1,3}){3}|0\.0\.0\.0|\[::1\]|\[::\]):(?<port>\d{1,5})(?!\d)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 100)]
    private static partial Regex LocalUrl();

    [GeneratedRegex(@"\b(?:listening|serving|server (?:started|running))(?:[^\r\n]{0,100}?)\bport\s*[:=]?\s*(?<port>\d{1,5})(?!\d)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 100)]
    private static partial Regex ListenMessage();
}
