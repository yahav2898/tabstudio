using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using TabStudio.Testing;
using TabStudio.Toolchains.Linux;

namespace TabStudio.Toolchains.Tests.Linux;

/// <summary>Unpacking root file systems and tool archives safely.</summary>
public sealed class ArchiveExtractorTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public async Task Unpacks_files_folders_links_and_modes()
    {
        var archive = WriteTarGz("rootfs.tar.gz", writer =>
        {
            writer.WriteEntry(new PaxTarEntry(TarEntryType.Directory, "./bin/") { Mode = (UnixFileMode)0b111_101_101 });
            writer.WriteEntry(File("./bin/busybox", "#!/bin/sh\n", 0b111_101_101));
            writer.WriteEntry(new PaxTarEntry(TarEntryType.SymbolicLink, "./bin/sh") { LinkName = "/bin/busybox" });
            writer.WriteEntry(File("./etc/alpine-release", "3.24.3\n", 0b110_100_100));
            writer.WriteEntry(new PaxTarEntry(TarEntryType.HardLink, "./bin/ash") { LinkName = "./bin/busybox" });
        });
        var root = _temp.Resolve("rootfs");

        var result = await ArchiveExtractor.ExtractAsync(archive, root);

        Assert.Equal(new ExtractionResult(Files: 2, Folders: 1, Links: 1, HardLinks: 1), result);
        Assert.Equal("3.24.3\n", await System.IO.File.ReadAllTextAsync(Path.Combine(root, "etc/alpine-release")));
        Assert.Equal("/bin/busybox", new FileInfo(Path.Combine(root, "bin/sh")).LinkTarget);
        Assert.Equal("#!/bin/sh\n", await System.IO.File.ReadAllTextAsync(Path.Combine(root, "bin/ash")));
        Assert.True((System.IO.File.GetUnixFileMode(Path.Combine(root, "bin/busybox")) & UnixFileMode.UserExecute) != 0);
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("./a/../../outside.txt")]
    public async Task Refuses_entries_that_leave_the_folder(string name)
    {
        var archive = WriteTarGz("evil.tar.gz", writer => writer.WriteEntry(File(name, "pwned", 0b110_100_100)));

        await Assert.ThrowsAsync<InvalidDataException>(() => ArchiveExtractor.ExtractAsync(archive, _temp.Resolve("root")));

        Assert.False(System.IO.File.Exists(_temp.Resolve("outside.txt")));
    }

    [Fact]
    public async Task Refuses_to_write_through_a_link_to_another_folder()
    {
        var outside = Directory.CreateDirectory(_temp.Resolve("outside")).FullName;
        var archive = WriteTarGz("evil.tar.gz", writer =>
        {
            writer.WriteEntry(new PaxTarEntry(TarEntryType.SymbolicLink, "escape") { LinkName = outside });
            writer.WriteEntry(File("escape/file.txt", "pwned", 0b110_100_100));
        });

        await Assert.ThrowsAsync<InvalidDataException>(() => ArchiveExtractor.ExtractAsync(archive, _temp.Resolve("root")));

        Assert.Empty(Directory.EnumerateFileSystemEntries(outside));
    }

    [Fact]
    public async Task Unpacks_a_wheel_as_a_zip()
    {
        var wheel = _temp.Resolve("debugpy-1.0-py2.py3-none-any.whl");
        using (var zip = ZipFile.Open(wheel, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("debugpy/__init__.py");
            await using (var stream = entry.Open())
            {
                await stream.WriteAsync("__version__ = '1.0'\n"u8.ToArray());
            }

            zip.CreateEntry("debugpy-1.0.dist-info/");
        }

        var result = await ArchiveExtractor.ExtractAsync(wheel, _temp.Resolve("site"));

        Assert.Equal(1, result.Files);
        Assert.Equal("__version__ = '1.0'\n", await System.IO.File.ReadAllTextAsync(_temp.Resolve("site/debugpy/__init__.py")));
        Assert.True(Directory.Exists(_temp.Resolve("site/debugpy-1.0.dist-info")));
    }

    [Fact]
    public async Task Reports_a_damaged_archive()
    {
        var archive = _temp.Write("damaged.tar.gz", "this is not gzip");

        await Assert.ThrowsAsync<InvalidDataException>(() => ArchiveExtractor.ExtractAsync(archive, _temp.Resolve("root")));
    }

    public void Dispose() => _temp.Dispose();

    private static PaxTarEntry File(string name, string content, int mode) =>
        new(TarEntryType.RegularFile, name) { DataStream = new MemoryStream(Encoding.UTF8.GetBytes(content)), Mode = (UnixFileMode)mode };

    private string WriteTarGz(string name, Action<TarWriter> write)
    {
        var path = _temp.Resolve(name);
        using var file = System.IO.File.Create(path);
        using var gzip = new GZipStream(file, CompressionLevel.Fastest);
        using var writer = new TarWriter(gzip, TarEntryFormat.Pax);
        write(writer);
        return path;
    }
}
