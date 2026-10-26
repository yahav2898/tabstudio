using System.Security.Cryptography;
using System.Text;
using TabStudio.Services.Compose;
using TabStudio.Toolchains.Processes;

namespace TabStudio.Services.Native;

/// <summary>Persistent workspace-scoped service data and named volumes, under private app storage.</summary>
public sealed class ServiceStorage
{
    private static readonly string[] SystemDirectories = ["/proc", "/dev", "/sys", "/bin", "/sbin", "/lib", "/usr/bin", "/usr/sbin", "/usr/lib"];

    public ServiceStorage(string appStorage, string workspaceDirectory)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(workspaceDirectory))))[..20];
        Root = Path.Combine(appStorage, "Services", hash);
        Directory.CreateDirectory(Root);
    }
    public string Root { get; }
    public string ServiceDirectory(string name) => Path.Combine(Root, "services", Safe(name));

    public IReadOnlyList<ProcessMount> Prepare(ComposeService service)
    {
        var directory = ServiceDirectory(service.Name);
        Directory.CreateDirectory(directory);
        Private(directory);
        var mounts = new List<ProcessMount> { new(directory, NativeServicePlan.GuestDirectory) };
        foreach (var volume in service.Volumes)
        {
            if (volume.Target is "/" or "/usr" or "/etc" or "/etc/passwd" or "/etc/shadow" or "/tabstudio-service" || SystemDirectories.Any(p => volume.Target == p || volume.Target.StartsWith(p + "/", StringComparison.Ordinal)))
                throw new ComposeException("A service volume cannot replace runtime or system directories. Use a remote host.");
            var storage = volume.IsBind
                ? Path.Combine(directory, "binds", Hash(volume.Target))
                : Path.Combine(Root, "volumes", volume.Source is { Length: > 0 } source ? Safe(source) : Safe(service.Name) + "-" + Hash(volume.Target));
            if (volume.IsBind && volume.Source is { } seed)
            {
                if (!File.Exists(seed) && !Directory.Exists(seed)) throw new ComposeException($"Bind source does not exist: {seed}");
                // Bind sources are seeds, never write destinations. Read-only configuration refreshes on start.
                if (volume.ReadOnly || (!File.Exists(storage) && !Directory.Exists(storage))) CopySeed(seed, storage);
            }
            else
            {
                Directory.CreateDirectory(storage);
            }

            mounts.Add(new ProcessMount(storage, volume.Target));
        }
        return mounts;
    }

    private static void CopySeed(string source, string destination)
    {
        if (File.GetAttributes(source).HasFlag(FileAttributes.ReparsePoint)) throw new ComposeException("Symlinks in bind seeds require a remote host.");
        if (File.Exists(source))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, overwrite: true);
        }
        else
        {
            Directory.CreateDirectory(destination);
            foreach (var path in Directory.EnumerateFileSystemEntries(source)) CopySeed(path, Path.Combine(destination, Path.GetFileName(path)));
        }
    }
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16];
    private static string Safe(string name) => name.Length > 0 && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.') && name is not ("." or "..") ? name : throw new ComposeException("Invalid service or volume name.");
    private static void Private(string path)
    {
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
}
