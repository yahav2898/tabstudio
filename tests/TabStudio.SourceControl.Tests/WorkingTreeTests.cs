using TabStudio.Testing;

namespace TabStudio.SourceControl.Tests;

public sealed class WorkingTreeTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public void Lists_files_except_ignored_ones_and_the_git_and_tabstudio_folders()
    {
        _temp.Write(".gitignore", "bin/\nobj/\n*.user\n");
        _temp.Write("App.csproj", "<Project />");
        _temp.Write("Program.cs", "return 0;");
        _temp.Write("App.csproj.user", "x");
        _temp.Write("bin/Debug/App.dll", "x");
        _temp.Write("src/obj/project.assets.json", "{}");
        _temp.Write("src/Lib/Lib.cs", "class Lib;");
        _temp.Write("src/Lib/.gitignore", "*.generated.cs\n");
        _temp.Write("src/Lib/Model.generated.cs", "x");
        _temp.Write(".git/HEAD", "ref: refs/heads/main");
        _temp.Write(".tabstudio/sync.json", "{}");

        var files = WorkingTree.EnumerateFiles(_temp.Path);

        Assert.Equal(new[] { ".gitignore", "App.csproj", "Program.cs", "src/Lib/.gitignore", "src/Lib/Lib.cs" }, files);
    }

    [Fact]
    public void A_repositorys_own_tabstudio_files_are_neither_deleted_nor_mixed_with_the_sync_state()
    {
        // A repository that commits .tabstudio/launch.json saw it as deleted right after Open from GitHub, and a
        // commit of every change deleted it on GitHub.
        _temp.Write("Program.cs", "return 0;");
        _temp.Write(".tabstudio/launch.json", "{ \"configurations\": [] }");
        _temp.Write(".tabstudio/sync.json", "{}");
        _temp.Write(".tabstudio/conflicts/Program.cs", "x");
        var tracked = new Dictionary<string, TrackedFile>
        {
            ["Program.cs"] = new(WorkingTree.HashFile(_temp.Path, "Program.cs"), GitObjects.RegularFileMode),
            [".tabstudio/launch.json"] = new(WorkingTree.HashFile(_temp.Path, ".tabstudio/launch.json"), GitObjects.RegularFileMode),
        };

        Assert.Equal(new[] { ".tabstudio/launch.json", "Program.cs" }, WorkingTree.EnumerateFiles(_temp.Path, tracked));
        Assert.Empty(WorkingTree.GetChanges(_temp.Path, tracked));

        _temp.Write(".tabstudio/launch.json", "{ \"configurations\": [{}] }");
        Assert.Equal(new FileChange(".tabstudio/launch.json", ChangeKind.Modified), Assert.Single(WorkingTree.GetChanges(_temp.Path, tracked)));
    }

    [Fact]
    public void Tracked_files_are_listed_even_when_ignored()
    {
        _temp.Write(".gitignore", "*.dll\nlibs/\n");
        _temp.Write("libs/Vendor.dll", "binary");
        var tracked = new Dictionary<string, TrackedFile> { ["libs/Vendor.dll"] = new("sha", GitObjects.RegularFileMode) };

        Assert.Contains("libs/Vendor.dll", WorkingTree.EnumerateFiles(_temp.Path, tracked));
        Assert.DoesNotContain("libs/Vendor.dll", WorkingTree.EnumerateFiles(_temp.Path));
    }

    [Fact]
    public void Finds_added_modified_and_deleted_files()
    {
        _temp.Write("same.txt", "same");
        _temp.Write("changed.txt", "after");
        _temp.Write("new.txt", "new");
        var tracked = new Dictionary<string, TrackedFile>
        {
            ["same.txt"] = new(Hash("same"), GitObjects.RegularFileMode),
            ["changed.txt"] = new(Hash("before"), GitObjects.RegularFileMode),
            ["gone.txt"] = new(Hash("gone"), GitObjects.RegularFileMode),
        };

        var changes = WorkingTree.GetChanges(_temp.Path, tracked);

        Assert.Equal(
            new[] { new FileChange("changed.txt", ChangeKind.Modified), new FileChange("gone.txt", ChangeKind.Deleted), new FileChange("new.txt", ChangeKind.Added) },
            changes);
    }

    [Fact]
    public void Paths_outside_the_folder_are_refused() =>
        Assert.Throws<InvalidOperationException>(() => WorkingTree.FullPath(_temp.Path, "../outside.txt"));

    [Fact]
    public void The_hash_cache_skips_files_that_have_not_changed_since_they_were_hashed()
    {
        var file = _temp.Write("a.txt", "one");
        var old = DateTime.UtcNow.AddHours(-1);
        File.SetLastWriteTimeUtc(file, old);

        var cache = HashCache.Empty();
        Assert.Equal(Hash("one"), cache.GetHash(_temp.Path, "a.txt"));
        cache.Save(_temp.Path);

        // Same size and time: a real edit would change the time, so the cached hash is trusted without reading.
        File.WriteAllText(file, "two");
        File.SetLastWriteTimeUtc(file, old);
        Assert.Equal(Hash("one"), HashCache.Load(_temp.Path).GetHash(_temp.Path, "a.txt"));

        // A new time means the file is read again.
        File.SetLastWriteTimeUtc(file, old.AddMinutes(1));
        Assert.Equal(Hash("two"), HashCache.Load(_temp.Path).GetHash(_temp.Path, "a.txt"));
    }

    [Fact]
    public void The_hash_cache_does_not_trust_a_file_written_just_before_it_was_hashed()
    {
        // Git's "racy" case: the file can still change within the timestamp's resolution.
        _temp.Write("a.txt", "one");
        var cache = HashCache.Empty();
        cache.Remember(_temp.Path, "a.txt", "made-up");

        Assert.Equal(Hash("one"), cache.GetHash(_temp.Path, "a.txt"));
    }

    public void Dispose() => _temp.Dispose();

    private static string Hash(string text) => GitObjects.BlobHash(System.Text.Encoding.UTF8.GetBytes(text));
}
