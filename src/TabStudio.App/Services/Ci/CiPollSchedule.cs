using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.Services.Ci;

/// <summary>
/// How often CI is asked again: often while something runs, rarely when all is done, more rarely signed out
/// (60 requests an hour), and not before the request limit resets once it is reached.
/// </summary>
public static class CiPollSchedule
{
    /// <summary>The wait while checks or runs are in progress, signed in. A 304 answer costs nothing then.</summary>
    public static readonly TimeSpan Active = TimeSpan.FromSeconds(12);

    /// <summary>The wait when everything has finished, signed in.</summary>
    public static readonly TimeSpan Idle = TimeSpan.FromSeconds(90);

    /// <summary>The wait while something runs, signed out, where every request counts against 60 an hour.</summary>
    public static readonly TimeSpan ActiveSignedOut = TimeSpan.FromMinutes(2);

    /// <summary>The wait when everything has finished, signed out.</summary>
    public static readonly TimeSpan IdleSignedOut = TimeSpan.FromMinutes(5);

    /// <summary>The wait after GitHub could not be reached or refused, before trying again.</summary>
    public static readonly TimeSpan AfterError = TimeSpan.FromMinutes(1);

    /// <summary>The wait while the app is in the background and runs someone waits for are in progress.</summary>
    public static readonly TimeSpan Background = TimeSpan.FromMinutes(1);

    /// <summary>How long after a new commit CI is asked often, so the checks it starts show up quickly.</summary>
    public static readonly TimeSpan AfterNewCommit = TimeSpan.FromMinutes(2);

    /// <summary>The wait before the next poll.</summary>
    /// <param name="isActive">Whether checks or runs are in progress, or a new commit was just made.</param>
    /// <param name="isSignedIn">Whether requests are made with a token.</param>
    /// <param name="rateLimit">The request limit the last response reported.</param>
    /// <param name="lastPollFailed">Whether the last poll failed.</param>
    /// <param name="now">The time now.</param>
    public static TimeSpan Next(bool isActive, bool isSignedIn, GitHubRateLimit? rateLimit, bool lastPollFailed, DateTimeOffset now)
    {
        if (rateLimit is { IsExhausted: true } exhausted)
        {
            var untilReset = exhausted.Reset - now + TimeSpan.FromSeconds(5);
            return untilReset > AfterError ? untilReset : AfterError;
        }

        var wait = lastPollFailed ? AfterError
            : isSignedIn ? (isActive ? Active : Idle)
            : isActive ? ActiveSignedOut : IdleSignedOut;

        // Running low: slow down so the rest of the IDE keeps some requests.
        if (rateLimit is { } limit && limit.Limit > 0 && limit.Remaining < limit.Limit / 10)
        {
            wait *= 4;
        }

        return wait;
    }
}
