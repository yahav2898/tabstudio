using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Text;
using RoslynCompletionTrigger = Microsoft.CodeAnalysis.Completion.CompletionTrigger;

namespace TabStudio.Compilation.Features;

/// <summary>
/// Code completion: the IntelliSense list, what committing an item inserts, and item descriptions.
/// </summary>
/// <remarks>
/// <para>
/// The list comes from Roslyn's <see cref="CompletionService"/>, so it has everything Visual Studio offers:
/// members, locals, keywords, snippets, overrides and types from namespaces that are not imported yet.
/// </para>
/// <para>
/// The last few lists are remembered, so an item can be committed or described after the user typed more to
/// filter it. Call <see cref="RoslynWorkspaceHost.UpdateText"/> with every keystroke before committing.
/// </para>
/// </remarks>
public sealed partial class CompletionFeature
{
    private const int RememberedSessions = 4;

    private readonly RoslynWorkspaceHost _host;
    private readonly Lock _gate = new();
    private readonly LinkedList<CompletionSession> _sessions = new();
    private long _lastSession;

    internal CompletionFeature(RoslynWorkspaceHost host) => _host = host;

    /// <summary>Returns the completion list at a position.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="position">Zero-based offset of the caret, after the trigger character when there is one.</param>
    /// <param name="trigger">What made the editor ask.</param>
    /// <param name="cancellationToken">Cancels the request, typically because the user typed again.</param>
    /// <returns>The list, which is empty when the file is not loaded or nothing applies.</returns>
    public async Task<CompletionResult> GetCompletionsAsync(string filePath, int position, CompletionTrigger trigger, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        if (_host.IsRazorDocument(filePath))
        {
            return await GetRazorCompletionsAsync(filePath, position, trigger, cancellationToken).ConfigureAwait(false);
        }

        if (_host.GetDocument(filePath) is not { } document || CompletionService.GetService(document) is not { } service)
        {
            return CompletionResult.Empty;
        }

        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var caret = Math.Clamp(position, 0, text.Length);
        var list = await service.GetCompletionsAsync(document, caret, ToRoslyn(trigger), cancellationToken: cancellationToken).ConfigureAwait(false);
        var items = list.ItemsList;
        if (items.Count == 0)
        {
            return CompletionResult.Empty;
        }

        var session = Remember(document, items);
        var entries = new List<CompletionEntry>(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            entries.Add(ToEntry(items[i], new CompletionHandle(session, i)));
        }

        entries.Sort(CompareEntries);
        return new CompletionResult(entries, list.Span.Start, list.Span.Length, list.SuggestionModeItem is not null);
    }

    /// <summary>
    /// Whether typing a character should open the completion list, such as a dot after an expression or the first
    /// letter of an identifier. Answers from the text alone, so it is fast enough to call on every keystroke.
    /// </summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="position">Zero-based offset of the caret, after the typed character.</param>
    /// <param name="typedCharacter">The character that was typed. It is already in the document.</param>
    public bool ShouldTriggerCompletion(string filePath, int position, char typedCharacter)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        if (_host.IsRazorDocument(filePath))
        {
            return ShouldTriggerRazorCompletion(filePath, position, typedCharacter);
        }

        if (_host.GetDocument(filePath) is not { } document || CompletionService.GetService(document) is not { } service)
        {
            return false;
        }

        if (!document.TryGetText(out var text))
        {
            return char.IsLetter(typedCharacter) || typedCharacter is '.' or '_';
        }

        return service.ShouldTriggerCompletion(text, Math.Clamp(position, 0, text.Length), RoslynCompletionTrigger.CreateInsertionTrigger(typedCharacter));
    }

    /// <summary>Computes the edits that commit a completion item.</summary>
    /// <param name="filePath">Full path of the file the list was shown in.</param>
    /// <param name="entry">The item from <see cref="GetCompletionsAsync"/>.</param>
    /// <param name="commitCharacter">
    /// The character that committed the item, such as <c>(</c> or <c>.</c>, or <see langword="null"/> for Tab and
    /// Enter. Call this before typing that character into the document.
    /// </param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>Edits against the current text of the document.</returns>
    public async Task<CompletionCommit> GetCompletionChangeAsync(string filePath, CompletionEntry entry, char? commitCharacter = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(entry);

        if (_host.IsRazorDocument(filePath))
        {
            return await GetRazorCompletionChangeAsync(filePath, entry, cancellationToken).ConfigureAwait(false);
        }

        if (_host.GetDocument(filePath) is not { } current)
        {
            return Fallback(entry, currentText: null);
        }

        var currentText = (await current.GetTextAsync(cancellationToken).ConfigureAwait(false)).ToString();
        if (!TryFind(entry.Handle, out var session, out var item)
            || !string.Equals(session.Document.FilePath, filePath, StringComparison.Ordinal)
            || CompletionService.GetService(session.Document) is not { } service)
        {
            return Fallback(entry, currentText);
        }

        var change = await service.GetChangeAsync(session.Document, item, commitCharacter, cancellationToken).ConfigureAwait(false);
        ImmutableArray<TextChange> changes = change.TextChanges.IsDefaultOrEmpty ? [change.TextChange] : change.TextChanges;
        var edits = changes
            .OrderBy(c => c.Span.Start)
            .Select(c => new TextEdit(c.Span.Start, c.Span.Length, c.NewText ?? string.Empty))
            .ToList();

        var originalText = (await session.Document.GetTextAsync(cancellationToken).ConfigureAwait(false)).ToString();
        if (TextChangeMapper.Map(originalText, currentText, edits) is not { } mapped)
        {
            return Fallback(entry, currentText);
        }

        var caret = change.NewPosition;
        if (caret is { } newPosition && !string.Equals(originalText, currentText, StringComparison.Ordinal))
        {
            caret = TextChangeMapper.MapPosition(
                TextChangeMapper.Apply(originalText, edits),
                TextChangeMapper.Apply(currentText, mapped),
                newPosition);
        }

        return new CompletionCommit(mapped, caret, change.IncludesCommitCharacter);
    }

    /// <summary>Returns the description of a completion item, shown in a tooltip next to the list.</summary>
    /// <param name="entry">The item from <see cref="GetCompletionsAsync"/>.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The description, or an empty list when the item is from a list that is no longer remembered.</returns>
    public async Task<IReadOnlyList<TaggedRun>> GetCompletionDescriptionAsync(CompletionEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!TryFind(entry.Handle, out var session, out var item) || CompletionService.GetService(session.Document) is not { } service)
        {
            return [];
        }

        var description = await service.GetDescriptionAsync(session.Document, item, cancellationToken).ConfigureAwait(false);
        return description is null ? [] : TaggedTextMapper.Map(description.TaggedParts);
    }

    private static RoslynCompletionTrigger ToRoslyn(CompletionTrigger trigger) => trigger.Kind switch
    {
        CompletionTriggerKind.Insertion => RoslynCompletionTrigger.CreateInsertionTrigger(trigger.Character),
        CompletionTriggerKind.Deletion => RoslynCompletionTrigger.CreateDeletionTrigger(trigger.Character),
        _ => RoslynCompletionTrigger.Invoke,
    };

    private static CompletionEntry ToEntry(CompletionItem item, CompletionHandle handle) => new(
        handle,
        item.DisplayText,
        item.DisplayTextPrefix ?? string.Empty,
        item.DisplayTextSuffix ?? string.Empty,
        item.FilterText,
        item.SortText,
        item.InlineDescription ?? string.Empty,
        GlyphMapper.FromTags(item.Tags),
        item.Rules.MatchPriority == MatchPriority.Preselect,
        item.Span.Start,
        item.Span.Length);

    private static int CompareEntries(CompletionEntry x, CompletionEntry y)
    {
        var bySortText = string.Compare(x.SortText, y.SortText, StringComparison.OrdinalIgnoreCase);
        return bySortText != 0 ? bySortText : string.Compare(x.DisplayText, y.DisplayText, StringComparison.Ordinal);
    }

    /// <summary>Inserts the display text over the typed word, for items whose list is no longer remembered.</summary>
    private static CompletionCommit Fallback(CompletionEntry entry, string? currentText)
    {
        var start = entry.SpanStart;
        var end = start + entry.SpanLength;
        if (currentText is not null)
        {
            start = Math.Min(start, currentText.Length);
            end = start;
            while (end < currentText.Length && (char.IsLetterOrDigit(currentText[end]) || currentText[end] == '_'))
            {
                end++;
            }
        }

        return new CompletionCommit([new TextEdit(start, end - start, entry.DisplayText)], CaretPosition: null, IncludesCommitCharacter: false);
    }

    private long Remember(Document document, IReadOnlyList<CompletionItem> items)
    {
        lock (_gate)
        {
            var id = ++_lastSession;
            _sessions.AddFirst(new CompletionSession(id, document, items));
            while (_sessions.Count > RememberedSessions)
            {
                _sessions.RemoveLast();
            }

            return id;
        }
    }

    private bool TryFind(
        CompletionHandle handle,
        [NotNullWhen(true)] out CompletionSession? session,
        [NotNullWhen(true)] out CompletionItem? item)
    {
        lock (_gate)
        {
            foreach (var candidate in _sessions)
            {
                if (candidate.Id == handle.Session && handle.Index >= 0 && handle.Index < candidate.Items.Count)
                {
                    session = candidate;
                    item = candidate.Items[handle.Index];
                    return true;
                }
            }
        }

        session = null;
        item = null;
        return false;
    }

    /// <summary>A computed completion list and the document snapshot it was computed for.</summary>
    private sealed record CompletionSession(long Id, Document Document, IReadOnlyList<CompletionItem> Items);
}
