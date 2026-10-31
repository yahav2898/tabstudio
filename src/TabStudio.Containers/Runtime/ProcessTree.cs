using System.Globalization;

namespace TabStudio.Containers.Runtime;

/// <summary>A container's processes, read from <c>/proc</c>: every descendant of its PRoot process.</summary>
internal static class ProcessTree
{
    /// <summary>The descendants of <paramref name="root"/>, nearest first.</summary>
    public static List<int> Descendants(int root)
    {
        var parents = new Dictionary<int, List<int>>();
        try
        {
            foreach (var folder in Directory.EnumerateDirectories("/proc"))
            {
                if (!int.TryParse(Path.GetFileName(folder), NumberStyles.None, CultureInfo.InvariantCulture, out var pid)) continue;
                if (Stat(pid) is not { } stat) continue;
                if (!parents.TryGetValue(stat.Parent, out var children)) parents[stat.Parent] = children = [];
                children.Add(pid);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        var result = new List<int>();
        var queue = new Queue<int>([root]);
        while (queue.TryDequeue(out var pid))
        {
            if (!parents.TryGetValue(pid, out var children)) continue;
            foreach (var child in children)
            {
                result.Add(child);
                queue.Enqueue(child);
            }
        }

        return result;
    }

    /// <summary>Processor time and memory of a PRoot process and its descendants.</summary>
    public static ContainerStats Measure(int root)
    {
        var pids = Descendants(root);
        double ticks = 0;
        long pages = 0;
        foreach (var pid in pids)
        {
            if (Stat(pid) is { } stat) ticks += stat.UserTicks + stat.SystemTicks;
            try
            {
                var statm = File.ReadAllText($"/proc/{pid}/statm").Split(' ');
                if (statm.Length > 1 && long.TryParse(statm[1], NumberStyles.None, CultureInfo.InvariantCulture, out var resident)) pages += resident;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        return new(ticks / 100.0, pages * Environment.SystemPageSize, pids.Count);
    }

    private readonly record struct ProcStat(int Parent, long UserTicks, long SystemTicks);

    private static ProcStat? Stat(int pid)
    {
        try
        {
            var text = File.ReadAllText($"/proc/{pid}/stat");
            var close = text.LastIndexOf(')');
            if (close < 0) return null;
            var fields = text[(close + 2)..].Split(' ');
            return fields.Length > 13
                ? new(int.Parse(fields[1], CultureInfo.InvariantCulture), long.Parse(fields[11], CultureInfo.InvariantCulture), long.Parse(fields[12], CultureInfo.InvariantCulture))
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        {
            return null;
        }
    }
}
