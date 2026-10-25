using System.Text;
using TabStudio.Link.Language;
using TabStudio.Testing;

namespace TabStudio.Link.Tests;

public sealed class MirrorTests
{
    private const string TabletRoot = "/storage/emulated/0/TabStudio/HelloTablet";

    [Fact]
    public void Only_new_and_changed_files_are_sent_and_removed_files_are_deleted()
    {
        using var temp = new TempDirectory();
        WorkspaceFileEntry[] first = [Entry("HelloTablet.csproj", "<Project />"), Entry("Program.cs", "Console.WriteLine(1);")];

        var mirror = new LinkWorkspaceMirror(temp.Path);
        Assert.Equal([0, 1], mirror.Prepare(Request(first)));
        mirror.Write([0, 1], [Bytes("<Project />"), Bytes("Console.WriteLine(1);")]);
        var map = mirror.Commit();
        Assert.Equal("Console.WriteLine(1);", File.ReadAllText(map.RelativeToHost("Program.cs")!));

        // Something the host made itself, such as a restore's output, is not the tablet's to remove.
        var restored = Path.Combine(map.HostRoot, "obj", "project.assets.json");
        Directory.CreateDirectory(Path.GetDirectoryName(restored)!);
        File.WriteAllText(restored, "{}");

        WorkspaceFileEntry[] second = [first[0], Entry("Program.cs", "Console.WriteLine(2);"), Entry("Greeter.cs", "class Greeter;")];
        var reopened = new LinkWorkspaceMirror(temp.Path);
        Assert.Equal([1, 2], reopened.Prepare(Request(second)));
        reopened.Write([1, 2], [Bytes("Console.WriteLine(2);"), Bytes("class Greeter;")]);
        reopened.Commit();

        WorkspaceFileEntry[] third = [second[0], second[1]];
        Assert.Empty(reopened.Prepare(Request(third)));
        reopened.Commit();

        Assert.False(File.Exists(map.RelativeToHost("Greeter.cs")));
        Assert.Equal("Console.WriteLine(2);", File.ReadAllText(map.RelativeToHost("Program.cs")!));
        Assert.True(File.Exists(restored));
    }

    [Fact]
    public void Files_must_be_listed_before_they_are_sent()
    {
        using var temp = new TempDirectory();
        var mirror = new LinkWorkspaceMirror(temp.Path);

        Assert.Throws<LinkRemoteException>(() => mirror.Write([0], [Bytes("x")]));
        Assert.Throws<LinkRemoteException>(() => mirror.Commit());
    }

    [Fact]
    public void A_file_list_that_would_write_outside_the_mirror_writes_nothing_there()
    {
        using var temp = new TempDirectory();
        var mirror = new LinkWorkspaceMirror(Path.Combine(temp.Path, "mirrors"));
        WorkspaceFileEntry[] files = [Entry("../../escaped.txt", "x"), Entry("Program.cs", "y")];

        Assert.Equal([1], mirror.Prepare(Request(files)));
        mirror.Write([0, 1], [Bytes("x"), Bytes("y")]);
        mirror.Commit();

        Assert.False(File.Exists(Path.Combine(temp.Path, "escaped.txt")));
        Assert.Empty(Directory.GetFiles(temp.Path, "escaped.txt", SearchOption.AllDirectories));
    }

    [Fact]
    public void Each_workspace_of_a_tablet_has_its_own_folder()
    {
        using var temp = new TempDirectory();
        var mirror = new LinkWorkspaceMirror(temp.Path);

        mirror.Prepare(Request([Entry("Program.cs", "1")]));
        var first = mirror.Commit();
        mirror.Prepare(new PrepareWorkspaceRequest("Other", "/storage/emulated/0/TabStudio/Other", "/", [Entry("Program.cs", "1")]));
        var second = mirror.Commit();

        Assert.NotEqual(first.HostRoot, second.HostRoot);
        Assert.StartsWith("HelloTablet-", Path.GetFileName(first.HostRoot), StringComparison.Ordinal);
    }

    private static PrepareWorkspaceRequest Request(IReadOnlyList<WorkspaceFileEntry> files) => new("HelloTablet", TabletRoot, "/", files);

    private static WorkspaceFileEntry Entry(string path, string content)
    {
        var hash = FileHash.Of(Bytes(content));
        return new WorkspaceFileEntry(path, hash.Length, hash.Hash);
    }

    private static byte[] Bytes(string content) => Encoding.UTF8.GetBytes(content);
}
