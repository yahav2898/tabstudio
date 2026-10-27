using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Compilation.Building;
using TabStudio.Core.Diagnostics;

namespace TabStudio.App.ViewModels;

/// <summary>One row of the Error List.</summary>
/// <param name="Diagnostic">The diagnostic the row shows.</param>
public sealed partial record ErrorItemViewModel(BuildDiagnostic Diagnostic)
{
    /// <summary>The diagnostic id, such as CS0103.</summary>
    public string Code => Diagnostic.Id;

    /// <summary>The message.</summary>
    public string Description => Diagnostic.Message;

    /// <summary>The project the diagnostic belongs to.</summary>
    public string Project => Diagnostic.ProjectName ?? string.Empty;

    /// <summary>The file name, without its folder.</summary>
    public string File => Diagnostic.FileName ?? string.Empty;

    /// <summary>The 1-based line, or empty for diagnostics without a location.</summary>
    public string Line => Diagnostic.Location is { } location ? location.Line.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;

    /// <summary>Whether this is an error.</summary>
    public bool IsError => Diagnostic.Severity == DiagnosticSeverityLevel.Error;

    /// <summary>Whether this is a warning.</summary>
    public bool IsWarning => Diagnostic.Severity == DiagnosticSeverityLevel.Warning;

    /// <summary>Whether this is a message.</summary>
    public bool IsMessage => Diagnostic.Severity <= DiagnosticSeverityLevel.Info;

    /// <summary>Whether tapping the row can open the file.</summary>
    public bool HasLocation => Diagnostic.Location is not null;

    /// <summary>Whether the agent can be asked to fix it: an error or a warning.</summary>
    public bool CanFixWithAgent => Diagnostic.Severity >= DiagnosticSeverityLevel.Warning;
}

/// <summary>
/// The Error List: every error, warning and message in the solution, from live analysis of the files being
/// edited, background analysis of the whole solution, and the last build.
/// </summary>
public sealed partial class ErrorListViewModel : ObservableObject
{
    private readonly Dictionary<string, IReadOnlyList<BuildDiagnostic>> _byFile = new(StringComparer.Ordinal);
    private IReadOnlyList<BuildDiagnostic> _withoutLocation = [];
    private readonly Dictionary<string, IReadOnlyList<BuildDiagnostic>> _restore = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _reportedByProject = new(StringComparer.Ordinal);

    /// <summary>The rows that pass the current filters, most severe first.</summary>
    public ObservableCollection<ErrorItemViewModel> Items { get; } = [];

    /// <summary>Whether errors are listed.</summary>
    [ObservableProperty]
    public partial bool ShowErrors { get; set; } = true;

    /// <summary>Whether warnings are listed.</summary>
    [ObservableProperty]
    public partial bool ShowWarnings { get; set; } = true;

    /// <summary>Whether messages are listed.</summary>
    [ObservableProperty]
    public partial bool ShowMessages { get; set; } = true;

    /// <summary>Only rows whose code, message, project or file contain this text are listed.</summary>
    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    /// <summary>Whether only the current document's diagnostics are listed.</summary>
    [ObservableProperty]
    public partial bool CurrentDocumentOnly { get; set; }

    /// <summary>The number of errors, before filtering.</summary>
    [ObservableProperty]
    public partial int ErrorCount { get; private set; }

    /// <summary>The number of warnings, before filtering.</summary>
    [ObservableProperty]
    public partial int WarningCount { get; private set; }

    /// <summary>The number of messages, before filtering.</summary>
    [ObservableProperty]
    public partial int MessageCount { get; private set; }

    /// <summary>Whether any row passes the filters.</summary>
    [ObservableProperty]
    public partial bool HasItems { get; private set; }

    /// <summary>"3 Errors", for the filter button.</summary>
    public string ErrorsLabel => Plural(ErrorCount, "Error");

    /// <summary>"1 Warning", for the filter button.</summary>
    public string WarningsLabel => Plural(WarningCount, "Warning");

    /// <summary>"2 Messages", for the filter button.</summary>
    public string MessagesLabel => Plural(MessageCount, "Message");

    /// <summary>The path of the active document, for the current-document filter.</summary>
    public string? CurrentDocumentPath
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                if (CurrentDocumentOnly)
                {
                    Refresh();
                }
            }
        }
    }

    /// <summary>Raised when the user opens a row; the workspace navigates to the location.</summary>
    public event EventHandler<BuildDiagnostic>? NavigateRequested;

    /// <summary>Raised when the user asks the agent to fix a row's problem; the workspace hands it to the agent.</summary>
    public event EventHandler<BuildDiagnostic>? FixWithAgentRequested;

    /// <summary>Raised after the diagnostics changed and the list was read again, as a TabStudio Live host shares it.</summary>
    public event EventHandler? Refreshed;

    /// <summary>All diagnostics with a location in a file, before filtering.</summary>
    public IEnumerable<BuildDiagnostic> AllDiagnostics =>
        WindowsBridgeDiagnostics.Concat(_byFile.Values.SelectMany(d => d)).Concat(_withoutLocation).Concat(_restore.Values.SelectMany(d => d)).Concat(LayoutDiagnostics).Concat(LiveHostDiagnostics);

    /// <summary>The current diagnostics of one file.</summary>
    /// <param name="filePath">Full path of the file.</param>
    public IReadOnlyList<BuildDiagnostic> ForFile(string filePath) =>
        _byFile.TryGetValue(filePath, out var diagnostics) ? diagnostics : [];

    /// <summary>Replaces the diagnostics of one file, after live analysis.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="diagnostics">Its diagnostics.</param>
    public void SetFileDiagnostics(string filePath, IReadOnlyList<BuildDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(diagnostics);
        _byFile[filePath] = diagnostics.Where(d => d.Location?.FilePath == filePath).ToList();
        Refresh();
    }

    /// <summary>Replaces the diagnostics of every file in a project folder, after analyzing the project.</summary>
    /// <param name="projectDirectory">The project folder.</param>
    /// <param name="diagnostics">The project's diagnostics.</param>
    public void SetProjectDiagnostics(string projectDirectory, IReadOnlyList<BuildDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(projectDirectory);
        ArgumentNullException.ThrowIfNull(diagnostics);

        // The project's files, and those its previous analysis reported outside its folder, such as generated code.
        var prefix = projectDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var reportedBefore = _reportedByProject.GetValueOrDefault(projectDirectory) ?? [];
        foreach (var file in _byFile.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal) || reportedBefore.Contains(k)).ToList())
        {
            _byFile.Remove(file);
        }

        var reported = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in diagnostics.Where(d => d.Location is not null).GroupBy(d => d.Location!.FilePath, StringComparer.Ordinal))
        {
            _byFile[group.Key] = group.ToList();
            reported.Add(group.Key);
        }

        _reportedByProject[projectDirectory] = reported;
        Refresh();
    }

    /// <summary>Shows the problems found restoring a project's NuGet packages, replacing its previous ones.</summary>
    /// <param name="projectFilePath">The project.</param>
    /// <param name="diagnostics">The restore's diagnostics.</param>
    public void SetRestoreDiagnostics(string projectFilePath, IReadOnlyList<BuildDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(projectFilePath);
        ArgumentNullException.ThrowIfNull(diagnostics);
        _restore[projectFilePath] = diagnostics;
        Refresh();
    }

    /// <summary>Shows the result of a build: its diagnostics replace those of the files it compiled.</summary>
    /// <param name="build">The build result.</param>
    public void SetBuildResult(BuildResult build)
    {
        ArgumentNullException.ThrowIfNull(build);
        foreach (var group in build.Diagnostics.Where(d => d.Location is not null).GroupBy(d => d.Location!.FilePath, StringComparer.Ordinal))
        {
            _byFile[group.Key] = group.ToList();
        }

        _withoutLocation = build.Diagnostics.Where(d => d.Location is null).ToList();
        Refresh();
    }

    /// <summary>Removes everything, when the workspace closes.</summary>
    public void Clear()
    {
        _byFile.Clear();
        _withoutLocation = [];
        _restore.Clear();
        _reportedByProject.Clear();
        Refresh();
    }

    [RelayCommand]
    private void Navigate(ErrorItemViewModel? item)
    {
        if (item?.HasLocation == true)
        {
            NavigateRequested?.Invoke(this, item.Diagnostic);
        }

        if (item is not null)
        {
            OpenLayoutFinding(item);
        }
    }

    /// <summary>"Fix with Agent" on a row: asks Claude to fix the error or warning.</summary>
    [RelayCommand]
    private void FixWithAgent(ErrorItemViewModel? item)
    {
        if (item is not null)
        {
            FixWithAgentRequested?.Invoke(this, item.Diagnostic);
        }
    }

    [RelayCommand]
    private void ToggleScope() => CurrentDocumentOnly = !CurrentDocumentOnly;

    [RelayCommand]
    private void ToggleErrors() => ShowErrors = !ShowErrors;

    [RelayCommand]
    private void ToggleWarnings() => ShowWarnings = !ShowWarnings;

    [RelayCommand]
    private void ToggleMessages() => ShowMessages = !ShowMessages;

    partial void OnShowErrorsChanged(bool value) => Refresh();

    partial void OnShowWarningsChanged(bool value) => Refresh();

    partial void OnShowMessagesChanged(bool value) => Refresh();

    partial void OnSearchTextChanged(string value) => Refresh();

    partial void OnCurrentDocumentOnlyChanged(bool value) => Refresh();

    partial void OnErrorCountChanged(int value) => OnPropertyChanged(nameof(ErrorsLabel));

    partial void OnWarningCountChanged(int value) => OnPropertyChanged(nameof(WarningsLabel));

    partial void OnMessageCountChanged(int value) => OnPropertyChanged(nameof(MessagesLabel));

    private void Refresh()
    {
        var local = AllDiagnostics.ToList();
        var all = local.Concat(VisibleCiDiagnostics(local)).DistinctBy(d => (d.Id, d.Location?.FilePath, d.Location?.Start, d.Message)).ToList();
        ErrorCount = all.Count(d => d.Severity == DiagnosticSeverityLevel.Error);
        WarningCount = all.Count(d => d.Severity == DiagnosticSeverityLevel.Warning);
        MessageCount = all.Count(d => d.Severity <= DiagnosticSeverityLevel.Info);

        var visible = all
            .Where(d => d.Severity switch
            {
                DiagnosticSeverityLevel.Error => ShowErrors,
                DiagnosticSeverityLevel.Warning => ShowWarnings,
                _ => ShowMessages,
            })
            .Where(d => !CurrentDocumentOnly || d.Location?.FilePath == CurrentDocumentPath)
            .Where(Matches)
            .OrderByDescending(d => d.Severity)
            .ThenBy(d => d.Location?.FilePath, StringComparer.Ordinal)
            .ThenBy(d => d.Location?.Start ?? 0)
            .Select(CreateItem)
            .ToList();

        if (!visible.SequenceEqual(Items))
        {
            Items.Clear();
            foreach (var item in visible)
            {
                Items.Add(item);
            }
        }

        HasItems = Items.Count > 0;
        Refreshed?.Invoke(this, EventArgs.Empty);
    }

    private bool Matches(BuildDiagnostic diagnostic)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        var text = SearchText.Trim();
        return diagnostic.Id.Contains(text, StringComparison.OrdinalIgnoreCase)
            || diagnostic.Message.Contains(text, StringComparison.OrdinalIgnoreCase)
            || (diagnostic.ProjectName?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false)
            || (diagnostic.FileName?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static string Plural(int count, string noun) =>
        count == 1 ? $"1 {noun}" : $"{count.ToString(System.Globalization.CultureInfo.InvariantCulture)} {noun}s";
}
