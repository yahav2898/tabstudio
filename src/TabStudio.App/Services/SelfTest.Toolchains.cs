using System.Diagnostics;
using System.Globalization;
using TabStudio.App.ViewModels;
using TabStudio.Toolchains.Processes;

namespace TabStudio.App.Services;

/// <summary>The self test's toolchain part: Python and Node.js installed from Alpine Linux's real mirror and run.</summary>
public static partial class SelfTest
{
    /// <summary>The Python project the self test creates.</summary>
    public const string PythonAppName = "SelfTestPython";

    /// <summary>
    /// Creates a Python app from the template, installs Python from the Toolchain Store, presses Ctrl+F5 and answers
    /// the program's question; then installs Node.js and runs <c>node -e</c>: the Linux environment set up from the
    /// real Alpine mirror, packages verified and installed, and programs running through PRoot on the device.
    /// </summary>
    /// <returns>Whether both ran, with a one-line summary.</returns>
    private static async Task<SelfTestResult> RunToolchainsAsync(MainViewModel main, ProjectTemplates templates, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        await main.OpenWorkspaceAsync(templates.Create(ProjectTemplates.PythonId, PythonAppName));
        if (main.Workspace is not { } workspace || workspace.ToolchainProjects.Count == 0)
        {
            return new SelfTestResult(false, "Toolchains: the Python app did not open as a Python project.", clock.Elapsed);
        }

        var store = workspace.ToolchainStore;
        var manager = workspace.Toolchains.Manager;
        if (manager.CanInstall && !manager.IsReady("python"))
        {
            await store.InstallAsync("python").WaitAsync(cancellationToken);
            if (!manager.IsReady("python"))
            {
                return new SelfTestResult(false, $"Toolchains: Python did not install: {manager.Status("python").Error}", clock.Elapsed);
            }
        }

        var installed = clock.Elapsed;
        void Answer(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
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
            await workspace.RunCommand.ExecuteAsync(null).WaitAsync(cancellationToken);
        }
        finally
        {
            workspace.Output.PropertyChanged -= Answer;
        }

        var output = workspace.Output.Text;
        var python = output.Split('\n').FirstOrDefault(l => l.Contains($"Hello, {Name}! This is Python", StringComparison.Ordinal))?.Trim();
        if (python is null || !output.Contains("has exited with code 0", StringComparison.Ordinal))
        {
            return new SelfTestResult(false, $"Toolchains: the Python app did not run: {LastLines(workspace)}", clock.Elapsed);
        }

        if (manager.CanInstall && !manager.IsReady("node"))
        {
            await store.InstallAsync("node").WaitAsync(cancellationToken);
            if (!manager.IsReady("node"))
            {
                return new SelfTestResult(false, $"Toolchains: Node.js did not install: {manager.Status("node").Error}", clock.Elapsed);
            }
        }

        var node = await CommandRunner.RunAsync(
            workspace.Toolchains.Runtime,
            new ProcessStartSpec { Program = "node", Arguments = ["-e", "console.log(`node ${process.version} says ${6 * 7}`)"] },
            null,
            cancellationToken);
        var said = node.Output.Trim();
        var success = node.Succeeded && said.EndsWith("says 42", StringComparison.Ordinal);
        var message = string.Create(
            CultureInfo.InvariantCulture,
            $"Toolchains: installed in {installed.TotalSeconds:0} s, {python} | {(success ? said : $"node -e failed: {node.Exit.Description}: {node.Tail(3)}")} | {clock.Elapsed.TotalSeconds:0} s in all");
        return new SelfTestResult(success, message, clock.Elapsed);
    }
}
