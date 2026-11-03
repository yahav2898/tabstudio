using TabStudio.SourceControl.Git;

namespace TabStudio.SourceControl.GitHub;

public sealed partial class GitHubClient
{
    /// <summary>The HTTP client, shared with Git's own protocol and Git LFS.</summary>
    internal HttpClient Http => _http;

    /// <summary>
    /// The address of a repository on this server for Git's own protocol, as <c>git clone</c> uses it; <see langword="null"/>
    /// when the server's web address is not known.
    /// </summary>
    internal Uri? GitAddress(string owner, string repository) =>
        _webAddress is { } web ? new Uri(web, $"{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repository)}.git") : null;

    /// <summary>The sign-in Git's protocol takes for an address on this server: the token, when signed in.</summary>
    internal GitCredentials? GitCredentialsFor(Uri address) =>
        _token is not null && _webAddress is { } web && string.Equals(web.Host, address.Host, StringComparison.OrdinalIgnoreCase)
            ? GitCredentials.ForGitHubToken(_token)
            : null;
}
