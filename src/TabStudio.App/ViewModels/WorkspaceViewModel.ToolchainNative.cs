using System.Text.Json;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Projects;

namespace TabStudio.App.ViewModels;

public sealed partial class WorkspaceViewModel
{
    private async Task<(RunPlan Plan, OutputProcessResult Result)> BuildNativeToolchainAsync(ToolchainProject project, RunPlan plan, CancellationToken cancellationToken)
    {
        foreach (var step in plan.Preparation ?? [])
        {
            Output.WriteLine("> " + step.CommandLine, OutputKind.Info);
            var prepared = await OutputProcess.RunAsync(_toolchains.Runtime, step, Output, null, cancellationToken, toolchain: project.Toolchain.Id);
            if (!prepared.Succeeded)
            {
                return (plan, prepared);
            }
        }

        var compiled = await BuildJavaOrToolchainAsync(project, plan, cancellationToken);
        plan = compiled.Plan;
        var built = compiled.Result;
        if (!built.Succeeded || project.Toolchain.Id != "cpp")
        {
            return (plan, built);
        }

        try
        {
            string? executable;
            string message;
            if (plan.EntryPoint == "CMakeLists.txt")
            {
                var build = Path.Combine(project.Directory, ".tabstudio", "cmake");
                var database = Path.Combine(build, "compile_commands.json");
                if (File.Exists(database))
                {
                    File.Copy(database, Path.Combine(project.Directory, "compile_commands.json"), overwrite: true);
                }

                var targets = CMakeProject.Executables(build, project.Directory);
                executable = targets.Count == 1 ? targets[0].Path : targets.FirstOrDefault(t => t.Sources.Contains(ActiveDocument?.FilePath, StringComparer.Ordinal))?.Path;
                message = targets.Count == 0
                    ? "The CMake build has no executable target to run. Add one with add_executable, or choose the program in .tabstudio/launch.json."
                    : $"The CMake build has {targets.Count} executable targets ({string.Join(", ", targets.Take(4).Select(t => Path.GetFileName(t.Path)))}). Open a source file of the one to run, or choose the program in .tabstudio/launch.json.";
            }
            else if (project.Marker is { } marker && ToolchainProjectDetector.IsMakefile(marker))
            {
                var binaries = NativePrograms.Find(project.Directory);
                executable = binaries.Count == 1 ? binaries[0] : null;
                message = binaries.Count == 0
                    ? $"make built no program in {project.Name} or its bin, build or out folder. Choose the program to run in .tabstudio/launch.json."
                    : $"make built {binaries.Count} programs ({string.Join(", ", binaries.Take(4).Select(Path.GetFileName))}). Choose the one to run in .tabstudio/launch.json.";
            }
            else
            {
                executable = plan.Run.Program;
                message = $"The build of {project.Name} produced no program to run.";
            }

            if (executable is not null)
            {
                return (plan with { Run = plan.Run with { Program = executable, Arguments = [] } }, built);
            }

            Output.WriteLine(message, OutputKind.Failure);
            return (plan, new(new ProcessExit(1, null, false), [message]));
        }
        catch (Exception ex) when (ex is IOException or JsonException or KeyNotFoundException)
        {
            Output.WriteLine(ex.Message, OutputKind.Failure);
            return (plan, new(new ProcessExit(1, null, false), [ex.Message]));
        }
    }
}
