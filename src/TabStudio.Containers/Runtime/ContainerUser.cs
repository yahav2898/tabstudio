using System.Globalization;
using TabStudio.Containers.Store;

namespace TabStudio.Containers.Runtime;

/// <summary>
/// The identity a container's process runs as. Root runs as PRoot's simulated root (<c>-0</c>); any other user as a
/// simulated identity (<c>-i uid:gid</c>), so files look owned by it and programs that refuse root, such as
/// PostgreSQL, start.
/// </summary>
/// <param name="Uid">The user number; 0 for root.</param>
/// <param name="Gid">The group number.</param>
/// <param name="Name">The user's name, when <c>/etc/passwd</c> has one.</param>
/// <param name="Home">The home folder.</param>
public sealed record ContainerUser(int Uid, int Gid, string Name, string Home)
{
    /// <summary>Root.</summary>
    public static ContainerUser Root { get; } = new(0, 0, "root", "/root");

    /// <summary>Resolves <c>name</c>, <c>uid</c>, <c>name:group</c> or <c>uid:gid</c> in a root file system.</summary>
    /// <exception cref="ContainerException">The name is not in the container's <c>/etc/passwd</c>.</exception>
    public static ContainerUser Resolve(string rootfs, string? user)
    {
        if (string.IsNullOrWhiteSpace(user))
        {
            return Root;
        }

        var parts = user.Split(':', 2);
        var passwd = Read(rootfs, "/etc/passwd");
        string[]? entry;
        if (int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var uid))
        {
            entry = passwd.FirstOrDefault(p => p.Length > 2 && p[2] == parts[0]);
        }
        else
        {
            entry = passwd.FirstOrDefault(p => p.Length > 0 && p[0] == parts[0])
                ?? throw new ContainerException($"The user '{parts[0]}' is not in the container's /etc/passwd.");
            uid = int.Parse(entry[2], CultureInfo.InvariantCulture);
        }

        var gid = entry is { Length: > 3 } && int.TryParse(entry[3], NumberStyles.None, CultureInfo.InvariantCulture, out var primary) ? primary : uid;
        if (parts.Length == 2)
        {
            if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out gid))
            {
                var group = Read(rootfs, "/etc/group").FirstOrDefault(g => g.Length > 2 && g[0] == parts[1])
                    ?? throw new ContainerException($"The group '{parts[1]}' is not in the container's /etc/group.");
                gid = int.Parse(group[2], CultureInfo.InvariantCulture);
            }
        }

        var name = entry is { Length: > 0 } ? entry[0] : parts[0];
        var home = entry is { Length: > 5 } && entry[5].Length > 0 ? entry[5] : uid == 0 ? "/root" : "/";
        return new(uid, gid, name, home);
    }

    private static List<string[]> Read(string rootfs, string path)
    {
        try
        {
            var file = SafePath.ResolveFollowing(rootfs, path);
            return File.Exists(file) ? [.. File.ReadLines(file).Where(l => l.Length > 0 && l[0] != '#').Select(l => l.Split(':'))] : [];
        }
        catch (IOException)
        {
            return [];
        }
    }
}
