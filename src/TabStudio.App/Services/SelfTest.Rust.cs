using System.ComponentModel;
using System.Diagnostics;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Services;

public static partial class SelfTest
{
    /// <summary>Installs Rust, Cargo, rust-analyzer and LLDB from Alpine's real mirror, then runs the console template.</summary>
    private static async Task<SelfTestResult> CheckRustToolchainAsync(MainViewModel main, ProjectTemplates templates, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        await main.OpenWorkspaceAsync(templates.Create(ProjectTemplates.RustId, "SelfTestRust"));
        var workspace = main.Workspace!;
        if (workspace.Toolchains.Manager.CanInstall)
        {
            await workspace.ToolchainStore.InstallAsync("rust").WaitAsync(cancellationToken);
        }

        if (!workspace.Toolchains.Manager.IsReady("rust"))
        {
            return new(false, $"Rust: install failed: {workspace.Toolchains.Manager.Status("rust").Error}", clock.Elapsed);
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

        var success = workspace.Output.Text.Contains($"Hello, {Name}! This is Rust", StringComparison.Ordinal)
            && workspace.Output.Text.Contains("has exited with code 0", StringComparison.Ordinal);
        if (success)
        {
            var debugged = await DebugCompiledTemplateAsync(workspace, "src/main.rs", "return message", "answer", "42", cancellationToken);
            if (!debugged.Success)
            {
                return debugged;
            }

            return new(true, debugged.Message, clock.Elapsed);
        }

        return new(success, success ? "Rust: installed from Alpine, template built and ran with console input" : $"Rust: {LastLines(workspace)}", clock.Elapsed);
    }
}
