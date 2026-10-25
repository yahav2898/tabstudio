using System.Globalization;
using TabStudio.App.Services;
using TabStudio.Compilation.Features;
using TabStudio.Extensions.Snippets;

namespace TabStudio.App.Editing.Snippets;

/// <summary>
/// Adds the snippets installed extensions contribute for a document's language to its completion list, as VS Code
/// does, on top of the document's own IntelliSense (Roslyn's for C#), or alone for a file without any. While a choice
/// tab stop is being filled in, the list offers its options instead. Everything else goes to the inner IntelliSense.
/// </summary>
/// <remarks>
/// Built by one editor and called on its UI thread. A snippet is inserted by the editor itself
/// (<see cref="ISnippetEditor.InsertSnippet"/>) while the list commits it, so the completion list has nothing more to
/// apply: Enter accepts the snippet without also starting a new line, and a commit character such as a space only
/// closes the list.
/// </remarks>
public sealed class SnippetCompletionFeatures : ILanguageFeatures
{
    private const long SnippetList = -7_001;
    private const long ChoiceList = -7_002;
    private const string NotAvailable = "This works in C# files of the solution.";

    private static readonly CompletionCommit NothingToApply = new([], null, IncludesCommitCharacter: false);

    private readonly ILanguageFeatures? _inner;
    private readonly ISnippetEditor _editor;
    private readonly List<SnippetDefinition> _listed = [];
    private (int Start, IReadOnlyList<string> Options)? _choices;

    /// <summary>Creates the completion source for an editor.</summary>
    /// <param name="inner">The document's own IntelliSense, or <see langword="null"/> when it has none.</param>
    /// <param name="editor">The editor.</param>
    public SnippetCompletionFeatures(ILanguageFeatures? inner, ISnippetEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        _inner = inner;
        _editor = editor;
    }

    /// <summary>The document's own IntelliSense under the snippets, or <see langword="null"/>.</summary>
    public ILanguageFeatures? Inner => _inner;

    /// <summary>Makes the next completion request list the options of a choice tab stop.</summary>
    /// <param name="start">Where the tab stop starts; the list opens there.</param>
    /// <param name="options">The options.</param>
    public void OfferChoices(int start, IReadOnlyList<string> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _choices = (start, options);
    }

    /// <inheritdoc />
    public async Task<CompletionResult> GetCompletionsAsync(string filePath, int position, CompletionTrigger trigger, CancellationToken cancellationToken)
    {
        // The list opens once the typed character is laid out, as with a language service's answer, so the
        // editor can place the list under the word.
        await Task.Yield();
        if (_choices is { } choices)
        {
            _choices = null;
            return new CompletionResult(
                [.. choices.Options.Select((option, index) => new CompletionEntry(
                    new CompletionHandle(ChoiceList, index),
                    option,
                    string.Empty,
                    string.Empty,
                    option,
                    index.ToString("D4", CultureInfo.InvariantCulture),
                    string.Empty,
                    SymbolGlyph.EnumMember,
                    index == 0,
                    choices.Start,
                    0))],
                choices.Start,
                0,
                IsSuggestionMode: false);
        }

        var inner = _inner is null ? CompletionResult.Empty : await _inner.GetCompletionsAsync(filePath, position, trigger, cancellationToken);
        var snippets = _editor.Snippets;
        if (snippets.Count == 0)
        {
            return inner;
        }

        var spanStart = inner.Items.Count > 0 ? inner.SpanStart : WordStart(position);
        var entries = new List<CompletionEntry>();
        _listed.Clear();
        foreach (var snippet in snippets)
        {
            foreach (var prefix in snippet.Prefixes.Where(p => p.Length > 0))
            {
                _listed.Add(snippet);
                entries.Add(new CompletionEntry(
                    new CompletionHandle(SnippetList, _listed.Count - 1),
                    prefix,
                    string.Empty,
                    string.Empty,
                    prefix,
                    prefix,
                    snippet.Name,
                    SymbolGlyph.Snippet,
                    false,
                    spanStart,
                    position - spanStart));
            }
        }

        return inner.Items.Count > 0
            ? inner with { Items = [.. inner.Items, .. entries] }
            : new CompletionResult(entries, spanStart, position - spanStart, IsSuggestionMode: false);
    }

    /// <inheritdoc />
    public bool ShouldTriggerCompletion(string filePath, int position, char typedCharacter)
    {
        if (_inner?.ShouldTriggerCompletion(filePath, position, typedCharacter) == true)
        {
            return true;
        }

        // The first letter of a word opens the list when a snippet's prefix starts with it.
        var before = _editor.LineTextBefore(position);
        if (!CompletionMatcher.IsIdentifierCharacter(typedCharacter) || (before.Length >= 2 && CompletionMatcher.IsIdentifierCharacter(before[^2])))
        {
            return false;
        }

        var typed = char.ToUpperInvariant(typedCharacter);
        return _editor.Snippets.Any(s => s.Prefixes.Any(p => p.Length > 0 && char.ToUpperInvariant(p[0]) == typed));
    }

    /// <inheritdoc />
    public Task<CompletionCommit> GetCompletionChangeAsync(string filePath, CompletionEntry entry, char? commitCharacter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        switch (entry.Handle.Session)
        {
            case ChoiceList:
                if (commitCharacter is null)
                {
                    _editor.ChooseOption(entry.DisplayText);
                }

                return Task.FromResult(NothingToApply);
            case SnippetList:
                if (commitCharacter is null && entry.Handle.Index < _listed.Count)
                {
                    var end = Math.Max(entry.SpanStart, _editor.Caret);
                    _editor.InsertSnippet(_listed[entry.Handle.Index], entry.SpanStart, end - entry.SpanStart);
                }

                return Task.FromResult(NothingToApply);
            default:
                return _inner?.GetCompletionChangeAsync(filePath, entry, commitCharacter, cancellationToken) ?? Task.FromResult(NothingToApply);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TaggedRun>> GetCompletionDescriptionAsync(CompletionEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        switch (entry.Handle.Session)
        {
            case SnippetList when entry.Handle.Index < _listed.Count:
                return Task.FromResult(Describe(_listed[entry.Handle.Index]));
            case SnippetList or ChoiceList:
                return Task.FromResult<IReadOnlyList<TaggedRun>>([]);
            default:
                return _inner?.GetCompletionDescriptionAsync(entry, cancellationToken) ?? Task.FromResult<IReadOnlyList<TaggedRun>>([]);
        }
    }

    /// <inheritdoc />
    public Task<QuickInfoResult?> GetQuickInfoAsync(string filePath, int position, CancellationToken cancellationToken) =>
        _inner?.GetQuickInfoAsync(filePath, position, cancellationToken) ?? Task.FromResult<QuickInfoResult?>(null);

    /// <inheritdoc />
    public Task<SignatureHelpResult?> GetSignatureHelpAsync(string filePath, int position, CancellationToken cancellationToken) =>
        _inner?.GetSignatureHelpAsync(filePath, position, cancellationToken) ?? Task.FromResult<SignatureHelpResult?>(null);

    /// <inheritdoc />
    public Task<DefinitionResult?> FindDefinitionAsync(string filePath, int position, CancellationToken cancellationToken) =>
        _inner?.FindDefinitionAsync(filePath, position, cancellationToken) ?? Task.FromResult<DefinitionResult?>(null);

    /// <inheritdoc />
    public Task<IReadOnlyList<DefinitionResult>> FindImplementationsAsync(string filePath, int position, CancellationToken cancellationToken) =>
        _inner?.FindImplementationsAsync(filePath, position, cancellationToken) ?? Task.FromResult<IReadOnlyList<DefinitionResult>>([]);

    /// <inheritdoc />
    public Task<IReadOnlyList<ReferenceItem>> FindReferencesAsync(string filePath, int position, CancellationToken cancellationToken) =>
        _inner?.FindReferencesAsync(filePath, position, cancellationToken) ?? Task.FromResult<IReadOnlyList<ReferenceItem>>([]);

    /// <inheritdoc />
    public Task<IReadOnlyList<DocumentHighlight>> GetDocumentHighlightsAsync(string filePath, int position, CancellationToken cancellationToken) =>
        _inner?.GetDocumentHighlightsAsync(filePath, position, cancellationToken) ?? Task.FromResult<IReadOnlyList<DocumentHighlight>>([]);

    /// <inheritdoc />
    public Task<RenameTarget> CanRenameAsync(string filePath, int position, CancellationToken cancellationToken) =>
        _inner?.CanRenameAsync(filePath, position, cancellationToken) ?? Task.FromResult(RenameTarget.Unavailable(NotAvailable));

    /// <inheritdoc />
    public Task<RenameResult> GetRenameEditsAsync(string filePath, int position, string newName, RenameOptions? options, CancellationToken cancellationToken) =>
        _inner?.GetRenameEditsAsync(filePath, position, newName, options, cancellationToken) ?? Task.FromResult(RenameResult.Failed(NotAvailable));

    /// <inheritdoc />
    public Task<IReadOnlyList<CodeActionItem>> GetCodeActionsAsync(string filePath, int start, int length, CancellationToken cancellationToken) =>
        _inner?.GetCodeActionsAsync(filePath, start, length, cancellationToken) ?? Task.FromResult<IReadOnlyList<CodeActionItem>>([]);

    /// <inheritdoc />
    public Task<IReadOnlyList<DocumentEdit>> GetCodeActionEditsAsync(string id, CancellationToken cancellationToken) =>
        _inner?.GetCodeActionEditsAsync(id, cancellationToken) ?? Task.FromResult<IReadOnlyList<DocumentEdit>>([]);

    /// <inheritdoc />
    public Task<IReadOnlyList<DocumentSymbol>> GetDocumentSymbolsAsync(string filePath, CancellationToken cancellationToken) =>
        _inner?.GetDocumentSymbolsAsync(filePath, cancellationToken) ?? Task.FromResult<IReadOnlyList<DocumentSymbol>>([]);

    /// <inheritdoc />
    public Task<IReadOnlyList<DocumentSymbol>> GetSymbolPathAsync(string filePath, int position, CancellationToken cancellationToken) =>
        _inner?.GetSymbolPathAsync(filePath, position, cancellationToken) ?? Task.FromResult<IReadOnlyList<DocumentSymbol>>([]);

    /// <inheritdoc />
    public Task WarmUpAsync(string filePath, CancellationToken cancellationToken) =>
        _inner?.WarmUpAsync(filePath, cancellationToken) ?? Task.CompletedTask;

    /// <summary>The snippet's description and its text, as the list shows next to the selected item.</summary>
    /// <param name="snippet">The snippet.</param>
    internal static IReadOnlyList<TaggedRun> Describe(SnippetDefinition snippet)
    {
        var preview = SnippetExpander.Expand(snippet.Body, new StandardSnippetVariables(), SnippetInsertion.Default).Text;
        var lines = preview.Split('\n');
        var shown = string.Join('\n', lines.Take(12)) + (lines.Length > 12 ? "\n…" : string.Empty);
        var title = string.IsNullOrWhiteSpace(snippet.Description) ? snippet.Name : snippet.Description;
        return
        [
            new TaggedRun(title, TaggedTextKind.Text),
            new TaggedRun("\n", TaggedTextKind.LineBreak),
            new TaggedRun("\n", TaggedTextKind.LineBreak),
            new TaggedRun(shown, TaggedTextKind.Text),
        ];
    }

    /// <summary>Where the word that ends at an offset starts.</summary>
    private int WordStart(int position)
    {
        var before = _editor.LineTextBefore(position);
        var start = before.Length;
        while (start > 0 && CompletionMatcher.IsIdentifierCharacter(before[start - 1]))
        {
            start--;
        }

        return position - (before.Length - start);
    }
}
