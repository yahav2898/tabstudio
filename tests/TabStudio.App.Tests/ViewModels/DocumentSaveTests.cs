using System.Text;
using Avalonia.Headless.XUnit;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.Testing;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>
/// Saving never marks unwritten text as saved: keystrokes typed while a save was writing the file were marked saved
/// when it finished, so the tab lost its asterisk, closing it did not save, and the characters were gone.
/// </summary>
public sealed class DocumentSaveTests
{
    private static readonly DeviceProfile Profile = DeviceProfile.For(new DeviceInfo(8L * 1024 * 1024 * 1024, 4, false));

    [AvaloniaFact]
    public async Task Typing_while_a_save_writes_the_file_stays_unsaved()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("Program.cs", "class C {}");
        using var document = new DocumentViewModel(path, new TextFileContent("class C {}", new UTF8Encoding(false)), null, Profile);
        document.Document.Insert(0, "// one\n");

        var save = document.SaveAsync();
        document.Document.Insert(0, "// two\n");
        await save;

        Assert.True(document.IsDirty);
        Assert.False(document.Document.UndoStack.IsOriginalFile);
        await document.SaveAsync();
        Assert.False(document.IsDirty);
        Assert.Equal("// two\n// one\nclass C {}", await File.ReadAllTextAsync(path));
    }

    [AvaloniaFact]
    public async Task A_save_that_fails_leaves_the_document_unsaved()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("Program.cs", "class C {}");
        using var document = new DocumentViewModel(path, new TextFileContent("class C {}", new UTF8Encoding(false)), null, Profile)
        {
            SaveExternalAsync = _ => throw new IOException("The server did not answer."),
        };
        document.Document.Insert(0, "// edit\n");

        await Assert.ThrowsAsync<IOException>(() => document.SaveAsync());

        Assert.True(document.IsDirty);
        Assert.False(document.Document.UndoStack.IsOriginalFile);
    }
}
