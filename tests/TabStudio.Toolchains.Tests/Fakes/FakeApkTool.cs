using System.Formats.Tar;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using TabStudio.Toolchains.Alpine;

namespace TabStudio.Toolchains.Tests.Fakes;

/// <summary>
/// Plays <c>apk</c> on a root file system for tests: installs the packages in the local repositories into it,
/// writing <c>lib/apk/db/installed</c> and <c>etc/apk/world</c> as apk does, and removes packages with the
/// dependencies nothing else needs.
/// </summary>
internal sealed class FakeApkTool(string rootfs) : IApkTool
{
    private readonly Dictionary<string, List<string>> _files = new(StringComparer.Ordinal);

    /// <summary>Every call, in order: <c>add main,community python3</c>.</summary>
    public List<string> Calls { get; } = [];

    /// <summary>Makes the next install fail with this output.</summary>
    public string? FailNextAdd { get; set; }

    /// <summary>Makes the next install throw this, as storage filling up while apk writes does.</summary>
    public Exception? ThrowNextAdd { get; set; }

    public Task<ApkResult> AddAsync(IReadOnlyList<string> packages, IReadOnlyList<string> repositories, bool upgrade, Action<string> line, CancellationToken cancellationToken)
    {
        Calls.Add($"add{(upgrade ? " --upgrade" : string.Empty)} {string.Join(',', repositories.Select(Path.GetFileName))} {string.Join(' ', packages)}");
        if (ThrowNextAdd is { } exception)
        {
            ThrowNextAdd = null;
            throw exception;
        }

        if (FailNextAdd is { } failure)
        {
            FailNextAdd = null;
            line(failure);
            return Task.FromResult(new ApkResult(1, failure + "\n"));
        }

        var files = repositories
            .SelectMany(r => Directory.Exists(Host(r)) ? Directory.EnumerateFiles(Host(r), "*.apk", SearchOption.AllDirectories) : [])
            .ToList();
        var output = new StringBuilder();
        for (var i = 0; i < files.Count; i++)
        {
            var (name, version) = Install(files[i]);
            var text = string.Create(CultureInfo.InvariantCulture, $"({i + 1}/{files.Count}) Installing {name} ({version})");
            output.AppendLine(text);
            line(text);
        }

        var world = ReadWorld();
        world.UnionWith(packages);
        WriteWorld(world);
        foreach (var package in packages)
        {
            if (ApkInstalledDatabase.Read(rootfs).Find(package) is null)
            {
                var error = $"ERROR: unable to select packages: {package} (no such package)";
                line(error);
                return Task.FromResult(new ApkResult(1, output + error + "\n"));
            }
        }

        line("OK");
        return Task.FromResult(new ApkResult(0, output + "OK\n"));
    }

    public Task<ApkResult> AddOnlineAsync(IReadOnlyList<string> packages, bool upgrade, Action<string> line, CancellationToken cancellationToken)
    {
        Calls.Add($"add-online {string.Join(' ', packages)}");
        line("ERROR: network unavailable");
        return Task.FromResult(new ApkResult(1, "ERROR: network unavailable\n"));
    }

    public Task<ApkResult> DeleteAsync(IReadOnlyList<string> packages, Action<string> line, CancellationToken cancellationToken)
    {
        Calls.Add($"del {string.Join(' ', packages)}");
        var world = ReadWorld();
        world.ExceptWith(packages);
        WriteWorld(world);

        // Keep what the world needs; purge the rest, as apk does.
        var database = ApkInstalledDatabase.Read(rootfs);
        var needed = ApkResolver.Closure(database.Packages, world).Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        var kept = new List<string>();
        var output = new StringBuilder();
        foreach (var package in database.Packages)
        {
            if (needed.Contains(package.Name) || !_files.TryGetValue(package.Name, out var files))
            {
                kept.Add(Entry(package));
                continue;
            }

            foreach (var file in files)
            {
                File.Delete(Path.Combine(rootfs, file));
            }

            var text = $"Purging {package.Name} ({package.Version})";
            output.AppendLine(text);
            line(text);
        }

        File.WriteAllText(Path.Combine(rootfs, ApkInstalledDatabase.DatabasePath), string.Concat(kept));
        return Task.FromResult(new ApkResult(0, output.ToString()));
    }

    private string Host(string guestPath) => Path.Combine(rootfs, guestPath.TrimStart('/'));

    /// <summary>Extracts a package's files and adds it to the database.</summary>
    private (string Name, string Version) Install(string file)
    {
        var info = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var written = new List<string>();
        using (var gzip = new GZipStream(File.OpenRead(file), CompressionMode.Decompress))
        using (var reader = new TarReader(gzip))
        {
            while (reader.GetNextEntry() is { } entry)
            {
                if (entry.Name == ".PKGINFO")
                {
                    using var text = new StreamReader(entry.DataStream!);
                    while (text.ReadLine() is { } line && line.Split(" = ", 2) is [var key, var value])
                    {
                        (info.TryGetValue(key, out var values) ? values : info[key] = []).Add(value);
                    }
                }
                else if (!entry.Name.StartsWith('.') && entry.EntryType is TarEntryType.RegularFile or TarEntryType.V7RegularFile)
                {
                    var path = Path.Combine(rootfs, entry.Name);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    entry.ExtractToFile(path, overwrite: true);
                    written.Add(entry.Name);
                }
            }
        }

        var name = info["pkgname"][0];
        var version = info["pkgver"][0];
        _files[name] = written;
        var package = new ApkPackage
        {
            Name = name,
            Version = version,
            Architecture = "x86_64",
            Size = new FileInfo(file).Length,
            InstalledSize = long.Parse(info["size"][0], CultureInfo.InvariantCulture),
            Depends = [.. info.GetValueOrDefault("depend", []).Select(ApkDependency.Parse)],
        };

        var database = Path.Combine(rootfs, ApkInstalledDatabase.DatabasePath);
        var others = ApkInstalledDatabase.Read(rootfs).Packages.Where(p => p.Name != name).Select(Entry);
        File.WriteAllText(database, string.Concat(others) + Entry(package));
        return (name, version);
    }

    private static string Entry(ApkPackage package) =>
        string.Create(CultureInfo.InvariantCulture, $"C:Q1fake=\nP:{package.Name}\nV:{package.Version}\nA:x86_64\nS:{package.Size}\nI:{package.InstalledSize}\n")
        + (package.Depends.Count > 0 ? $"D:{string.Join(' ', package.Depends)}\n" : string.Empty)
        + "\n";

    private HashSet<string> ReadWorld()
    {
        var path = Path.Combine(rootfs, ApkInstalledDatabase.WorldPath);
        return File.Exists(path) ? [.. File.ReadAllLines(path).Where(l => l.Length > 0)] : [];
    }

    private void WriteWorld(HashSet<string> world) =>
        File.WriteAllLines(Path.Combine(rootfs, ApkInstalledDatabase.WorldPath), world.Order(StringComparer.Ordinal));
}
