using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Notebooks.Markdown;
using TabStudio.SourceControl;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.ViewModels.PullRequests;

/// <summary>The overview: the description, read as Markdown, and the reviewers with their verdicts.</summary>
public sealed partial class PullRequestViewModel
{
    /// <summary>The description, read as Markdown.</summary>
    public IReadOnlyList<MarkdownBlock> Description => Info?.Body is { } body ? MarkdownParser.Parse(body) : [];

    /// <summary>Whether there is a description.</summary>
    public bool HasDescription => Info?.Body is not null;

    /// <summary>The people asked to review and those who did, each with their latest verdict.</summary>
    public ObservableCollection<ReviewerViewModel> Reviewers { get; } = [];

    /// <summary>Whether anyone reviewed or was asked to.</summary>
    public bool HasReviewers => Reviewers.Count > 0;

    /// <summary>Lists the reviewers: everyone who submitted a review, by their latest verdict, then those still asked to.</summary>
    private void BuildReviewers(IReadOnlyList<PullRequestReview> reviews)
    {
        Reviewers.Clear();
        var verdicts = new Dictionary<string, ReviewerState>(StringComparer.OrdinalIgnoreCase);
        foreach (var review in reviews.Where(r => r.State != ReviewState.Pending && !string.Equals(r.Author, Author, StringComparison.OrdinalIgnoreCase)))
        {
            var state = review.State switch
            {
                ReviewState.Approved => ReviewerState.Approved,
                ReviewState.ChangesRequested => ReviewerState.ChangesRequested,
                _ => ReviewerState.Commented,
            };

            // A comment after a verdict leaves the verdict standing, as on GitHub.
            if (state != ReviewerState.Commented || !verdicts.ContainsKey(review.Author))
            {
                verdicts[review.Author] = state;
            }
        }

        foreach (var requested in Info?.RequestedReviewers ?? [])
        {
            verdicts[requested] = ReviewerState.Requested;
        }

        foreach (var (login, state) in verdicts.OrderBy(v => v.Key, StringComparer.OrdinalIgnoreCase))
        {
            Reviewers.Add(new ReviewerViewModel(login, state));
        }

        OnPropertyChanged(nameof(HasReviewers));
    }

    /// <summary>Asks someone to review, by their login.</summary>
    [RelayCommand]
    private async Task RequestReviewAsync()
    {
        if (!RequireSignIn("request reviews"))
        {
            return;
        }

        var login = await _host.Prompt.AskAsync("Request a Review", $"GitHub asks this person to review pull request {NumberText}. Enter their GitHub user name.", "Request");
        if (string.IsNullOrWhiteSpace(login))
        {
            return;
        }

        ErrorText = null;
        try
        {
            var info = await _host.CreateClient().RequestReviewersAsync(Owner, Repository, Number, [login.TrimStart('@')], _lifetime.Token);
            if (Details is { } details)
            {
                Details = details with { Info = info };
            }

            BuildReviewers(_reviews);
            InfoText = $"Asked {login.TrimStart('@')} to review.";
            _host.ReportStatus(InfoText);
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
}
