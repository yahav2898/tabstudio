using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Services.Ci;
using TabStudio.App.ViewModels.Actions;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.ViewModels.PullRequests;

/// <summary>
/// The checks of the pull request's head commit, beside its mergeability in the header: GitHub Actions jobs, other
/// apps' check runs and external CI statuses, in the same terms as the status bar's CI item.
/// </summary>
public sealed partial class PullRequestViewModel
{
    /// <summary>The head commit's checks, once read; <see langword="null"/> when GitHub could not be asked.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChecks), nameof(ChecksVisual), nameof(ChecksText))]
    public partial CiSummary? Checks { get; private set; }

    /// <summary>Whether any check has reported on the head commit.</summary>
    public bool HasChecks => Checks is { Total: > 0 };

    /// <summary>The checks' icon and color.</summary>
    public RunVisual ChecksVisual => Checks is { } checks ? RunVisuals.For(checks.State) : RunVisual.None;

    /// <summary>"All 5 checks passed", "1 of 5 checks failed" or "2 of 5 checks done".</summary>
    public string ChecksText => Checks?.Description ?? string.Empty;

    /// <summary>
    /// Reads the head commit's check runs and statuses. The checks only add to the page, so when GitHub refuses them
    /// (a token without the Checks permission, say) the header simply leaves them out.
    /// </summary>
    private async Task LoadChecksAsync(GitHubClient client, string sha, CancellationToken cancellationToken)
    {
        try
        {
            var checkRuns = client.GetCheckRunsAsync(Owner, Repository, sha, cancellationToken);
            var statuses = client.GetCombinedStatusAsync(Owner, Repository, sha, cancellationToken);
            await Task.WhenAll(checkRuns, statuses);
            Checks = CiSummary.From(sha, checkRuns.Result, statuses.Result);
        }
        catch (Exception ex) when (ex is GitHubApiException or HttpRequestException)
        {
            Checks = null;
        }
    }

    /// <summary>Opens the pull request's Checks tab on GitHub, which lists every check with its details.</summary>
    [RelayCommand]
    private async Task OpenChecksAsync()
    {
        var page = Info?.WebUri is { } web
            ? new Uri(web.AbsoluteUri.TrimEnd('/') + "/checks")
            : new Uri(string.Create(CultureInfo.InvariantCulture, $"https://github.com/{Owner}/{Repository}/pull/{Number}/checks"));
        await _host.OpenUriAsync(page);
    }
}
