using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Editing;
using TabStudio.SourceControl;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.ViewModels;

/// <summary>How a file differs from the last sync with GitHub, for the Git Changes window and Solution Explorer glyphs.</summary>
public enum GitFileStatus
{
    /// <summary>The file's content changed.</summary>
    Modified,

    /// <summary>The file is new.</summary>
    Added,

    /// <summary>The file was deleted.</summary>
    Deleted,

    /// <summary>A pull could not merge the file; it needs a decision.</summary>
    Conflict,
}

/// <summary>What a row of the Git Changes window can do. Implemented by <see cref="GitChangesViewModel"/>.</summary>
public interface IGitChangeActions
{
    /// <summary>Opens the file in an editor.</summary>
    Task OpenAsync(GitChangeItemViewModel item);

    /// <summary>Opens a tab comparing the file with its unmodified version.</summary>
    Task CompareAsync(GitChangeItemViewModel item);

    /// <summary>Throws the file's changes away, after asking.</summary>
    Task UndoChangesAsync(GitChangeItemViewModel item);

    /// <summary>Adds a new file to the <c>.gitignore</c>.</summary>
    Task IgnoreAsync(GitChangeItemViewModel item);

    /// <summary>Settles a merge conflict.</summary>
    Task ResolveAsync(GitChangeItemViewModel item, ConflictResolution resolution);
}

/// <summary>A changed or conflicted file in the Git Changes window.</summary>
public sealed partial class GitChangeItemViewModel : ObservableObject
{
    private readonly IGitChangeActions _actions;

    /// <summary>Creates the row.</summary>
    /// <param name="relativePath">The path in the repository, with <c>/</c> separators.</param>
    /// <param name="fullPath">The full path on this device.</param>
    /// <param name="status">How the file changed.</param>
    /// <param name="actions">What the row's commands do.</param>
    public GitChangeItemViewModel(string relativePath, string fullPath, GitFileStatus status, IGitChangeActions actions)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        ArgumentNullException.ThrowIfNull(fullPath);
        ArgumentNullException.ThrowIfNull(actions);
        RelativePath = relativePath;
        FullPath = fullPath;
        Status = status;
        _actions = actions;
        var type = FileTypes.For(relativePath);
        Badge = type.Badge;
        BadgeKind = type.BadgeKind;
    }

    /// <summary>The path in the repository, with <c>/</c> separators.</summary>
    public string RelativePath { get; }

    /// <summary>The full path on this device.</summary>
    public string FullPath { get; }

    /// <summary>How the file changed.</summary>
    public GitFileStatus Status { get; }

    /// <summary>The file name.</summary>
    public string Name => RelativePath[(RelativePath.LastIndexOf('/') + 1)..];

    /// <summary>The folder the file is in, or an empty string at the root.</summary>
    public string Folder => RelativePath.LastIndexOf('/') is var slash and > 0 ? RelativePath[..slash] : string.Empty;

    /// <summary>The badge text, such as "C#".</summary>
    public string Badge { get; }

    /// <summary>How the badge is colored.</summary>
    public BadgeKind BadgeKind { get; }

    /// <summary>The status letter, as in Visual Studio: M, A, D, or ! for a conflict.</summary>
    public string StatusLetter => Status switch
    {
        GitFileStatus.Modified => "M",
        GitFileStatus.Added => "A",
        GitFileStatus.Deleted => "D",
        _ => "!",
    };

    /// <summary>The status in words, for tooltips.</summary>
    public string StatusText => Status switch
    {
        GitFileStatus.Modified => "Modified",
        GitFileStatus.Added => "Added",
        GitFileStatus.Deleted => "Deleted",
        _ => "Merge conflict",
    };

    /// <summary>Whether the file is checked, so "Commit Selected and Push" includes it.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Whether the file exists and can be opened.</summary>
    public bool CanOpen => Status != GitFileStatus.Deleted;

    /// <summary>Whether the file is new and so can be ignored instead of committed.</summary>
    public bool CanIgnore => Status == GitFileStatus.Added;

    [RelayCommand(CanExecute = nameof(CanOpen))]
    private Task OpenAsync() => _actions.OpenAsync(this);

    [RelayCommand]
    private Task CompareAsync() => _actions.CompareAsync(this);

    [RelayCommand]
    private Task UndoChangesAsync() => _actions.UndoChangesAsync(this);

    [RelayCommand(CanExecute = nameof(CanIgnore))]
    private Task IgnoreAsync() => _actions.IgnoreAsync(this);

    [RelayCommand]
    private Task KeepMineAsync() => _actions.ResolveAsync(this, ConflictResolution.KeepMine);

    [RelayCommand]
    private Task TakeTheirsAsync() => _actions.ResolveAsync(this, ConflictResolution.TakeTheirs);

    [RelayCommand]
    private Task MarkResolvedAsync() => _actions.ResolveAsync(this, ConflictResolution.MarkResolved);
}

/// <summary>A branch in the branch picker.</summary>
public sealed class GitBranchItemViewModel
{
    /// <summary>Creates the row.</summary>
    /// <param name="branch">The branch.</param>
    /// <param name="isCurrent">Whether the folder is on this branch.</param>
    /// <param name="isDefault">Whether it is the repository's default branch.</param>
    /// <param name="select">Switches to it.</param>
    public GitBranchItemViewModel(GitHubBranch branch, bool isCurrent, bool isDefault, IAsyncRelayCommand<GitBranchItemViewModel?> select)
    {
        ArgumentNullException.ThrowIfNull(branch);
        Name = branch.Name;
        IsProtected = branch.IsProtected;
        IsCurrent = isCurrent;
        IsDefault = isDefault;
        Select = select;
    }

    /// <summary>The branch name.</summary>
    public string Name { get; }

    /// <summary>Whether GitHub restricts pushing to it.</summary>
    public bool IsProtected { get; }

    /// <summary>Whether the folder is on this branch.</summary>
    public bool IsCurrent { get; }

    /// <summary>Whether it is the repository's default branch.</summary>
    public bool IsDefault { get; }

    /// <summary>A note after the name, such as "default".</summary>
    public string? Note => (IsDefault, IsProtected) switch
    {
        (true, true) => "default · protected",
        (true, false) => "default",
        (false, true) => "protected",
        _ => null,
    };

    /// <summary>Switches to the branch.</summary>
    public IAsyncRelayCommand<GitBranchItemViewModel?> Select { get; }
}

/// <summary>A commit in the history list.</summary>
public sealed class GitCommitItemViewModel
{
    /// <summary>Creates the row.</summary>
    /// <param name="commit">The commit.</param>
    /// <param name="now">The current time, for "2 hours ago".</param>
    /// <param name="open">Opens the commit on GitHub.</param>
    public GitCommitItemViewModel(GitCommit commit, DateTimeOffset now, IAsyncRelayCommand<GitCommitItemViewModel?> open)
    {
        ArgumentNullException.ThrowIfNull(commit);
        Sha = commit.Sha;
        ShortSha = commit.ShortSha;
        Summary = commit.Summary;
        Author = commit.Author?.Name is { Length: > 0 } name ? name : "Unknown author";
        When = commit.Author is { } author && author.Date > DateTimeOffset.MinValue ? Describe(now - author.Date, author.Date) : string.Empty;
        Open = open;
    }

    /// <summary>The commit's full hash.</summary>
    public string Sha { get; }

    /// <summary>The first seven characters of the hash.</summary>
    public string ShortSha { get; }

    /// <summary>The first line of the message.</summary>
    public string Summary { get; }

    /// <summary>Who wrote it.</summary>
    public string Author { get; }

    /// <summary>When, such as "3 hours ago".</summary>
    public string When { get; }

    /// <summary>Opens the commit on GitHub.</summary>
    public IAsyncRelayCommand<GitCommitItemViewModel?> Open { get; }

    private static string Describe(TimeSpan age, DateTimeOffset date) => age.TotalMinutes switch
    {
        < 1 => "just now",
        < 2 => "1 minute ago",
        < 60 => $"{(int)age.TotalMinutes} minutes ago",
        < 120 => "1 hour ago",
        < 24 * 60 => $"{(int)age.TotalHours} hours ago",
        < 48 * 60 => "yesterday",
        < 30 * 24 * 60 => $"{(int)age.TotalDays} days ago",
        _ => date.ToLocalTime().ToString("d MMM yyyy", CultureInfo.CurrentCulture),
    };
}
