using System.Globalization;
using System.Text.Json;
using TabStudio.LanguageServers.Configuration;
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
            var root = Workspace?.RootDirectory ?? project.Directory;
            var launch = NativeLaunchConfigurations(root);
            var launchPrograms = launch.Select(c => c.Program!).ToList();
            string? executable;
            string? chosenTarget = null;
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
                var chosen = CMakeProject.Choose(targets, project.Directory, ActiveDocument?.FilePath, launchPrograms, root);
                executable = chosen?.Path;
                chosenTarget = chosen?.Name;
                message = targets.Count == 0
                    ? $"{project.Name} has no executable target to run. Add one with add_executable, or choose the program in .tabstudio/launch.json."
                    : $"{project.Name} has {targets.Count.ToString(CultureInfo.InvariantCulture)} executable targets ({NamesOf(targets.Select(t => t.Name))}). Open a source file of the one to run, or name it as the \"program\" of a configuration in .tabstudio/launch.json.";
            }
            else if (project.Marker is { } marker && ToolchainProjectDetector.IsMakefile(marker))
            {
                var binaries = NativePrograms.Find(project.Directory);
                executable = NativePrograms.Choose(binaries, project.Directory, launchPrograms, root);
                message = binaries.Count == 0
                    ? $"make built no program in {project.Name} or its bin, build or out folder. Choose the program to run in .tabstudio/launch.json."
                    : $"make built {binaries.Count.ToString(CultureInfo.InvariantCulture)} programs ({NamesOf(binaries.Select(Path.GetFileName))}). Name the one to run as the \"program\" of a configuration in .tabstudio/launch.json.";
            }
            else
            {
                executable = plan.Run.Program;
                message = $"The build of {project.Name} produced no program to run.";
            }

            if (executable is { } program)
            {
                // A launch configuration that names the program also gives its arguments.
                var arguments = launch.FirstOrDefault(c => NativePrograms.Names(c.Program!, program, root) || string.Equals(c.Program, chosenTarget, StringComparison.Ordinal))?.Arguments ?? [];
                return (plan with { Run = plan.Run with { Program = program, Arguments = arguments } }, built);
            }

            // Nothing ran, so there is no exit code to report: the run ends saying what to choose.
            return (plan, OutputProcessResult.NothingToRun(message));
        }
        catch (Exception ex) when (ex is IOException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            Output.WriteLine(ex.Message, OutputKind.Failure);
            return (plan, new(null, [ex.Message], ex.Message));
        }
    }

    /// <summary>
    /// The launch configurations of <c>.tabstudio/launch.json</c> that start a program, with their variables replaced,
    /// in order: their <c>program</c> chooses among the executables of a CMake or Make build.
    /// </summary>
    /// <param name="root">The workspace's root folder.</param>
    private List<LaunchConfiguration> NativeLaunchConfigurations(string root)
    {
        var configuration = _languageServers?.Configuration ?? WorkspaceConfiguration.Load(root);
        var variables = new LaunchVariables(root, ActiveDocument?.FilePath);
        return [.. configuration.LaunchConfigurations
            .Where(c => !c.IsAttach && !string.IsNullOrWhiteSpace(c.Program))
            .Select(c => c.Substitute(variables))
            .Where(c => !string.IsNullOrWhiteSpace(c.Program))];
    }

    /// <summary>The first few names of a list, for a one-line message, such as <c>cJSON_test, readme_examples, …</c>.</summary>
    private static string NamesOf(IEnumerable<string?> names)
    {
        var all = names.ToList();
        return string.Join(", ", all.Take(4)) + (all.Count > 4 ? ", …" : string.Empty);
    }
}
