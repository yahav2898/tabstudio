using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Projects;

namespace TabStudio.App.ViewModels;

public sealed partial class WorkspaceViewModel
{
    /// <summary>Cargo reports actual binary artifacts; all other toolchains keep their ordinary build path.</summary>
    private async Task<(RunPlan Plan, OutputProcessResult Result)> BuildCargoOrToolchainAsync(ToolchainProject project, RunPlan plan, CancellationToken cancellationToken)
    {
        if (project.Toolchain.Id != "rust")
        {
            return (plan, await OutputProcess.RunAsync(_toolchains.Runtime, plan.Build!, Output, null, cancellationToken));
        }

        try
        {
            var result = await CommandRunner.RunAsync(_toolchains.Runtime, plan.Build!, line =>
            {
                if (CargoBuildOutput.Display(line) is { Length: > 0 } text)
                {
                    Output.WriteLine(text, OutputKind.Program);
                }
            }, cancellationToken);
            var built = new OutputProcessResult(result.Exit, CargoBuildOutput.Diagnostics(result.Output));
            if (!result.Succeeded)
            {
                return (plan, built);
            }

            var artifacts = CargoBuildOutput.Executables(result.Output);
            var active = ActiveDocument?.FilePath;
            var selected = artifacts.Count == 1 ? artifacts[0]
                : artifacts.FirstOrDefault(a => a.Source == active);
            if (selected is null)
            {
                const string message = "Cargo built no unique command to run. Select a package's Cargo.toml as the startup project, or open the main source file of the binary to run.";
                Output.WriteLine(message, OutputKind.Failure);
                return (plan, new OutputProcessResult(new ProcessExit(1, null, false), [message]));
            }

            return (plan with { Run = plan.Run with { Program = selected.Path, Arguments = [] } }, built);
        }
        catch (ToolchainRuntimeException ex)
        {
            Output.WriteLine(ex.Message, OutputKind.Failure);
            return (plan, new OutputProcessResult(null, [], ex.Message));
        }
    }
}
