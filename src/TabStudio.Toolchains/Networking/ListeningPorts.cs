using System.Globalization;
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
            foreach (var process in ProcessTree.Descendants(pid).Prepend(pid))
            {
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

            return [.. listeners.Where(p => inodes.Contains(p.Inode)).Select(p => p.Port).Distinct().Order()];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static bool IsLocal(string address) => address.ToUpperInvariant() switch
    {
        "00000000" or "00000000000000000000000000000000" or "00000000000000000000000001000000" => true,
        _ => address.Length == 8 && address.EndsWith("7F", StringComparison.OrdinalIgnoreCase),
    };

    [GeneratedRegex(@"\bhttp://(?:localhost|127(?:\.\d{1,3}){3}|0\.0\.0\.0|\[::1\]|\[::\]):(?<port>\d{1,5})(?!\d)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 100)]
    private static partial Regex LocalUrl();

    [GeneratedRegex(@"\b(?:listening|serving|server (?:started|running))(?:[^\r\n]{0,100}?)\bport\s*[:=]?\s*(?<port>\d{1,5})(?!\d)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 100)]
    private static partial Regex ListenMessage();
}
