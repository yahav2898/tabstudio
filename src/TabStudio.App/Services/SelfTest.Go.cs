using System.ComponentModel;
using System.Diagnostics;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Services;

public static partial class SelfTest
{
    /// <summary>Installs Go, gopls and Delve from Alpine's real mirror, then runs the console template.</summary>
    private static async Task<SelfTestResult> CheckGoToolchainAsync(MainViewModel main, ProjectTemplates templates, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        await main.OpenWorkspaceAsync(templates.Create(ProjectTemplates.GoId, "SelfTestGo"));
        var workspace = main.Workspace!;
        if (workspace.Toolchains.Manager.CanInstall && !workspace.Toolchains.Manager.IsReady("go"))
        {
            await workspace.ToolchainStore.InstallAsync("go").WaitAsync(cancellationToken);
        }

        if (!workspace.Toolchains.Manager.IsReady("go"))
        {
            return new(false, $"Go: install failed: {workspace.Toolchains.Manager.Status("go").Error}", clock.Elapsed);
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

        var success = workspace.Output.Text.Contains($"Hello, {Name}! This is Go", StringComparison.Ordinal)
            && workspace.Output.Text.Contains("has exited with code 0", StringComparison.Ordinal);
        if (success)
        {
            var debugged = await DebugCompiledTemplateAsync(workspace, "main.go", "return message", "name", Name, cancellationToken);
            if (!debugged.Success)
            {
                return debugged;
            }

            return new(true, debugged.Message, clock.Elapsed);
        }

        return new(success, success ? "Go: installed from Alpine, template built and ran with console input" : $"Go: {LastLines(workspace)}", clock.Elapsed);
    }
}
