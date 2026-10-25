namespace TabStudio.SourceControl.GitHub;

/// <summary>The signed-in GitHub account.</summary>
/// <param name="Login">The user name, such as <c>octocat</c>.</param>
/// <param name="Name">The display name, if the account has one.</param>
public sealed record GitHubUser(string Login, string? Name);

/// <summary>A repository on GitHub.</summary>
/// <param name="Owner">The owner's login.</param>
/// <param name="Name">The repository name.</param>
/// <param name="DefaultBranch">The branch GitHub shows first, usually <c>main</c>.</param>
/// <param name="IsPrivate">Whether only invited people can see it.</param>
/// <param name="CanPush">Whether the signed-in account can push to it. Always false when signed out.</param>
/// <param name="WebUri">The repository's page.</param>
public sealed record GitHubRepositoryInfo(string Owner, string Name, string DefaultBranch, bool IsPrivate, bool CanPush, Uri WebUri);

/// <summary>A branch and the commit at its tip.</summary>
/// <param name="Name">The branch name.</param>
/// <param name="CommitSha">The commit at the tip.</param>
/// <param name="IsProtected">Whether GitHub restricts pushing to it.</param>
public sealed record GitHubBranch(string Name, string CommitSha, bool IsProtected);

/// <summary>The author or committer of a commit.</summary>
/// <param name="Name">The person's name.</param>
/// <param name="Email">The person's email address.</param>
/// <param name="Date">When they wrote or committed it.</param>
public sealed record GitSignature(string Name, string Email, DateTimeOffset Date);

/// <summary>A commit.</summary>
/// <param name="Sha">The commit's hash.</param>
/// <param name="TreeSha">The hash of the tree of files it records.</param>
/// <param name="Message">The commit message.</param>
/// <param name="Parents">The hashes of its parents.</param>
/// <param name="Author">Who wrote the change.</param>
public sealed record GitCommit(string Sha, string TreeSha, string Message, IReadOnlyList<string> Parents, GitSignature? Author)
{
    /// <summary>The first seven characters of the hash, as Git shows them.</summary>
    public string ShortSha => Sha.Length > 7 ? Sha[..7] : Sha;

    /// <summary>The first line of the message.</summary>
    public string Summary => Message.Split('\n', 2)[0].TrimEnd('\r');
}

/// <summary>The kind of an entry in a Git tree.</summary>
public enum GitTreeEntryKind
{
    /// <summary>A file.</summary>
    Blob,

    /// <summary>A folder.</summary>
    Tree,

    /// <summary>A submodule: a commit of another repository.</summary>
    Commit,
}

/// <summary>An entry of a Git tree.</summary>
/// <param name="Path">The path relative to the tree, with <c>/</c> separators.</param>
/// <param name="Mode">The Git mode, such as <c>100644</c> for a file or <c>120000</c> for a symbolic link.</param>
/// <param name="Kind">Whether it is a file, folder or submodule.</param>
/// <param name="Sha">The hash of the object.</param>
/// <param name="Size">The size of a file in bytes.</param>
public sealed record GitTreeEntry(string Path, string Mode, GitTreeEntryKind Kind, string Sha, long? Size)
{
    /// <summary>The mode of a symbolic link.</summary>
    public const string SymbolicLinkMode = "120000";

    /// <summary>Whether the entry is an ordinary or executable file, which TabStudio writes to disk.</summary>
    public bool IsRegularFile => Kind == GitTreeEntryKind.Blob && Mode != SymbolicLinkMode;
}

/// <summary>A tree of files.</summary>
/// <param name="Sha">The tree's hash.</param>
/// <param name="Entries">Its entries; every file and folder at any depth when read recursively.</param>
/// <param name="Truncated">Whether GitHub left entries out because the tree is too large.</param>
public sealed record GitTree(string Sha, IReadOnlyList<GitTreeEntry> Entries, bool Truncated);

/// <summary>A change to a tree, for creating a new one.</summary>
/// <param name="Path">The path of the file, with <c>/</c> separators.</param>
/// <param name="Mode">The Git mode of the file.</param>
/// <param name="Sha">The blob to put at the path, or <see langword="null"/> to delete the file.</param>
public sealed record GitTreeChange(string Path, string Mode, string? Sha);

/// <summary>How two commits relate.</summary>
/// <param name="Status">GitHub's verdict: <c>identical</c>, <c>ahead</c>, <c>behind</c> or <c>diverged</c>.</param>
/// <param name="AheadBy">Commits the head has that the base does not.</param>
/// <param name="BehindBy">Commits the base has that the head does not.</param>
public sealed record GitComparison(string Status, int AheadBy, int BehindBy);

/// <summary>A pull request.</summary>
/// <param name="Number">Its number in the repository.</param>
/// <param name="Title">Its title.</param>
/// <param name="State"><c>open</c> or <c>closed</c>.</param>
/// <param name="HeadBranch">The branch with the changes.</param>
/// <param name="BaseBranch">The branch the changes go into.</param>
/// <param name="WebUri">The pull request's page.</param>
/// <param name="IsDraft">Whether it is a draft.</param>
public sealed record GitHubPullRequest(int Number, string Title, string State, string HeadBranch, string BaseBranch, Uri WebUri, bool IsDraft);
