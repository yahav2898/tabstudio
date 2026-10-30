using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace TabStudio.App.Services;

/// <summary>A GitHub repository, and optionally a branch, tag or commit in it.</summary>
/// <param name="Owner">The user or organization.</param>
/// <param name="Name">The repository name.</param>
/// <param name="Reference">A branch, tag or commit, or <see langword="null"/> for the default branch.</param>
public sealed partial record GitHubRepository(string Owner, string Name, string? Reference)
{
    /// <summary>The repository's web address.</summary>
    public Uri WebUri => new($"https://github.com/{Owner}/{Name}");

    /// <inheritdoc />
    public override string ToString() => Reference is null ? $"{Owner}/{Name}" : $"{Owner}/{Name}@{Reference}";

    /// <summary>
    /// Reads a repository from what a user might paste: a web address, a clone address, <c>owner/name</c>,
    /// an address of a branch (<c>/tree/branch</c>, which may go on with a folder in it), or any page of it.
    /// </summary>
    /// <param name="text">The pasted text.</param>
    /// <param name="repository">The repository, when the text names one.</param>
    public static bool TryParse(string? text, [NotNullWhen(true)] out GitHubRepository? repository)
    {
        repository = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var match = RepositoryPattern().Match(text.Trim());
        if (!match.Success)
        {
            return false;
        }

        var name = match.Groups["name"].Value;
        if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }

        var reference = match.Groups["ref"].Success ? Uri.UnescapeDataString(match.Groups["ref"].Value.TrimEnd('/')) : null;
        if (reference is not null && reference.Split('/').Any(part => part is "." or ".."))
        {
            // Not a branch name Git allows; as a request path it would leave the repository.
            return false;
        }

        repository = new GitHubRepository(match.Groups["owner"].Value, name, string.IsNullOrEmpty(reference) ? null : reference);
        return true;
    }

    // Owner: letters, digits and single hyphens. Name: letters, digits, '.', '-' and '_'. The address's scheme and
    // host in any case (Android's keyboard capitalizes the first letter), and any page of the repository: a file's
    // (/blob/ref/path), a pull request's or the issues' address opens the repository.
    [GeneratedRegex(
        @"^(?:(?i:(?:https?://)?(?:www\.)?github\.com[/:]|git@github\.com:|ssh://git@github\.com/))?(?<owner>[A-Za-z0-9](?:[A-Za-z0-9-]{0,38}))/(?<name>[A-Za-z0-9._-]+?)(?:/(?:tree|commit|commits|releases/tag)/(?<ref>[^?#]+)|/blob/(?<ref>[^/?#]+)(?:/[^?#]*)?|/(?:pulls?|issues|actions|wiki|discussions|releases|tags|branches|security|projects)(?:/[^?#]*)?)?/?(?:[?#].*)?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex RepositoryPattern();
}
