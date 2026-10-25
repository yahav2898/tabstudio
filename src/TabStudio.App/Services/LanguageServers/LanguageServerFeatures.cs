using TabStudio.App.Editing;
using TabStudio.Compilation;
using TabStudio.Compilation.Features;
using TabStudio.LanguageServers.Documents;
using TabStudio.LanguageServers.Features;
using TabStudio.LanguageServers.Hosting;
using TabStudio.LanguageServers.JsonRpc;

namespace TabStudio.App.Services.LanguageServers;

/// <summary>
/// IntelliSense for a file a language server owns, through the same <see cref="ILanguageFeatures"/> Roslyn answers
/// for C#: the completion list, Quick Info, Parameter Info, navigation, references, highlights, rename, the light bulb
/// and the breadcrumb, each a Language Server Protocol request about the document.
/// </summary>
/// <remarks>
/// The document's text reaches the server with every edit, so the offsets here are offsets into the editor's text.
/// A feature the server lacks answers nothing, as a C# file outside a project does.
/// </remarks>
internal sealed class LanguageServerFeatures : ILanguageFeatures, ICodeActionFollowUp
{
    private const int RememberedLists = 4;

    private static readonly char[] SignatureSeparators = [' ', '(', ')'];

    private readonly LanguageServerDocument _document;
    private readonly Func<string, string?> _readText;
    private readonly Lock _gate = new();
    private readonly LinkedList<(long Session, LspCompletionList List)> _lists = new();
    private readonly Dictionary<string, LspCodeAction> _actions = new(StringComparer.Ordinal);
    private (int Version, IReadOnlyList<DocumentSymbol> Symbols)? _symbols;
    private long _lastSession;
    private long _lastAction;

    /// <summary>Creates the features of a document.</summary>
    /// <param name="document">The document as the language server knows it.</param>
    /// <param name="readText">Reads a file that is not open, for results in other files.</param>
    public LanguageServerFeatures(LanguageServerDocument document, Func<string, string?> readText)
    {
        _document = document;
        _readText = readText;
    }

    /// <inheritdoc />
    public async Task<CompletionResult> GetCompletionsAsync(string filePath, int position, CompletionTrigger trigger, CancellationToken cancellationToken)
    {
        var isTrigger = trigger.Kind == CompletionTriggerKind.Insertion && _document.Capabilities.CompletionTriggerCharacters.Contains(trigger.Character);
        var list = await Ask(() => _document.GetCompletionsAsync(
            position,
            isTrigger ? LspCompletionTriggerKind.TriggerCharacter : LspCompletionTriggerKind.Invoked,
            isTrigger ? trigger.Character : null,
            cancellationToken), null);
        if (list is not { Items.Count: > 0 })
        {
            return CompletionResult.Empty;
        }

        long session;
        lock (_gate)
        {
            session = ++_lastSession;
            _lists.AddFirst((session, list));
            while (_lists.Count > RememberedLists)
            {
                _lists.RemoveLast();
            }
        }

        var entries = list.Items
            .Select((item, index) => ToEntry(item, new CompletionHandle(session, index)))
            .OrderBy(e => e.SortText, StringComparer.Ordinal)
            .ThenBy(e => e.DisplayText, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // The list filters by what is typed from the start of the word; servers replace the same word with every item.
        var start = Math.Clamp(list.Items[0].Edit.Span.Start, 0, position);
        return new CompletionResult(entries, start, position - start, IsSuggestionMode: false);
    }

    /// <inheritdoc />
    public bool ShouldTriggerCompletion(string filePath, int position, char typedCharacter)
    {
        if (_document.Capabilities.CompletionTriggerCharacters.Contains(typedCharacter))
        {
            return true;
        }

        // As in VS Code: the first letter of a word opens the list; letters after it only filter it.
        if (!char.IsLetter(typedCharacter) && typedCharacter != '_')
        {
            return false;
        }

        var text = _document.Snapshot.Text;
        var before = position - 2;
        return before < 0 || before >= text.Length || !CompletionMatcher.IsIdentifierCharacter(text[before]);
    }

    /// <inheritdoc />
    public async Task<CompletionCommit> GetCompletionChangeAsync(string filePath, CompletionEntry entry, char? commitCharacter, CancellationToken cancellationToken)
    {
        if (Find(entry.Handle) is not { } found)
        {
            return new CompletionCommit([], null, false);
        }

        var (list, item) = found;
        if (_document.Capabilities.CompletionResolve && item.AdditionalEdits.Count == 0)
        {
            // Auto-imports often come with the resolved item.
            item = await Ask(() => _document.ResolveCompletionAsync(item, cancellationToken), item);
        }

        // The list was computed before the user typed on to filter it: the word to replace grew by what was typed.
        var current = _document.Snapshot.Text.Length;
        var typed = current - (list.Snapshot?.Text.Length ?? current);
        var main = item.Edit;
        var mainEnd = main.Span.End >= list.Offset ? main.Span.End + typed : main.Span.End;
        mainEnd = Math.Clamp(mainEnd, main.Span.Start, current);
        var edits = new List<TextEdit>();
        foreach (var additional in item.AdditionalEdits)
        {
            var start = additional.Span.Start >= list.Offset ? additional.Span.Start + typed : additional.Span.Start;
            if (start + additional.Span.Length <= main.Span.Start || start >= mainEnd)
            {
                edits.Add(new TextEdit(start, additional.Span.Length, additional.NewText));
            }
        }

        // Edits that come before the item's text, such as an import at the top, move where its caret goes.
        var shift = edits.Where(e => e.Start < main.Span.Start).Sum(e => e.NewText.Length - e.Length);
        edits.Add(new TextEdit(main.Span.Start, mainEnd - main.Span.Start, main.NewText));
        edits.Sort((a, b) => a.Start.CompareTo(b.Start));
        var caret = main.Span.Start + shift + Math.Clamp(main.CaretOffset, 0, main.NewText.Length);
        return new CompletionCommit(edits, caret, IncludesCommitCharacter: false)
        {
            SelectionLength = main.SelectionLength,
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TaggedRun>> GetCompletionDescriptionAsync(CompletionEntry entry, CancellationToken cancellationToken)
    {
        if (Find(entry.Handle) is not { } found)
        {
            return [];
        }

        var item = found.Item;
        if (_document.Capabilities.CompletionResolve && (item.Documentation is null || item.Detail is null))
        {
            item = await Ask(() => _document.ResolveCompletionAsync(item, cancellationToken), item);
        }

        var runs = new List<TaggedRun>();
        if (!string.IsNullOrWhiteSpace(item.Detail))
        {
            runs.AddRange(MarkupRuns.Code(item.Detail.Trim()));
        }

        if (item.Documentation is { IsEmpty: false } documentation)
        {
            if (runs.Count > 0)
            {
                runs.Add(new TaggedRun("\n", TaggedTextKind.LineBreak));
            }

            runs.AddRange(MarkupRuns.ToRuns(documentation));
        }

        return runs;
    }

    /// <inheritdoc />
    public async Task<QuickInfoResult?> GetQuickInfoAsync(string filePath, int position, CancellationToken cancellationToken)
    {
        if (await Ask(() => _document.GetHoverAsync(position, cancellationToken), null) is not { } hover)
        {
            return null;
        }

        var sections = MarkupRuns.ToSections(hover.Contents);
        if (sections.Count == 0)
        {
            return null;
        }

        var span = hover.Span ?? WordAt(_document.Snapshot.Text, position);
        var signature = sections.FirstOrDefault(s => s.Kind == QuickInfoSectionKind.Description)?.Text ?? string.Empty;
        return new QuickInfoResult(span.Start, span.Length, GlyphOf(signature), sections);
    }

    /// <inheritdoc />
    public async Task<SignatureHelpResult?> GetSignatureHelpAsync(string filePath, int position, CancellationToken cancellationToken)
    {
        var text = _document.Snapshot.Text;
        char? trigger = position > 0 && position <= text.Length && _document.Capabilities.SignatureHelpTriggerCharacters.Contains(text[position - 1])
            ? text[position - 1]
            : null;
        if (await Ask(() => _document.GetSignatureHelpAsync(position, trigger, isRetrigger: false, cancellationToken), null) is not { Signatures.Count: > 0 } help)
        {
            return null;
        }

        var signatures = help.Signatures.Select(s => ToSignature(s, help.ActiveParameter)).ToList();
        var active = Math.Clamp(help.ActiveSignature, 0, signatures.Count - 1);
        var start = CallStart(text, position);
        var argument = signatures[active].ActiveParameter;
        var parameters = signatures[active].Parameters;
        return new SignatureHelpResult(
            signatures,
            active,
            argument,
            argument >= 0 && argument < parameters.Count ? parameters[argument].Name : null,
            start,
            position - start);
    }

    /// <inheritdoc />
    public async Task<DefinitionResult?> FindDefinitionAsync(string filePath, int position, CancellationToken cancellationToken)
    {
        var locations = await Ask(() => _document.GetDefinitionsAsync(position, cancellationToken), []);
        if (locations.Count == 0)
        {
            locations = await Ask(() => _document.GetDeclarationsAsync(position, cancellationToken), []);
        }

        return ToDefinition(position, locations);
    }

    /// <summary>Go to Declaration: where the symbol at a position is declared, as opposed to defined.</summary>
    /// <param name="position">A position on the symbol.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<DefinitionResult?> FindDeclarationAsync(int position, CancellationToken cancellationToken) =>
        ToDefinition(position, await Ask(() => _document.GetDeclarationsAsync(position, cancellationToken), []));

    /// <summary>Go to Type Definition: where the type of the symbol at a position is defined.</summary>
    /// <param name="position">A position on the symbol.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<DefinitionResult?> FindTypeDefinitionAsync(int position, CancellationToken cancellationToken) =>
        ToDefinition(position, await Ask(() => _document.GetTypeDefinitionsAsync(position, cancellationToken), []));

    /// <summary>Whether the server can tell where things are declared.</summary>
    public bool CanGoToDeclaration => _document.Capabilities.Declaration;

    /// <summary>Whether the server can tell where the types of things are defined.</summary>
    public bool CanGoToTypeDefinition => _document.Capabilities.TypeDefinition;

    /// <inheritdoc />
    public async Task<IReadOnlyList<DefinitionResult>> FindImplementationsAsync(string filePath, int position, CancellationToken cancellationToken)
    {
        var locations = await Ask(() => _document.GetImplementationsAsync(position, cancellationToken), []);
        if (ToDefinition(position, locations) is not { } result)
        {
            return [];
        }

        return [result];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReferenceItem>> FindReferencesAsync(string filePath, int position, CancellationToken cancellationToken)
    {
        var references = await Ask(() => _document.FindReferencesAsync(position, includeDeclaration: true, cancellationToken), []);
        var definitions = await Ask(() => _document.GetDefinitionsAsync(position, cancellationToken), []);
        var lines = new Dictionary<string, string[]>(StringComparer.Ordinal);
        return references
            .Select(r => new ReferenceItem(
                r.FilePath,
                r.Span.Start,
                r.Span.Length,
                r.Line,
                r.Column,
                LineText(lines, r.FilePath, r.Line),
                definitions.Any(d => d.FilePath == r.FilePath && d.Span.Start == r.Span.Start)))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentHighlight>> GetDocumentHighlightsAsync(string filePath, int position, CancellationToken cancellationToken)
    {
        var highlights = await Ask(() => _document.GetHighlightsAsync(position, cancellationToken), []);
        return highlights
            .Select(h => new DocumentHighlight(h.Span.Start, h.Span.Length, h.Kind == LspHighlightKind.Write ? HighlightKind.Definition : HighlightKind.Reference))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<RenameTarget> CanRenameAsync(string filePath, int position, CancellationToken cancellationToken)
    {
        if (!_document.Capabilities.Rename)
        {
            return RenameTarget.Unavailable($"{_document.ServerName} cannot rename.");
        }

        try
        {
            return await _document.PrepareRenameAsync(position, cancellationToken) is { } target
                ? new RenameTarget(true, null, target.Placeholder, target.Span.Start, target.Span.Length)
                : RenameTarget.Unavailable("This element cannot be renamed.");
        }
        catch (JsonRpcException ex) when (!ex.IsConnectionClosed)
        {
            return RenameTarget.Unavailable(ex.Message);
        }
        catch (JsonRpcException)
        {
            return RenameTarget.Unavailable($"{_document.ServerName} stopped.");
        }
    }

    /// <inheritdoc />
    public async Task<RenameResult> GetRenameEditsAsync(string filePath, int position, string newName, RenameOptions? options, CancellationToken cancellationToken)
    {
        try
        {
            return await _document.RenameAsync(position, newName, cancellationToken) is { EditCount: > 0 } edit
                ? new RenameResult(true, null, LspMapping.ToDocumentEdits(edit, _readText), [])
                : RenameResult.Failed("There is nothing to rename here.");
        }
        catch (JsonRpcException ex)
        {
            return RenameResult.Failed(ex.IsConnectionClosed ? $"{_document.ServerName} stopped." : ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CodeActionItem>> GetCodeActionsAsync(string filePath, int start, int length, CancellationToken cancellationToken)
    {
        var actions = await Ask(() => _document.GetCodeActionsAsync(new TextSpan(start, length), cancellationToken), []);
        var items = new List<(CodeActionItem Item, bool IsPreferred)>();
        lock (_gate)
        {
            _actions.Clear();
            foreach (var action in actions.Where(a => a.DisabledReason is null))
            {
                var id = "lsp:" + (++_lastAction).ToString(System.Globalization.CultureInfo.InvariantCulture);
                _actions[id] = action;
                var kind = action.IsQuickFix ? CodeActionKind.QuickFix : CodeActionKind.Refactoring;
                var code = action.DiagnosticCodes.Count > 0 ? action.DiagnosticCodes[0] : null;
                items.Add((new CodeActionItem(id, action.Title, null, kind, code), action.IsPreferred));
            }
        }

        // Preferred fixes first, as VS Code lists them.
        return items.OrderBy(i => i.IsPreferred ? 0 : 1).ThenBy(i => i.Item.Kind).Select(i => i.Item).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentEdit>> GetCodeActionEditsAsync(string id, CancellationToken cancellationToken)
    {
        LspCodeAction? action;
        lock (_gate)
        {
            _actions.TryGetValue(id, out action);
        }

        if (action is null)
        {
            return [];
        }

        try
        {
            var edit = await _document.GetCodeActionEditAsync(action, cancellationToken);
            var edits = LspMapping.ToDocumentEdits(edit, _readText);
            if (edits.Count == 0 && action.HasCommandAfterEdit)
            {
                // A command that only does its work on the server, such as organizing imports in another way.
                await _document.RunCodeActionCommandAsync(action, cancellationToken);
            }

            return edits;
        }
        catch (JsonRpcException)
        {
            return [];
        }
    }

    /// <inheritdoc />
    public async Task RunAfterEditsAsync(string id, CancellationToken cancellationToken)
    {
        LspCodeAction? action;
        lock (_gate)
        {
            _actions.TryGetValue(id, out action);
        }

        if (action is { HasCommandAfterEdit: true })
        {
            try
            {
                await _document.RunCodeActionCommandAsync(action, cancellationToken);
            }
            catch (JsonRpcException)
            {
                // The edit was applied; the server says in its log why its command failed.
            }
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentSymbol>> GetDocumentSymbolsAsync(string filePath, CancellationToken cancellationToken)
    {
        var version = _document.Snapshot.Version;
        if (_symbols is { } cached && cached.Version == version)
        {
            return cached.Symbols;
        }

        var symbols = (await Ask(() => _document.GetSymbolsAsync(cancellationToken), [])).Select(ToSymbol).ToList();
        _symbols = (version, symbols);
        return symbols;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentSymbol>> GetSymbolPathAsync(string filePath, int position, CancellationToken cancellationToken)
    {
        var path = new List<DocumentSymbol>();
        IReadOnlyList<DocumentSymbol> level = await GetDocumentSymbolsAsync(filePath, cancellationToken);
        while (level.LastOrDefault(s => s.Contains(position)) is { } symbol)
        {
            path.Add(symbol);
            level = symbol.Children;
        }

        return path;
    }

    /// <inheritdoc />
    public Task WarmUpAsync(string filePath, CancellationToken cancellationToken) => _document.WaitUntilReadyAsync(cancellationToken);

    /// <summary>The icon of a symbol from the first words of its signature, such as Pyright's "(function)".</summary>
    internal static SymbolGlyph GlyphOf(string signature)
    {
        var text = signature.TrimStart();
        var first = text.Split(SignatureSeparators, 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return first switch
        {
            "function" or "method" or "def" or "fn" or "func" or "constructor" => SymbolGlyph.Method,
            "class" or "type" or "struct" => SymbolGlyph.Class,
            "interface" or "trait" => SymbolGlyph.Interface,
            "enum" => SymbolGlyph.Enum,
            "module" or "namespace" or "package" or "import" or "alias" => SymbolGlyph.Namespace,
            "property" or "getter" or "setter" => SymbolGlyph.Property,
            "parameter" => SymbolGlyph.Parameter,
            "field" => SymbolGlyph.Field,
            "const" or "constant" => SymbolGlyph.Constant,
            "variable" or "var" or "let" or "local" => SymbolGlyph.Local,
            "type_alias" or "typeParameter" => SymbolGlyph.TypeParameter,
            _ => SymbolGlyph.Other,
        };
    }

    /// <summary>Where the arguments of the call around a position start: just after its unclosed parenthesis.</summary>
    internal static int CallStart(string text, int position)
    {
        var depth = 0;
        for (var i = Math.Min(position, text.Length) - 1; i >= 0; i--)
        {
            switch (text[i])
            {
                case ')' or ']' or '}':
                    depth++;
                    break;
                case '(' when depth == 0:
                    return i + 1;
                case '(' or '[' or '{':
                    depth = Math.Max(0, depth - 1);
                    break;
                case '\n' when depth == 0 && i < position - 400:
                    return position;
            }
        }

        return position;
    }

    private static CompletionEntry ToEntry(LspCompletionItem item, CompletionHandle handle) =>
        new(
            handle,
            item.Label,
            string.Empty,
            item.LabelDetail ?? string.Empty,
            item.FilterText,
            item.SortText,
            item.LabelDescription ?? string.Empty,
            LspMapping.Glyph(item.Kind),
            item.IsPreselected,
            item.Edit.Span.Start,
            item.Edit.Span.Length)
        {
            CommitCharacters = item.CommitCharacters,
        };

    private static SignatureItem ToSignature(LspSignature signature, int activeParameter)
    {
        var label = signature.Label;
        var parameters = signature.Parameters
            .Where(p => p.LabelStart >= 0 && p.LabelStart + p.LabelLength <= label.Length)
            .ToList();
        IReadOnlyList<TaggedRun> prefix;
        IReadOnlyList<TaggedRun> suffix;
        var items = new List<SignatureParameter>();
        if (parameters.Count == 0)
        {
            var open = label.IndexOf('(', StringComparison.Ordinal);
            var split = open < 0 ? label.Length : open + 1;
            prefix = MarkupRuns.Code(label[..split]);
            suffix = MarkupRuns.Code(label[split..]);
        }
        else
        {
            prefix = MarkupRuns.Code(label[..parameters[0].LabelStart]);
            var last = parameters[^1];
            suffix = MarkupRuns.Code(label[(last.LabelStart + last.LabelLength)..]);
            foreach (var parameter in parameters)
            {
                var display = label.Substring(parameter.LabelStart, parameter.LabelLength);
                var name = new string(display.TakeWhile(c => char.IsLetterOrDigit(c) || c is '_' or '$' or '*' or '.').ToArray()).TrimStart('*', '.');
                items.Add(new SignatureParameter(
                    name.Length == 0 ? display : name,
                    MarkupRuns.Code(display),
                    MarkupRuns.ToPlainText(parameter.Documentation),
                    display.Contains('=', StringComparison.Ordinal) || display.Contains('?', StringComparison.Ordinal)));
            }
        }

        var active = Math.Clamp(signature.ActiveParameter ?? activeParameter, -1, Math.Max(-1, items.Count - 1));
        return new SignatureItem(prefix, items, suffix, MarkupRuns.ToPlainText(signature.Documentation), active, IsVariadic: false);
    }

    private static DocumentSymbol ToSymbol(LspDocumentSymbol symbol) =>
        new(
            symbol.Name,
            symbol.Name,
            LspMapping.Glyph(symbol.Kind),
            SymbolAccessibility.NotApplicable,
            symbol.Span.Start,
            symbol.Span.Length,
            symbol.NameSpan.Start,
            symbol.NameSpan.Length,
            symbol.Children.Select(ToSymbol).ToList());

    private static TextSpan WordAt(string text, int position)
    {
        var start = Math.Clamp(position, 0, text.Length);
        var end = start;
        while (start > 0 && CompletionMatcher.IsIdentifierCharacter(text[start - 1]))
        {
            start--;
        }

        while (end < text.Length && CompletionMatcher.IsIdentifierCharacter(text[end]))
        {
            end++;
        }

        return TextSpan.FromBounds(start, end);
    }

    /// <summary>Asks the server; when it is not running, has stopped, or refused, answers what a missing feature answers.</summary>
    private static async Task<T> Ask<T>(Func<Task<T>> request, T fallback)
    {
        try
        {
            return await request();
        }
        catch (JsonRpcException)
        {
            return fallback;
        }
    }

    private DefinitionResult? ToDefinition(int position, IReadOnlyList<LspLocation> locations)
    {
        if (locations.Count == 0)
        {
            return null;
        }

        var word = WordAt(_document.Snapshot.Text, position);
        var name = word.Length > 0 ? _document.Snapshot.Text.Substring(word.Start, word.Length) : "symbol";
        return new DefinitionResult(
            name,
            SymbolGlyph.Other,
            locations.Select(l => new SourceLocation(l.FilePath, l.Span.Start, l.Span.Length, l.Line, l.Column)).ToList(),
            null);
    }

    private (LspCompletionList List, LspCompletionItem Item)? Find(CompletionHandle handle)
    {
        lock (_gate)
        {
            foreach (var (session, list) in _lists)
            {
                if (session == handle.Session && handle.Index >= 0 && handle.Index < list.Items.Count)
                {
                    return (list, list.Items[handle.Index]);
                }
            }
        }

        return null;
    }

    private string LineText(Dictionary<string, string[]> cache, string path, int line)
    {
        if (!cache.TryGetValue(path, out var lines))
        {
            var text = string.Equals(path, _document.FilePath, StringComparison.Ordinal) ? _document.Snapshot.Text : _readText(path) ?? string.Empty;
            lines = text.Split('\n');
            cache[path] = lines;
        }

        return line >= 1 && line <= lines.Length ? lines[line - 1].TrimEnd('\r') : string.Empty;
    }
}
