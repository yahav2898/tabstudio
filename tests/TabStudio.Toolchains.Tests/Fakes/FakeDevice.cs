using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using TabStudio.Testing;
using TabStudio.Toolchains.Alpine;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Downloads;
using TabStudio.Toolchains.Installation;
using TabStudio.Toolchains.Linux;

namespace TabStudio.Toolchains.Tests.Fakes;

/// <summary>
/// A tablet for installer tests: its storage in a temporary folder, a native library folder with stand-ins for
/// PRoot, a fake Alpine mirror and <see cref="FakeApkTool"/> playing apk, wired together as the app wires the
/// real ones. Its catalog has two toolchains: Hello, whose package needs a library and whose setup unpacks an
/// archive, and Shared, which needs one of Hello's packages; or, for the IDE's tests, the real catalog's Python
/// and Node.js over fake packages of the same names.
/// </summary>
internal sealed class FakeDevice : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly HttpClient _http;

    public FakeDevice(long freeSpace = 50_000_000_000)
        : this(freeSpace, pythonAndNode: false)
    {
    }

    private FakeDevice(long freeSpace, bool pythonAndNode, string[]? catalogIds = null)
    {
        FreeSpace = freeSpace;
        var native = Directory.CreateDirectory(Path.Combine(_temp.Path, "lib")).FullName;
        File.WriteAllText(Path.Combine(native, "libproot.so"), string.Empty);
        File.WriteAllText(Path.Combine(native, "libproot-loader.so"), string.Empty);
        Platform = new ToolchainPlatform
        {
            NativeLibraryDirectory = native,
            Architecture = FakeAlpineRepository.Architecture,
            AndroidApiLevel = 35,
            DnsServers = () => ["192.0.2.53"],
            FreeSpace = _ => FreeSpace,
        };

        Mirror.Add("main", "libhello", "1.0-r0", new Dictionary<string, string> { ["usr/lib/libhello.so.1"] = "library" });
        Mirror.Add("main", "hello", "2.0-r1", new Dictionary<string, string> { ["usr/bin/hello"] = "#!/bin/sh\necho hello\n" }, depends: ["libhello"]);
        Mirror.Add("community", "hello-tools", "2.0-r1", new Dictionary<string, string> { ["usr/bin/hello-lint"] = "lint" }, depends: ["hello"]);
        Archive = BuildArchive(("hello-debugger/adapter.js", "console.log('adapter');\n"));
        Mirror.Server.Add(ArchiveAddress, Archive);

        Hello = new ToolchainDefinition
        {
            Id = "hello",
            Name = "Hello",
            Description = "The Hello toolchain.",
            Listed = true,
            Packages = ["hello", "hello-tools"],
            VersionPackage = "hello",
            EstimatedDownloadSize = 1000,
            EstimatedInstalledSize = 5000,
            FileExtensions = [".hello"],
            Setup =
            [
                new SetupStepDefinition
                {
                    Name = "Hello debugger",
                    Archive = new ArchiveDefinition { Url = ArchiveAddress, Sha256 = Convert.ToHexStringLower(SHA256.HashData(Archive)), Size = Archive.Length, Destination = "/opt/tabstudio/hello" },
                },
            ],
        };
        Shared = Hello with { Id = "shared", Name = "Shared", Packages = ["hello"], Setup = [] };
        Catalog = catalogIds is { Length: > 0 } ? Mirror.Catalog(FromCatalog(catalogIds))
            : pythonAndNode ? Mirror.Catalog(PythonAndNode())
            : Mirror.Catalog(Hello, Shared);

        _http = Mirror.Server.CreateClient();
        Downloader = new Downloader(_http) { RetryDelay = TimeSpan.FromMilliseconds(5) };
        Environment = new AlpineEnvironment(Path.Combine(_temp.Path, "Toolchains", "linux"), Catalog.Linux, Platform, Downloader, [Path.Combine(_temp.Path, "Projects")]);
        Apk = new FakeApkTool(Environment.Rootfs);
        Packages = new AlpinePackageManager(Environment, _http, Downloader, Apk);
        Installer = new LinuxToolchainInstaller(Environment, Packages, Downloader);
        Manager = new ToolchainManager(Catalog, Installer);
    }

    public static Uri ArchiveAddress { get; } = new("https://downloads.example.org/hello-debugger-1.0.tar.gz");

    /// <summary>A device whose Store offers the real catalog's Python and Node.js, installed from fake packages.</summary>
    public static FakeDevice WithPythonAndNode() => new(50_000_000_000, pythonAndNode: true);

    /// <summary>A device whose Store offers these toolchains of the real catalog, over empty packages of the same names on the fake mirror.</summary>
    /// <param name="ids">The toolchains' ids, such as "android", in the Store's order.</param>
    public static FakeDevice WithCatalogToolchains(params string[] ids) => new(50_000_000_000, pythonAndNode: false, ids);

    public long FreeSpace { get; set; }

    public FakeAlpineRepository Mirror { get; } = new();

    public byte[] Archive { get; }

    public ToolchainPlatform Platform { get; }

    public ToolchainDefinition Hello { get; }

    public ToolchainDefinition Shared { get; }

    public ToolchainCatalog Catalog { get; }

    public Downloader Downloader { get; }

    public AlpineEnvironment Environment { get; }

    public FakeApkTool Apk { get; }

    public AlpinePackageManager Packages { get; }

    public LinuxToolchainInstaller Installer { get; }

    public ToolchainManager Manager { get; }

    /// <summary>
    /// The real Python and Node.js, over packages of the same names on the fake mirror, sized like the real ones;
    /// without their setup steps, which download from the internet and run programs.
    /// </summary>
    private ToolchainDefinition[] PythonAndNode()
    {
        Mirror.Add("main", "python3", "3.14.8-r0", new Dictionary<string, string> { ["usr/bin/python3"] = "#!/bin/sh\n" }, installedSize: 52_400_000);
        Mirror.Add("community", "py3-pip", "25.3-r0", depends: ["python3"], installedSize: 14_200_000);
        Mirror.Add("main", "nodejs", "24.11.1-r0", new Dictionary<string, string> { ["usr/bin/node"] = "#!/bin/sh\n" }, installedSize: 68_000_000);
        Mirror.Add("community", "npm", "11.6.2-r0", depends: ["nodejs"], installedSize: 11_300_000);
        var catalog = ToolchainCatalog.Default;
        return [catalog.Find("python")! with { Setup = [] }, catalog.Find("node")! with { Setup = [] }];
    }

    /// <summary>
    /// Toolchains of the real catalog, without their setup steps, which download from the internet and run programs; each
    /// package they name is on the fake mirror, empty.
    /// </summary>
    private ToolchainDefinition[] FromCatalog(string[] ids)
    {
        var catalog = ToolchainCatalog.Default;
        var toolchains = ids.Select(id => catalog.Find(id) ?? throw new ArgumentException($"The catalog has no toolchain {id}.", nameof(ids))).ToArray();
        foreach (var package in toolchains.SelectMany(t => t.Packages).Distinct(StringComparer.Ordinal))
        {
            Mirror.Add("main", package, "1.0-r0");
        }

        return [.. toolchains.Select(t => t with { Setup = [] })];
    }

    /// <summary>A path inside the environment's root file system.</summary>
    public string Rootfs(string guestPath) => Path.Combine(Environment.Rootfs, guestPath.TrimStart('/'));

    public void Dispose()
    {
        Manager.Dispose();
        Packages.Dispose();
        _http.Dispose();
        Mirror.Dispose();
        _temp.Dispose();
    }

    private static byte[] BuildArchive(params (string Name, string Content)[] files)
    {
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
        using (var writer = new TarWriter(gzip, TarEntryFormat.Pax))
        {
            foreach (var (name, content) in files)
            {
                writer.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, name) { DataStream = new MemoryStream(Encoding.UTF8.GetBytes(content)), Mode = (UnixFileMode)0b110_100_100 });
            }
        }

        return buffer.ToArray();
    }
}
