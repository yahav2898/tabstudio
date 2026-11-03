using System.Diagnostics;
using System.Formats.Tar;
using System.Text;
using TabStudio.Containers.Runtime;
using TabStudio.Containers.Store;
using TabStudio.Testing;

namespace TabStudio.Containers.Tests;

public sealed class LayerTests
{
    [Fact]
    public async Task Whiteouts_remove_lower_files_and_opaque_folders_hide_their_contents()
    {
        using var folder = new TempDirectory();
        var root = Path.Combine(folder.Path, "rootfs");
        await Apply(root, Tar(
            Dir("etc"), File("etc/keep", "1"), File("etc/gone", "1"), Dir("var/cache"), File("var/cache/a", "a"), Dir("var/cache/sub"), File("var/cache/sub/b", "b")));
        await Apply(root, Tar(
            File("etc/.wh.gone", ""), Dir("var/cache"), File("var/cache/.wh..wh..opq", ""), File("var/cache/new", "n")));

        Assert.True(System.IO.File.Exists(Path.Combine(root, "etc/keep")));
        Assert.False(System.IO.File.Exists(Path.Combine(root, "etc/gone")));
        Assert.False(System.IO.File.Exists(Path.Combine(root, "etc/.wh.gone")));
        Assert.Equal(["new"], Directory.EnumerateFileSystemEntries(Path.Combine(root, "var/cache")).Select(Path.GetFileName));
    }

    [Fact]
    public async Task Links_permissions_and_the_diff_id_are_kept()
    {
        using var folder = new TempDirectory();
        var root = Path.Combine(folder.Path, "rootfs");
        var tar = Tar(Dir("usr/bin"), File("usr/bin/tool", "#!/bin/sh\n", 0b111_101_101), Link("bin", "usr/bin"), Hard("usr/bin/alias", "usr/bin/tool"));

        var result = await Apply(root, tar);

        Assert.Equal("usr/bin", new FileInfo(Path.Combine(root, "bin")).LinkTarget);
        Assert.Equal("#!/bin/sh\n", System.IO.File.ReadAllText(Path.Combine(root, "usr/bin/alias")));
        if (!OperatingSystem.IsWindows()) Assert.True((System.IO.File.GetUnixFileMode(Path.Combine(root, "usr/bin/tool")) & UnixFileMode.OtherExecute) != 0);
        Assert.Equal(Registry.RegistryClient.Digest(tar), result.DiffId);
        await Assert.ThrowsAsync<InvalidDataException>(() => LayerUnpacker.ApplyAsync(new MemoryStream(tar), Images.MediaTypes.OciLayer, Path.Combine(folder.Path, "other"), "sha256:" + new string('0', 64)));
    }

    [Fact]
    public async Task Entries_cannot_escape_the_root_through_dot_dot_or_links()
    {
        using var folder = new TempDirectory();
        var root = Path.Combine(folder.Path, "rootfs");
        var outside = Path.Combine(folder.Path, "outside");
        Directory.CreateDirectory(outside);

        await Apply(root, Tar(File("../../escaped", "x"), Link("evil", outside), File("evil/through-link", "x"), Link("abs", "/"), File("abs/etc/written", "x")));

        Assert.Empty(Directory.EnumerateFileSystemEntries(outside));
        Assert.False(System.IO.File.Exists(Path.Combine(folder.Path, "escaped")));
        Assert.True(System.IO.File.Exists(Path.Combine(root, "escaped")));
        Assert.True(System.IO.File.Exists(Path.Combine(root, "etc/written")));
    }

    [Fact]
    public async Task A_hard_link_to_a_link_pointing_outside_reads_nothing_from_the_device()
    {
        // The hard link's target was resolved without following its last component, and where hard links fail (Android)
        // File.Copy followed the device's link: an image could copy TabStudio's private files into its container.
        using var folder = new TempDirectory();
        var root = Path.Combine(folder.Path, "rootfs");
        var secret = Path.Combine(folder.Path, "private", "id_ed25519");
        Directory.CreateDirectory(Path.GetDirectoryName(secret)!);
        await System.IO.File.WriteAllTextAsync(secret, "private key");

        await Assert.ThrowsAsync<InvalidDataException>(() => Apply(root, Tar(Link("key", secret), Hard("copy", "key"))));

        var copy = new FileInfo(Path.Combine(root, "copy"));
        Assert.False(copy.Exists && copy.LinkTarget is null && System.IO.File.ReadAllText(copy.FullName) == "private key");
    }

    [Fact]
    public void Paths_resolve_links_inside_the_root()
    {
        using var folder = new TempDirectory();
        var root = Path.Combine(folder.Path, "r");
        Directory.CreateDirectory(Path.Combine(root, "usr/lib"));
        System.IO.File.CreateSymbolicLink(Path.Combine(root, "lib"), "usr/lib");
        System.IO.File.CreateSymbolicLink(Path.Combine(root, "up"), "../../..");

        Assert.Equal(Path.Combine(root, "usr/lib/x.so"), SafePath.Resolve(root, "/lib/x.so"));
        Assert.Equal(Path.Combine(root, "etc"), SafePath.Resolve(root, "up/etc"));
        Assert.Equal("/usr/lib", SafePath.ToGuest(root, Path.Combine(root, "usr/lib")));
    }

    [Fact]
    public async Task A_copied_tree_is_independent_of_its_source()
    {
        using var folder = new TempDirectory();
        var source = Path.Combine(folder.Path, "image");
        await Apply(source, Tar(Dir("etc"), File("etc/config", "original"), Link("etc/link", "config")));
        var copy = Path.Combine(folder.Path, "container");

        await TreeCopy.CopyAsync(source, copy);
        await System.IO.File.WriteAllTextAsync(Path.Combine(copy, "etc/config"), "changed");

        Assert.Equal("original", System.IO.File.ReadAllText(Path.Combine(source, "etc/config")));
        Assert.Equal("config", new FileInfo(Path.Combine(copy, "etc/link")).LinkTarget);
    }

    [Fact]
    public async Task A_container_copy_of_an_image_with_a_FIFO_finishes_and_keeps_the_FIFO()
    {
        if (OperatingSystem.IsWindows()) return;
        using var folder = new TempDirectory();
        var source = Path.Combine(folder.Path, "image");
        await Apply(source, Tar(Dir("run"), new PaxTarEntry(TarEntryType.Fifo, "run/initctl") { Mode = (UnixFileMode)0b110_000_000 }, File("run/ok", "ok")));
        Assert.True(UnixFiles.IsSpecial(Path.Combine(source, "run/initctl")));
        Assert.False(UnixFiles.IsSpecial(Path.Combine(source, "run/ok")));
        var copy = Path.Combine(folder.Path, "container");

        // File.Copy opened the FIFO and waited for a writer, so the copy never ended.
        await TreeCopy.CopyAsync(source, copy).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.True(UnixFiles.IsSpecial(Path.Combine(copy, "run/initctl")));
        Assert.Equal("ok", System.IO.File.ReadAllText(Path.Combine(copy, "run/ok")));
    }

    [Fact]
    public async Task A_container_shares_the_image_program_files_and_its_own_writes_never_reach_the_image()
    {
        // Every container was a full copy of its image, so a large image needed its space again for each container.
        if (!OperatingSystem.IsLinux() || Environment.IsPrivilegedProcess) return;
        using var folder = new TempDirectory();
        var image = Path.Combine(folder.Path, "image");
        var big = new string('x', 256 * 1024);
        await Apply(image, Tar(Dir("usr"), Dir("usr/lib"), Dir("etc"),
            File("usr/lib/libbig.so", big, 0b111_101_101),
            File("usr/lib/small.so", "small", 0b111_101_101),
            File("etc/big.conf", big)));
        var container = Path.Combine(folder.Path, "container");
        var library = Path.Combine(container, "usr/lib/libbig.so");

        var copied = await TreeCopy.ShareAsync(image, container);

        // The large library is the image's own file: one inode with two links, and no new space.
        Assert.Equal(Stat("%i", Path.Combine(image, "usr/lib/libbig.so")), Stat("%i", library));
        Assert.Equal("2", Stat("%h", library));
        Assert.Equal((long)(big.Length + "small".Length), copied);
        // Small files and configuration are the container's own copies.
        Assert.NotEqual(Stat("%i", Path.Combine(image, "usr/lib/small.so")), Stat("%i", Path.Combine(container, "usr/lib/small.so")));
        Assert.NotEqual(Stat("%i", Path.Combine(image, "etc/big.conf")), Stat("%i", Path.Combine(container, "etc/big.conf")));

        // A container's process cannot write the shared file in place: the kernel refuses, and the image is unchanged.
        Assert.Throws<UnauthorizedAccessException>(() => System.IO.File.AppendAllText(library, "y"));
        Assert.Equal(big, System.IO.File.ReadAllText(Path.Combine(image, "usr/lib/libbig.so")));

        // The engine's own write breaks the link first, so the image keeps its file.
        ContainerEngine.WriteReplacing(library, "patched");
        Assert.Equal("patched", System.IO.File.ReadAllText(library));
        Assert.Equal(big, System.IO.File.ReadAllText(Path.Combine(image, "usr/lib/libbig.so")));
        Assert.Equal("1", Stat("%h", Path.Combine(image, "usr/lib/libbig.so")));
    }

    [Fact]
    public async Task A_copy_of_an_image_whose_files_a_container_shares_keeps_the_image_s_own_modes()
    {
        // Sharing took the write permission off the image's own file for good, so a build from the image after a
        // container of it was created got a read-only file, which a RUN could not write in place: the build passed or
        // failed by what had run on the device before.
        if (!OperatingSystem.IsLinux() || Environment.IsPrivilegedProcess) return;
        using var folder = new TempDirectory();
        var image = Path.Combine(folder.Path, "image");
        var big = new string('x', 256 * 1024);
        await Apply(image, Tar(Dir("usr"), Dir("usr/lib"), File("usr/lib/libbig.so", big, 0b111_101_101)));
        var own = System.IO.File.GetUnixFileMode(Path.Combine(image, "usr/lib/libbig.so"));
        Assert.True((own & UnixFileMode.UserWrite) != 0);
        await TreeCopy.ShareAsync(image, Path.Combine(folder.Path, "container"));
        Assert.Equal("2", Stat("%h", Path.Combine(image, "usr/lib/libbig.so")));
        Assert.Throws<UnauthorizedAccessException>(() => System.IO.File.AppendAllText(Path.Combine(folder.Path, "container/usr/lib/libbig.so"), "y"));

        var stage = Path.Combine(folder.Path, "stage");
        await TreeCopy.CopyAsync(image, stage);

        // The copy has the image's modes, as before any container shared the file, and can be written in place.
        var library = Path.Combine(stage, "usr/lib/libbig.so");
        Assert.Equal(own, System.IO.File.GetUnixFileMode(library));
        System.IO.File.AppendAllText(library, "y");
        Assert.Equal(big, System.IO.File.ReadAllText(Path.Combine(image, "usr/lib/libbig.so")));

        // Another container shares the file again, and it stays read-only there.
        await TreeCopy.ShareAsync(image, Path.Combine(folder.Path, "second"));
        Assert.Equal("3", Stat("%h", Path.Combine(image, "usr/lib/libbig.so")));
        Assert.Equal(own, TreeCopy.ModeOf(Path.Combine(folder.Path, "second/usr/lib/libbig.so")));
    }

    /// <summary>A file's inode number (<c>%i</c>) or link count (<c>%h</c>), from coreutils' stat.</summary>
    private static string Stat(string format, string path)
    {
        using var process = Process.Start(new ProcessStartInfo("stat") { RedirectStandardOutput = true, ArgumentList = { "-c", format, path } })!;
        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        return output;
    }

    private static Task<LayerResult> Apply(string root, byte[] tar) =>
        LayerUnpacker.ApplyAsync(new MemoryStream(FakeRegistry.Gzip(tar)), Images.MediaTypes.OciLayerGzip, root, null);

    private static PaxTarEntry Dir(string name) => new PaxTarEntry(TarEntryType.Directory, name) { Mode = (UnixFileMode)0b111_101_101 };
    private static PaxTarEntry File(string name, string content, int mode = 0b110_100_100) =>
        new PaxTarEntry(TarEntryType.RegularFile, name) { DataStream = new MemoryStream(Encoding.UTF8.GetBytes(content)), Mode = (UnixFileMode)mode };
    private static PaxTarEntry Link(string name, string target) => new PaxTarEntry(TarEntryType.SymbolicLink, name) { LinkName = target };
    private static PaxTarEntry Hard(string name, string target) => new PaxTarEntry(TarEntryType.HardLink, name) { LinkName = target };

    private static byte[] Tar(params TarEntry[] entries)
    {
        using var stream = new MemoryStream();
        using (var writer = new TarWriter(stream, TarEntryFormat.Pax, leaveOpen: true))
        {
            foreach (var entry in entries) writer.WriteEntry(entry);
        }

        return stream.ToArray();
    }
}

