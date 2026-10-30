using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TabStudio.App.Accessibility;
using TabStudio.App.Controls;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.Views;

namespace TabStudio.App.Tests.Accessibility;

/// <summary>
/// What TalkBack reads (docs/design/README.md, "Screen readers"): every control has a name that says what it is,
/// worked out by <see cref="AccessibleNames"/> for every view at once, and the editor reads its line.
/// </summary>
public sealed class ScreenReaderTests
{
    [AvaloniaFact]
    public void Controls_are_named_by_their_text_their_tooltip_their_placeholder_or_their_label()
    {
        var withText = new Button { Content = new StackPanel { Children = { new StrokeIcon(), new TextBlock { Text = "Build Solution" } } } };
        var iconOnly = new Button { Content = new StrokeIcon() };
        ToolTip.SetTip(iconOnly, "Save All (Ctrl+Shift+S)");
        var search = new TextBox { Watermark = "Search Solution Explorer" };
        var labeled = new TextBox();
        var picker = new ComboBox();
        var named = new Button { Content = new StrokeIcon() };
        AutomationProperties.SetName(named, "Close the panel");
        var menu = new MenuItem { Header = "_Build Solution" };
        var window = new Window
        {
            Content = new StackPanel
            {
                Children =
                {
                    withText, iconOnly, search,
                    new StackPanel { Children = { new TextBlock { Text = "Branch name" }, labeled } },
                    new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = { new TextBlock { Text = "Configuration:" }, Column(picker, 1) } },
                    named,
                    new Menu { Items = { menu } },
                },
            },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            Assert.Equal("Build Solution", NameOf(withText));
            Assert.Equal("Save All (Ctrl+Shift+S)", NameOf(iconOnly));
            Assert.Equal("Search Solution Explorer", NameOf(search));
            Assert.Equal("Branch name", NameOf(labeled));
            Assert.Equal("Configuration", NameOf(picker));
            Assert.Equal("Close the panel", NameOf(named));
            Assert.Equal("Build Solution", NameOf(menu));

            // A name follows what the control shows.
            ToolTip.SetTip(iconOnly, "Save (Ctrl+S)");
            ((TextBlock)((StackPanel)withText.Content!).Children[1]).Text = "Rebuild Solution";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Save (Ctrl+S)", NameOf(iconOnly));
            Assert.Equal("Rebuild Solution", NameOf(withText));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task The_editor_reads_its_line_and_the_Solution_Explorer_its_files()
    {
        using var fixture = new WorkspaceFixture();
        var solution = await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        var program = Directory.EnumerateFiles(Path.GetDirectoryName(solution)!, "Program.cs", SearchOption.AllDirectories).First();
        await workspace.OpenDocumentAsync(program);
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = workspace } };
        window.Show();
        try
        {
            await UiTestHelpers.WaitUntilAsync(() => Editor(window) is not null, because: "the editor");
            var view = Editor(window)!;
            var editor = view.Editor;
            Assert.StartsWith("Program.cs, line 1 of ", view.AccessibleName, StringComparison.Ordinal);

            editor.TextArea.Caret.Line = 3;
            Dispatcher.UIThread.RunJobs();
            var third = editor.Document.GetText(editor.Document.GetLineByNumber(3)).Trim();
            Assert.Contains($"line 3 of {editor.Document.LineCount}: {(third.Length == 0 ? "blank" : third)}", view.AccessibleName, StringComparison.Ordinal);
            Assert.Equal(view.AccessibleName, ControlAutomationPeer.CreatePeerForElement(editor.TextArea).GetName());

            // Every row of the Solution Explorer is read by its name, never by its class.
            await UiTestHelpers.WaitUntilAsync(() => true);
            var rows = window.GetVisualDescendants().OfType<TreeViewItem>().Where(r => r.IsEffectivelyVisible).ToList();
            Assert.NotEmpty(rows);
            Assert.All(rows, row => Assert.Null(AccessibilityAudit.NameProblem(row)));
            Assert.Contains(rows, row => NameOf(row)?.Contains("Program.cs", StringComparison.Ordinal) == true);

            // And so is every control of the shell.
            var unnamed = window.GetVisualDescendants().OfType<Control>()
                .Where(c => c.IsEffectivelyVisible && AccessibleNames.IsTarget(c) && c.TemplatedParent is null && !AccessibilityAudit.IsContent(c))
                .Select(c => (Control: c, Problem: AccessibilityAudit.NameProblem(c)))
                .Where(c => c.Problem is not null)
                .Select(c => $"{c.Control.GetType().Name}#{c.Control.Name}: {c.Problem}")
                .ToList();
            Assert.True(unnamed.Count == 0, "Controls TalkBack cannot name:\n" + string.Join("\n", unnamed));
        }
        finally
        {
            window.Close();
        }
    }

    private static CodeEditorView? Editor(Window window) =>
        window.GetVisualDescendants().OfType<CodeEditorView>().FirstOrDefault(e => e.IsEffectivelyVisible);

    private static string? NameOf(Control control) => ControlAutomationPeer.CreatePeerForElement(control).GetName();

    private static Control Column(Control control, int column)
    {
        Grid.SetColumn(control, column);
        return control;
    }
}
