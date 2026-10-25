using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using TabStudio.App.Editing.Extensions;
using TabStudio.Core.Workspaces;
using TabStudio.Testing.Extensions;

namespace TabStudio.App.Tests.Extensions;

/// <summary>A file icon theme extension: its icons in the Solution Explorer and on tabs, chosen in the picker.</summary>
[Collection(ExtensionsTestGroup.Name)]
public sealed class FileIconThemeTests
{
    [AvaloniaFact]
    public async Task The_chosen_icon_theme_gives_files_and_folders_their_icons_in_the_Solution_Explorer_and_on_tabs()
    {
        using var fixture = new ExtensionsFixture();
        await fixture.InstallLumenAsync();
        using (var package = new MemoryStream(IconThemeExtension.Build()))
        {
            await fixture.Service.InstallFileAsync(package, "lumen-icons-1.0.0.vsix");
        }

        Directory.CreateDirectory(Path.Combine(fixture.Folder, "docs"));
        File.WriteAllText(Path.Combine(fixture.Folder, "docs", "guide.md"), "# Guide\n");
        await fixture.OpenFolderAsync();
        var window = fixture.Show();
        var document = await fixture.OpenAsync("hello.lumen");
        Assert.False(document.HasFileIcon);
        Assert.Null(FileIcons.ForFile("hello.lumen"));

        var pick = fixture.Workspace.SelectFileIconThemeCommand.ExecuteAsync(null);
        var picker = fixture.Workspace.QuickPick;
        await UiTestHelpers.WaitUntilAsync(() => picker.IsOpen, because: "the icon theme picker");
        Assert.Equal(new[] { "None", IconThemeExtension.Label, "Browse File Icon Themes on Open VSX…" }, picker.Items.Select(i => i.Label).ToArray());
        picker.MoveSelection(1);
        picker.Accept();
        await pick;

        var lumen = Assert.IsType<DrawingImage>(FileIcons.ForFile("hello.lumen"));
        Assert.Equal(32, lumen.Size.Width);
        Assert.IsAssignableFrom<Bitmap>(FileIcons.ForFile("notes.txt"));
        Assert.Equal(24, Assert.IsType<DrawingImage>(FileIcons.ForFile("Program.cs")).Size.Width);
        Assert.True(document.HasFileIcon);

        var rows = fixture.Workspace.Explorer.Rows;
        var file = rows.Single(r => r.Name == "hello.lumen");
        Assert.True(file.HasThemeIcon);
        Assert.False(file.ShowsBadge);
        var folder = rows.Single(r => r.Name == "docs");
        Assert.Same(FileIcons.ForFolder("docs", expanded: false), folder.ThemeIcon);
        fixture.Workspace.Explorer.Toggle(folder);
        Assert.Same(FileIcons.ForFolder("docs", expanded: true), folder.ThemeIcon);
        Assert.NotSame(FileIcons.ForFolder("docs", expanded: false), folder.ThemeIcon);
        Assert.True(rows.Single(r => r.Node.Kind == WorkspaceNodeKind.Root || r.Node.Kind == WorkspaceNodeKind.Solution).ShowsSymbol);

        await fixture.OpenAsync("notes.txt");
        await UiTestHelpers.WaitUntilAsync(() => true);
        UiTestHelpers.SaveScreenshot(window, "ext-12-file-icon-theme");

        fixture.Service.SelectIconTheme(null);
        Assert.False(document.HasFileIcon);
        Assert.True(fixture.Workspace.Explorer.Rows.Single(r => r.Name == "hello.lumen").ShowsBadge);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Without_an_icon_theme_a_language_shows_its_own_icon_and_the_Extensions_tab_its_symbol()
    {
        using var fixture = new ExtensionsFixture();
        var package = LumenExtension.PackageJson().Replace(
            "\"configuration\": \"./language-configuration.json\"",
            "\"configuration\": \"./language-configuration.json\", \"icon\": { \"light\": \"./images/icon.png\", \"dark\": \"./images/icon.png\" }",
            StringComparison.Ordinal);
        await fixture.InstallLumenAsync(LumenExtension.Builder().Add("package.json", package).Build());
        await fixture.OpenFolderAsync();
        var window = fixture.Show();

        var document = await fixture.OpenAsync("hello.lumen");

        Assert.IsAssignableFrom<Bitmap>(FileIcons.ForFile("hello.lumen"));
        Assert.True(document.HasFileIcon);
        Assert.Null(FileIcons.ForFile("notes.txt"));
        Assert.True(fixture.Workspace.Explorer.Rows.Single(r => r.Name == "hello.lumen").HasThemeIcon);
        Assert.True(fixture.Workspace.Explorer.Rows.Single(r => r.Name == "notes.txt").ShowsBadge);
        Assert.True(fixture.Workspace.ShowsEditorStatus);

        var page = fixture.Workspace.OpenExtensions()!;

        Assert.True(fixture.Workspace.ActiveDocument!.HasFileIcon);
        Assert.False(fixture.Workspace.ShowsEditorStatus);
        Assert.False(fixture.Workspace.ShowsEditorTouchBar);
        Assert.Empty(fixture.Workspace.Breadcrumb);
        Assert.NotNull(page);
        window.Close();
    }
}
