using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Editing;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.ViewModels.PullRequests;

/// <summary>A file a pull request changes, as its Files changed list shows it; tapping it opens the file's diff.</summary>
public sealed partial class PullRequestFileViewModel : ObservableObject
{
    private readonly PullRequestViewModel _owner;
    private DiffPatch? _patch;

    internal PullRequestFileViewModel(PullRequestViewModel owner, PullRequestFile file)
    {
        _owner = owner;
        File = file;
        var type = FileTypes.For(file.Path);
        Badge = type.Badge;
        BadgeKind = type.BadgeKind;
    }

    /// <summary>The file as GitHub lists it.</summary>
    public PullRequestFile File { get; }

    /// <summary>The path, with <c>/</c> separators.</summary>
    public string Path => File.Path;

    /// <summary>The file name.</summary>
    public string Name => Path[(Path.LastIndexOf('/') + 1)..];

    /// <summary>The folder, or an empty string at the root; for a renamed file, where it came from.</summary>
    public string Folder => File.PreviousPath is { } previous
        ? $"from {previous}"
        : Path.LastIndexOf('/') is var slash and > 0 ? Path[..slash] : string.Empty;

    /// <summary>The badge text, such as "C#".</summary>
    public string Badge { get; }

    /// <summary>How the badge is colored.</summary>
    public BadgeKind BadgeKind { get; }

    /// <summary>The status letter: A, D, M or R.</summary>
    public string StatusLetter => File.StatusLetter;

    /// <summary>The status as Git Changes colors it.</summary>
    public GitFileStatus GitStatus => File.Status switch
    {
        PullRequestFileStatus.Added or PullRequestFileStatus.Copied => GitFileStatus.Added,
        PullRequestFileStatus.Removed => GitFileStatus.Deleted,
        _ => GitFileStatus.Modified,
    };

    /// <summary>"+12".</summary>
    public string AdditionsText => "+" + File.Additions.ToString(CultureInfo.CurrentCulture);

    /// <summary>"−3".</summary>
    public string DeletionsText => "−" + File.Deletions.ToString(CultureInfo.CurrentCulture);

    /// <summary>The diff GitHub shows, whose lines are the ones comments can go on.</summary>
    public DiffPatch Patch => _patch ??= DiffPatch.Parse(File.Patch);

    /// <summary>How many conversations the file has.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasThreads))]
    public partial int ThreadCount { get; private set; }

    /// <summary>How many of the signed-in account's pending comments are on the file.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPending))]
    public partial int PendingCount { get; private set; }

    /// <summary>How many comments Claude drafted on the file.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDrafts))]
    public partial int DraftCount { get; private set; }

    /// <summary>Whether the file has conversations.</summary>
    public bool HasThreads => ThreadCount > 0;

    /// <summary>Whether the file has pending comments.</summary>
    public bool HasPending => PendingCount > 0;

    /// <summary>Whether Claude drafted comments on the file.</summary>
    public bool HasDrafts => DraftCount > 0;

    /// <summary>Updates the counts after the review changed.</summary>
    internal void SetCounts(int threads, int pending, int drafts)
    {
        ThreadCount = threads;
        PendingCount = pending;
        DraftCount = drafts;
    }

    /// <summary>Opens the file's diff with its review.</summary>
    [RelayCommand]
    private Task OpenAsync() => _owner.OpenFileAsync(this);
}
