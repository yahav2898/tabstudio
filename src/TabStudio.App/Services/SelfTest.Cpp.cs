using System.ComponentModel;
using System.Diagnostics;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Services;

public static partial class SelfTest
{
    /// <summary>Installs Clang, CMake, clangd and LLDB from Alpine's real mirror, then runs the console template.</summary>
    private static async Task<SelfTestResult> CheckCppToolchainAsync(MainViewModel main, ProjectTemplates templates, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        await main.OpenWorkspaceAsync(templates.Create(ProjectTemplates.CppId, "SelfTestCpp"));
        var workspace = main.Workspace!;
        if (workspace.Toolchains.Manager.CanInstall)
        {
            await workspace.ToolchainStore.InstallAsync("cpp").WaitAsync(cancellationToken);
        }

        if (!workspace.Toolchains.Manager.IsReady("cpp"))
        {
            return new(false, $"C/C++: install failed: {workspace.Toolchains.Manager.Status("cpp").Error}", clock.Elapsed);
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

        var success = workspace.Output.Text.Contains($"Hello, {Name}! This is C++", StringComparison.Ordinal)
            && workspace.Output.Text.Contains("has exited with code 0", StringComparison.Ordinal);
        return new(success, success ? "C/C++: installed from Alpine, template built and ran with console input" : $"C/C++: {LastLines(workspace)}", clock.Elapsed);
    }
}
