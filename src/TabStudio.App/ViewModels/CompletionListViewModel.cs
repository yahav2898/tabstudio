using CommunityToolkit.Mvvm.ComponentModel;
using TabStudio.App.Editing;
using TabStudio.App.Services;
using TabStudio.Compilation;
using TabStudio.Compilation.Features;

namespace TabStudio.App.ViewModels;

/// <summary>Where the caret goes after a completion item is committed, and whether to type the commit character.</summary>
/// <param name="Caret">The new caret offset.</param>
/// <param name="TypeCommitCharacter">Whether the editor still has to type the character that committed the item.</param>
public sealed record CompletionCommitOutcome(int Caret, bool TypeCommitCharacter);

/// <summary>
/// The completion list of one editor, as in Visual Studio: it opens when a character such as <c>.</c> triggers it or
/// on Ctrl+Space, filters as the user types, selects the best match (softly when the user may be typing a new name)
/// and commits the selected item through the language service, which can also add a <c>using</c> directive.
/// </summary>
/// <remarks>
/// The view feeds it caret moves and keys and shows <see cref="Items"/> in a popup. Everything runs on the UI thread;
/// only the language service queries run in the background.
/// </remarks>
public sealed partial class CompletionListViewModel : ObservableObject, IDisposable
{
    /// <summary>How many rows Page Up and Page Down move.</summary>
    public const int PageSize = 8;

    private const int RememberedCommits = 16;

    // Items committed recently, most recent first, preferred when matches are otherwise equal, as in Visual Studio.
    private static readonly List<string> RecentCommits = [];

    private readonly DocumentViewModel _document;
    private readonly ILanguageFeatures _features;
    private readonly Func<int> _caret;
    private CompletionItemViewModel[] _all = [];
    private bool _isSuggestionMode;
    private CancellationTokenSource? _request;
    private CancellationTokenSource? _description;
    private bool _disposed;

    /// <summary>Creates the completion list for an editor.</summary>
    /// <param name="document">The document being edited.</param>
    /// <param name="features">The language service's IntelliSense.</param>
    /// <param name="caret">Reads the editor's caret offset.</param>
    public CompletionListViewModel(DocumentViewModel document, ILanguageFeatures features, Func<int> caret)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(features);
        ArgumentNullException.ThrowIfNull(caret);
        _document = document;
        _features = features;
        _caret = caret;
    }

    /// <summary>The items that match what was typed, in list order.</summary>
    public RangeObservableCollection<CompletionItemViewModel> Items { get; } = [];

    /// <summary>The selected item, which Tab or Enter commits.</summary>
    [ObservableProperty]
    public partial CompletionItemViewModel? SelectedItem { get; private set; }

    /// <summary>
    /// Whether the selection is only a suggestion: the list is open but only Tab, a tap or an arrow key picks the item,
    /// so typing a space or punctuation does not commit it. Used when nothing was typed yet and when the user may be
    /// naming something new.
    /// </summary>
    [ObservableProperty]
    public partial bool IsSoftSelection { get; private set; }

    /// <summary>Whether the list is shown.</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; private set; }

    /// <summary>Whether an item is being committed. The editor holds back typing until it is done.</summary>
    [ObservableProperty]
    public partial bool IsCommitting { get; private set; }

    /// <summary>The description of the selected item, shown next to the list.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDescription))]
    public partial IReadOnlyList<StyledRun> Description { get; private set; } = [];

    /// <summary>Whether there is a description to show.</summary>
    public bool HasDescription => Description.Count > 0;

    /// <summary>Whether a list was requested and has not arrived yet.</summary>
    public bool IsRequesting => _request is not null;

    /// <summary>Where the word being completed starts. The popup is placed under it.</summary>
    public int SpanStart { get; private set; }

    /// <summary>What was typed from the start of the word to the caret.</summary>
    public string FilterText { get; private set; } = string.Empty;

    /// <summary>
    /// Asks the language service for the list at the caret and opens it. A request replaces the one before it.
    /// </summary>
    /// <param name="trigger">What opened the list: a typed character, or the user asking for it.</param>
    /// <returns>Whether the list is open.</returns>
    public async Task<bool> RequestAsync(CompletionTrigger trigger)
    {
        CancelRequest();
        var request = new CancellationTokenSource();
        _request = request;
        _document.SyncText();
        var caret = _caret();

        CompletionResult result;
        try
        {
            result = await _features.GetCompletionsAsync(_document.FilePath, caret, trigger, request.Token);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            if (ReferenceEquals(_request, request))
            {
                _request = null;
            }

            request.Dispose();
        }

        if (request.IsCancellationRequested || _disposed)
        {
            return false;
        }

        if (result.Items.Count == 0)
        {
            Close();
            return false;
        }

        _all = result.Items.Select((entry, index) => new CompletionItemViewModel(entry, index)).ToArray();
        SpanStart = result.SpanStart;
        _isSuggestionMode = result.IsSuggestionMode;
        Refilter();
        return IsOpen;
    }

    /// <summary>
    /// Filters the list again after the text or caret changed, and closes it when the caret left the word.
    /// </summary>
    public void Refilter()
    {
        if (_all.Length == 0)
        {
            return;
        }

        var caret = _caret();
        var document = _document.Document;
        if (caret < SpanStart || caret > document.TextLength)
        {
            Close();
            return;
        }

        // A number is not a name: typing one, or anything that cannot be in a name, closes the list.
        var typed = document.GetText(SpanStart, caret - SpanStart);
        if (!typed.All(CompletionMatcher.IsIdentifierCharacter) || (typed.Length > 0 && char.IsDigit(typed[0])))
        {
            Close();
            return;
        }

        FilterText = typed;
        var visible = new List<CompletionItemViewModel>();
        CompletionItemViewModel? best = null;
        foreach (var item in _all)
        {
            if (item.Filter(typed))
            {
                visible.Add(item);
                if (best is null || IsBetter(item, best))
                {
                    best = item;
                }
            }
        }

        if (best is null)
        {
            Close();
            return;
        }

        Items.ReplaceAll(visible);
        IsSoftSelection = _isSuggestionMode || (typed.Length == 0 && !best.Entry.IsPreselected);
        if (ReferenceEquals(SelectedItem, best))
        {
            // The list was reset, which cleared the view's selection: show the same item as selected again.
            OnPropertyChanged(nameof(SelectedItem));
        }
        else
        {
            SelectedItem = best;
        }

        IsOpen = true;
    }

    /// <summary>Moves the selection, as the arrow and page keys do, and makes it a real selection.</summary>
    /// <param name="delta">How many rows to move; negative moves up.</param>
    public void MoveSelection(int delta)
    {
        if (Items.Count == 0)
        {
            return;
        }

        var index = SelectedItem is null ? -1 : Items.IndexOf(SelectedItem);
        index = index < 0 ? (delta > 0 ? 0 : Items.Count - 1) : Math.Clamp(index + delta, 0, Items.Count - 1);
        Select(Items[index]);
    }

    /// <summary>Selects an item, as tapping it with a mouse does.</summary>
    /// <param name="item">The item.</param>
    public void Select(CompletionItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        SelectedItem = item;
        IsSoftSelection = false;
    }

    /// <summary>
    /// Commits an item: the language service computes the edits, which replace the typed word and may add a
    /// <c>using</c> directive, and they are applied as one undo step.
    /// </summary>
    /// <param name="item">The item, or <see langword="null"/> for the selected one.</param>
    /// <param name="commitCharacter">The character that committed it, which is not typed yet, or <see langword="null"/>.</param>
    /// <returns>Where the caret goes, or <see langword="null"/> when nothing was committed.</returns>
    public async Task<CompletionCommitOutcome?> CommitAsync(CompletionItemViewModel? item, char? commitCharacter)
    {
        item ??= SelectedItem;
        if (item is null || IsCommitting)
        {
            return null;
        }

        IsCommitting = true;
        try
        {
            _document.SyncText();
            var edits = _document.EditCount;
            var caret = _caret();
            var commit = await _features.GetCompletionChangeAsync(_document.FilePath, item.Entry, commitCharacter, CancellationToken.None);
            Close();
            if (_disposed || edits != _document.EditCount || commit.Edits.Count == 0)
            {
                return null;
            }

            _document.ApplyEdits(commit.Edits);
            Remember(item.Entry.DisplayText);
            var newCaret = commit.CaretPosition ?? EndOfInsertion(commit.Edits, caret);
            return new CompletionCommitOutcome(newCaret, commitCharacter is not null && !commit.IncludesCommitCharacter);
        }
        finally
        {
            IsCommitting = false;
        }
    }

    /// <summary>Closes the list and forgets it. A pending request is cancelled.</summary>
    public void Close()
    {
        CancelRequest();
        _all = [];
        FilterText = string.Empty;
        IsOpen = false;
        SelectedItem = null;
        Items.ReplaceAll([]);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
        _request?.Cancel();
        _description?.Cancel();
    }

    partial void OnSelectedItemChanged(CompletionItemViewModel? value) => _ = LoadDescriptionAsync(value);

    private async Task LoadDescriptionAsync(CompletionItemViewModel? item)
    {
        _description?.Cancel();
        _description = null;
        Description = [];
        if (item is null)
        {
            return;
        }

        var request = new CancellationTokenSource();
        _description = request;
        try
        {
            // Arrowing through the list should not ask for every description on the way.
            await Task.Delay(TimeSpan.FromMilliseconds(60), request.Token);
            var runs = await _features.GetCompletionDescriptionAsync(item.Entry, request.Token);
            if (!request.IsCancellationRequested && ReferenceEquals(SelectedItem, item))
            {
                Description = StyledRun.From(runs);
            }
        }
        catch (OperationCanceledException)
        {
            // Another item was selected.
        }
        finally
        {
            if (ReferenceEquals(_description, request))
            {
                _description = null;
            }

            request.Dispose();
        }
    }

    private void CancelRequest()
    {
        _request?.Cancel();
        _request = null;
    }

    /// <summary>Whether one matching item is a better pick than another, as Visual Studio decides.</summary>
    private static bool IsBetter(CompletionItemViewModel candidate, CompletionItemViewModel best)
    {
        var a = candidate.Match!;
        var b = best.Match!;
        if (a.Kind != b.Kind)
        {
            return a.Kind < b.Kind;
        }

        if (a.IsCaseSensitive != b.IsCaseSensitive)
        {
            return a.IsCaseSensitive;
        }

        if (candidate.Entry.IsPreselected != best.Entry.IsPreselected)
        {
            return candidate.Entry.IsPreselected;
        }

        var recentA = RecentRank(candidate.Entry.DisplayText);
        var recentB = RecentRank(best.Entry.DisplayText);
        return recentA != recentB ? recentA < recentB : candidate.Order < best.Order;
    }

    private static int RecentRank(string displayText)
    {
        var index = RecentCommits.IndexOf(displayText);
        return index < 0 ? int.MaxValue : index;
    }

    private static void Remember(string displayText)
    {
        RecentCommits.Remove(displayText);
        RecentCommits.Insert(0, displayText);
        if (RecentCommits.Count > RememberedCommits)
        {
            RecentCommits.RemoveAt(RecentCommits.Count - 1);
        }
    }

    /// <summary>The end of the inserted text: the end of the edit that replaced the word at the caret.</summary>
    private static int EndOfInsertion(IReadOnlyList<TextEdit> edits, int caret)
    {
        var main = edits[^1];
        foreach (var edit in edits)
        {
            if (edit.Start <= caret && caret <= edit.Start + edit.Length)
            {
                main = edit;
            }
        }

        return TextEdits.EndAfterApplying(edits, main);
    }
}
