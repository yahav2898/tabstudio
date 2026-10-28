using TabStudio.Core.Progress;
using TabStudio.Toolchains.Projects;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Ctrl+Shift+B for PHP, Kotlin and Swift projects: each one's build, without running it (<c>swift build</c>,
/// Gradle's or Maven's compile, <c>kotlinc</c>, and <c>php -l</c> over PHP's own files), with its errors in the Error
/// List, after the solution's C# and Visual Basic projects.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    private async Task BuildToolchainProjectsAsync(bool clearOutput)
    {
        var projects = _toolchainProjects.Where(t => t.Toolchain.Id is "php" or "kotlin" or "swift").ToList();
        if (projects.Count == 0 || BlockLocalToolchainActionForRemote() || !await SaveAllCoreAsync())
        {
            return;
        }

        if (clearOutput)
        {
            Output.Clear();
        }

        SelectedPanel = BottomPanel.Output;
        IsBottomPanelVisible = true;
        int succeeded = 0, failed = 0;
        foreach (var item in projects)
        {
            var project = item.Project;
            if (!await EnsureToolchainReadyAsync(project.Toolchain, $"Building {project.Name} needs {project.Toolchain.Name}, which is not installed yet."))
            {
                return;
            }

            if (RunPlanner.PlanBuild(project) is not { } build)
            {
                continue;
            }

            Output.WriteLine($"------ Build started: {project.Name} ({project.Toolchain.Name}) ------", OutputKind.Info);
            Output.WriteLine("> " + build.CommandLine, OutputKind.Info);
            ErrorList.SetRunError(project.Directory, null);
            using var cancellation = new CancellationTokenSource();
            _run = cancellation;
            _runDirectory = project.Directory;
            State = IdeState.Running;
            StatusText = $"Building {project.Name}";
            OutputProcessResult result;
            try
            {
                await _toolchains.Manager.PrepareRuntimeAsync(cancellationToken: cancellation.Token);
                var stage = OutputProgressParser.StageOf(build.Program, build.Arguments, project.Name);
                using var operation = new CompletingOperation(Operations.Begin("build:" + project.Directory, "Building " + project.Name, new OperationProgress(stage), cancellation.Cancel));
                using (FollowOutput(operation.Operation))
                {
                    result = await OutputProcess.RunAsync(_toolchains.Runtime, build, Output, null, cancellation.Token, toolchain: project.Toolchain.Id);
                    operation.Outcome = result.Succeeded ? OperationOutcome.Succeeded
                        : cancellation.IsCancellationRequested ? OperationOutcome.Cancelled : OperationOutcome.Failed;
                }
            }
            catch (OperationCanceledException)
            {
                State = IdeState.Ready;
                StatusText = "Build cancelled";
                return;
            }
            finally
            {
                _run = null;
            }

            ErrorList.SetRunErrors(project.Directory, [.. Matcher(project.Toolchain).FindErrors(result.Lines, IsWorkspaceFile).Select(error => ToDiagnostic(project, error))]);
            if (result.Succeeded)
            {
                succeeded++;
            }
            else
            {
                failed++;
            }

            Output.WriteLine($"{project.Name}: build {(result.Succeeded ? "succeeded" : "failed")}.", result.Succeeded ? OutputKind.Success : OutputKind.Failure);
        }

        if (succeeded + failed > 0)
        {
            Output.WriteLine($"========== Build: {succeeded} succeeded, {failed} failed ==========", failed == 0 ? OutputKind.Success : OutputKind.Failure);
            State = failed == 0 ? IdeState.Ready : IdeState.Failed;
            StatusText = failed == 0 ? "Build succeeded" : "Build failed";
        }
    }
}
