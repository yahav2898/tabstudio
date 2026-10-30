using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Accessibility;
using TabStudio.App.Services;
using TabStudio.App.Services.Appearance;
using TabStudio.App.Tests.Theming;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Accessibility;

/// <summary>
/// The text size (docs/design/README.md, "Type"): one setting from Small to Very large, applied at once to the whole
/// IDE and the editor, following Android's font size by default.
/// </summary>
[Collection(AppearanceTestGroup.Name)]
public sealed class TextSizeTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public void Sizes_go_from_85_to_150_percent_and_the_system_size_is_clamped_to_them()
    {
        Assert.Equal(TextSize.System, AppearanceSettings.Default.TextSize);
        Assert.Equal(0.85, TextSizes.Factor(TextSize.Small, 1));
        Assert.Equal(1, TextSizes.Factor(TextSize.Default, 3));
        Assert.Equal(1.5, TextSizes.Factor(TextSize.VeryLarge, 1));
        Assert.Equal(1.3, TextSizes.Factor(TextSize.System, 1.3));
        Assert.Equal(1.5, TextSizes.Factor(TextSize.System, 2));
        Assert.Equal(0.85, TextSizes.Factor(TextSize.System, 0.5));
        Assert.Equal(1, TextSizes.Factor(TextSize.System, double.NaN));

        Assert.Equal(TextSize.Large, TextSizes.Step(1, larger: true));
        Assert.Equal(TextSize.Small, TextSizes.Step(1, larger: false));
        Assert.Equal(TextSize.VeryLarge, TextSizes.Step(1.5, larger: true));
        Assert.Equal(TextSize.Larger, TextSizes.Step(1.2, larger: true));
    }

    [Fact]
    public void Every_font_size_a_view_once_wrote_has_a_token()
    {
        Assert.Equal("Type.Body", TypeScale.TokenFor(12.5));
        Assert.Equal("Type.Small", TypeScale.TokenFor(12));
        Assert.Equal("Type.Title", TypeScale.TokenFor(18));
        Assert.Equal(21, TypeScale.Scaled(14, 1.5));
        Assert.Equal(18.75, TypeScale.Scaled(12.5, 1.5));
    }

    [Fact]
    public void No_view_writes_a_font_size_as_a_number()
    {
        // A number would not follow the text size: views use the type scale's tokens (Theme/Type.axaml).
        var root = Path.Combine(RepositoryRoot(), "src", "TabStudio.App");
        var found = new List<string>();
        foreach (var file in Directory.EnumerateFiles(root, "*.axaml", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (relative.StartsWith("Previews/", StringComparison.Ordinal) || relative == "Theme/Type.axaml")
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(lines[i], @"(FontSize|LineHeight)=""[0-9]|Property=""(TextElement\.)?(FontSize|LineHeight)"" Value=""[0-9]"))
                {
                    found.Add($"{relative}:{i + 1}: {lines[i].Trim()}");
                }
            }
        }

        Assert.True(found.Count == 0, "Font sizes written as numbers:\n" + string.Join("\n", found));
    }

    [AvaloniaFact]
    public void The_system_font_size_is_followed_until_a_size_is_chosen_and_the_choice_is_remembered()
    {
        var system = new SystemAccessibility();
        var store = new AppearanceSettingsStore(new AppPaths(_temp.Path));
        var appearance = new AppearanceService(store, system);
        try
        {
            appearance.Apply();
            Assert.Equal(1, TypeScale.Factor);

            system.Update(1.3, reduceMotion: false, isRightToLeft: false);
            Assert.Equal(1.3, TypeScale.Factor);
            Assert.Equal(TypeScale.Scaled(12.5, 1.3), Application.Current!.Resources["Type.Body"]);

            system.Update(2, reduceMotion: false, isRightToLeft: false);
            Assert.Equal(TextSizes.Largest, TypeScale.Factor);

            appearance.SetTextSize(TextSize.Small);
            Assert.Equal(0.85, TypeScale.Factor);
            system.Update(1.15, reduceMotion: false, isRightToLeft: false);
            Assert.Equal(0.85, TypeScale.Factor);

            Assert.Equal(TextSize.Small, new AppearanceService(store, system).TextSize);
        }
        finally
        {
            TypeScale.Apply(1);
        }
    }

    [AvaloniaFact]
    public async Task Text_that_takes_its_size_from_its_button_grows_with_it()
    {
        // A tab's string content inherits its size from the button's theme (PanelTabButton): it must be laid out again,
        // not only drawn larger in its old box.
        var tab = new Button { Content = "Error List", Theme = (Avalonia.Styling.ControlTheme)Application.Current!.FindResource("PanelTabButton")! };
        var card = new Button { Theme = (Avalonia.Styling.ControlTheme)Application.Current!.FindResource("CardButton")!, Content = new TextBlock { Text = "Open from GitHub" } };
        var window = new Window { Width = 800, Height = 300, Content = new StackPanel { Children = { new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden, Content = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Children = { tab } } }, card } } };
        window.Show();
        try
        {
            await UiTestHelpers.WaitUntilAsync(() => true);
            var text = tab.GetVisualDescendants().OfType<TextBlock>().First();
            var cardText = (TextBlock)card.Content!;
            var before = (text.Bounds.Width, cardText.Bounds.Width);

            TypeScale.Apply(TextSizes.Largest);
            await UiTestHelpers.WaitUntilAsync(() => true);

            var detail = $"tab text {text.FontSize} at {text.Bounds.Width} (desired {text.DesiredSize.Width}, was {before.Item1}); card text {cardText.FontSize} at {cardText.Bounds.Width} (desired {cardText.DesiredSize.Width}, was {before.Item2})";
            Assert.True(text.Bounds.Width > before.Item1 * 1.3, detail);
            Assert.True(cardText.Bounds.Width > before.Item2 * 1.3, detail);
        }
        finally
        {
            TypeScale.Apply(1);
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Tools_Text_Size_changes_the_whole_IDE_and_the_editor_at_once()
    {
        using var services = AppServices.Create(new AppPaths(_temp.Path));
        var main = services.GetRequiredService<MainViewModel>();
        var window = new MainWindow { DataContext = main, Width = 1280, Height = 800 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            var solution = await services.GetRequiredService<SampleLibrary>().EnsureExtractedAsync();
            await main.OpenWorkspaceAsync(solution);
            var workspace = main.Workspace!;
            if (workspace.ActiveDocument is null)
            {
                var program = Directory.EnumerateFiles(Path.GetDirectoryName(solution)!, "Program.cs", SearchOption.AllDirectories).First();
                await workspace.OpenDocumentAsync(program);
            }

            await UiTestHelpers.WaitUntilAsync(() => window.GetVisualDescendants().OfType<CodeEditorView>().Any(e => e.IsEffectivelyVisible), because: "the editor");

            var editor = window.GetVisualDescendants().OfType<CodeEditorView>().First(e => e.IsEffectivelyVisible).Editor;
            var menu = window.GetVisualDescendants().OfType<Menu>().Single(m => m.Name == "MainMenu");
            var sizes = Item(Item(menu.Items, "T_ools").Items, "Te_xt Size");
            var veryLarge = Item(sizes.Items, "_Very Large");
            Assert.Equal(14, editor.FontSize);

            veryLarge.Command!.Execute(veryLarge.CommandParameter);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(TextSize.VeryLarge, workspace.CurrentTextSize);
            Assert.True(veryLarge.IsChecked);
            Assert.Equal(21, editor.FontSize);
            Assert.Equal(TypeScale.Scaled(14, 1.5), window.FontSize);
            var menuItem = window.GetVisualDescendants().OfType<MenuItem>().First(m => m.IsEffectivelyVisible);
            Assert.Equal(TypeScale.Scaled(13, 1.5), menuItem.FontSize);

            // Ctrl+Shift+, and Ctrl+Shift+. step it as Visual Studio's zoom does.
            workspace.MakeTextSmallerCommand.Execute(null);
            Assert.Equal(TextSize.Larger, workspace.CurrentTextSize);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(TypeScale.Scaled(14, 1.3), editor.FontSize);

            var system = Item(sizes.Items, "Follow the S_ystem");
            system.Command!.Execute(system.CommandParameter);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(TextSize.System, workspace.CurrentTextSize);
            Assert.Equal(14, editor.FontSize);
        }
        finally
        {
            TypeScale.Apply(1);
            window.Close();
            main.Workspace?.Dispose();
        }
    }

    public void Dispose() => _temp.Dispose();

    private static MenuItem Item(IEnumerable<object?> items, string header) =>
        items.OfType<MenuItem>().Single(item => (item.Header as string) == header);

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TabStudio.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("The repository was not found.");
    }
}
