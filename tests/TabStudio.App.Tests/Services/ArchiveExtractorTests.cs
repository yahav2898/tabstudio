using System.IO.Compression;
using TabStudio.App.Services;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Services;

public sealed class ArchiveExtractorTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public void Drops_the_top_level_folder_of_a_GitHub_archive()
    {
        var archive = CreateArchive(("repo-main/README.md", "hello"), ("repo-main/src/App.cs", "class App {}"));

        ArchiveExtractor.ExtractStrippingRoot(archive, _temp.Resolve("out"));

        Assert.Equal("hello", File.ReadAllText(_temp.Resolve("out/README.md")));
        Assert.True(File.Exists(_temp.Resolve("out/src/App.cs")));
    }

    [Fact]
    public void Keeps_the_layout_when_there_is_no_single_top_level_folder()
    {
        var archive = CreateArchive(("a.txt", "a"), ("folder/b.txt", "b"));

        ArchiveExtractor.ExtractStrippingRoot(archive, _temp.Resolve("out"));

        Assert.True(File.Exists(_temp.Resolve("out/a.txt")));
        Assert.True(File.Exists(_temp.Resolve("out/folder/b.txt")));
    }

    [Fact]
    public void Refuses_entries_that_would_escape_the_target_folder()
    {
        var archive = CreateArchive(("repo/../../evil.txt", "x"), ("repo/ok.txt", "y"));

        Assert.Throws<ImportException>(() => ArchiveExtractor.ExtractStrippingRoot(archive, _temp.Resolve("out")));
        Assert.False(File.Exists(_temp.Resolve("evil.txt")));
    }

    public void Dispose() => _temp.Dispose();

    private string CreateArchive(params (string Name, string Content)[] entries)
    {
        var path = _temp.Resolve($"archive-{Guid.NewGuid():N}.zip");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, content) in entries)
        {
            using var writer = new StreamWriter(zip.CreateEntry(name).Open());
            writer.Write(content);
        }

        return path;
    }
}
