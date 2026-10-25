using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Editing;
using TabStudio.SourceControl;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.ViewModels.PullRequests;

/// <summary>The changed files and their diffs, and the commits.</summary>
public sealed partial class PullRequestViewModel
{
    /// <summary>The commit the head branch left the base at, which diffs start from, once asked for.</summary>
    private string? _mergeBase;

    /// <summary>The files the pull request changes.</summary>
    public ObservableCollection<PullRequestFileViewModel> Files { get; } = [];

    /// <summary>The pull request's commits, oldest first.</summary>
    public ObservableCollection<PullRequestCommitItemViewModel> Commits { get; } = [];

    /// <summary>
    /// Opens a file's diff, from the version the branch started from to the pull request's head, with the review's
    /// threads under their lines.
    /// </summary>
    /// <param name="file">The file.</param>
    internal async Task OpenFileAsync(PullRequestFileViewModel file)
    {
        if (Info is not { } info)
        {
            return;
        }

        ErrorText = null;
        var token = _lifetime.Token;
        try
        {
            var client = _host.CreateClient();
            _mergeBase ??= await client.GetMergeBaseAsync(Owner, Repository, info.Base.Sha, info.Head.Sha, token);
            var before = file.File.Status == PullRequestFileStatus.Added
                ? null
                : await client.GetFileContentAsync(Owner, Repository, file.File.PreviousPath ?? file.Path, _mergeBase, token);
            var after = file.File.Status == PullRequestFileStatus.Removed ? null : await ReadHeadAsync(client, info, file.Path, token);
            if (!FileComparison.TryReadText(before, out var oldText) || !FileComparison.TryReadText(after, out var newText))
            {
                ErrorText = $"{file.Name} is a binary file, so its changes cannot be shown line by line.";
                return;
            }

            var diff = await Task.Run(() => InlineDiff.Create(oldText, newText), token);
            var comparison = new FileComparison(_host.ReviewFilePath(file.Path), file.Path, $"{info.Base.Name} @ {_mergeBase[..Math.Min(7, _mergeBase.Length)]}", diff)
            {
                Caption = $"{file.Name} ({NumberText})",
                BaseName = "Base",
                HeadName = $"{info.Head.Name} @ {info.Head.Sha[..Math.Min(7, info.Head.Sha.Length)]}",
            };
            _host.ShowFileReview(comparison, new FileReviewViewModel(this, file));
        }
        catch (OperationCanceledException)
        {
            // The workspace closed.
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            ErrorText = SyncErrors.Describe(ex);
        }
    }

    /// <summary>The review threads on a file, published and pending.</summary>
    /// <param name="path">The file's path.</param>
    internal IEnumerable<ReviewThreadViewModel> ThreadsFor(string path) => Threads.Where(t => t.Path == path);

    /// <summary>Claude's drafts on a file.</summary>
    /// <param name="path">The file's path.</param>
    internal IEnumerable<DraftCommentViewModel> DraftsFor(string path) => Drafts.Where(d => d.Path == path);

    /// <summary>The file row for a path, if the pull request changes the file.</summary>
    /// <param name="path">The file's path.</param>
    internal PullRequestFileViewModel? FileAt(string path) => Files.FirstOrDefault(f => f.Path == path);

    /// <summary>
    /// Reads the head version of a file. A fork's commits are in the base repository too, as GitHub keeps every pull
    /// request's head there; the fork itself is asked when they are not.
    /// </summary>
    private async Task<byte[]?> ReadHeadAsync(GitHubClient client, PullRequestInfo info, string path, CancellationToken token)
    {
        var content = await client.GetFileContentAsync(Owner, Repository, path, info.Head.Sha, token);
        if (content is null && info.IsFromFork(Owner, Repository) && info.Head.RepositoryFullName?.Split('/') is [var owner, var name])
        {
            content = await client.GetFileContentAsync(owner, name, path, info.Head.Sha, token);
        }

        return content;
    }

    private void ApplyFiles(IReadOnlyList<PullRequestFile> files)
    {
        Files.Clear();
        foreach (var file in files)
        {
            Files.Add(new PullRequestFileViewModel(this, file));
        }

        OnPropertyChanged(nameof(FilesTabText));
    }

    private void ApplyCommits(IReadOnlyList<GitCommit> commits)
    {
        Commits.Clear();
        var now = DateTimeOffset.Now;
        foreach (var commit in commits)
        {
            var when = commit.Author is { } author && author.Date > DateTimeOffset.MinValue ? ReviewDisplay.Age(author.Date, now) : string.Empty;
            Commits.Add(new PullRequestCommitItemViewModel(commit.Sha, commit.Summary, commit.Author?.Name is { Length: > 0 } name ? name : "Unknown author", when, OpenCommitCommand));
        }

        OnPropertyChanged(nameof(CommitsTabText));
    }

    /// <summary>Counts each file's threads, pending comments and drafts, for the Files changed list.</summary>
    private void UpdateFileCounts()
    {
        foreach (var file in Files)
        {
            var threads = ThreadsFor(file.Path).ToList();
            file.SetCounts(
                threads.Count(t => !t.IsPending),
                threads.Sum(t => t.Comments.Count(c => c.IsPending)),
                DraftsFor(file.Path).Count());
        }
    }

    [RelayCommand]
    private async Task OpenCommitAsync(PullRequestCommitItemViewModel? commit)
    {
        // The pull request's page is …/owner/repository/pull/12; its commits are at …/owner/repository/commit/sha.
        if (commit is not null && Info is { } info)
        {
            await _host.OpenUriAsync(new Uri(info.WebUri, $"../commit/{commit.Sha}"));
        }
    }
}
