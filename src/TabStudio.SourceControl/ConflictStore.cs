using System.Security.Cryptography;
using System.Text;

namespace TabStudio.SourceControl;

/// <summary>What a file looked like locally before a pull wrote conflict markers into it.</summary>
/// <param name="Mine">The local content, or <see langword="null"/> when the file had been deleted locally.</param>
internal sealed record ConflictRecord(byte[]? Mine);

/// <summary>
/// Keeps the local version of each conflicted file in <c>.tabstudio/conflicts</c>, so "keep mine" can put it
/// back exactly, whatever the merge wrote.
/// </summary>
internal static class ConflictStore
{
    public static void Save(string root, string path, byte[]? mine)
    {
        var folder = Folder(root);
        Directory.CreateDirectory(folder);
        Delete(root, path);
        var name = Key(path);
        if (mine is null)
        {
            File.WriteAllBytes(Path.Combine(folder, name + ".deleted"), []);
        }
        else
        {
            File.WriteAllBytes(Path.Combine(folder, name + ".mine"), mine);
        }
    }

    public static ConflictRecord? Load(string root, string path)
    {
        var name = Path.Combine(Folder(root), Key(path));
        if (File.Exists(name + ".mine"))
        {
            return new ConflictRecord(File.ReadAllBytes(name + ".mine"));
        }

        return File.Exists(name + ".deleted") ? new ConflictRecord(null) : null;
    }

    public static void Delete(string root, string path)
    {
        var name = Path.Combine(Folder(root), Key(path));
        File.Delete(name + ".mine");
        File.Delete(name + ".deleted");
    }

    private static string Folder(string root) => Path.Combine(root, SyncMetadata.FolderName, "conflicts");

    private static string Key(string path) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(path)))[..32];
}
