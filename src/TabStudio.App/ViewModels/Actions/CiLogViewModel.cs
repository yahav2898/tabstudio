using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Platform;
using TabStudio.App.Services.Ci;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.ViewModels.Actions;

/// <summary>Which job's log a tab shows.</summary>
/// <param name="Owner">The repository's owner.</param>
/// <param name="Repository">The repository's name.</param>
/// <param name="Run">The run the job belongs to.</param>
/// <param name="Job">The job.</param>
public sealed record CiLogSource(string Owner, string Repository, GitHubWorkflowRun Run, GitHubJob Job);

/// <summary>
/// A job's log in a read-only editor tab: downloads it (its start and end first when it is large), makes it
/// readable with <see cref="CiLogParser"/>, and lets the user show timestamps, the full log, or the log on GitHub.
/// </summary>
public sealed partial class CiLogViewModel : ObservableObject, IDisposable
{
    private readonly Func<GitHubClient> _client;
    private readonly IStoragePicker _browser;
    private readonly CancellationTokenSource _lifetime = new();
    private GitHubJobLog? _raw;
    private bool _disposed;

    /// <summary>Creates the log. Call <see cref="LoadAsync"/> to download it.</summary>
    /// <param name="source">The job.</param>
    /// <param name="client">Returns the GitHub client to download with.</param>
    /// <param name="browser">Opens the job's page.</param>
    public CiLogViewModel(CiLogSource source, Func<GitHubClient> client, IStoragePicker browser)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(browser);
        Source = source;
        _client = client;
        _browser = browser;
    }

    /// <summary>Raised on the UI thread when <see cref="Log"/> changes, after which the tab's text is the new log's.</summary>
    public event EventHandler? LogChanged;

    /// <summary>Raised when the tab should show a step, or the first error when the step is <see langword="null"/>.</summary>
    public event EventHandler<int?>? StepRequested;

    /// <summary>The job whose log this is.</summary>
    public CiLogSource Source { get; private set; }

    /// <summary>The job's name.</summary>
    public string JobName => Source.Job.Name;

    /// <summary>"CI #57 · attempt 2 · octocat/hello".</summary>
    public string RunText
    {
        get
        {
            var run = Source.Run;
            var attempt = run.Attempt > 1 ? $" · attempt {run.Attempt.ToString(CultureInfo.CurrentCulture)}" : string.Empty;
            return $"{run.WorkflowName} #{run.RunNumber.ToString(CultureInfo.CurrentCulture)}{attempt} · {Source.Owner}/{Source.Repository}";
        }
    }

    /// <summary>The job's icon and color.</summary>
    public RunVisual Visual => RunVisuals.For(Source.Job.Status, Source.Job.Conclusion);

    /// <summary>The job's state, such as "failed · 2m 28s".</summary>
    public string StateText
    {
        get
        {
            var job = Source.Job;
            var state = ActionsFormat.State(job.Status, job.Conclusion);
            return job.StartedAt is not null && GitHubRunStates.Duration(job.StartedAt, job.CompletedAt, DateTimeOffset.UtcNow) is { } time
                ? $"{state} · {ActionsFormat.Duration(time)}"
                : state;
        }
    }

    /// <summary>The readable log, once downloaded.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLog), nameof(ErrorCountText), nameof(HasErrors))]
    public partial CiLog? Log { get; private set; }

    /// <summary>Whether the log has been downloaded.</summary>
    public bool HasLog => Log is not null;

    /// <summary>Whether the log is downloading.</summary>
    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    /// <summary>Why the log could not be downloaded.</summary>
    [ObservableProperty]
    public partial string? ErrorText { get; private set; }

    /// <summary>Whether each line shows its time.</summary>
    [ObservableProperty]
    public partial bool ShowTimestamps { get; private set; }

    /// <summary>Whether part of the log was left out because it is large.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TruncatedText))]
    public partial bool IsTruncated { get; private set; }

    /// <summary>Whether the full log was asked for.</summary>
    [ObservableProperty]
    public partial bool IsFull { get; private set; }

    /// <summary>"Showing the start and end of this 37.2 MB log."</summary>
    public string? TruncatedText => _raw is { IsComplete: false } raw
        ? $"Showing the start and end of this {CiLogParser.Size(raw.TotalBytes)} log; {CiLogParser.Size(raw.OmittedBytes)} in the middle are left out."
        : null;

    /// <summary>"3 errors", for the header.</summary>
    public string ErrorCountText => Log?.ErrorLines.Count switch
    {
        null or 0 => "No errors",
        1 => "1 error",
        var count => $"{count.Value.ToString(CultureInfo.CurrentCulture)} errors",
    };

    /// <summary>Whether the log has errors to step through.</summary>
    public bool HasErrors => Log?.ErrorLines.Count > 0;

    /// <summary>The step to show once the log is loaded; <see langword="null"/> for the first error.</summary>
    public int? PendingStep { get; private set; }

    /// <summary>Downloads the log: its start and end when it is large.</summary>
    public Task LoadAsync() => LoadCoreAsync(GitHubJobLogLimits.Preview);

    /// <summary>Shows a step's part of the log, or the first error, now or once the log is loaded.</summary>
    /// <param name="stepNumber">The step, or <see langword="null"/> for the first error.</param>
    public void ShowStep(int? stepNumber)
    {
        PendingStep = stepNumber;
        if (Log is not null)
        {
            StepRequested?.Invoke(this, stepNumber);
        }
    }

    /// <summary>Points the tab at the job as it is now, such as after it finished, and downloads its log again.</summary>
    /// <param name="source">The job.</param>
    public Task UpdateAsync(CiLogSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Source = source;
        OnPropertyChanged(nameof(Visual));
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(RunText));
        return LoadCoreAsync(IsFull ? GitHubJobLogLimits.Full : GitHubJobLogLimits.Preview);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _lifetime.Cancel();
            _lifetime.Dispose();
        }
    }

    [RelayCommand]
    private Task ShowFullLogAsync()
    {
        IsFull = true;
        return LoadCoreAsync(GitHubJobLogLimits.Full);
    }

    [RelayCommand]
    private Task ReloadAsync() => LoadCoreAsync(IsFull ? GitHubJobLogLimits.Full : GitHubJobLogLimits.Preview);

    [RelayCommand]
    private async Task ToggleTimestampsAsync()
    {
        ShowTimestamps = !ShowTimestamps;
        if (_raw is not null)
        {
            await ParseAsync();
        }
    }

    [RelayCommand]
    private Task OpenOnGitHubAsync() => _browser.OpenUriAsync(Source.Job.WebUri ?? Source.Run.WebUri);

    private async Task LoadCoreAsync(GitHubJobLogLimits limits)
    {
        if (_disposed)
        {
            return;
        }

        IsLoading = true;
        ErrorText = null;
        var client = _client();
        try
        {
            _raw = await client.GetJobLogAsync(Source.Owner, Source.Repository, Source.Job.Id, limits, _lifetime.Token);
            IsTruncated = !_raw.IsComplete;
            OnPropertyChanged(nameof(TruncatedText));
            await ParseAsync();
        }
        catch (OperationCanceledException)
        {
            // The tab closed.
        }
        catch (GitHubApiException ex)
        {
            ErrorText = ex.IsNotFound
                ? Source.Job.IsCompleted
                    ? "GitHub no longer has this log: logs are kept for 90 days unless the repository keeps them for less."
                    : "GitHub publishes a job's log once the job finishes. Reload when it is done."
                : ActionsErrors.Describe(ex, client.IsSignedIn);
        }
        finally
        {
            if (!_disposed)
            {
                IsLoading = false;
            }
        }
    }

    private async Task ParseAsync()
    {
        if (_raw is not { } raw)
        {
            return;
        }

        var steps = Source.Job.Steps;
        var showTimestamps = ShowTimestamps;
        var failed = Source.Job.IsFailed;
        var log = await Task.Run(() => CiLogParser.Parse(raw, steps, showTimestamps, failed));
        if (_disposed)
        {
            return;
        }

        Log = log;
        LogChanged?.Invoke(this, EventArgs.Empty);
    }
}
