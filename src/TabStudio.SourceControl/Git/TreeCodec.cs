using System.Text;

namespace TabStudio.SourceControl.Git;

/// <summary>One entry of a Git tree object.</summary>
/// <param name="Mode">Its mode as Git writes it in trees: <c>40000</c>, <c>100644</c>, <c>100755</c>, <c>120000</c> or <c>160000</c>.</param>
/// <param name="Name">Its name in the folder.</param>
/// <param name="Id">The object it names.</param>
internal sealed record TreeItem(string Mode, string Name, string Id)
{
    /// <summary>Whether the entry is a folder.</summary>
    public bool IsTree => Mode == TreeCodec.TreeMode;

    /// <summary>Whether the entry is a submodule's commit.</summary>
    public bool IsSubmodule => Mode == TreeCodec.SubmoduleMode;

    /// <summary>Whether the entry is a symbolic link.</summary>
    public bool IsSymbolicLink => Mode == TreeCodec.SymbolicLinkMode;
}

/// <summary>Reads and writes Git tree objects, and flattens a commit's trees into its files.</summary>
internal static class TreeCodec
{
    /// <summary>The mode of a folder.</summary>
    public const string TreeMode = "40000";

    /// <summary>The mode of a submodule.</summary>
    public const string SubmoduleMode = "160000";

    /// <summary>The mode of a symbolic link.</summary>
    public const string SymbolicLinkMode = "120000";

    /// <summary>Reads a tree object's entries.</summary>
    public static List<TreeItem> Parse(ReadOnlySpan<byte> content)
    {
        var items = new List<TreeItem>();
        while (content.Length > 0)
        {
            var space = content.IndexOf((byte)' ');
            var nul = content.IndexOf((byte)0);
            if (space <= 0 || nul <= space || content.Length < nul + 1 + GitHash.Size)
            {
                throw new InvalidDataException("A tree object is damaged.");
            }

            var mode = Encoding.ASCII.GetString(content[..space]);
            var name = Encoding.UTF8.GetString(content[(space + 1)..nul]);
            var id = GitHash.ToHex(content.Slice(nul + 1, GitHash.Size));
            items.Add(new TreeItem(NormalizeMode(mode), name, id));
            content = content[(nul + 1 + GitHash.Size)..];
        }

        return items;
    }

    /// <summary>Writes a tree object, sorting its entries as Git does (a folder sorts as if its name ended in '/').</summary>
    public static byte[] Write(IEnumerable<TreeItem> items)
    {
        var sorted = items.ToList();
        sorted.Sort(static (a, b) => CompareNames(a, b));
        using var output = new MemoryStream();
        foreach (var item in sorted)
        {
            output.Write(Encoding.ASCII.GetBytes(item.Mode + " "));
            output.Write(Encoding.UTF8.GetBytes(item.Name));
            output.WriteByte(0);
            output.Write(GitHash.FromHex(item.Id));
        }

        return output.ToArray();
    }

    /// <summary>
    /// Every entry of a tree and its subtrees that is not a folder, by its path with <c>/</c> separators: files,
    /// symbolic links and submodules.
    /// </summary>
    /// <param name="store">The database holding the trees.</param>
    /// <param name="treeId">The root tree.</param>
    public static Dictionary<string, TreeItem> Flatten(ObjectStore store, string treeId)
    {
        var result = new Dictionary<string, TreeItem>(StringComparer.Ordinal);
        var pending = new Stack<(string Prefix, string Id)>();
        pending.Push((string.Empty, treeId));
        while (pending.Count > 0)
        {
            var (prefix, id) = pending.Pop();
            var tree = store.Read(id);
            if (tree.Kind != GitObjectKind.Tree)
            {
                throw new InvalidDataException($"The object {id} is not a tree.");
            }

            foreach (var item in Parse(tree.Content))
            {
                var path = prefix + item.Name;
                if (item.IsTree)
                {
                    pending.Push((path + "/", item.Id));
                }
                else
                {
                    result[path] = item with { Name = path };
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Writes the trees for a set of paths into the database and returns the root tree's name, as
    /// <c>git write-tree</c> does from the index.
    /// </summary>
    /// <param name="store">Where the trees go.</param>
    /// <param name="entries">Every file, symbolic link and submodule, by path with <c>/</c> separators.</param>
    public static string WriteTrees(ObjectStore store, IReadOnlyDictionary<string, (string Mode, string Id)> entries)
    {
        var root = new Folder();
        foreach (var (path, entry) in entries)
        {
            var parts = path.Split('/');
            var folder = root;
            for (var i = 0; i < parts.Length - 1; i++)
            {
                if (!folder.Folders.TryGetValue(parts[i], out var child))
                {
                    child = new Folder();
                    folder.Folders[parts[i]] = child;
                }

                folder = child;
            }

            folder.Files[parts[^1]] = entry;
        }

        return Store(store, root);
    }

    /// <summary>The mode Git uses in trees, from the forms other tools write (<c>040000</c>, <c>100664</c>).</summary>
    public static string NormalizeMode(string mode) => mode switch
    {
        "040000" => TreeMode,
        "100664" => GitObjects.RegularFileMode,
        _ => mode,
    };

    private static string Store(ObjectStore store, Folder folder)
    {
        var items = new List<TreeItem>();
        foreach (var (name, child) in folder.Folders)
        {
            items.Add(new TreeItem(TreeMode, name, Store(store, child)));
        }

        foreach (var (name, entry) in folder.Files)
        {
            items.Add(new TreeItem(entry.Mode, name, entry.Id));
        }

        return store.Write(GitObjectKind.Tree, Write(items));
    }

    private static int CompareNames(TreeItem a, TreeItem b)
    {
        var left = Encoding.UTF8.GetBytes(a.IsTree ? a.Name + "/" : a.Name);
        var right = Encoding.UTF8.GetBytes(b.IsTree ? b.Name + "/" : b.Name);
        return left.AsSpan().SequenceCompareTo(right);
    }

    private sealed class Folder
    {
        public Dictionary<string, Folder> Folders { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, (string Mode, string Id)> Files { get; } = new(StringComparer.Ordinal);
    }
}
