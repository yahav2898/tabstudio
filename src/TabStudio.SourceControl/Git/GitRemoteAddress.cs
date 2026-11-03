using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace TabStudio.SourceControl.Git;

/// <summary>
/// A Git repository's address on any host — GitHub, GitLab, Bitbucket, Azure DevOps, Codeberg, Gitea or a server of
/// one's own — as the HTTPS address TabStudio fetches and pushes over.
/// </summary>
/// <param name="Address">The HTTPS (or HTTP) address of the repository, without a sign-in.</param>
/// <param name="Owner">The path before the repository's name, such as a GitLab group with its subgroups or an Azure DevOps organization and project.</param>
/// <param name="Name">The repository's name, without <c>.git</c>.</param>
public sealed partial record GitRemoteAddress(Uri Address, string Owner, string Name)
{
    /// <summary>The host, such as <c>gitlab.com</c>.</summary>
    public string Host => Address.Host;

    /// <summary>Whether the repository is on github.com.</summary>
    public bool IsGitHub => string.Equals(Address.Host, "github.com", StringComparison.OrdinalIgnoreCase);

    /// <summary>The repository's web page, as the address without <c>.git</c>.</summary>
    public Uri WebUri
    {
        get
        {
            var text = Address.GetLeftPart(UriPartial.Path).TrimEnd('/');
            return new Uri(text.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? text[..^4] : text);
        }
    }

    /// <summary>"owner/name".</summary>
    public string FullName => Owner.Length == 0 ? Name : $"{Owner}/{Name}";

    /// <inheritdoc />
    public override string ToString() => Address.ToString();

    /// <summary>
    /// Reads a repository's address from what a person might paste: an HTTPS clone address, an SSH one
    /// (<c>git@host:path.git</c>, <c>ssh://git@host/path</c>), which is fetched over HTTPS instead, or a page of the
    /// repository on GitLab, Bitbucket, Gitea or Azure DevOps, which may name a branch.
    /// </summary>
    /// <param name="text">The pasted text.</param>
    /// <param name="address">The repository.</param>
    /// <param name="reference">A branch, tag or commit the address names, such as GitLab's <c>/-/tree/dev</c>.</param>
    /// <param name="credentials">A sign-in written into the address, as <c>https://user:token@host/…</c>.</param>
    public static bool TryParse(string? text, [NotNullWhen(true)] out GitRemoteAddress? address, out string? reference, out GitCredentials? credentials)
    {
        address = null;
        reference = null;
        credentials = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        text = text.Trim();
        if (!text.Contains("://", StringComparison.Ordinal) && BareHostPattern().IsMatch(text))
        {
            // "gitlab.com/group/project", as an address bar shows it.
            text = "https://" + text;
        }

        string host;
        string path;
        var scheme = "https";
        var port = -1;
        if (ScpPattern().Match(text) is { Success: true } scp)
        {
            host = scp.Groups["host"].Value;
            path = scp.Groups["path"].Value;
        }
        else if (Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http" or "ssh" or "git")
        {
            host = uri.Host;
            path = Uri.UnescapeDataString(uri.AbsolutePath);
            if (uri.Scheme is "https" or "http")
            {
                scheme = uri.Scheme;
                port = uri.IsDefaultPort ? -1 : uri.Port;
                var info = uri.UserInfo;
                var colon = info.IndexOf(':', StringComparison.Ordinal);
                if (colon > 0)
                {
                    credentials = new GitCredentials(Uri.UnescapeDataString(info[..colon]), Uri.UnescapeDataString(info[(colon + 1)..]));
                }
            }
        }
        else
        {
            return false;
        }

        if (host.Length == 0)
        {
            return false;
        }

        // SSH addresses of the big hosts, as their HTTPS ones.
        if (string.Equals(host, "ssh.dev.azure.com", StringComparison.OrdinalIgnoreCase) || string.Equals(host, "vs-ssh.visualstudio.com", StringComparison.OrdinalIgnoreCase))
        {
            var parts = path.Trim('/').Split('/');
            if (parts.Length == 4 && parts[0] == "v3")
            {
                host = "dev.azure.com";
                path = $"/{parts[1]}/{parts[2]}/_git/{parts[3]}";
            }
        }
        else if (host.StartsWith("ssh.", StringComparison.OrdinalIgnoreCase) && host.EndsWith(".com", StringComparison.OrdinalIgnoreCase) && host.Contains("gitlab", StringComparison.OrdinalIgnoreCase))
        {
            host = host[4..];
        }

        var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();

        // Pages of a repository: GitLab's /-/tree/ref, Bitbucket's /src/ref, Gitea's and Codeberg's /src/branch/ref.
        var dash = segments.IndexOf("-");
        if (dash > 0)
        {
            if (dash + 2 < segments.Count && segments[dash + 1] is "tree" or "blob" or "commits" or "commit")
            {
                reference = segments[dash + 2];
            }

            segments = segments.GetRange(0, dash);
        }
        else if (segments.IndexOf("_git") is var git && git > 0)
        {
            // Azure DevOps: organization/project/_git/repository, then ?version=GBbranch.
            if (git + 1 >= segments.Count)
            {
                return false;
            }

            segments = segments.GetRange(0, git + 2);
            if (Uri.TryCreate(text, UriKind.Absolute, out var page) && VersionPattern().Match(page.Query) is { Success: true } version)
            {
                reference = Uri.UnescapeDataString(version.Groups["ref"].Value);
            }
        }
        else if (segments.Count >= 4 && segments[2] == "src")
        {
            reference = segments[3] is "branch" or "tag" or "commit" && segments.Count >= 5 ? segments[4] : segments[3];
            segments = segments.GetRange(0, 2);
        }
        else if (segments.Count >= 4 && segments[2] is "tree" or "blob" or "commit")
        {
            reference = segments[3];
            segments = segments.GetRange(0, 2);
        }

        if (segments.Count == 0 || segments.Any(s => s is "." or ".."))
        {
            return false;
        }

        var name = segments[^1];
        if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }

        var owner = string.Join('/', segments.Take(segments.Count - 1).Where(s => s != "_git"));
        var repositoryPath = string.Join('/', segments);

        // Azure DevOps and some servers serve the repository at its address as it is; the others take ".git".
        var builder = new UriBuilder(scheme, host.ToLowerInvariant(), port, "/" + (segments.Contains("_git") || repositoryPath.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? repositoryPath : repositoryPath + ".git"));
        address = new GitRemoteAddress(builder.Uri, owner, name);
        if (reference?.Split('/').Any(part => part is "." or "..") == true)
        {
            reference = null;
        }

        return true;
    }

    [GeneratedRegex(@"^(?:[A-Za-z0-9._-]+@)?(?<host>[A-Za-z0-9.-]+\.[A-Za-z]{2,}):(?!//)(?<path>[^\s]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex ScpPattern();

    [GeneratedRegex(@"^[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)+(?::\d+)?/[^\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex BareHostPattern();

    [GeneratedRegex(@"[?&]version=G[BTC](?<ref>[^&]+)", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();
}
