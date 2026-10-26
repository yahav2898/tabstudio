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
            var prepared = await OutputProcess.RunAsync(_toolchains.Runtime, step, Output, null, cancellationToken);
            if (!prepared.Succeeded)
            {
                return (plan, prepared);
            }
        }

        var built = await OutputProcess.RunAsync(_toolchains.Runtime, plan.Build!, Output, null, cancellationToken);
        if (!built.Succeeded || project.Toolchain.Id != "cpp")
        {
            return (plan, built);
        }

        try
        {
            string? executable;
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
            }
            else if (project.Marker is "Makefile" or "makefile" or "GNUmakefile")
            {
                var binaries = NativePrograms.Find(project.Directory);
                executable = binaries.Count == 1 ? binaries[0] : null;
            }
            else
            {
                executable = plan.Run.Program;
            }

            if (executable is not null)
            {
                return (plan with { Run = plan.Run with { Program = executable, Arguments = [] } }, built);
            }

            const string message = "The build produced no unique executable. Open a source file of the CMake target, or choose the program in .tabstudio/launch.json.";
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
