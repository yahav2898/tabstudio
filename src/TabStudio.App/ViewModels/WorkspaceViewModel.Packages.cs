using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using TabStudio.Compilation.Wpf;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.Packages;

namespace TabStudio.App.ViewModels;

/// <summary>
/// NuGet packages: restored in the background as soon as a workspace opens, again when a project file is
/// saved, and awaited before every build, the way Visual Studio restores on open and on build.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    private Task _restore = Task.CompletedTask;

    /// <summary>The package restore's progress, for the status bar; null when nothing is being restored.</summary>
    [ObservableProperty]
    public partial string? PackagesText { get; private set; }

    /// <summary>Restores the packages of every C# project that uses any, without waiting for it.</summary>
    /// <param name="workspace">The loaded workspace.</param>
    private void StartPackageRestore(Workspace workspace)
    {
        var projects = workspace.Projects.Where(p => NeedsRestore(workspace, p)).ToList();
        var previous = _restore;
        _restore = projects.Count == 0 ? previous : RestoreAfterAsync(previous, projects, _background?.Token ?? CancellationToken.None);
        StartFrameworkGenerators(workspace);
    }

    /// <summary>Waits for the restore in progress, if any, so a build sees its packages.</summary>
    private async Task WaitForPackagesAsync()
    {
        if (!_restore.IsCompleted)
        {
            StatusText = "Waiting for packages";
            await _restore;
        }
    }

    /// <summary>
    /// Whether a project needs a restore: it is C# and it, or a project it references, uses packages or a shared
    /// framework such as ASP.NET Core. Projects without any are never restored, so no restore files appear in
    /// their folders.
    /// </summary>
    private static bool NeedsRestore(Workspace workspace, ProjectInfo project)
    {
        if (project.Language != ProjectLanguage.CSharp)
        {
            return false;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<ProjectInfo>([project]);
        while (pending.TryPop(out var next))
        {
            if (!seen.Add(next.FilePath))
            {
                continue;
            }

            if (next.PackageReferences.Count > 0 || next.FrameworkReferences.Count > 0 || WpfProject.UsesWpf(next) || UnoProjects.UsesUno(next))
            {
                return true;
            }

            foreach (var reference in next.ProjectReferences)
            {
                if (workspace.Projects.FirstOrDefault(p => string.Equals(p.FilePath, reference, StringComparison.Ordinal)) is { } referenced)
                {
                    pending.Push(referenced);
                }
            }
        }

        return false;
    }

    private async Task RestoreAfterAsync(Task previous, List<ProjectInfo> projects, CancellationToken cancellationToken)
    {
        await previous;
        await RestorePackagesAsync(projects, cancellationToken);
    }

    private async Task RestorePackagesAsync(List<ProjectInfo> projects, CancellationToken cancellationToken)
    {
        if (_language is not { } language)
        {
            return;
        }

        var restoredAny = false;
        try
        {
            foreach (var project in projects)
            {
                PackagesText = $"Restoring {project.Name}";
                var progress = new Progress<RestoreProgress>(p =>
                {
                    if (p.Stage == RestoreStage.Downloading && p.Total > 0)
                    {
                        PackagesText = $"Downloading packages {p.Completed.ToString(CultureInfo.InvariantCulture)}/{p.Total.ToString(CultureInfo.InvariantCulture)}";
                    }
                });

                var result = await language.RestorePackagesAsync(project, progress, cancellationToken);
                ErrorList.SetRestoreDiagnostics(project.FilePath, result.Diagnostics);
                restoredAny |= result.Success;
                if (!result.FromCache)
                {
                    var seconds = result.Duration.TotalSeconds.ToString("0.00", CultureInfo.InvariantCulture);
                    Output.WriteLine(
                        result.Success ? $"Restored {project.FilePath} (in {seconds} sec)." : $"Failed to restore {project.FilePath}.",
                        result.Success ? OutputKind.Info : OutputKind.Failure);
                }

                foreach (var diagnostic in result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverityLevel.Warning))
                {
                    Output.WriteLine($"{project.Name}: {diagnostic.Id}: {diagnostic.Message}", diagnostic.Severity == DiagnosticSeverityLevel.Error ? OutputKind.Failure : OutputKind.Program);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The workspace closed.
            return;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The restorer reports problems as diagnostics; anything else is a bug, which must not stop builds.
            Output.WriteLine($"Package restore stopped: {ex.Message}", OutputKind.Failure);
        }
        finally
        {
            PackagesText = null;
        }

        // Code that uses the packages was analyzed without them: analyze it again now they are referenced.
        if (restoredAny)
        {
            foreach (var document in Documents)
            {
                _ = document.AnalyzeAgainAsync();
            }

            if (_profile.AnalyzeWholeSolution)
            {
                StartSolutionAnalysis();
            }
        }
    }

    /// <summary>After a project or package-version file is saved, reloads the workspace and restores again.</summary>
    /// <param name="document">The saved document.</param>
    private async Task OnProjectFileSavedAsync(DocumentViewModel document)
    {
        var name = Path.GetFileName(document.FilePath);
        if (!ProjectParser.IsProjectFile(document.FilePath)
            && !name.Equals("Directory.Packages.props", StringComparison.OrdinalIgnoreCase)
            && !name.Equals("Directory.Build.props", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await ReloadWorkspaceAsync();
        if (Workspace is { } workspace)
        {
            StartPackageRestore(workspace);
        }
    }
}
