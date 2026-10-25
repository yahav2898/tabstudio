using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using TabStudio.App.Editing;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.ViewModels.PullRequests;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Pull request review's documents: a pull request's page, the diff of one of its files with the review's threads,
/// and the threads shown in a code file's editor while the folder is on the pull request's branch.
/// </summary>
public sealed partial class DocumentViewModel
{
    /// <summary>The tab badge of a pull request's page.</summary>
    private static readonly FileType PullRequestFileType = new("Pull Request", "PR", BadgeKind.Script, null, null, null);

    private DocumentViewModel(PullRequestViewModel pullRequest, string tabPath, DeviceProfile profile)
        : this(tabPath, new TextFileContent(string.Empty, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)), language: null, profile)
    {
        PullRequest = pullRequest;
        FileType = PullRequestFileType;
    }

    /// <summary>For a pull request's page, the pull request; <see langword="null"/> for other documents.</summary>
    public PullRequestViewModel? PullRequest { get; }

    /// <summary>Whether this is a pull request's page.</summary>
    public bool IsPullRequest => PullRequest is not null;

    /// <summary>For the diff of a pull request's file, the file's review; <see langword="null"/> for other documents.</summary>
    public FileReviewViewModel? Review { get; private set; }

    /// <summary>Whether this is the diff of a pull request's file.</summary>
    public bool IsReview => Review is not null;

    /// <summary>A pull request's threads on this code file, shown in its editor while the folder is on the pull request's branch.</summary>
    [ObservableProperty]
    public partial EditorReviewViewModel? EditorReview { get; set; }

    /// <summary>Creates a pull request's page. Call on the UI thread.</summary>
    /// <param name="pullRequest">The pull request.</param>
    /// <param name="tabPath">A path for the tab that names no file on disk.</param>
    /// <param name="profile">The device profile.</param>
    public static DocumentViewModel ForPullRequest(PullRequestViewModel pullRequest, string tabPath, DeviceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);
        ArgumentException.ThrowIfNullOrEmpty(tabPath);
        ArgumentNullException.ThrowIfNull(profile);
        return new DocumentViewModel(pullRequest, tabPath, profile);
    }

    /// <summary>Creates the read-only diff of a pull request's file, with its review. Call on the UI thread.</summary>
    /// <param name="comparison">The two versions of the file.</param>
    /// <param name="review">The file's review.</param>
    /// <param name="profile">The device profile.</param>
    public static DocumentViewModel ForReview(FileComparison comparison, FileReviewViewModel review, DeviceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(review);
        var document = ForComparison(comparison, profile);
        document.Review = review;
        review.Attach(document);
        return document;
    }

    partial void OnEditorReviewChanged(EditorReviewViewModel? oldValue, EditorReviewViewModel? newValue) => oldValue?.Dispose();

    /// <summary>Lets go of the review's events when the document closes.</summary>
    private void DisposePullRequestReview()
    {
        Review?.Dispose();
        EditorReview = null;
    }
}
