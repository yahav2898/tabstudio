using TabStudio.SourceControl.GitHub;

namespace TabStudio.SourceControl;

/// <summary>Turns source control failures into sentences for the user.</summary>
public static class SyncErrors
{
    /// <summary>Describes a failure.</summary>
    /// <param name="exception">The failure.</param>
    public static string Describe(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception switch
        {
            SyncException sync => sync.Message,
            GitHubApiException { IsUnauthorized: true } => "GitHub did not accept your token. Sign in to GitHub again.",
            GitHubApiException { IsRateLimited: true } api =>
                $"GitHub's request limit is reached until {api.RateLimitReset!.Value.ToLocalTime():HH:mm}. Signing in to GitHub raises the limit.",
            GitHubApiException { IsNotFound: true } => "GitHub could not find the repository or branch, or your account cannot see it.",
            GitHubApiException { StatusCode: System.Net.HttpStatusCode.Forbidden } api => $"GitHub refused: {api.Message}",
            GitHubApiException api => api.Message,
            OperationCanceledException => "Cancelled.",
            IOException io => $"A file could not be read or written: {io.Message}",
            UnauthorizedAccessException access => $"A file could not be read or written: {access.Message}",
            _ => exception.Message,
        };
    }
}
