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
    public async Task A_file_that_is_not_UTF_8_keeps_every_byte_when_saved()
    {
        // "// הערה" in Windows-1255: read as UTF-8 it became replacement characters, and the first save wrote those.
        var path = _temp.Resolve("legacy.cs");
        byte[] hebrew = [0x2F, 0x2F, 0x20, 0xE4, 0xF2, 0xF8, 0xE4, 0x0D, 0x0A];
        await File.WriteAllBytesAsync(path, [.. hebrew, .. Encoding.ASCII.GetBytes("class C {}")]);

        var content = await TextFile.ReadAsync(path);
        Assert.Equal("ISO-8859-1", content.EncodingLabel);
        Assert.DoesNotContain('\uFFFD', content.Text);
        await TextFile.WriteAsync(path, content.Text + " // edited", content.Encoding);

        byte[] expected = [.. hebrew, .. Encoding.ASCII.GetBytes("class C {} // edited")];
        Assert.Equal(expected, await File.ReadAllBytesAsync(path));

        // A character the file's encoding cannot store is refused, not replaced, so the save says why and keeps the edit.
        var error = await Assert.ThrowsAsync<IOException>(() => TextFile.WriteAsync(path, content.Text + " שלום", content.Encoding));
        Assert.Contains("convert the file to UTF-8", error.Message, StringComparison.Ordinal);
        Assert.Equal(expected, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task Saving_keeps_a_scripts_execute_bit_writes_through_a_link_and_leaves_no_copy_behind()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        // A saved gradlew lost its execute bit, a saved link became a copy, and a failed save left a .tabstudio-save
        // file in the repository.
        var script = _temp.Write("tools/gradlew", "#!/bin/sh\n");
        File.SetUnixFileMode(script, File.GetUnixFileMode(script) | UnixFileMode.UserExecute);
        await TextFile.WriteAsync(script, "#!/bin/sh\nexec java\n", new UTF8Encoding(false));
        Assert.True(File.GetUnixFileMode(script).HasFlag(UnixFileMode.UserExecute));

        var real = _temp.Write("tools/config.json", "{}");
        var link = _temp.Resolve("tools/linked.json");
        File.CreateSymbolicLink(link, real);
        await TextFile.WriteAsync(link, "{ \"a\": 1 }", new UTF8Encoding(false));
        Assert.NotNull(new FileInfo(link).LinkTarget);
        Assert.Equal("{ \"a\": 1 }", await File.ReadAllTextAsync(real));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => TextFile.WriteAsync(real, "{}", new UTF8Encoding(false), new CancellationToken(canceled: true)));
        string[] left = ["config.json", "gradlew", "linked.json"];
        Assert.Equal(left, Directory.EnumerateFileSystemEntries(_temp.Resolve("tools")).Select(f => Path.GetFileName(f)).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_UTF_32_file_is_not_read_as_UTF_16()
    {
        var path = _temp.Resolve("wide.txt");
        await File.WriteAllBytesAsync(path, [.. new UTF32Encoding(false, true).GetPreamble(), .. new UTF32Encoding(false, false).GetBytes("hi")]);

        var content = await TextFile.ReadAsync(path);

        Assert.Equal(("hi", "UTF-32 LE"), (content.Text, content.EncodingLabel));
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

    [Fact]
    public async Task A_link_to_an_endless_device_is_refused_instead_of_filling_memory()
    {
        // A repository's symbolic link to /dev/zero reports no length, passed the size check, and was read until the
        // app ran out of memory.
        if (OperatingSystem.IsWindows() || !File.Exists("/dev/zero"))
        {
            return;
        }

        var link = _temp.Resolve("zero.txt");
        File.CreateSymbolicLink(link, "/dev/zero");

        var error = await Assert.ThrowsAsync<IOException>(() => TextFile.ReadAsync(link));

        Assert.Contains("16 MB", error.Message, StringComparison.Ordinal);
    }

    public void Dispose() => _temp.Dispose();
}
