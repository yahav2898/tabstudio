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
        var traced = workspace.TraceDebugAdapterMessages;
        workspace.TraceDebugAdapterMessages = true;
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
            await workspace.StartDebuggingCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromMinutes(3), cancellationToken);
            await WaitForAsync(() => workspace.IsPaused || (!workspace.IsDebugging && workspace.State != IdeState.Busy), TimeSpan.FromMinutes(3), cancellationToken);
            if (!workspace.IsPaused)
            {
                return new(false, $"{project.Toolchain.Name} debugging: no breakpoint: {CompiledDebugFailure(workspace)}", clock.Elapsed);
            }

            await workspace.PauseShown.WaitAsync(cancellationToken);
            await WaitForAsync(() => workspace.Locals.Tree.Rows.Any(r => r.Name == localName), TimeSpan.FromMinutes(2), cancellationToken);
            var value = workspace.Locals.Tree.Rows.FirstOrDefault(r => r.Name == localName)?.Value;
            workspace.Breakpoints.Remove(breakpoint);
            workspace.StartDebuggingCommand.Execute(null);
            await WaitForAsync(() => !workspace.IsDebugging, TimeSpan.FromMinutes(2), cancellationToken);
            var success = value?.Contains(expected, StringComparison.Ordinal) == true
                && !workspace.IsDebugging && workspace.Output.Text.Contains($"Hello, {Name}!", StringComparison.Ordinal);
            return new(success, success
                ? $"{project.Toolchain.Name} debugging: stopped at {line}, {localName} = {value}, console input and completed greeting"
                : $"{project.Toolchain.Name} debugging: {localName} = {value ?? "missing"}; {CompiledDebugFailure(workspace)}", clock.Elapsed);
        }
        catch (TimeoutException)
        {
            return new(false, $"{project.Toolchain.Name} debugging: launch timed out: {CompiledDebugFailure(workspace)}", clock.Elapsed);
        }
        finally
        {
            workspace.TraceDebugAdapterMessages = traced;
            workspace.Output.PropertyChanged -= Answer;
            workspace.Breakpoints.Remove(breakpoint);
        }
    }

    private static string CompiledDebugFailure(WorkspaceViewModel workspace)
    {
        var lines = workspace.DebugAdapterOutput.Text.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        var servers = workspace.LanguageServerOutput.Text.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l));
        return LastLines(workspace) + " | " + workspace.StatusText + " | Locals: " + string.Join(", ", workspace.Locals.Tree.Rows.Select(r => r.Name + "=" + r.Value)) + " | Adapter: " + string.Join(" | ", lines.Take(20).Concat(lines.TakeLast(20)).Distinct()) + " | Language servers: " + string.Join(" | ", servers.TakeLast(8));
    }

}
