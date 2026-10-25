using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using TabStudio.App.ViewModels;
using TabStudio.LanguageServers.Hosting;

namespace TabStudio.App.Services;

/// <summary>
/// The self test's part for the language server and debug adapter that come with a toolchain: in the Python app the
/// toolchains part installed Python into, Pyright starts for <c>main.py</c> by itself and squiggles a mistake typed
/// into it, and F5 debugs the app with debugpy to a breakpoint, with the program's question answered in the Output
/// panel and a local read in the Locals panel.
/// </summary>
public static partial class SelfTest
{
    private const string PythonMistake = "print(undefined_name)\n";
    private static readonly TimeSpan PythonToolTimeout = TimeSpan.FromMinutes(2);

    /// <summary>Checks Pyright and debugpy in the Python app that is open, with Python installed.</summary>
    /// <returns>Whether both worked, with a one-line summary.</returns>
    private static async Task<SelfTestResult> CheckPythonToolsAsync(MainViewModel main, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        if (main.Workspace is not { ToolchainProjects: [{ } app, ..] } workspace || !workspace.Toolchains.Manager.IsReady("python"))
        {
            return new SelfTestResult(false, "Python tools: the Python app is not open with Python installed.", clock.Elapsed);
        }

        var path = Path.Combine(app.Project.Directory, "main.py");
        if (await workspace.OpenDocumentAsync(path) is not { } document)
        {
            return new SelfTestResult(false, "Python tools: main.py did not open.", clock.Elapsed);
        }

        // Pyright started when Python was installed, since main.py was open; it may still be starting.
        if (!await WaitForAsync(() => document.LanguageServer?.ServerState is LanguageServerState.Running or LanguageServerState.Failed, PythonToolTimeout, cancellationToken)
            || document.LanguageServer is not { ServerState: LanguageServerState.Running } server)
        {
            return new SelfTestResult(false, $"Python tools: Pyright did not start: {document.LanguageServer?.ServerMessage ?? workspace.LanguageServerText ?? workspace.StatusText}", clock.Elapsed);
        }

        var ready = clock.Elapsed;
        var offset = document.Document.TextLength;
        document.Document.Insert(offset, PythonMistake);
        var squiggled = await WaitForAsync(() => document.Diagnostics.Any(d => d.Message.Contains("undefined_name", StringComparison.Ordinal)), PythonToolTimeout, cancellationToken);
        var diagnostic = document.Diagnostics.FirstOrDefault(d => d.Message.Contains("undefined_name", StringComparison.Ordinal));
        document.Document.Remove(offset, PythonMistake.Length);
        if (!squiggled || diagnostic is null)
        {
            return new SelfTestResult(false, $"Python tools: {server.ServerName} did not squiggle undefined_name.", clock.Elapsed);
        }

        var debugged = await DebugPythonAppAsync(workspace, document, cancellationToken);
        var pyright = string.Create(CultureInfo.InvariantCulture, $"Python tools: {server.ServerName} ready in {ready.TotalSeconds:0} s, squiggled undefined_name ({diagnostic.Id})");
        return new SelfTestResult(debugged.Success, $"{pyright} | {debugged.Message}", clock.Elapsed);
    }

    /// <summary>Sets a breakpoint in the Python app, debugs it with F5, answers its question, reads a local and lets it end.</summary>
    private static async Task<SelfTestResult> DebugPythonAppAsync(WorkspaceViewModel workspace, DocumentViewModel document, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        if (FindLine(document, "return f\"Hello, {name}!") is not { } line)
        {
            return new SelfTestResult(false, "debugpy: main.py lost its greeting.", clock.Elapsed);
        }

        void Answer(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(OutputViewModel.IsWaitingForInput) && workspace.Output.IsWaitingForInput)
            {
                workspace.Output.InputText = Name;
                workspace.Output.SubmitInputCommand.Execute(null);
            }
        }

        var breakpoint = workspace.Breakpoints.Add(document.FilePath, line);
        workspace.Output.PropertyChanged += Answer;
        try
        {
            await workspace.StartDebuggingCommand.ExecuteAsync(null).WaitAsync(cancellationToken);
            await WaitForAsync(() => workspace.IsPaused || (!workspace.IsDebugging && workspace.State != IdeState.Busy), PythonToolTimeout, cancellationToken);
            if (!workspace.IsPaused)
            {
                return new SelfTestResult(false, $"debugpy: the app did not stop at line {line}: {workspace.StatusText} {LastLines(workspace)}", clock.Elapsed);
            }

            await workspace.PauseShown.WaitAsync(cancellationToken);
            await WaitForAsync(() => workspace.Locals.Tree.Rows.Any(r => r.Name == "name"), PythonToolTimeout, cancellationToken);
            var where = workspace.StatusText;
            var name = workspace.Locals.Tree.Rows.FirstOrDefault(r => r.Name == "name")?.Value;
            workspace.StartDebuggingCommand.Execute(null);
            await WaitForAsync(() => !workspace.IsDebugging, PythonToolTimeout, cancellationToken);

            var greeted = workspace.Output.Text.Contains($"Hello, {Name}! This is Python", StringComparison.Ordinal);
            return name == $"'{Name}'" && greeted
                ? new SelfTestResult(true, string.Create(CultureInfo.InvariantCulture, $"debugpy: {where}, name = {name}, in {clock.Elapsed.TotalSeconds:0} s"), clock.Elapsed)
                : new SelfTestResult(false, $"debugpy: stopped, but name was {name ?? "missing"}, and the output {(greeted ? "had" : "lacked")} the greeting: {LastLines(workspace)}", clock.Elapsed);
        }
        finally
        {
            workspace.Output.PropertyChanged -= Answer;
            workspace.Breakpoints.Remove(breakpoint);
        }
    }

    /// <summary>What the IDE was doing, for a self test that ran out of time.</summary>
    private static string DescribeActivity(MainViewModel main) =>
        main.Workspace is { } workspace
            ? $" (while: {workspace.StatusText}{(workspace.ToolchainStore.Items.FirstOrDefault(i => i.IsBusy) is { } busy ? $"; {busy.Name}: {busy.ProgressText}" : string.Empty)})"
            : string.Empty;
}
