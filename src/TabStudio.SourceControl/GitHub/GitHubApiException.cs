using System.Net;

namespace TabStudio.SourceControl.GitHub;

/// <summary>GitHub refused or failed a request.</summary>
public sealed class GitHubApiException : Exception
{
    /// <summary>Creates the exception.</summary>
    public GitHubApiException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What went wrong.</param>
    public GitHubApiException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and its cause.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">The cause.</param>
    public GitHubApiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates the exception for a response.</summary>
    /// <param name="statusCode">The HTTP status GitHub answered with.</param>
    /// <param name="message">GitHub's explanation, or a description of the failure.</param>
    /// <param name="rateLimitReset">When the rate limit resets, if the request was refused because of it.</param>
    public GitHubApiException(HttpStatusCode statusCode, string message, DateTimeOffset? rateLimitReset = null)
        : base(message)
    {
        StatusCode = statusCode;
        RateLimitReset = rateLimitReset;
    }

    /// <summary>The HTTP status, or <see langword="null"/> when GitHub could not be reached.</summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>When the rate limit resets, if the request was refused because the limit was reached.</summary>
    public DateTimeOffset? RateLimitReset { get; }

    /// <summary>The token is missing, wrong or expired.</summary>
    public bool IsUnauthorized => StatusCode == HttpStatusCode.Unauthorized;

    /// <summary>The repository, branch or object does not exist, or the account cannot see it.</summary>
    public bool IsNotFound => StatusCode == HttpStatusCode.NotFound;

    /// <summary>Too many requests were made; see <see cref="RateLimitReset"/>.</summary>
    public bool IsRateLimited => RateLimitReset is not null;

    /// <summary>GitHub rejected the change, for example a push that is not a fast-forward.</summary>
    public bool IsRejected => StatusCode is HttpStatusCode.UnprocessableEntity or HttpStatusCode.Conflict;
}
