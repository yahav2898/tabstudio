using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.ViewModels;

/// <summary>The ways to start on the start screen.</summary>
public enum StartAction
{
    /// <summary>Download a repository from GitHub.</summary>
    GitHub,

    /// <summary>Open or import a folder from the device.</summary>
    Folder,

    /// <summary>Create a project from a template.</summary>
    NewProject,

    /// <summary>Open the bundled sample.</summary>
    Sample,
}

/// <summary>A recent workspace on the start screen.</summary>
/// <param name="Entry">The stored entry.</param>
public sealed record RecentItemViewModel(RecentWorkspace Entry)
{
    /// <summary>The display name.</summary>
    public string Name => Entry.Name;

    /// <summary>What it is, such as "C# · 2 projects".</summary>
    public string Description => Entry.Description;

    /// <summary>Where it is, shortened for display.</summary>
    public string Location => Entry.Path;
}

/// <summary>A group of recent workspaces, such as "Today".</summary>
/// <param name="Title">The group title.</param>
/// <param name="Items">The workspaces in it.</param>
public sealed record RecentGroupViewModel(string Title, IReadOnlyList<RecentItemViewModel> Items);

/// <summary>
/// The start screen: recent workspaces, and the ways to get code onto the tablet — download from GitHub,
/// open a folder, create a project or open the sample. Imported code is inspected first, so the user sees
/// what it contains and where each project runs before opening it.
/// </summary>
public sealed partial class StartViewModel : ObservableObject, IDisposable
{
    private readonly RecentWorkspaces _recent;
    private readonly SampleLibrary _samples;
    private readonly ProjectTemplates _templates;
    private readonly GitHubImporter _gitHub;
    private readonly FolderImporter _folders;
    private readonly IStoragePicker _picker;
    private CancellationTokenSource? _import;

    /// <summary>Creates the start screen.</summary>
    public StartViewModel(
        RecentWorkspaces recent,
        SampleLibrary samples,
        ProjectTemplates templates,
        GitHubImporter gitHub,
        FolderImporter folders,
        IStoragePicker picker,
        GitHubAccountViewModel gitHubAccount,
        LinkViewModel link,
        Live.LiveJoinViewModel? liveJoin = null)
    {
        LiveJoin = liveJoin;
        _recent = recent;
        _samples = samples;
        _templates = templates;
        _gitHub = gitHub;
        _folders = folders;
        _picker = picker;
        GitHubAccount = gitHubAccount;
        Link = link;
        SelectedTemplate = Templates[0];
        RefreshRecent();
    }

    /// <summary>The GitHub account, for opening private repositories.</summary>
    public GitHubAccountViewModel GitHubAccount { get; }

    /// <summary>TabStudio Link, for linking this device to a host or hosting one from the start screen.</summary>
    public LinkViewModel Link { get; }

    /// <summary>TabStudio Live's Join dialog, for joining another device's session from the start screen; none in some tests.</summary>
    public Live.LiveJoinViewModel? LiveJoin { get; }

    /// <summary>Whether the start screen offers to join a Live session.</summary>
    public bool HasLive => LiveJoin is not null;

    /// <summary>Join a Live session: scan or type another device's session code.</summary>
    [RelayCommand]
    private void JoinLiveSession() => LiveJoin?.Open();

    /// <summary>Raised when the user chooses a workspace to open, with its path.</summary>
    public event EventHandler<string>? OpenRequested;

    /// <summary>The recent workspaces, grouped by when they were opened.</summary>
    public ObservableCollection<RecentGroupViewModel> RecentGroups { get; } = [];

    /// <summary>Whether there are any recent workspaces.</summary>
    [ObservableProperty]
    public partial bool HasRecent { get; private set; }

    /// <summary>Filters the recent list.</summary>
    [ObservableProperty]
    public partial string RecentSearch { get; set; } = string.Empty;

    /// <summary>The action whose form is shown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGitHubSelected), nameof(IsFolderSelected), nameof(IsNewProjectSelected), nameof(IsSampleSelected), nameof(IsGitHubFormVisible), nameof(IsFolderFormVisible))]
    public partial StartAction SelectedAction { get; set; } = StartAction.Sample;

    /// <summary>Whether the GitHub card is selected.</summary>
    public bool IsGitHubSelected => SelectedAction == StartAction.GitHub;

    /// <summary>Whether the folder card is selected.</summary>
    public bool IsFolderSelected => SelectedAction == StartAction.Folder;

    /// <summary>Whether the GitHub form is shown: its card is selected and no import is being previewed.</summary>
    public bool IsGitHubFormVisible => IsGitHubSelected && Preview is null;

    /// <summary>Whether the folder form is shown: its card is selected and no import is being previewed.</summary>
    public bool IsFolderFormVisible => IsFolderSelected && Preview is null;

    /// <summary>Whether the new project form is shown.</summary>
    public bool IsNewProjectSelected => SelectedAction == StartAction.NewProject;

    /// <summary>Whether the sample card is shown.</summary>
    public bool IsSampleSelected => SelectedAction == StartAction.Sample;

    /// <summary>The repository address typed by the user.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
    public partial string RepositoryText { get; set; } = string.Empty;

    /// <summary>The templates for a new project.</summary>
    public IReadOnlyList<ProjectTemplate> Templates { get; } = ProjectTemplates.All;

    /// <summary>The selected template.</summary>
    [ObservableProperty]
    public partial ProjectTemplate SelectedTemplate { get; set; }

    /// <summary>The new project's name.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateProjectCommand))]
    [NotifyPropertyChangedFor(nameof(ProjectNameError))]
    public partial string ProjectName { get; set; } = "MyApp";

    /// <summary>Why the project name is invalid, or <see langword="null"/>.</summary>
    public string? ProjectNameError => ProjectTemplates.Validate(ProjectName);

    /// <summary>Whether a download, copy or extraction is running.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand), nameof(PickFolderCommand), nameof(CreateProjectCommand), nameof(OpenSampleCommand))]
    public partial bool IsBusy { get; private set; }

    /// <summary>What the running operation is doing, such as "Downloading 3.2 MB".</summary>
    [ObservableProperty]
    public partial string? ProgressText { get; private set; }

    /// <summary>The last error, in plain words.</summary>
    [ObservableProperty]
    public partial string? ErrorText { get; private set; }

    /// <summary>What an imported folder or repository contains, before it is opened.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGitHubFormVisible), nameof(IsFolderFormVisible))]
    public partial WorkspacePreviewViewModel? Preview { get; private set; }

    /// <inheritdoc />
    public void Dispose() => _import?.Dispose();

    /// <summary>Shows an error, for example when a workspace could not be opened.</summary>
    /// <param name="message">The message, in plain words.</param>
    public void ShowError(string message) => ErrorText = message;

    /// <summary>Reloads the recent list, for example after returning from a workspace.</summary>
    public void RefreshRecent()
    {
        var now = DateTimeOffset.Now;
        var search = RecentSearch.Trim();
        var items = _recent.GetAll()
            .Where(e => search.Length == 0
                || e.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || e.Path.Contains(search, StringComparison.OrdinalIgnoreCase))
            .Select(e => new RecentItemViewModel(e))
            .ToList();

        RecentGroups.Clear();
        foreach (var group in items.GroupBy(i => GroupTitle(i.Entry.LastOpened, now)))
        {
            RecentGroups.Add(new RecentGroupViewModel(group.Key, group.ToList()));
        }

        HasRecent = items.Count > 0;
    }

    [RelayCommand]
    private void Select(StartAction action)
    {
        SelectedAction = action;
        Preview = null;
        ErrorText = null;
    }

    [RelayCommand]
    private void OpenRecent(RecentItemViewModel? item)
    {
        if (item is not null)
        {
            OpenRequested?.Invoke(this, item.Entry.Path);
        }
    }

    [RelayCommand]
    private void RemoveRecent(RecentItemViewModel? item)
    {
        if (item is not null)
        {
            _recent.Remove(item.Entry.Path);
            RefreshRecent();
        }
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task OpenSampleAsync()
    {
        await RunAsync(async token =>
        {
            ProgressText = "Preparing the sample";
            var path = await _samples.EnsureExtractedAsync(token);
            OpenRequested?.Invoke(this, path);
        });
    }

    [RelayCommand(CanExecute = nameof(CanDownload))]
    private async Task DownloadAsync()
    {
        if (!GitHubRepository.TryParse(RepositoryText, out var repository))
        {
            ErrorText = "Enter a GitHub repository, such as github.com/owner/name.";
            return;
        }

        await RunAsync(async token =>
        {
            var progress = new Progress<ImportProgress>(p => ProgressText = Describe(p));
            ProgressText = $"Connecting to GitHub";
            var folder = await _gitHub.ImportAsync(repository, progress, token);
            await ShowPreviewAsync(folder, repository.ToString(), _gitHub.LastNotes, token);
        });
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task PickFolderAsync()
    {
        var folder = await _picker.PickFolderAsync("Open a folder");
        if (folder is null)
        {
            return;
        }

        await RunAsync(async token =>
        {
            using (folder)
            {
                var progress = new Progress<ImportProgress>(p => ProgressText = Describe(p));
                ProgressText = "Reading the folder";
                var path = await _folders.ImportAsync(folder, progress, token);
                await ShowPreviewAsync(path, folder.Name, null, token);
            }
        });
    }

    [RelayCommand(CanExecute = nameof(CanCreateProject))]
    private async Task CreateProjectAsync()
    {
        await RunAsync(_ =>
        {
            var solution = _templates.Create(TemplateIdToCreate, ProjectName.Trim());
            OpenRequested?.Invoke(this, solution);
            return Task.CompletedTask;
        });
    }

    [RelayCommand]
    private void OpenPreview()
    {
        if (Preview is { } preview)
        {
            Preview = null;
            OpenRequested?.Invoke(this, preview.Path);
        }
    }

    [RelayCommand]
    private void DismissPreview() => Preview = null;

    [RelayCommand]
    private void Cancel() => _import?.Cancel();

    partial void OnRecentSearchChanged(string value) => RefreshRecent();

    private bool CanStart() => !IsBusy;

    private bool CanDownload() => !IsBusy && !string.IsNullOrWhiteSpace(RepositoryText);

    private bool CanCreateProject() => !IsBusy && ProjectNameError is null;

    private async Task ShowPreviewAsync(string path, string source, IReadOnlyList<string>? notes, CancellationToken cancellationToken)
    {
        ProgressText = "Detecting the stack";
        var preview = await WorkspaceInspector.InspectAsync(path, cancellationToken);
        Preview = new WorkspacePreviewViewModel(preview, source) { Notes = notes ?? [] };

        // In Recent at once: dismissed before it was opened, a downloaded repository was in no list, and downloading it
        // again made a second copy.
        var projects = preview.Workspace.Projects.Count;
        _recent.Add(new RecentWorkspace(Preview.Path, Preview.Name, projects switch { 0 => "Folder", 1 => "1 project", _ => $"{projects} projects" }, DateTimeOffset.Now));
        RefreshRecent();
    }

    private async Task RunAsync(Func<CancellationToken, Task> work)
    {
        ErrorText = null;
        IsBusy = true;
        _import = new CancellationTokenSource();
        try
        {
            await work(_import.Token);
        }
        catch (OperationCanceledException)
        {
            ErrorText = "Cancelled.";
        }
        catch (Exception ex) when (ex is ImportException or IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or ProjectLoadException)
        {
            ErrorText = ex.Message;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Anything else, such as a repository's file the inspection could not read, is said too: before 0.21.2 it
            // left the start screen as it was, with no word of what happened.
            ErrorText = $"Something went wrong: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            ProgressText = null;
            _import.Dispose();
            _import = null;
        }
    }

    private static string Describe(ImportProgress progress) => progress.Stage switch
    {
        "Downloading" when progress.TotalBytes is { } total => $"Downloading {Megabytes(progress.BytesReceived)} of {Megabytes(total)}",
        "Downloading" => $"Downloading {Megabytes(progress.BytesReceived)}",
        "Copying" => $"Copying {progress.BytesReceived.ToString("N0", CultureInfo.CurrentCulture)} files",
        _ => progress.Stage,
    };

    private static string Megabytes(long bytes) => $"{(bytes / (1024.0 * 1024)).ToString("0.0", CultureInfo.CurrentCulture)} MB";

    private static string GroupTitle(DateTimeOffset opened, DateTimeOffset now)
    {
        var days = (now.Date - opened.ToLocalTime().Date).TotalDays;
        return days switch
        {
            < 1 => "Today",
            < 7 => "This week",
            < 31 => "This month",
            _ => "Older",
        };
    }
}

/// <summary>What an imported workspace contains: detected stacks and the Compatibility Doctor's verdicts.</summary>
public sealed class WorkspacePreviewViewModel
{
    /// <summary>Creates the preview.</summary>
    /// <param name="preview">The inspection result.</param>
    /// <param name="source">Where it came from, such as "owner/repo".</param>
    public WorkspacePreviewViewModel(WorkspacePreview preview, string source)
    {
        ArgumentNullException.ThrowIfNull(preview);
        Path = preview.Workspace.Solution?.FilePath ?? preview.Workspace.RootDirectory;
        Name = preview.Workspace.Name;
        Source = source;
        Summary = preview.Summary;
        Stacks = preview.Stack.Components
            .Select(c => new StackChipViewModel(c.Version is null ? c.DisplayName : $"{c.DisplayName} {c.Version}", c.Evidence))
            .ToList();
        Projects = preview.Reports.Select(r => new CompatibilityRowViewModel(r)).ToList();
    }

    /// <summary>What to open: the solution file, or the folder.</summary>
    public string Path { get; }

    /// <summary>The workspace name.</summary>
    public string Name { get; }

    /// <summary>Where it came from.</summary>
    public string Source { get; }

    /// <summary>A one-line summary.</summary>
    public string Summary { get; }

    /// <summary>The detected technologies.</summary>
    public IReadOnlyList<StackChipViewModel> Stacks { get; }

    /// <summary>The Compatibility Doctor's verdict for each project.</summary>
    public IReadOnlyList<CompatibilityRowViewModel> Projects { get; }

    /// <summary>Whether there are projects to show.</summary>
    public bool HasProjects => Projects.Count > 0;

    /// <summary>What the download could not bring, such as a submodule on another host, one sentence each.</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];

    /// <summary>The notes, one per line.</summary>
    public string NotesText => string.Join(Environment.NewLine, Notes);

    /// <summary>Whether there are notes to show.</summary>
    public bool HasNotes => Notes.Count > 0;
}
