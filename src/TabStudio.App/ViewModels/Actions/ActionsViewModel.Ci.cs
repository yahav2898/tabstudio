using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Services.Ci;
using TabStudio.SourceControl;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.ViewModels.Actions;

/// <summary>
/// The workspace commit's CI: the "This commit" section, the status bar's CI item, and CI's errors and warnings
/// in the Error List, which replace the previous ones when CI reports on a new commit and go away when it passes.
/// </summary>
public sealed partial class ActionsViewModel
{
    private const int ToolTipChecks = 8;

    private string? _diagnosticsKey;
    private CancellationTokenSource? _mapping;

    /// <summary>The checks of the workspace's commit, failed ones first.</summary>
    public ObservableCollection<CiCheckItemViewModel> Checks { get; } = [];

    /// <summary>The CI state of the workspace's commit.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CiVisual), nameof(CiText), nameof(ChecksSummary), nameof(HasChecks))]
    public partial CiState CiState { get; private set; }

    /// <summary>Whether the workspace commit's CI has been read.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CiText), nameof(ChecksSummary))]
    public partial bool IsCiLoaded { get; private set; }

    /// <summary>Why the workspace commit's CI could not be read.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CiText), nameof(HasCiError))]
    public partial string? CiErrorText { get; private set; }

    /// <summary>The status bar's tooltip: the commit, the counts and the checks.</summary>
    [ObservableProperty]
    public partial string CiToolTip { get; private set; } = "GitHub Actions";

    /// <summary>"2/5", "Passed", "1 failed".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChecksSummary))]
    public partial string CiProgressText { get; private set; } = string.Empty;

    /// <summary>Whether the status bar shows the CI item: when the workspace is linked to GitHub.</summary>
    public bool IsCiVisible => IsLinked;

    /// <summary>Whether reading CI failed.</summary>
    public bool HasCiError => CiErrorText is not null;

    /// <summary>Whether the commit has checks.</summary>
    public bool HasChecks => Checks.Count > 0;

    /// <summary>The CI state's icon and color.</summary>
    public RunVisual CiVisual => RunVisuals.For(CiState);

    /// <summary>The status bar's text.</summary>
    public string CiText => CiState switch
    {
        CiState.None when CiErrorText is not null => "CI",
        CiState.None => IsCiLoaded ? "No checks" : "CI",
        _ => CiProgressText,
    };

    /// <summary>"This commit · 1a2b3c4 on main".</summary>
    public string ChecksHeader => WorkspaceLink is { } link
        ? $"{(link.BaseCommit.Length > 7 ? link.BaseCommit[..7] : link.BaseCommit)}{(link.Branch is { } branch ? $" on {branch}" : string.Empty)}"
        : string.Empty;

    /// <summary>"2 of 5 checks done · 1 failed", or that none has reported.</summary>
    public string ChecksSummary => Monitor.Summary is { } summary
        ? summary.Description
        : IsLinked ? "Reading this commit's checks…" : string.Empty;

    /// <summary>Called by the monitor after every poll: shows the state, and CI's diagnostics when something failed.</summary>
    private void OnMonitorChanged(object? sender, EventArgs e)
    {
        var summary = Monitor.Summary;
        CiErrorText = Monitor.ErrorText;
        IsCiLoaded = summary is not null;
        CiProgressText = summary is null ? string.Empty : ProgressText(summary);
        CiState = summary?.State ?? CiState.None;
        ShowChecks(summary);
        CiToolTip = ToolTipText(summary);
        OnPropertyChanged(nameof(ChecksSummary));
        OnPropertyChanged(nameof(ChecksHeader));
        UpdateCiDiagnostics(summary);
        if (IsShown)
        {
            RequestWindowRefresh();
        }
    }

    /// <summary>The workspace moved to another commit, branch or repository, or was unlinked.</summary>
    private void OnLinkChanged()
    {
        OnPropertyChanged(nameof(ChecksHeader));
        OnPropertyChanged(nameof(ChecksSummary));
        if (WorkspaceLink is null)
        {
            _diagnosticsKey = null;
            _mapping?.Cancel();
            _host.ShowCiDiagnostics([]);
        }
    }

    private void ShowChecks(CiSummary? summary)
    {
        Checks.Clear();
        var now = DateTimeOffset.UtcNow;
        foreach (var check in summary?.Checks ?? [])
        {
            Checks.Add(new CiCheckItemViewModel(check, now, OpenCheckCommand));
        }

        OnPropertyChanged(nameof(HasChecks));
    }

    /// <summary>
    /// Shows the annotations of a failing commit in the Error List; clears them once a commit passes. While a new
    /// commit's CI runs without failures, the previous commit's stay, labelled with that commit.
    /// </summary>
    private void UpdateCiDiagnostics(CiSummary? summary)
    {
        if (summary is null || _host.WorkspaceRoot is not { } workspaceRoot)
        {
            return;
        }

        if (summary.State == CiState.Passed)
        {
            if (_diagnosticsKey is not null)
            {
                _diagnosticsKey = null;
                _mapping?.Cancel();
                _host.ShowCiDiagnostics([]);
            }

            return;
        }

        if (summary.FailedCount == 0 || GitHubSync.FindRoot(workspaceRoot) is not { } repositoryRoot)
        {
            return;
        }

        var annotations = Monitor.Annotations;
        var key = $"{summary.Sha}:{string.Join(',', annotations.Select(a => a.Check.Id).Distinct())}:{annotations.Count.ToString(CultureInfo.InvariantCulture)}";
        if (key == _diagnosticsKey)
        {
            return;
        }

        _diagnosticsKey = key;
        _mapping?.Cancel();
        _mapping?.Dispose();
        _mapping = new CancellationTokenSource();
        var token = _mapping.Token;
        _ = MapDiagnosticsAsync(annotations, summary.Sha, repositoryRoot, workspaceRoot, token);
    }

    private async Task MapDiagnosticsAsync(IReadOnlyList<CiAnnotation> annotations, string sha, string repositoryRoot, string workspaceRoot, CancellationToken token)
    {
        var diagnostics = await Task.Run(() => CiDiagnostics.Map(annotations, sha, repositoryRoot, workspaceRoot, _host.ProjectOf), token);
        if (!token.IsCancellationRequested && !_disposed)
        {
            _host.ShowCiDiagnostics(diagnostics);
        }
    }

    /// <summary>Opens a check: a GitHub Actions job's log, or another check's page.</summary>
    [RelayCommand]
    private async Task OpenCheckAsync(CiCheckItemViewModel? item)
    {
        if (item is null || WorkspaceLink is not { } link)
        {
            return;
        }

        if (item.Check is { Kind: CiCheckKind.Job, CheckRun: { WorkflowRunId: { } runId } check })
        {
            try
            {
                var run = await Client.GetWorkflowRunAsync(link.Owner, link.Repository, runId, _lifetime.Token);
                var jobs = await Client.GetJobsAsync(link.Owner, link.Repository, runId, _lifetime.Token);
                var job = jobs.FirstOrDefault(j => j.Id == check.Id) ?? jobs.FirstOrDefault(j => j.Name == check.Name);
                if (job is not null)
                {
                    await OpenLogAsync(link.Owner, link.Repository, run, job, null);
                    return;
                }
            }
            catch (GitHubApiException ex)
            {
                ErrorText = Describe(ex);
                return;
            }
        }

        if (item.Check.WebUri is { } page)
        {
            await _browser.OpenUriAsync(page);
        }
    }

    private static string ProgressText(CiSummary summary)
    {
        var culture = CultureInfo.CurrentCulture;
        return summary.State switch
        {
            CiState.Pending => "Queued",
            CiState.Running => $"{summary.Done.ToString(culture)}/{summary.Total.ToString(culture)}",
            CiState.Passed => "Passed",
            CiState.Failed => $"{summary.FailedCount.ToString(culture)} failed",
            CiState.Cancelled => "Cancelled",
            _ => "No checks",
        };
    }

    private string ToolTipText(CiSummary? summary)
    {
        if (WorkspaceLink is not { } link)
        {
            return "GitHub Actions";
        }

        var text = new StringBuilder();
        var sha = link.BaseCommit.Length > 7 ? link.BaseCommit[..7] : link.BaseCommit;
        text.Append(CultureInfo.CurrentCulture, $"CI of {sha}{(link.Branch is { } branch ? $" on {branch}" : string.Empty)}");
        if (Monitor.ErrorText is { } error)
        {
            text.Append('\n').Append(error);
        }
        else if (summary is null)
        {
            text.Append("\nReading the checks…");
        }
        else
        {
            text.Append('\n').Append(ChecksSummary);
            foreach (var check in summary.Checks.Take(ToolTipChecks))
            {
                text.Append("\n• ").Append(check.Name).Append(": ").Append(ActionsFormat.State(check.Status, check.Conclusion));
            }

            if (summary.Checks.Count > ToolTipChecks)
            {
                text.Append(CultureInfo.CurrentCulture, $"\n…and {(summary.Checks.Count - ToolTipChecks).ToString(CultureInfo.CurrentCulture)} more");
            }
        }

        text.Append("\nTap to open GitHub Actions.");
        return text.ToString();
    }
}
