using System.Diagnostics;
using TabStudio.App.ViewModels;
using TabStudio.Compilation.Features;
using TabStudio.Core.Diagnostics;
using TabStudio.LanguageServers.Hosting;
using TabStudio.LanguageServers.Loopback;

namespace TabStudio.App.Services;

/// <summary>
/// The self test's language server part: a script of the built-in loopback language opens with IntelliSense from the
/// loopback language server, its warning reaches the Error List, and the loopback debug adapter stops it at a
/// breakpoint with its locals in the Locals panel, as Python with Pyright and debugpy would on a device that has them.
/// </summary>
public static partial class SelfTest
{
    private const string LoopbackSource = """
        # TabStudio's self test: a script in the built-in loopback language.
        let name = "Tablet"
        let count = 2
        print name
        shout name
        print count
        """;

    /// <summary>Opens a loopback script in a new folder, checks its IntelliSense, then debugs it.</summary>
    /// <returns>Whether the language server and the debug adapter worked, with a one-line summary.</returns>
    private static async Task<SelfTestResult> CheckLanguageServersAsync(MainViewModel main, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();

        // Beside the sample, in the app's own storage, which is writable on every platform.
        var sample = main.Workspace?.Workspace?.RootDirectory ?? Path.GetTempPath();
        var folder = Path.Combine(Path.GetDirectoryName(sample.TrimEnd(Path.DirectorySeparatorChar)) ?? sample, "SelfTest.Loopback");
        Directory.CreateDirectory(Path.Combine(folder, ".tabstudio"));
        await File.WriteAllTextAsync(Path.Combine(folder, ".tabstudio", "settings.json"), LoopbackLauncher.Settings, cancellationToken);
        var path = Path.Combine(folder, "greet" + LoopbackScript.Extension);
        await File.WriteAllTextAsync(path, LoopbackSource, cancellationToken);

        await main.OpenWorkspaceAsync(folder);
        if (main.Workspace is not { } workspace || await workspace.OpenDocumentAsync(path) is not { } document)
        {
            return new SelfTestResult(false, "Language servers: the loopback script did not open.", clock.Elapsed);
        }

        if (document.LanguageServer is not { } server || document.Features is not { } features)
        {
            return new SelfTestResult(false, $"Language servers: the loopback script has no language server: {workspace.StatusText}.", clock.Elapsed);
        }

        await WaitAsync(() => server.ServerState is LanguageServerState.Running or LanguageServerState.Failed, cancellationToken);
        await WaitAsync(() => workspace.ErrorList.AllDiagnostics.Any(d => d.Id == "LB0003") || server.ServerState == LanguageServerState.Failed, cancellationToken);
        if (server.ServerState == LanguageServerState.Failed)
        {
            return new SelfTestResult(false, $"Language servers: {server.ServerName} failed: {server.ServerMessage}.", clock.Elapsed);
        }

        var warning = workspace.ErrorList.AllDiagnostics.First(d => d.Id == "LB0003");
        var offset = LoopbackSource.IndexOf("print count", StringComparison.Ordinal) + "print c".Length;
        var completion = await features.GetCompletionsAsync(path, offset, CompletionTrigger.Invoke, cancellationToken);
        var quickInfo = await features.GetQuickInfoAsync(path, LoopbackSource.IndexOf("print name", StringComparison.Ordinal) + "print n".Length, cancellationToken);
        if (!completion.Items.Any(i => i.DisplayText == "count") || quickInfo is null)
        {
            return new SelfTestResult(false, "Language servers: the loopback server answered no completion or Quick Info.", clock.Elapsed);
        }

        var debugged = await DebugLoopbackScriptAsync(workspace, document, cancellationToken);
        var severity = warning.Severity == DiagnosticSeverityLevel.Warning ? "warning" : warning.Severity.ToString();
        return new SelfTestResult(
            debugged.Success,
            $"Language servers: {server.ServerName} ready, {severity} {warning.Id} on line {warning.Location?.Line}, completion and Quick Info | {debugged.Message}",
            clock.Elapsed);
    }

    /// <summary>Sets a breakpoint in the loopback script, debugs it with F5, reads a local, and continues it to the end.</summary>
    private static async Task<SelfTestResult> DebugLoopbackScriptAsync(WorkspaceViewModel workspace, DocumentViewModel document, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        if (FindLine(document, "print name") is not { } line)
        {
            return new SelfTestResult(false, "Debug adapters: the loopback script lost its print line.", clock.Elapsed);
        }

        var breakpoint = workspace.Breakpoints.Add(document.FilePath, line);
        try
        {
            if (!workspace.StartDebuggingCommand.CanExecute(null))
            {
                return new SelfTestResult(false, $"Debug adapters: F5 is not available for the script: {workspace.StatusText}.", clock.Elapsed);
            }

            await workspace.StartDebuggingCommand.ExecuteAsync(null).WaitAsync(cancellationToken);
            await WaitAsync(() => workspace.IsPaused || (!workspace.IsDebugging && workspace.State != IdeState.Busy), cancellationToken);
            if (!workspace.IsPaused)
            {
                return new SelfTestResult(false, $"Debug adapters: the script did not stop at line {line}: {workspace.StatusText}.", clock.Elapsed);
            }

            await workspace.PauseShown.WaitAsync(cancellationToken);
            await WaitAsync(() => workspace.Locals.Tree.Rows.Any(r => r.Name == "name"), cancellationToken);
            var where = workspace.StatusText;
            var name = workspace.Locals.Tree.Rows.First(r => r.Name == "name").Value;
            var frames = workspace.CallStack.Frames.Count;
            workspace.StartDebuggingCommand.Execute(null);
            await WaitAsync(() => !workspace.IsDebugging, cancellationToken);

            var printed = workspace.Output.Text.Contains("Tablet", StringComparison.Ordinal);
            return name == "\"Tablet\"" && frames > 0 && printed
                ? new SelfTestResult(true, $"Debug adapters: {where}, name = {name}", clock.Elapsed)
                : new SelfTestResult(false, $"Debug adapters: stopped, but name was {name} with {frames} frames, and the output {(printed ? "had" : "lacked")} the greeting.", clock.Elapsed);
        }
        finally
        {
            workspace.Breakpoints.Remove(breakpoint);
        }
    }
}
