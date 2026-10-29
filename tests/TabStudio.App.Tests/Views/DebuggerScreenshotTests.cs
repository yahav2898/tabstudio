using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Services;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Views;

/// <summary>
/// The debugger in the real IDE window, rendered with Skia: the breakpoint margin, a paused session with Locals and
/// Call Stack, a caller chosen in the Call Stack, DataTips and the exception helper.
/// </summary>
public sealed class DebuggerScreenshotTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly ServiceProvider _services;
    private readonly MainViewModel _main;

    public DebuggerScreenshotTests()
    {
        _services = AppServices.Create(new AppPaths(_temp.Path));
        _main = _services.GetRequiredService<MainViewModel>();
    }

    [AvaloniaFact]
    public async Task Recording_timeline_shows_an_earlier_loop_value_after_exit()
    {
        var window = Show(1280, 800);
        var workspace = await OpenSampleAsync();
        workspace.ActiveDocument!.Document.Text = RecordingTests.Loop;
        await workspace.Watch.AddAsync("total");
        await workspace.StartWithRecordingCommand.ExecuteAsync(null);
        await DebuggingTests.WaitForExitAsync(workspace);
        await workspace.StepBackCommand.ExecuteAsync(null);
        await workspace.JumpToLastWriteCommand.ExecuteAsync("total");
        await workspace.JumpToLastWriteCommand.ExecuteAsync("total");
        await UiTestHelpers.WaitUntilAsync(() => true);
        UiTestHelpers.SaveScreenshot(window, "debug-06-recording-timeline");
        Assert.Equal("3", workspace.Locals.Tree.Rows.Single(r => r.Name == "total").Value);
        Assert.True(workspace.IsHistorical);

        var program = workspace.ActiveDocument!;
        await program.AnalyzeNowAsync();
        var editor = VisibleEditor(window);
        var textArea = editor.Editor.TextArea;
        var textView = textArea.TextView;
        var location = program.Document.GetLocation(program.Document.Text.IndexOf("total +=", StringComparison.Ordinal) + 2);
        var position = textView.GetVisualPosition(new TextViewPosition(location), VisualYPosition.LineMiddle) - textView.ScrollOffset;
        var press = textView.TranslatePoint(position, textArea) ?? throw new InvalidOperationException("The variable is not in view.");
        textArea.RaiseEvent(new HoldingRoutedEventArgs(HoldingState.Started, press, PointerType.Touch));
        await UiTestHelpers.WaitUntilAsync(() => editor.IsDataTipOpen, because: "the historical DataTip");
        await workspace.JumpToLastWriteCommand.ExecuteAsync("total");
        Assert.Equal("1", workspace.Locals.Tree.Rows.Single(r => r.Name == "total").Value);
        Assert.False(editor.IsDataTipOpen);
    }

    [AvaloniaFact]
    public async Task Tapping_the_margin_sets_and_clears_breakpoints()
    {
        var window = Show(1280, 800);
        var workspace = await OpenSampleAsync();
        var editor = VisibleEditor(window);

        window.MouseDown(MarginPoint(editor, window, 15), MouseButton.Left);
        window.MouseUp(MarginPoint(editor, window, 15), MouseButton.Left);
        window.MouseDown(MarginPoint(editor, window, 16), MouseButton.Left);
        window.MouseUp(MarginPoint(editor, window, 16), MouseButton.Left);
        await UiTestHelpers.WaitUntilAsync(() => workspace.Breakpoints.All.Count == 2, because: "two breakpoints");

        UiTestHelpers.SaveScreenshot(window, "debug-01-breakpoints");
        Assert.Equal(new[] { 15, 16 }, workspace.Breakpoints.All.Select(b => b.Line).Order());

        window.MouseDown(MarginPoint(editor, window, 16), MouseButton.Left);
        window.MouseUp(MarginPoint(editor, window, 16), MouseButton.Left);
        await UiTestHelpers.WaitUntilAsync(() => workspace.Breakpoints.All.Count == 1, because: "the second tap to clear it");
        Assert.Equal(15, Assert.Single(workspace.Breakpoints.All).Line);
    }

    [AvaloniaFact]
    public async Task Paused_at_a_breakpoint_with_Locals_Call_Stack_and_a_DataTip()
    {
        var window = Show(1280, 800);
        var workspace = await OpenSampleAsync();
        AnswerInput(workspace, "Ada");
        var program = workspace.ActiveDocument!;
        workspace.Breakpoints.Toggle(program.FilePath, 30);
        workspace.Breakpoints.Toggle(program.FilePath, 33);

        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        await DebuggingTests.WaitForPauseAsync(workspace, 30);
        var language = workspace.Locals.Tree.Rows.Single(r => r.Name == "language");
        await workspace.Locals.Tree.ToggleCommand.ExecuteAsync(language);
        await UiTestHelpers.WaitUntilAsync(() => true);

        UiTestHelpers.SaveScreenshot(window, "debug-02-paused");

        Assert.True(workspace.IsPaused);
        Assert.True(workspace.ShowsCallStackBeside);
        Assert.Contains(workspace.Locals.Tree.Rows, r => r.Name == "Year" && r.Value == "2000");

        // A long press on touch, as Avalonia raises it on the text area, which captures the touch.
        var editor = VisibleEditor(window);
        var textArea = editor.Editor.TextArea;
        var textView = textArea.TextView;
        var location = program.Document.GetLocation(program.Document.Text.IndexOf("in languages", StringComparison.Ordinal) + 5);
        var visual = textView.GetVisualPosition(new TextViewPosition(location), VisualYPosition.LineMiddle) - textView.ScrollOffset;
        var press = textView.TranslatePoint(visual, textArea) ?? throw new InvalidOperationException("The text is not in view.");
        textArea.RaiseEvent(new HoldingRoutedEventArgs(HoldingState.Started, press, PointerType.Touch));
        await UiTestHelpers.WaitUntilAsync(() => editor.IsDataTipOpen, because: "the DataTip");
        await UiTestHelpers.WaitUntilAsync(() => true);

        // The same press raises the context menu; on a value it stays closed and the DataTip shows alone.
        Assert.False(editor.Editor.ContextMenu?.IsOpen ?? false, "The context menu opened over the DataTip.");
        UiTestHelpers.SaveScreenshot(window, "debug-03-datatip");

        await StopAsync(workspace);
        Assert.False(editor.IsDataTipOpen);
    }

    [AvaloniaFact]
    public async Task A_caller_chosen_in_the_Call_Stack()
    {
        var window = Show(1280, 800);
        var workspace = await OpenSampleAsync();
        AnswerInput(workspace, "Ada");
        var greeterPath = Path.Combine(workspace.Workspace!.Projects.Single(p => p.Name == "Greetings").DirectoryPath, "Greeter.cs");
        workspace.Breakpoints.Toggle(greeterPath, 25);

        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        await DebuggingTests.WaitForPauseAsync(workspace, 25);
        workspace.SelectPanelCommand.Execute(BottomPanel.CallStack);
        workspace.CallStack.Activate(workspace.CallStack.Frames[1]);
        await UiTestHelpers.WaitUntilAsync(() => workspace.ActiveDocument?.CallerStatement is not null, because: "the caller");

        UiTestHelpers.SaveScreenshot(window, "debug-04-call-stack");

        Assert.Equal("Program.cs", workspace.ActiveDocument!.FileName);
        Assert.True(workspace.IsCallStackSelected);
        await StopAsync(workspace);
    }

    [AvaloniaFact]
    public async Task The_exception_helper_at_the_line_that_threw()
    {
        var window = Show(1280, 800);
        var workspace = await OpenSampleAsync();
        var program = workspace.ActiveDocument!;
        program.Document.Text = """
            var orders = new List<decimal>();
            Console.WriteLine($"Orders today: {orders.Count}");
            var largest = orders.Max();
            Console.WriteLine($"Largest order: {largest}");
            """;

        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => program.ExceptionHelper is not null, because: "the exception");
        await workspace.PauseShown;
        var editor = VisibleEditor(window);
        await UiTestHelpers.WaitUntilAsync(() => editor.IsExceptionHelperOpen, because: "the exception helper");
        program.ExceptionHelper!.ToggleDetailsCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => true);

        UiTestHelpers.SaveScreenshot(window, "debug-05-exception-helper");

        Assert.Equal("System.InvalidOperationException", program.ExceptionHelper.ExceptionType);
        await StopAsync(workspace);
        Assert.False(editor.IsExceptionHelperOpen);
    }

    public void Dispose()
    {
        _main.Workspace?.Dispose();
        _services.Dispose();
        _temp.Dispose();
    }

    private static CodeEditorView VisibleEditor(MainWindow window) =>
        window.GetVisualDescendants().OfType<CodeEditorView>().First(e => e.IsVisible);

    /// <summary>The middle of a line in the breakpoint margin, in window coordinates.</summary>
    private static Point MarginPoint(CodeEditorView editor, MainWindow window, int line)
    {
        var margin = editor.GlyphMargin;
        var textView = editor.Editor.TextArea.TextView;
        var visualLine = textView.GetVisualLine(line) ?? throw new InvalidOperationException($"Line {line} is not in view.");
        var y = visualLine.GetTextLineVisualYPosition(visualLine.TextLines[0], VisualYPosition.LineMiddle) - textView.VerticalOffset;
        return margin.TranslatePoint(new Point(margin.Bounds.Width / 2, y), window) ?? throw new InvalidOperationException("The margin is not in the window.");
    }

    private static void AnswerInput(WorkspaceViewModel workspace, string line) =>
        workspace.Output.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(OutputViewModel.IsWaitingForInput) && workspace.Output.IsWaitingForInput)
            {
                workspace.Output.InputText = line;
                workspace.Output.SubmitInputCommand.Execute(null);
            }
        };

    private static async Task StopAsync(WorkspaceViewModel workspace)
    {
        workspace.StopDebuggingCommand.Execute(null);
        await DebuggingTests.WaitForExitAsync(workspace);
    }

    private MainWindow Show(double width, double height)
    {
        var window = new MainWindow { DataContext = _main, Width = width, Height = height };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return window;
    }

    private async Task<WorkspaceViewModel> OpenSampleAsync()
    {
        var solution = await _services.GetRequiredService<SampleLibrary>().EnsureExtractedAsync();
        await _main.OpenWorkspaceAsync(solution);
        var workspace = _main.Workspace!;
        var document = workspace.ActiveDocument!;
        await document.AnalyzeNowAsync();
        await UiTestHelpers.WaitUntilAsync(() => document.Classifications.Count > 0, because: "syntax colors");
        return workspace;
    }
}
