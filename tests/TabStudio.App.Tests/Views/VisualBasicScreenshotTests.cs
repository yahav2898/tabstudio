using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TabStudio.App.Services;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using static TabStudio.App.Tests.Views.EditorHarness;

namespace TabStudio.App.Tests.Views;

/// <summary>
/// The Visual Basic console template in the IDE, compiled in the app as C# is: Roslyn's completion in the editor, F5
/// to a breakpoint with Locals and the Call Stack, and the program's input answered in the Output panel.
/// </summary>
public sealed class VisualBasicScreenshotTests
{
    [AvaloniaFact]
    public async Task A_Visual_Basic_console_app_gets_completion_and_stops_at_a_breakpoint()
    {
        using var fixture = new WorkspaceFixture();
        var workspace = fixture.Workspace;
        var solution = new ProjectTemplates(fixture.Paths).Create(ProjectTemplates.VisualBasicConsoleId, "Greeter");
        await workspace.LoadAsync(solution);
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = workspace } };
        window.Show();
        var program = Path.Combine(Path.GetDirectoryName(solution)!, "Greeter", "Program.vb");
        var document = (await workspace.OpenDocumentAsync(program))!;
        await UiTestHelpers.WaitUntilAsync(() => workspace.ActiveDocument == document, because: "Program.vb in the editor");

        var view = ActiveEditor(window);
        StartLineAfter(view.Editor, "Dim answer = 42");
        Type(view.Editor, "Console.Wri");
        await UiTestHelpers.WaitUntilAsync(() => view.Completion!.IsOpen && view.Completion.Items.Any(i => i.Entry.DisplayText == "WriteLine"), because: "Roslyn's Visual Basic completion");
        UiTestHelpers.SaveScreenshot(window, "vb-01-completion");
        view.Completion!.Close();
        document.Document.Text = File.ReadAllText(program);

        var line = document.Document.Lines.First(l => document.Document.GetText(l).Contains("Return $\"Hello", StringComparison.Ordinal)).LineNumber;
        workspace.Breakpoints.Toggle(document.FilePath, line);
        AnswerInput(workspace, "Ada");
        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.IsPaused, TimeSpan.FromSeconds(60), because: "the Visual Basic breakpoint");
        await UiTestHelpers.WaitUntilAsync(() => workspace.Locals.Tree.Rows.Any(r => r.Name == "answer"), because: "the locals");
        Assert.Contains(workspace.Locals.Tree.Rows, r => r.Name == "answer" && r.Value == "42");
        Assert.Contains(workspace.CallStack.Frames, f => f.MethodName.Contains("Greet", StringComparison.Ordinal));
        UiTestHelpers.SaveScreenshot(window, "vb-02-breakpoint");

        workspace.StartDebuggingCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => !workspace.IsDebugging, TimeSpan.FromSeconds(60), because: "the program to end");
        Assert.Contains("Hello, Ada! This is Visual Basic on TabStudio. The answer is 42.", workspace.Output.Text, StringComparison.Ordinal);
        window.Close();
    }

    private static void AnswerInput(WorkspaceViewModel workspace, string answer)
    {
        workspace.Output.PropertyChanged += Answer;

        void Answer(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(OutputViewModel.IsWaitingForInput) && workspace.Output.IsWaitingForInput)
            {
                workspace.Output.PropertyChanged -= Answer;
                workspace.Output.InputText = answer;
                workspace.Output.SubmitInputCommand.Execute(null);
            }
        }
    }
}
