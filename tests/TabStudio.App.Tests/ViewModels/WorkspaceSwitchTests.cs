using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.Testing;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>
/// Opening another workspace saves the open one's edits first: closing disposed its documents without saving, so
/// joining a Live session from an open workspace, which opens the shared copy in its place, lost every unsaved edit.
/// </summary>
public sealed class WorkspaceSwitchTests
{
    [AvaloniaFact]
    public async Task Opening_another_workspace_saves_the_open_ones_edits_first()
    {
        using var temp = new TempDirectory();
        var notes = temp.Write("first/notes.txt", "one\n");
        temp.Write("second/readme.txt", "two\n");
        using var services = AppServices.Create(new AppPaths(temp.Resolve("app")));
        var main = services.GetRequiredService<MainViewModel>();

        Assert.True(await main.OpenWorkspaceAsync(temp.Resolve("first")));
        var first = main.Workspace!;
        var document = (await first.OpenDocumentAsync(notes))!;
        document.Document.Insert(document.Document.TextLength, "edited\n");
        Assert.True(document.IsDirty);

        Assert.True(await main.OpenWorkspaceAsync(temp.Resolve("second")));

        Assert.NotSame(first, main.Workspace);
        Assert.Equal("one\nedited\n", await File.ReadAllTextAsync(notes));
        main.Workspace!.Dispose();
    }
}
