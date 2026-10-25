using System.Text;
using TabStudio.App.Services;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Services;

public sealed class SampleAndFilesTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public async Task The_sample_extracts_into_a_loadable_solution()
    {
        var samples = new SampleLibrary(new AppPaths(_temp.Path));

        var solution = await samples.EnsureExtractedAsync();
        var workspace = WorkspaceLoader.Load(solution);

        Assert.Equal(new[] { "Greetings", "HelloTablet" }, workspace.Projects.Select(p => p.Name).Order());
        Assert.Equal("HelloTablet", workspace.DefaultStartupProject?.Name);
    }

    [Fact]
    public async Task The_sample_keeps_the_users_changes_when_opened_again()
    {
        var samples = new SampleLibrary(new AppPaths(_temp.Path));
        var solution = await samples.EnsureExtractedAsync();
        var program = Path.Combine(Path.GetDirectoryName(solution)!, "src", "HelloTablet", "Program.cs");
        await File.WriteAllTextAsync(program, "// edited");

        await samples.EnsureExtractedAsync();

        Assert.Equal("// edited", await File.ReadAllTextAsync(program));
    }

    [Theory]
    [InlineData(new byte[] { 0xEF, 0xBB, 0xBF }, "UTF-8 BOM")]
    [InlineData(new byte[0], "UTF-8")]
    public async Task Text_files_keep_their_encoding_when_saved(byte[] preamble, string label)
    {
        var path = _temp.Resolve($"file-{label.Length}.cs");
        await File.WriteAllBytesAsync(path, [.. preamble, .. Encoding.UTF8.GetBytes("class C {}\r\n")]);

        var content = await TextFile.ReadAsync(path);
        await TextFile.WriteAsync(path, content.Text + "// more", content.Encoding);
        var bytes = await File.ReadAllBytesAsync(path);

        Assert.Equal(label, content.EncodingLabel);
        Assert.Equal("CRLF", content.NewlineLabel);
        Assert.Equal(preamble, bytes.Take(preamble.Length));
        Assert.Equal("class C {}\r\n// more", Encoding.UTF8.GetString(bytes, preamble.Length, bytes.Length - preamble.Length));
    }

    [Fact]
    public async Task Binary_files_are_not_opened_as_text()
    {
        var path = _temp.Resolve("image.png");
        await File.WriteAllBytesAsync(path, [0x89, 0x50, 0x4E, 0x47, 0x00, 0x01]);

        await Assert.ThrowsAsync<IOException>(() => TextFile.ReadAsync(path));
    }

    [Theory]
    [InlineData("my repo", "my repo")]
    [InlineData("../escape", "escape")]
    [InlineData("a/b:c", "a-b-c")]
    [InlineData("...", "Project")]
    public void New_folder_names_are_safe(string name, string expected) => Assert.Equal(expected, AppPaths.MakeSafeFolderName(name));

    [Theory]
    [InlineData(".gitignore", ".gitignore")]
    [InlineData("..", "_")]
    [InlineData("a:b", "a-b")]
    public void Imported_file_names_stay_as_close_as_possible(string name, string expected) => Assert.Equal(expected, AppPaths.MakeSafeFileName(name));

    public void Dispose() => _temp.Dispose();
}
