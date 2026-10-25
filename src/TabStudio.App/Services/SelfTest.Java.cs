using System.ComponentModel;
using System.Diagnostics;
using TabStudio.App.ViewModels;
using TabStudio.Toolchains.Processes;

namespace TabStudio.App.Services;

public static partial class SelfTest
{
    /// <summary>Installs Java, JDT LS and java-debug from Alpine's real mirror, then runs the console template.</summary>
    private static async Task<SelfTestResult> CheckJavaToolchainAsync(MainViewModel main, ProjectTemplates templates, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        await main.OpenWorkspaceAsync(templates.Create(ProjectTemplates.JavaId, "SelfTestJava"));
        var workspace = main.Workspace!;
        if (workspace.Toolchains.Manager.CanInstall)
        {
            await workspace.ToolchainStore.InstallAsync("java").WaitAsync(cancellationToken);
        }

        if (!workspace.Toolchains.Manager.IsReady("java"))
        {
            return new(false, $"Java: install failed: {workspace.Toolchains.Manager.Status("java").Error}", clock.Elapsed);
        }

        var started = await OutputProcess.RunAsync(workspace.Toolchains.Runtime, new ProcessStartSpec
        {
            Program = "java", Arguments = ["-Xlog:os=debug:stderr", "-version"],
        }, workspace.Output, null, cancellationToken);
        if (!started.Succeeded)
        {
            await OutputProcess.RunAsync(workspace.Toolchains.Runtime, new ProcessStartSpec
            {
                Program = "apk", Arguments = ["add", "--no-cache", "strace"],
            }, workspace.Output, null, cancellationToken);
            await OutputProcess.RunAsync(workspace.Toolchains.Runtime, new ProcessStartSpec
            {
                Program = "strace", Arguments = ["-f", "-e", "trace=mmap,mprotect", "java", "-version"],
            }, workspace.Output, null, cancellationToken);
            return new(false, $"Java VM startup: {LastLines(workspace)}", clock.Elapsed);
        }

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
            await workspace.RunCommand.ExecuteAsync(null).WaitAsync(cancellationToken);
        }
        finally
        {
            workspace.Output.PropertyChanged -= Answer;
        }

        var success = workspace.Output.Text.Contains($"Hello, {Name}! This is Java", StringComparison.Ordinal)
            && workspace.Output.Text.Contains("has exited with code 0", StringComparison.Ordinal);
        if (success)
        {
            var debugged = await DebugCompiledTemplateAsync(workspace, "src/main/java/com/example/App.java", "return message", "answer", "42", cancellationToken);
            if (!debugged.Success)
            {
                return debugged;
            }

            return new(true, debugged.Message, clock.Elapsed);
        }

        return new(success, success ? "Java: installed from Alpine, template built and ran with console input" : $"Java: {LastLines(workspace)}", clock.Elapsed);
    }
}
