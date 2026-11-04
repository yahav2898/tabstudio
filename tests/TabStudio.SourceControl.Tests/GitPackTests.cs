using System.IO.Compression;
using TabStudio.SourceControl.Git;
using TabStudio.Testing;

namespace TabStudio.SourceControl.Tests;

/// <summary>TabStudio's reading and writing of Git's object formats, checked against Git itself.</summary>
public sealed class GitPackTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Theory]
    [InlineData(1, CompressionLevel.Optimal)]
    [InlineData(1, CompressionLevel.Fastest)]
    [InlineData(100, CompressionLevel.NoCompression)]
    [InlineData(70_000, CompressionLevel.Optimal)]
    [InlineData(300_000, CompressionLevel.SmallestSize)]
    public void The_inflater_reads_zlib_data_and_stops_exactly_at_its_end(int length, CompressionLevel level)
    {
        var random = new Random(length);
        var data = new byte[length];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = i % 3 == 0 ? (byte)random.Next(256) : (byte)"abcab "[i % 6];
        }

        using var compressed = new MemoryStream();
        compressed.Write("XY"u8);
        using (var zlib = new ZLibStream(compressed, level, leaveOpen: true))
        {
            zlib.Write(data);
        }

        var end = compressed.Length;
        compressed.Write("next object"u8);
        compressed.Position = 0;
        var input = new PackInput(compressed) { Position = 2 };
        using var output = new MemoryStream();

        new Inflater().Inflate(input, output);

        Assert.Equal(data, output.ToArray());
        Assert.Equal(end, input.Position);
    }

    [Fact]
    public void A_pack_Git_wrote_with_deltas_is_indexed_exactly_as_git_index_pack_indexes_it()
    {
        var work = Path.Combine(_temp.Path, "repo");
        Directory.CreateDirectory(work);
        GitServer.Git(work, "init", "--quiet", ".");
        GitServer.Configure(work);
        var text = string.Concat(Enumerable.Range(0, 400).Select(i => $"line {i}\n"));
        for (var i = 0; i < 6; i++)
        {
            File.WriteAllText(Path.Combine(work, "file.txt"), text + $"change {i}\n");
            GitServer.Git(work, "add", ".");
            GitServer.Git(work, "commit", "--quiet", "-m", $"commit {i}");
        }

        GitServer.Git(work, "repack", "-a", "-d", "-q", "--depth=10", "--window=10");
        var pack = Directory.GetFiles(Path.Combine(work, ".git", "objects", "pack"), "*.pack").Single();
        var expected = File.ReadAllBytes(Path.ChangeExtension(pack, ".idx"));

        var indexed = PackIndexer.Index(pack, _ => null, CancellationToken.None);

        Assert.Equal(expected, indexed.Index);
        Assert.Contains("delta", GitServer.Git(work, "verify-pack", "-v", Path.ChangeExtension(pack, ".idx")), StringComparison.Ordinal);
    }

    [Fact]
    public void Objects_TabStudio_writes_are_read_by_Git_and_a_pack_it_writes_passes_git_verify_pack()
    {
        var work = Path.Combine(_temp.Path, "repo");
        Directory.CreateDirectory(work);
        GitServer.Git(work, "init", "--quiet", ".");
        var store = new ObjectStore(Path.Combine(work, ".git", "objects"));
        var blob = store.Write(GitObjectKind.Blob, "hello\n"u8);
        var tree = TreeCodec.WriteTrees(store, new Dictionary<string, (string Mode, string Id)>
        {
            ["a/b.txt"] = (GitObjects.RegularFileMode, blob),
            ["a.txt"] = (GitObjects.RegularFileMode, blob),
            ["run.sh"] = (GitObjects.ExecutableFileMode, blob),
        });
        var person = new CommitPerson("Tab Studio", "tab@example.test", DateTimeOffset.FromUnixTimeSeconds(1_800_000_000).ToOffset(TimeSpan.FromHours(2)));
        var commit = store.Write(GitObjectKind.Commit, CommitCodec.Write(new CommitData(tree, [], person, person, "First\n")));

        Assert.Equal(blob, GitObjects.BlobHash("hello\n"u8));
        GitServer.Git(work, "update-ref", "refs/heads/main", commit);
        GitServer.Git(work, "fsck", "--strict", "--no-dangling");
        Assert.Equal(tree, GitServer.Git(work, "rev-parse", commit + "^{tree}").Trim());
        var listing = GitServer.Git(work, "ls-tree", tree).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal($"100644 blob {blob}\ta.txt", listing[0]);
        Assert.StartsWith("040000 tree ", listing[1], StringComparison.Ordinal);
        Assert.EndsWith("\ta", listing[1], StringComparison.Ordinal);
        Assert.Equal($"100755 blob {blob}\trun.sh", listing[2]);
        Assert.Equal("Tab Studio|tab@example.test|1800000000 +0200|First", GitServer.Git(work, "log", "-1", "--date=raw", "--format=%an|%ae|%ad|%s", commit).Trim());

        var subtree = TreeCodec.Parse(store.Read(tree).Content).Single(i => i.IsTree).Id;
        var packPath = Path.Combine(_temp.Path, "out.pack");
        using (var file = File.Create(packPath))
        {
            PackWriter.Write(file, store, [commit, tree, subtree, blob]);
        }

        var other = new ObjectStore(Path.Combine(_temp.Path, "other", "objects"));
        var ids = other.AddPack(packPath, promisor: false, CancellationToken.None);
        Assert.Equal(4, ids.Count);
        var idx = Directory.GetFiles(Path.Combine(_temp.Path, "other", "objects", "pack"), "*.idx").Single();
        GitServer.Git(_temp.Path, "verify-pack", idx);
        Assert.Equal("hello\n"u8.ToArray(), other.Read(blob).Content);
    }

    [Fact]
    public void A_pack_that_ends_early_gives_up_the_objects_that_arrived_whole()
    {
        var store = new ObjectStore(Path.Combine(_temp.Path, "objects"));
        var random = new Random(5);
        var ids = Enumerable.Range(0, 5).Select(_ =>
        {
            var content = new byte[3000];
            random.NextBytes(content);
            return store.Write(GitObjectKind.Blob, content);
        }).ToList();
        var packPath = Path.Combine(_temp.Path, "broken.pack");
        using (var file = File.Create(packPath))
        {
            PackWriter.Write(file, store, ids);
        }

        var bytes = File.ReadAllBytes(packPath);
        File.WriteAllBytes(packPath, bytes[..(bytes.Length - 40)]);
        var target = new ObjectStore(Path.Combine(_temp.Path, "target"));

        var kept = target.Salvage(packPath, CancellationToken.None);

        Assert.Equal(ids.Take(4), kept);
        Assert.All(kept, id => Assert.True(target.Contains(id)));
        Assert.False(File.Exists(packPath));
    }

    [Fact]
    public void Git_LFS_attributes_choose_the_files_stored_with_Git_LFS()
    {
        File.WriteAllText(Path.Combine(_temp.Path, ".gitattributes"), "*.bin filter=lfs diff=lfs merge=lfs -text\nassets/** filter=lfs\n");
        Directory.CreateDirectory(Path.Combine(_temp.Path, "assets", "small"));
        File.WriteAllText(Path.Combine(_temp.Path, "assets", "small", ".gitattributes"), "*.txt -filter\n");

        var chosen = LfsAttributes.Select(_temp.Path, ["model.bin", "src/data.bin", "README.md", "assets/logo.png", "assets/small/note.txt"]);

        Assert.Equal(["model.bin", "src/data.bin", "assets/logo.png"], chosen);
    }

    public void Dispose() => _temp.Dispose();
}
