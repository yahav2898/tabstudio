using TabStudio.WindowsBridge.Windows.Native;

namespace TabStudio.WindowsBridge.Windows.Desktop;

/// <summary>The processes a process started, from Windows' list of running processes and their parents.</summary>
internal static class ProcessTree
{
    /// <summary>A process and its descendants that still run.</summary>
    /// <param name="root">The process.</param>
    public static IReadOnlyCollection<int> Of(int root)
    {
        var children = new Dictionary<int, List<int>>();
        var running = new HashSet<int>();
        var snapshot = NativeMethods.CreateToolhelp32Snapshot(NativeMethods.Th32csSnapProcess, 0);
        if (snapshot is 0 or -1)
        {
            return [root];
        }

        try
        {
            var entry = new NativeMethods.ProcessEntry32 { Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.ProcessEntry32>() };
            if (NativeMethods.Process32First(snapshot, ref entry))
            {
                do
                {
                    var id = (int)entry.ProcessId;
                    var parent = (int)entry.ParentProcessId;
                    running.Add(id);
                    if (id != parent)
                    {
                        if (!children.TryGetValue(parent, out var list))
                        {
                            list = [];
                            children[parent] = list;
                        }

                        list.Add(id);
                    }
                }
                while (NativeMethods.Process32Next(snapshot, ref entry));
            }
        }
        finally
        {
            _ = NativeMethods.CloseHandle(snapshot);
        }

        if (!running.Contains(root))
        {
            return [];
        }

        var tree = new HashSet<int> { root };
        var pending = new Queue<int>([root]);
        while (pending.TryDequeue(out var current))
        {
            foreach (var child in children.GetValueOrDefault(current) ?? [])
            {
                if (tree.Add(child))
                {
                    pending.Enqueue(child);
                }
            }
        }

        return tree;
    }
}
