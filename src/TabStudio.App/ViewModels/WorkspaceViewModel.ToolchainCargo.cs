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
            return await BuildNativeToolchainAsync(project, plan, cancellationToken);
        }

        try
        {
            // Cargo writes its progress to standard error: "Compiling …" is ordinary output, "error[E…]" is an error.
            var classifier = new StreamClassifier("rust");
            var result = await CommandRunner.RunAsync(_toolchains.Runtime, plan.Build!, line =>
            {
                if (CargoBuildOutput.Display(line) is { Length: > 0 } text)
                {
                    OutputProcess.WriteClassified(Output, classifier, text.EndsWith('\n') ? text : text + "\n");
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
                // Nothing ran, so there is no exit code to report: the run ends saying what to choose.
                const string message = "Cargo built no unique command to run. Select a package's Cargo.toml as the startup project, or open the main source file of the binary to run.";
                return (plan, OutputProcessResult.NothingToRun(message));
            }

            // The program is the binary Cargo built, such as fd, not its package (fd-find) or its manifest.
            return (plan with { Run = plan.Run with { Program = selected.Path, Arguments = [] }, ProgramName = Path.GetFileNameWithoutExtension(selected.Path) }, built);
        }
        catch (ToolchainRuntimeException ex)
        {
            Output.WriteLine(ex.Message, OutputKind.Failure);
            return (plan, new OutputProcessResult(null, [], ex.Message));
        }
    }
}
