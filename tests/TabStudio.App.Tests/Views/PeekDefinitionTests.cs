using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.Views;
using static TabStudio.App.Tests.Views.EditorHarness;

namespace TabStudio.App.Tests.Views;

/// <summary>Peek Definition in the real editor: Alt+F12 shows the definition under the line, and the title opens it.</summary>
public sealed class PeekDefinitionTests
{
    [AvaloniaFact]
    public async Task Alt_F12_shows_the_definition_under_the_line_and_Escape_closes_it()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var window = Show(fixture);
        var view = ActiveEditor(window);
        view.Editor.CaretOffset = OffsetOf(view.Editor, "new Greeter(") + "new Gree".Length;

        window.KeyPressQwerty(PhysicalKey.F12, RawInputModifiers.Alt);
        await UiTestHelpers.WaitUntilAsync(() => view.Peek is not null, because: "the peek");

        var peek = view.Peek!;
        Assert.Equal("Greeter.cs", peek.Current.FileName);
        Assert.StartsWith("Greeter.cs, line ", peek.Title, StringComparison.Ordinal);
        Assert.Contains("public sealed class Greeter", view.PeekView!.Text, StringComparison.Ordinal);
        Assert.Contains("Greeter", view.PeekView.Text.Split('\n')[view.PeekView.MarkedLine - 1], StringComparison.Ordinal);
        Assert.Equal("Program.cs", fixture.Workspace.ActiveDocument?.FileName);

        // It opens at a whole line, with a gap between the line numbers and the code.
        var peekEditor = view.PeekView.FindControl<AvaloniaEdit.TextEditor>("Editor")!;
        var expected = view.PeekView.TopOffsetOf(view.PeekView.MarkedLine);
        await UiTestHelpers.WaitUntilAsync(
            () => Math.Abs(peekEditor.VerticalOffset - Math.Min(expected, Math.Max(0, peekEditor.ExtentHeight - peekEditor.ViewportHeight))) < 0.5,
            because: "the peek to scroll to its line");
        Assert.Contains(peekEditor.TextArea.LeftMargins, margin => margin.Name == "PeekGutterGap" && margin.Width == PeekDefinitionView.GutterGap);
        UiTestHelpers.SaveScreenshot(window, "14-peek-definition");

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        await UiTestHelpers.WaitUntilAsync(() => view.Peek is null, because: "Escape to close the peek");
        window.Close();
    }

    [AvaloniaFact]
    public async Task The_peek_title_opens_the_definition_as_a_tab()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var window = Show(fixture);
        var view = ActiveEditor(window);
        view.Editor.CaretOffset = OffsetOf(view.Editor, "greeter.Greet()") + "greeter.Gr".Length;

        await fixture.Workspace.PeekDefinitionCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => view.Peek is not null, because: "the peek");
        Assert.Contains("public string Greet()", view.PeekView!.Text.Split('\n')[view.PeekView.MarkedLine - 1], StringComparison.Ordinal);

        view.PeekView.FindControl<Button>("TitleButton")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        await UiTestHelpers.WaitUntilAsync(() => fixture.Workspace.ActiveDocument?.FileName == "Greeter.cs", because: "the definition to open");

        Assert.Null(view.Peek);
        window.Close();
    }

    private static Window Show(WorkspaceFixture fixture)
    {
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = fixture.Workspace } };
        window.Show();
        return window;
    }
}
