using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Platform;
using TabStudio.App.Services.Ci;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.ViewModels.Actions;

/// <summary>The notice the shell shows when a run finishes; tapping it opens the run's details.</summary>
/// <param name="Owner">The repository's owner.</param>
/// <param name="Repository">The repository's name.</param>
/// <param name="Run">The run, as it finished.</param>
/// <param name="Title">"CI #57 failed".</param>
/// <param name="Text">"Fix the build · main · 3m 12s".</param>
public sealed record RunNoticeViewModel(string Owner, string Repository, GitHubWorkflowRun Run, string Title, string Text)
{
    /// <summary>The run's icon and color.</summary>
    public RunVisual Visual => RunVisuals.For(Run.Status, Run.Conclusion);
}

/// <summary>
/// Telling the user when a run finishes: runs on the workspace's branch, and runs started or re-run from TabStudio.
/// A notice in the shell, and on Android a system notification while TabStudio is in the background, if the user
/// turned that on. Both only while TabStudio is running.
/// </summary>
public sealed partial class ActionsViewModel
{
    private const string RunTagPrefix = "run:";
    private static readonly TimeSpan NoticeTime = TimeSpan.FromSeconds(12);

    private INotifications _notifications = null!;
    private CancellationTokenSource? _noticeTimer;

    /// <summary>The notice of the latest finished run, while it is shown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotice))]
    public partial RunNoticeViewModel? Notice { get; private set; }

    /// <summary>Whether a notice is shown.</summary>
    public bool HasNotice => Notice is not null;

    /// <summary>Why system notifications could not be turned on.</summary>
    [ObservableProperty]
    public partial string? NotificationsErrorText { get; private set; }

    /// <summary>Whether the platform has system notifications, so the window offers them.</summary>
    public bool AreSystemNotificationsSupported => _notifications.IsSupported;

    /// <summary>Whether to show a notice when a run finishes. Remembered between sessions.</summary>
    public bool NotifyWhenRunsFinish
    {
        get => _settings.NotifyWhenRunsFinish;
        set
        {
            if (value != _settings.NotifyWhenRunsFinish)
            {
                SaveSettings(_settings with { NotifyWhenRunsFinish = value });
                OnPropertyChanged();
                UpdateBackgroundPolling();
            }
        }
    }

    /// <summary>
    /// Whether to also show a system notification while TabStudio is in the background. Turning it on asks for
    /// Android's permission the first time.
    /// </summary>
    public bool UseSystemNotifications
    {
        get => _settings.UseSystemNotifications && _notifications.IsAllowed;
        set
        {
            if (value == UseSystemNotifications)
            {
                return;
            }

            if (!value)
            {
                SaveSettings(_settings with { UseSystemNotifications = false });
                OnPropertyChanged();
                UpdateBackgroundPolling();
                return;
            }

            _ = EnableSystemNotificationsAsync();
        }
    }

    private void InitializeNotifications(INotifications notifications)
    {
        _notifications = notifications;
        _notifications.Opened += OnNotificationOpened;
        UpdateBackgroundPolling();
    }

    private void DisposeNotifications()
    {
        _notifications.Opened -= OnNotificationOpened;
        _noticeTimer?.Cancel();
        _noticeTimer?.Dispose();
        _noticeTimer = null;
    }

    private async Task EnableSystemNotificationsAsync()
    {
        NotificationsErrorText = null;
        var allowed = _notifications.IsAllowed || await _notifications.RequestPermissionAsync();
        if (allowed)
        {
            SaveSettings(_settings with { UseSystemNotifications = true, NotifyWhenRunsFinish = true });
            OnPropertyChanged(nameof(NotifyWhenRunsFinish));
        }
        else
        {
            NotificationsErrorText = "Android did not allow TabStudio's notifications. Allow them in Settings > Apps > TabStudio > Notifications, then turn this on again.";
        }

        OnPropertyChanged(nameof(UseSystemNotifications));
        UpdateBackgroundPolling();
    }

    /// <summary>In the background, the monitor only polls for someone who will see the system notification.</summary>
    private void UpdateBackgroundPolling() =>
        Monitor.KeepsPollingInBackground = NotifyWhenRunsFinish && UseSystemNotifications;

    private void OnRunFinished(object? sender, CiRunFinished finished)
    {
        if (RunDetails is { } details && details.Run.Id == finished.Run.Id)
        {
            _ = details.RefreshAsync(_lifetime.Token);
        }

        if (!NotifyWhenRunsFinish)
        {
            return;
        }

        var run = finished.Run;
        var title = $"{run.WorkflowName} #{run.RunNumber.ToString(CultureInfo.CurrentCulture)} {ActionsFormat.State(run.Status, run.Conclusion)}";
        var parts = new List<string> { run.Title };
        if (run.HeadBranch is { } branch)
        {
            parts.Add(branch);
        }

        if (run.Duration(DateTimeOffset.UtcNow) is { } duration)
        {
            parts.Add(ActionsFormat.Duration(duration));
        }

        if (!string.Equals(finished.Repository, WorkspaceLink?.Repository, StringComparison.OrdinalIgnoreCase))
        {
            parts.Add($"{finished.Owner}/{finished.Repository}");
        }

        var text = string.Join(" · ", parts);
        ShowNotice(new RunNoticeViewModel(finished.Owner, finished.Repository, run, title, text));
        if (UseSystemNotifications && _activity.IsInBackground)
        {
            _notifications.Show(new SystemNotification(RunTag(finished.Owner, finished.Repository, run.Id), title, text, run.IsFailed));
        }
    }

    private void ShowNotice(RunNoticeViewModel notice)
    {
        Notice = notice;
        _noticeTimer?.Cancel();
        _noticeTimer?.Dispose();
        _noticeTimer = new CancellationTokenSource();
        _ = HideNoticeLaterAsync(notice, _noticeTimer.Token);
    }

    private async Task HideNoticeLaterAsync(RunNoticeViewModel notice, CancellationToken token)
    {
        try
        {
            await Task.Delay(NoticeTime, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (Notice == notice)
        {
            Notice = null;
        }
    }

    /// <summary>Opens the run of the notice.</summary>
    [RelayCommand]
    private async Task OpenNoticeAsync()
    {
        if (Notice is { } notice)
        {
            Notice = null;
            await ShowRunAsync(notice.Owner, notice.Repository, notice.Run);
        }
    }

    [RelayCommand]
    private void DismissNotice() => Notice = null;

    /// <summary>The user tapped a system notification: open its run.</summary>
    private async void OnNotificationOpened(object? sender, string tag)
    {
        if (!tag.StartsWith(RunTagPrefix, StringComparison.Ordinal))
        {
            return;
        }

        var parts = tag[RunTagPrefix.Length..].Split('/');
        if (parts.Length != 3 || !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var runId))
        {
            return;
        }

        try
        {
            var run = await Client.GetWorkflowRunAsync(parts[0], parts[1], runId, _lifetime.Token);
            await ShowRunAsync(parts[0], parts[1], run);
        }
        catch (GitHubApiException ex)
        {
            ErrorText = Describe(ex);
            _host.ShowActions();
        }
        catch (OperationCanceledException)
        {
            // The workspace closed.
        }
    }

    private static string RunTag(string owner, string repository, long runId) =>
        $"{RunTagPrefix}{owner}/{repository}/{runId.ToString(CultureInfo.InvariantCulture)}";
}
