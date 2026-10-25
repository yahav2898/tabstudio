using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Agent;
using TabStudio.Agent.Anthropic;
using TabStudio.SourceControl;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.ViewModels.PullRequests;

/// <summary>
/// Review with Claude: Claude reads the diff with the user's API key and drafts comments on its lines. The drafts
/// show in the files list and the diffs, marked as Claude's; nothing reaches GitHub unless the user keeps one.
/// </summary>
public sealed partial class PullRequestViewModel
{
    /// <summary>Claude's draft comments, by file and line.</summary>
    public ObservableCollection<DraftCommentViewModel> Drafts { get; } = [];

    /// <summary>Whether Claude drafted comments that wait for a decision.</summary>
    public bool HasDrafts => Drafts.Count > 0;

    /// <summary>"3 drafts".</summary>
    public string DraftsText => ReviewDisplay.Count(Drafts.Count, "draft comment");

    /// <summary>Whether Claude is reviewing.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReviewWithClaudeCommand))]
    public partial bool IsClaudeReviewing { get; private set; }

    /// <summary>What Claude is doing, while it reviews.</summary>
    [ObservableProperty]
    public partial string? ClaudeStatus { get; private set; }

    /// <summary>Claude's summary of the pull request, after it reviewed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasClaudeSummary))]
    public partial string? ClaudeSummary { get; private set; }

    /// <summary>Whether to show Claude's summary.</summary>
    public bool HasClaudeSummary => ClaudeSummary is not null;

    /// <summary>Whether to explain that Claude needs an API key, after the button was pressed without one.</summary>
    [ObservableProperty]
    public partial bool NeedsClaudeKey { get; private set; }

    /// <summary>Keeps one of Claude's drafts: it becomes a comment of the pending review.</summary>
    /// <param name="draft">The draft.</param>
    /// <returns>Why it could not be kept, or <see langword="null"/>.</returns>
    internal async Task<string?> KeepDraftAsync(DraftCommentViewModel draft)
    {
        var error = await AddPendingCommentAsync(new NewReviewComment(draft.Path, draft.Line, draft.Side, draft.Body.Trim()));
        if (error is null)
        {
            RemoveDraft(draft);
        }

        return error;
    }

    /// <summary>Dismisses one of Claude's drafts.</summary>
    /// <param name="draft">The draft.</param>
    internal void DismissDraft(DraftCommentViewModel draft) => RemoveDraft(draft);

    /// <summary>Asks Claude to review the diff, and shows its findings as drafts.</summary>
    [RelayCommand(CanExecute = nameof(CanReviewWithClaude))]
    private async Task ReviewWithClaudeAsync()
    {
        var settings = _host.LoadAgentSettings();
        if (!settings.HasApiKey)
        {
            NeedsClaudeKey = true;
            return;
        }

        NeedsClaudeKey = false;
        ErrorText = null;
        IsClaudeReviewing = true;
        ClaudeStatus = $"Claude is reviewing {ReviewDisplay.Count(Files.Count, "file")}…";
        try
        {
            var reviewer = new PullRequestReviewer(new AnthropicClient(_host.AgentHttp, settings.ApiKey!), settings.Model);
            var request = new PullRequestReviewRequest(Title, Info?.Body, [.. Files.Select(ToReviewedFile)]);
            var review = await reviewer.ReviewAsync(request, _lifetime.Token);

            Drafts.Clear();
            foreach (var finding in review.Findings)
            {
                Drafts.Add(new DraftCommentViewModel(this, finding));
            }

            ClaudeSummary = review.SkippedFiles > 0
                ? $"{review.Summary} ({ReviewDisplay.Count(review.SkippedFiles, "file")} left out: the diff was too long to send.)"
                : review.Summary;
            OnDraftsChanged();
            Section = PullRequestSection.Files;
            _host.ReportStatus(review.Findings.Count == 0 ? "Claude has no comments." : $"Claude drafted {DraftsText}.");
        }
        catch (OperationCanceledException)
        {
            // The workspace closed.
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            ErrorText = ex is AnthropicException ? ex.Message : SyncErrors.Describe(ex);
        }
        finally
        {
            IsClaudeReviewing = false;
            ClaudeStatus = null;
        }
    }

    [RelayCommand]
    private void OpenClaudeSettings()
    {
        NeedsClaudeKey = false;
        _host.ShowAgentSettings();
    }

    [RelayCommand]
    private void CloseClaudeNotice() => NeedsClaudeKey = false;

    [RelayCommand]
    private void DismissClaudeSummary() => ClaudeSummary = null;

    private bool CanReviewWithClaude() => !IsClaudeReviewing && Files.Count > 0;

    private void RemoveDraft(DraftCommentViewModel draft)
    {
        Drafts.Remove(draft);
        OnDraftsChanged();
    }

    private void OnDraftsChanged()
    {
        OnPropertyChanged(nameof(HasDrafts));
        OnPropertyChanged(nameof(DraftsText));
        UpdateFileCounts();
        RaiseReviewChanged();
    }

    /// <summary>A file's diff as Claude reads it: GitHub's patch, with the numbers of both versions.</summary>
    private static ReviewedFile ToReviewedFile(PullRequestFileViewModel file) => new(
        file.Path,
        file.File.Status switch
        {
            PullRequestFileStatus.Added or PullRequestFileStatus.Copied => "added",
            PullRequestFileStatus.Removed => "deleted",
            PullRequestFileStatus.Renamed => $"renamed from {file.File.PreviousPath}",
            _ => "modified",
        },
        [.. file.Patch.Lines.Select(l => new ReviewedLine(
            l.Kind switch
            {
                PatchLineKind.Added => ReviewedLineKind.Added,
                PatchLineKind.Removed => ReviewedLineKind.Removed,
                _ => ReviewedLineKind.Context,
            },
            l.OldLine,
            l.NewLine,
            l.Text))]);
}
