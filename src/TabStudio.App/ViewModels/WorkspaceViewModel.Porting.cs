using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Services;
using TabStudio.Core.Workspaces;
using TabStudio.Porting;

namespace TabStudio.App.ViewModels;

/// <summary>Ports a saved project into a sibling folder and adds it to the workspace without editing the source.</summary>
public sealed partial class WorkspaceViewModel
{
    private readonly Dictionary<PortSummaryViewModel, (string? Solution, string? Startup)> _ports = [];

    [ObservableProperty]
    public partial bool IsPorting { get; private set; }

    [RelayCommand]
    private Task PortToAvaloniaAsync(CompatibilityRowViewModel? row) => BeginPortAsync(row is null
        ? Explorer.SelectedItem?.Node.Project ?? StartupProject
        : Workspace?.Projects.FirstOrDefault(p => p.Name == row.Project));

    Task IExplorerActions.PortToAvaloniaAsync(WorkspaceNode node) => BeginPortAsync(node.Project);

    /// <summary>Asks for the new project's name and converts the selected project's saved files.</summary>
    public async Task BeginPortAsync(ProjectInfo? project)
    {
        if (project is null || !AvaloniaPorter.CanPort(project) || IsPorting)
        {
            StatusText = "Select a C# WPF or Windows Forms project to port.";
            return;
        }

        var name = await Prompt.AskAsync("Port to Avalonia…", "Convert the saved files into a new project beside the original. The original project stays as it is. New project name:", "Port", AvaloniaPorter.SuggestName(project));
        if (name is null)
        {
            return;
        }

        if (AvaloniaPorter.ValidateName(name) is { } error)
        {
            StatusText = error;
            return;
        }

        try
        {
            await PortProjectAsync(project, name);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            StatusText = "Port to Avalonia: " + ex.Message;
        }
    }

    /// <summary>Runs the same conversion transaction used by the dialog and emulator self test.</summary>
    public async Task<PortSummaryViewModel> PortProjectAsync(ProjectInfo project, string name)
    {
        if (Workspace is not { } workspace || IsPorting || !AvaloniaPorter.CanPort(project))
        {
            throw new InvalidOperationException("Select a supported project in an open workspace.");
        }

        if (AvaloniaPorter.ValidateName(name) is { } error)
        {
            throw new ArgumentException(error, nameof(name));
        }

        IsPorting = true;
        var target = AvaloniaPorter.SuggestDirectory(project, name);
        var solution = workspace.Solution?.FilePath;
        var startup = StartupProject?.FilePath;
        PortResult? result = null;
        try
        {
            var others = workspace.Projects.Where(p => p.FilePath != project.FilePath).Select(p => p.DirectoryPath).ToList();
            result = await Task.Run(() =>
            {
                var converted = AvaloniaPorter.Port(project, new PortOptions(name, target, ProjectTemplates.AvaloniaVersion), others);
                converted.Write();
                return converted;
            });
            if (solution is not null)
            {
                SolutionFileEditor.AddProject(solution, result.ProjectFilePath);
            }

            await ReloadWorkspaceAsync(solution ?? Path.GetDirectoryName(target));
            StartPackageRestore(Workspace!);
            SetStartupProject(Workspace!.Projects.First(p => p.FilePath == result.ProjectFilePath));
            var summary = new PortSummaryViewModel(this, result);
            _ports[summary] = (solution, startup);
            var document = DocumentViewModel.ForPortSummary(summary, Path.Combine(target, "Port-summary"), _profile);
            Documents.Add(document);
            ActiveDocument = document;
            StatusText = $"Ported {project.Name} to {name}. F5 runs the converted project.";
            if (result.Notes.Count > 0)
            {
                await SendPortNotesAsync(summary);
            }

            return summary;
        }
        catch
        {
            if (result is not null)
            {
                if (solution is not null)
                {
                    SolutionFileEditor.RemoveProject(solution, result.ProjectFilePath);
                }

                // Keep the files for inspection after an IDE reload failure; never delete user changes here.
            }

            throw;
        }
        finally
        {
            IsPorting = false;
        }
    }

    public Task SendPortNotesAsync(PortSummaryViewModel summary)
    {
        if (summary.IsUndone || summary.Result.Notes.Count == 0)
        {
            return Task.CompletedTask;
        }

        SelectSideBarPanel(SideBarPanel.Agent);
        var relative = Path.GetRelativePath(Workspace!.RootDirectory, summary.Result.ProjectDirectory).Replace('\\', '/');
        return Agent.AskAsync(summary.Result.DescribeForAgent(relative) + "\nPropose the changes for review using the agent's pending changes. Do not modify the original project.");
    }

    public async Task UndoPortAsync(PortSummaryViewModel summary)
    {
        if (!_ports.TryGetValue(summary, out var state) || summary.IsUndone)
        {
            return;
        }

        if (AppPreview.IsRunning || Agent.IsBusy)
        {
            StatusText = "Stop the running app and agent before undoing this port.";
            return;
        }

        var result = summary.Result;
        // Undo removes the workspace addition and moves all generated and subsequently edited files to trash.
        // This preserves changes the user or agent made after conversion.
        await CloseDocumentsUnderAsync(result.ProjectDirectory, save: true);
        if (state.Solution is not null)
        {
            SolutionFileEditor.RemoveProject(state.Solution, result.ProjectFilePath);
        }

        if (Directory.Exists(result.ProjectDirectory))
        {
            Directory.CreateDirectory(_paths.Trash);
            MoveDirectory(result.ProjectDirectory, Path.Combine(_paths.Trash, result.ProjectName + "-" + Guid.NewGuid().ToString("N")));
        }

        summary.IsUndone = true;
        await ReloadWorkspaceAsync();
        SetStartupProject(Workspace?.Projects.FirstOrDefault(p => p.FilePath == state.Startup));
        StatusText = "Port undone. The converted files are in TabStudio's trash.";
    }
}
