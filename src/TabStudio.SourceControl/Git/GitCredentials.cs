namespace TabStudio.SourceControl.Git;

/// <summary>
/// What a Git server's HTTPS sign-in takes: a user name and a password or access token, sent as Git sends them, with
/// HTTP basic authentication.
/// </summary>
/// <param name="UserName">The user name; hosts that take a token alone accept any, such as <c>oauth2</c> on GitLab.</param>
/// <param name="Password">The password or access token.</param>
public sealed record GitCredentials(string UserName, string Password)
{
    /// <summary>The credentials GitHub's Git server takes for a token.</summary>
    /// <param name="token">A GitHub token.</param>
    public static GitCredentials ForGitHubToken(string token) => new("x-access-token", token);

    /// <inheritdoc />
    public override string ToString() => $"{UserName}:***";
}
