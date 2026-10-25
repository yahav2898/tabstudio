using CommunityToolkit.Mvvm.ComponentModel;
using TabStudio.Agent;
using TabStudio.SourceControl.Merging;

namespace TabStudio.App.ViewModels.Agent;

/// <summary>An item in the agent's conversation.</summary>
public abstract class AgentItemViewModel : ObservableObject;

/// <summary>Something the user asked.</summary>
/// <param name="text">The request.</param>
public sealed class UserMessageViewModel(string text) : AgentItemViewModel
{
    /// <summary>The request.</summary>
    public string Text { get; } = text;
}

/// <summary>Text Claude wrote, growing as it streams in.</summary>
public sealed partial class AssistantMessageViewModel : AgentItemViewModel
{
    /// <summary>The text so far.</summary>
    [ObservableProperty]
    public partial string Text { get; private set; } = string.Empty;

    /// <summary>Adds streamed text.</summary>
    /// <param name="text">The new text.</param>
    public void Append(string text) => Text += text;
}

/// <summary>A tool call: what the agent did, and what came back.</summary>
/// <param name="callId">The call's id.</param>
/// <param name="description">What it does, such as "Read src/Program.cs".</param>
public sealed partial class ToolCallViewModel(string callId, string description) : AgentItemViewModel
{
    private const int MaxResultLength = 4000;

    /// <summary>The call's id.</summary>
    public string CallId { get; } = callId;

    /// <summary>What it does.</summary>
    public string Description { get; } = description;

    /// <summary>Whether it is still running.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDone))]
    public partial bool IsRunning { get; private set; } = true;

    /// <summary>Whether it finished.</summary>
    public bool IsDone => !IsRunning;

    /// <summary>Whether it failed.</summary>
    [ObservableProperty]
    public partial bool IsError { get; private set; }

    /// <summary>What it returned, shortened for display.</summary>
    [ObservableProperty]
    public partial string Result { get; private set; } = string.Empty;

    /// <summary>Whether the result is shown.</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    /// <summary>Records the result.</summary>
    /// <param name="content">What the tool returned.</param>
    /// <param name="isError">Whether it failed.</param>
    public void Finish(string content, bool isError)
    {
        Result = content.Length > MaxResultLength ? content[..MaxResultLength] + "\n…" : content;
        IsError = isError;
        IsRunning = false;
    }
}

/// <summary>Why a turn ended, when it is worth telling the user.</summary>
/// <param name="text">The notice.</param>
public sealed class AgentNoticeViewModel(string text) : AgentItemViewModel
{
    /// <summary>The notice.</summary>
    public string Text { get; } = text;
}

/// <summary>A line of a diff.</summary>
/// <param name="Kind">' ' for context, '+' for added, '-' for removed, '…' for skipped lines.</param>
/// <param name="Text">The line.</param>
public sealed record DiffLineViewModel(char Kind, string Text)
{
    /// <summary>Whether the line was added.</summary>
    public bool IsAdded => Kind == '+';

    /// <summary>Whether the line was removed.</summary>
    public bool IsRemoved => Kind == '-';

    /// <summary>Whether lines were left out here.</summary>
    public bool IsGap => Kind == '…';
}

/// <summary>A file the agent wants to change, with its diff, waiting for Keep or Undo.</summary>
public sealed partial class PendingChangeViewModel : ObservableObject
{
    private const int Context = 3;

    /// <summary>Creates the view of a change.</summary>
    /// <param name="change">The proposal.</param>
    public PendingChangeViewModel(ProposedChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        Change = change;
        var before = LineDiff.SplitLines(change.Original ?? string.Empty);
        var after = LineDiff.SplitLines(change.Proposed ?? string.Empty);
        var hunks = LineDiff.Compute(before, after);
        Added = hunks.Sum(h => h.NewLength);
        Removed = hunks.Sum(h => h.OldLength);
        Lines = BuildLines(before, after, hunks);
    }

    /// <summary>The proposal.</summary>
    public ProposedChange Change { get; }

    /// <summary>The file, relative to the workspace.</summary>
    public string Path => Change.RelativePath;

    /// <summary>"Added", "Deleted" or "Edited".</summary>
    public string KindLabel => Change.IsNew ? "Added" : Change.IsDeletion ? "Deleted" : "Edited";

    /// <summary>Lines added.</summary>
    public int Added { get; }

    /// <summary>Lines removed.</summary>
    public int Removed { get; }

    /// <summary>"+12 −3", for the list.</summary>
    public string Stats => $"+{Added} −{Removed}";

    /// <summary>The diff, with three lines of context around each change.</summary>
    public IReadOnlyList<DiffLineViewModel> Lines { get; }

    /// <summary>Whether the diff is shown.</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    private static List<DiffLineViewModel> BuildLines(IReadOnlyList<string> before, IReadOnlyList<string> after, IReadOnlyList<DiffHunk> hunks)
    {
        var lines = new List<DiffLineViewModel>();
        var position = 0;
        for (var h = 0; h < hunks.Count; h++)
        {
            var hunk = hunks[h];
            var contextStart = Math.Max(position, hunk.OldStart - Context);
            if (contextStart > position)
            {
                lines.Add(new DiffLineViewModel('…', string.Empty));
            }

            for (var i = contextStart; i < hunk.OldStart; i++)
            {
                lines.Add(new DiffLineViewModel(' ', Trim(before[i])));
            }

            for (var i = hunk.OldStart; i < hunk.OldEnd; i++)
            {
                lines.Add(new DiffLineViewModel('-', Trim(before[i])));
            }

            for (var i = hunk.NewStart; i < hunk.NewEnd; i++)
            {
                lines.Add(new DiffLineViewModel('+', Trim(after[i])));
            }

            // Context after the change, stopping where the next change begins.
            var nextStart = h + 1 < hunks.Count ? hunks[h + 1].OldStart : before.Count;
            var contextEnd = Math.Min(nextStart, hunk.OldEnd + Context);
            for (var i = hunk.OldEnd; i < contextEnd; i++)
            {
                lines.Add(new DiffLineViewModel(' ', Trim(before[i])));
            }

            position = contextEnd;
        }

        if (hunks.Count > 0 && position < before.Count)
        {
            lines.Add(new DiffLineViewModel('…', string.Empty));
        }

        return lines;
    }

    private static string Trim(string line) => line.TrimEnd('\r', '\n');
}
