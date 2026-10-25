using System.ComponentModel;
using System.Diagnostics;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Services;

public static partial class SelfTest
{
    /// <summary>Checks F5, console input, a bound breakpoint and Locals on the actual Android toolchain.</summary>
    private static async Task<SelfTestResult> DebugCompiledTemplateAsync(WorkspaceViewModel workspace, string relativeSource, string breakpointText, string localName, string expected, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        var project = workspace.ToolchainProjects[0].Project;
        var source = Path.Combine(project.Directory, relativeSource);
        if (await workspace.OpenDocumentAsync(source) is not { } document || FindLine(document, breakpointText) is not { } line)
        {
            return new(false, $"{project.Toolchain.Name} debugging: the template source did not open", clock.Elapsed);
        }

        var breakpoint = workspace.Breakpoints.Add(source, line);
        void Answer(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(OutputViewModel.IsWaitingForInput) && workspace.Output.IsWaitingForInput)
            {
                workspace.Output.InputText = Name;
                workspace.Output.SubmitInputCommand.Execute(null);
            }
        }

        workspace.Output.PropertyChanged += Answer;
        try
        {
            await workspace.StartDebuggingCommand.ExecuteAsync(null).WaitAsync(cancellationToken);
            await WaitForAsync(() => workspace.IsPaused || (!workspace.IsDebugging && workspace.State != IdeState.Busy), TimeSpan.FromMinutes(3), cancellationToken);
            if (!workspace.IsPaused)
            {
                return new(false, $"{project.Toolchain.Name} debugging: no breakpoint: {LastLines(workspace)}", clock.Elapsed);
            }

            await workspace.PauseShown.WaitAsync(cancellationToken);
            await WaitForAsync(() => workspace.Locals.Tree.Rows.Any(r => r.Name == localName), TimeSpan.FromMinutes(2), cancellationToken);
            var value = workspace.Locals.Tree.Rows.FirstOrDefault(r => r.Name == localName)?.Value;
            workspace.StartDebuggingCommand.Execute(null);
            await WaitForAsync(() => !workspace.IsDebugging, TimeSpan.FromMinutes(2), cancellationToken);
            var success = value?.Contains(expected, StringComparison.Ordinal) == true
                && !workspace.IsDebugging && workspace.Output.Text.Contains($"Hello, {Name}!", StringComparison.Ordinal);
            return new(success, success
                ? $"{project.Toolchain.Name} debugging: stopped at {line}, {localName} = {value}, console input and completed greeting"
                : $"{project.Toolchain.Name} debugging: {localName} = {value ?? "missing"}; {LastLines(workspace)}", clock.Elapsed);
        }
        finally
        {
            workspace.Output.PropertyChanged -= Answer;
            workspace.Breakpoints.Remove(breakpoint);
        }
    }
}
