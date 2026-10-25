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
/// real ones. Its catalog has one toolchain, Hello, whose package needs a library and whose setup unpacks an
/// archive.
/// </summary>
internal sealed class FakeDevice : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly HttpClient _http;

    public FakeDevice(long freeSpace = 50_000_000_000)
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
        Catalog = Mirror.Catalog(Hello, Shared);

        _http = Mirror.Server.CreateClient();
        Downloader = new Downloader(_http) { RetryDelay = TimeSpan.FromMilliseconds(5) };
        Environment = new AlpineEnvironment(Path.Combine(_temp.Path, "Toolchains", "linux"), Catalog.Linux, Platform, Downloader, [Path.Combine(_temp.Path, "Projects")]);
        Apk = new FakeApkTool(Environment.Rootfs);
        Packages = new AlpinePackageManager(Environment, _http, Downloader, Apk);
        Installer = new LinuxToolchainInstaller(Environment, Packages, Downloader);
        Manager = new ToolchainManager(Catalog, Installer);
    }

    public static Uri ArchiveAddress { get; } = new("https://downloads.example.org/hello-debugger-1.0.tar.gz");

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

    /// <summary>A path inside the environment's root file system.</summary>
    public string Rootfs(string guestPath) => Path.Combine(Environment.Rootfs, guestPath.TrimStart('/'));

    public void Dispose()
    {
        Manager.Dispose();
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
