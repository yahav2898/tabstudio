using System.Net;

namespace TabStudio.SourceControl.Git;

/// <summary>A Git server could not be reached, refused, or answered something TabStudio cannot use.</summary>
public sealed class GitTransportException : Exception
{
    /// <summary>Creates the exception.</summary>
    public GitTransportException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What went wrong, in plain words.</param>
    public GitTransportException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and its cause.</summary>
    /// <param name="message">What went wrong, in plain words.</param>
    /// <param name="innerException">The cause.</param>
    public GitTransportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates the exception for an HTTP answer.</summary>
    /// <param name="statusCode">What the server answered.</param>
    /// <param name="message">What went wrong, in plain words.</param>
    public GitTransportException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    /// <summary>The HTTP status, or <see langword="null"/> when the server was not reached or broke off.</summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>The server wants a sign-in, or refused the one given.</summary>
    public bool IsUnauthorized => StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;

    /// <summary>The server has no such repository, or does not show it without a sign-in.</summary>
    public bool IsNotFound => StatusCode == HttpStatusCode.NotFound;

    /// <summary>A failure that trying again may get past: the network, a timeout or a server error.</summary>
    public bool IsTransient => StatusCode is null or HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or >= HttpStatusCode.InternalServerError;
}
