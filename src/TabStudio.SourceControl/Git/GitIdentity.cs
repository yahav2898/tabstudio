namespace TabStudio.SourceControl.Git;

/// <summary>
/// Who TabStudio's commits name as their author, as Git decides it: the repository's <c>user.name</c> and
/// <c>user.email</c>, then the ones in the home folder's <c>.gitconfig</c>, then the signed-in account's.
/// </summary>
public static class GitIdentity
{
    private static readonly Lock Gate = new();
    private static (string Name, string Email)? _fallback;

    /// <summary>Sets the name and email used when no Git configuration gives them, such as the signed-in GitHub account's.</summary>
    /// <param name="name">The person's name.</param>
    /// <param name="email">Their email address.</param>
    public static void SetFallback(string name, string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        lock (Gate)
        {
            _fallback = (name.Trim(), email.Trim());
        }
    }

    /// <summary>The author and committer of a commit made now in a repository.</summary>
    internal static CommitPerson Committer(GitDirectory? directory)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var global = string.IsNullOrEmpty(home) ? null : Path.Combine(home, ".gitconfig");
        var name = directory?.ConfigValue("user", "name") ?? (global is null ? null : GitDirectory.ReadConfigValue(global, "user", "name"));
        var email = directory?.ConfigValue("user", "email") ?? (global is null ? null : GitDirectory.ReadConfigValue(global, "user", "email"));
        (string Name, string Email)? fallback;
        lock (Gate)
        {
            fallback = _fallback;
        }

        return new CommitPerson(
            string.IsNullOrWhiteSpace(name) ? fallback?.Name ?? "TabStudio" : name,
            string.IsNullOrWhiteSpace(email) ? fallback?.Email ?? "tabstudio@localhost" : email,
            DateTimeOffset.Now);
    }
}
