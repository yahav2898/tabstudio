using System.Net;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.Services.Ci;

/// <summary>Turns failures of the checks and Actions APIs into sentences for the user.</summary>
public static class ActionsErrors
{
    /// <summary>What a token needs for everything the GitHub Actions window does.</summary>
    public const string PermissionsHint =
        "A fine-grained token needs \"Actions: read and write\", \"Checks: read\" and \"Commit statuses: read\"; a classic token needs the repo scope.";

    /// <summary>Describes a failure.</summary>
    /// <param name="exception">The failure.</param>
    /// <param name="isSignedIn">Whether the request was made signed in.</param>
    public static string Describe(Exception exception, bool isSignedIn)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception switch
        {
            GitHubApiException { IsUnauthorized: true } => "GitHub did not accept your token. Sign in to GitHub again.",
            GitHubApiException { IsRateLimited: true } api => isSignedIn
                ? $"GitHub's request limit is reached until {api.RateLimitReset!.Value.ToLocalTime():HH:mm}."
                : $"GitHub's request limit is reached until {api.RateLimitReset!.Value.ToLocalTime():HH:mm}. Signing in to GitHub raises it from 60 to 5,000 requests an hour.",
            GitHubApiException { IsNotFound: true } => isSignedIn
                ? "GitHub could not find this, or your token cannot see it."
                : "GitHub could not find this. If the repository is private, sign in to GitHub.",
            GitHubApiException { StatusCode: HttpStatusCode.Forbidden } api when IsMissingPermission(api) =>
                $"Your GitHub token is missing a permission. {PermissionsHint}",
            GitHubApiException { StatusCode: HttpStatusCode.Forbidden } api => $"GitHub refused: {api.Message}",
            GitHubApiException api => api.Message,
            OperationCanceledException => "Cancelled.",
            _ => exception.Message,
        };
    }

    /// <summary>Describes a failure to re-run, cancel or start a run, where a missing permission is the usual cause.</summary>
    /// <param name="exception">The failure.</param>
    /// <param name="isSignedIn">Whether the request was made signed in.</param>
    public static string DescribeAction(Exception exception, bool isSignedIn)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (!isSignedIn && exception is GitHubApiException { IsUnauthorized: true } or GitHubApiException { IsNotFound: true })
        {
            return "Sign in to GitHub to re-run, cancel or start workflow runs.";
        }

        return exception is GitHubApiException { StatusCode: HttpStatusCode.Forbidden or HttpStatusCode.NotFound } api && isSignedIn && IsMissingPermission(api)
            ? "Your GitHub token cannot do this: it needs the \"Actions: read and write\" permission (or the repo scope for a classic token)."
            : Describe(exception, isSignedIn);
    }

    private static bool IsMissingPermission(GitHubApiException exception) =>
        exception.Message.Contains("not accessible", StringComparison.OrdinalIgnoreCase)
        || exception.Message.Contains("permission", StringComparison.OrdinalIgnoreCase)
        || exception.Message.Contains("Must have admin rights", StringComparison.OrdinalIgnoreCase);
}
