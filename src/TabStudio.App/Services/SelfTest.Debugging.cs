using System.Diagnostics;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Services;

/// <summary>The self test's second part: debugging the sample, as a person tries F9 and F5 after running it.</summary>
public static partial class SelfTest
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Sets a breakpoint where the sample creates its greeter, debugs the sample with F5, checks that the program
    /// stopped there with the name it was given, and continues it to the end.
    /// </summary>
    /// <returns>Whether the debugger worked, with a one-line summary.</returns>
    private static async Task<SelfTestResult> DebugSampleAsync(WorkspaceViewModel workspace, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        var program = workspace.Documents.FirstOrDefault(d => string.Equals(d.FileName, "Program.cs", StringComparison.Ordinal));
        if (program is null || FindLine(program, "new Greeter(") is not { } line)
        {
            return new SelfTestResult(false, "Debugger: the sample's Program.cs is not open.", clock.Elapsed);
        }

        void Answer(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(OutputViewModel.IsWaitingForInput) && workspace.Output.IsWaitingForInput)
            {
                workspace.Output.InputText = Name;
                workspace.Output.SubmitInputCommand.Execute(null);
            }
        }

        var breakpoint = workspace.Breakpoints.Add(program.FilePath, line);
        workspace.Output.PropertyChanged += Answer;
        try
        {
            if (!workspace.StartDebuggingCommand.CanExecute(null))
            {
                return new SelfTestResult(false, $"Debugger: F5 is not available: {workspace.StatusText}.", clock.Elapsed);
            }

            await workspace.StartDebuggingCommand.ExecuteAsync(null).WaitAsync(cancellationToken);
            await WaitAsync(() => workspace.IsPaused || !workspace.IsDebugging, cancellationToken);
            if (!workspace.IsPaused)
            {
                return new SelfTestResult(false, $"Debugger: the program did not stop at line {line}: {workspace.StatusText}.", clock.Elapsed);
            }

            await workspace.PauseShown.WaitAsync(cancellationToken);
            var where = workspace.StatusText;
            var name = workspace.Locals.Tree.Rows.FirstOrDefault(r => string.Equals(r.Name, "name", StringComparison.Ordinal))?.Value;
            workspace.StartDebuggingCommand.Execute(null);
            await WaitAsync(() => !workspace.IsDebugging, cancellationToken);

            var expected = $"\"{Name}\"";
            return string.Equals(name, expected, StringComparison.Ordinal)
                ? new SelfTestResult(true, $"Debugger: {where}, name = {name}", clock.Elapsed)
                : new SelfTestResult(false, $"Debugger: stopped, but the local 'name' was {name ?? "missing"}.", clock.Elapsed);
        }
        finally
        {
            workspace.Output.PropertyChanged -= Answer;
            workspace.Breakpoints.Remove(breakpoint);
        }
    }

    private static int? FindLine(DocumentViewModel document, string text)
    {
        foreach (var line in document.Document.Lines)
        {
            if (document.Document.GetText(line).Contains(text, StringComparison.Ordinal))
            {
                return line.LineNumber;
            }
        }

        return null;
    }

    private static async Task WaitAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        while (!condition())
        {
            await Task.Delay(PollInterval, cancellationToken);
        }
    }
}
