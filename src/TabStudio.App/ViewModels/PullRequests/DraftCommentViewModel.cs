using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Agent;
using TabStudio.App.Notebooks.Markdown;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.ViewModels.PullRequests;

/// <summary>
/// A comment Claude drafted on a line of the diff. Nothing is posted until the user keeps it, which adds it to
/// their pending review; it can be edited first, or dismissed.
/// </summary>
public sealed partial class DraftCommentViewModel : ObservableObject
{
    private readonly PullRequestViewModel _owner;

    internal DraftCommentViewModel(PullRequestViewModel owner, ReviewFinding finding)
    {
        _owner = owner;
        Path = finding.Path;
        Line = finding.Line;
        Side = finding.Side == ReviewSide.Left ? DiffSide.Left : DiffSide.Right;
        Severity = finding.Severity;
        Body = finding.Body;
    }

    /// <summary>The file.</summary>
    public string Path { get; }

    /// <summary>The line.</summary>
    public int Line { get; }

    /// <summary>Which version of the file the line counts in.</summary>
    public DiffSide Side { get; }

    /// <summary>How much it matters, in Claude's view.</summary>
    public ReviewSeverity Severity { get; }

    /// <summary>"Bug", "Warning" or "Suggestion".</summary>
    public string SeverityText => Severity switch
    {
        ReviewSeverity.Bug => "Bug",
        ReviewSeverity.Warning => "Warning",
        _ => "Suggestion",
    };

    /// <summary>"Line 12" or "Line 12 (removed)".</summary>
    public string LocationText => "Line " + Line.ToString(System.Globalization.CultureInfo.CurrentCulture) + (Side == DiffSide.Left ? " (removed)" : string.Empty);

    /// <summary>The comment, which the user may edit before keeping it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BodyBlocks))]
    [NotifyCanExecuteChangedFor(nameof(KeepCommand))]
    public partial string Body { get; set; }

    /// <summary>The comment, read as Markdown.</summary>
    public IReadOnlyList<MarkdownBlock> BodyBlocks => MarkdownParser.Parse(Body);

    /// <summary>Whether the comment is being edited.</summary>
    [ObservableProperty]
    public partial bool IsEditing { get; private set; }

    /// <summary>Whether the comment is being added to the review.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(KeepCommand))]
    public partial bool IsSending { get; private set; }

    /// <summary>Why the comment could not be kept.</summary>
    [ObservableProperty]
    public partial string? ErrorText { get; private set; }

    /// <summary>Adds the comment to the pending review.</summary>
    [RelayCommand(CanExecute = nameof(CanKeep))]
    private async Task KeepAsync()
    {
        ErrorText = null;
        IsSending = true;
        try
        {
            ErrorText = await _owner.KeepDraftAsync(this);
        }
        finally
        {
            IsSending = false;
        }
    }

    [RelayCommand]
    private void Edit() => IsEditing = true;

    [RelayCommand]
    private void FinishEditing() => IsEditing = false;

    [RelayCommand]
    private void Dismiss() => _owner.DismissDraft(this);

    private bool CanKeep() => !IsSending && !string.IsNullOrWhiteSpace(Body);
}
