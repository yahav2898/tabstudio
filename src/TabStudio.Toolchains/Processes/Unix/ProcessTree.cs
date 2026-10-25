using System.Globalization;

namespace TabStudio.Toolchains.Processes.Unix;

/// <summary>
/// Finds and kills a process's descendants on Linux and Android, through <c>/proc</c>.
/// </summary>
/// <remarks>
/// <para>
/// A program's children do not always stay in its process group: a shell with job control gives every job a group
/// of its own, and PRoot's guests are its children whatever group they are in. So stopping a run kills its whole
/// tree of descendants, found by their parent ids, and repeats until no new process appeared in between.
/// </para>
/// <para>
/// On Android this also keeps TabStudio within the system's rules: every process stays a descendant of the app's
/// own process, and none is left running once its run is over.
/// </para>
/// </remarks>
internal static class ProcessTree
{
    private const int MaximumRounds = 10;

    /// <summary>Kills a process and all of its descendants.</summary>
    /// <param name="pid">The process.</param>
    /// <param name="ownGroup">Whether the process leads a process group of its own, which is killed as a whole too.</param>
    public static void Kill(int pid, bool ownGroup)
    {
        UnixNative.EnsureLoaded();
        for (var round = 0; round < MaximumRounds; round++)
        {
            var descendants = Descendants(pid);
            if (ownGroup)
            {
                _ = UnixNative.kill(-pid, UnixConstants.SignalKill);
            }

            foreach (var descendant in descendants)
            {
                _ = UnixNative.kill(descendant, UnixConstants.SignalKill);
            }

            if (descendants.Count == 0)
            {
                break;
            }
        }

        _ = UnixNative.kill(pid, UnixConstants.SignalKill);
    }

    /// <summary>The descendants of a process: its children, their children and so on, nearest first.</summary>
    /// <param name="root">The process.</param>
    public static List<int> Descendants(int root)
    {
        var parents = ReadParents();
        var result = new List<int>();
        var queue = new Queue<int>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var parent = queue.Dequeue();
            foreach (var (pid, ppid) in parents)
            {
                if (ppid == parent && pid != root && !result.Contains(pid))
                {
                    result.Add(pid);
                    queue.Enqueue(pid);
                }
            }
        }

        return result;
    }

    /// <summary>Reads every visible process's parent, from <c>/proc/&lt;pid&gt;/stat</c>.</summary>
    private static List<(int Pid, int Parent)> ReadParents()
    {
        var result = new List<(int, int)>();
        if (!Directory.Exists("/proc"))
        {
            return result;
        }

        foreach (var directory in Directory.EnumerateDirectories("/proc"))
        {
            if (!int.TryParse(Path.GetFileName(directory), NumberStyles.None, CultureInfo.InvariantCulture, out var pid))
            {
                continue;
            }

            if (TryReadParent(Path.Combine(directory, "stat")) is { } parent)
            {
                result.Add((pid, parent));
            }
        }

        return result;
    }

    /// <summary>
    /// Reads the parent id from a <c>stat</c> line: <c>pid (comm) state ppid ...</c>, where the command name may
    /// contain spaces and parentheses, so the fields are counted from the last <c>)</c>.
    /// </summary>
    internal static int? TryReadParent(string statFile)
    {
        try
        {
            return ParseParent(File.ReadAllText(statFile));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The process has exited, or belongs to another user.
            return null;
        }
    }

    /// <summary>Parses the parent id out of the text of a <c>/proc/&lt;pid&gt;/stat</c> file.</summary>
    internal static int? ParseParent(string stat)
    {
        var end = stat.LastIndexOf(')');
        if (end < 0 || end + 2 >= stat.Length)
        {
            return null;
        }

        var fields = stat[(end + 2)..].Split(' ', 3);
        return fields.Length >= 2 && int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var parent)
            ? parent
            : null;
    }
}
