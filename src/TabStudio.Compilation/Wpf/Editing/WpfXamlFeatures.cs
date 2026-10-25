using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TabStudio.Compilation.Features;
using TabStudio.Compilation.Wpf.Xaml;
using TabStudio.Core.Diagnostics;

namespace TabStudio.Compilation.Wpf.Editing;

/// <summary>
/// IntelliSense for the XAML files of WPF projects, from the types of the project's compilation (OpenSilver's, and
/// the project's own): completion, Quick Info, Go to Definition, live errors and the light bulb's "add the event
/// handler". Positions are offsets into the text the workspace host last received for the file.
/// </summary>
/// <remarks>Every method may be called from any thread.</remarks>
public sealed class WpfXamlFeatures
{
    /// <summary>How many completion lists are remembered for committing and describing their items.</summary>
    private const int RememberedLists = 4;

    /// <summary>The start of the ids of the light bulb's actions this class makes.</summary>
    private const string ActionPrefix = "wpf-xaml-handler|";

    private readonly RoslynWorkspaceHost _host;
    private readonly Lock _gate = new();
    private readonly LinkedList<(long Id, IReadOnlyList<XamlCompletionItem> Items, Microsoft.CodeAnalysis.Compilation Compilation)> _lists = new();
    private long _lastList;

    /// <summary>Creates the features over a workspace host.</summary>
    /// <param name="host">The host, which has the WPF projects loaded.</param>
    public WpfXamlFeatures(RoslynWorkspaceHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
    }

    /// <summary>Whether a file is a XAML file of a loaded WPF project, which these features answer for.</summary>
    /// <param name="filePath">Full path of the file.</param>
    public bool AppliesTo(string filePath) => _host.IsWpfXamlDocument(filePath);

    /// <summary>The completion list at a position.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="position">The caret.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<CompletionResult> GetCompletionsAsync(string filePath, int position, CancellationToken cancellationToken = default)
    {
        if (await _host.GetWpfXamlFileAsync(filePath, cancellationToken).ConfigureAwait(false) is not { } file)
        {
            return CompletionResult.Empty;
        }

        var semantics = XamlSemantics.For(file);
        var caret = XamlCaret.At(semantics.Document, position);
        var (found, start, end) = XamlCompletion.Complete(semantics, caret, cancellationToken);
        if (found.Count == 0)
        {
            return CompletionResult.Empty;
        }

        var items = found
            .DistinctBy(i => i.DisplayText, StringComparer.Ordinal)
            .OrderBy(i => i.SortText ?? i.DisplayText, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var id = Remember(items, file.Compilation);
        var entries = items.Select((item, index) => new CompletionEntry(
            new CompletionHandle(id, index),
            item.DisplayText,
            string.Empty,
            string.Empty,
            item.FilterText ?? item.DisplayText,
            item.SortText ?? item.DisplayText,
            item.Description ?? string.Empty,
            item.Glyph,
            item.IsPreselected,
            start,
            end - start)
        {
            CommitCharacters = CommitCharacters(item),
        }).ToList();
        return new CompletionResult(entries, start, end - start, IsSuggestionMode: false);
    }

    /// <summary>Whether typing a character should open the list: <c>&lt;</c>, a space in a tag, a quote, a brace, or a letter of a name or value.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="position">The caret, after the typed character.</param>
    /// <param name="typedCharacter">The typed character.</param>
    public bool ShouldTriggerCompletion(string filePath, int position, char typedCharacter)
    {
        if (typedCharacter == '<')
        {
            return true;
        }

        if (_host.TryGetWpfXamlText(filePath) is not { } text)
        {
            return false;
        }

        var caret = XamlCaret.At(XamlDocument.Parse(text), position);
        return caret.Kind switch
        {
            XamlCaretKind.ElementName or XamlCaretKind.EndTagName => XamlText.IsNameChar(typedCharacter) || typedCharacter == '/',
            XamlCaretKind.AttributeName => typedCharacter == ' ' ? caret.Attribute is null : XamlText.IsNameChar(typedCharacter),
            XamlCaretKind.AttributeValue => typedCharacter is '"' or '\'' or '{' or ',' or '=' or '.'
                || (typedCharacter == ' ' && XamlMarkupExtension.At(caret.Attribute!.RawValue, caret.OffsetInValue) is not null)
                || (char.IsLetter(typedCharacter) && (caret.Typed.Length <= 1 || XamlMarkupExtension.At(caret.Attribute!.RawValue, caret.OffsetInValue) is not null)),
            _ => false,
        };
    }

    /// <summary>The edit that commits an item, over the word as it is now.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="entry">The item.</param>
    /// <param name="commitCharacter">The character typed to commit it, or <see langword="null"/> for Tab and Enter.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    public async Task<CompletionCommit> GetCompletionChangeAsync(string filePath, CompletionEntry entry, char? commitCharacter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var text = _host.TryGetWpfXamlText(filePath) ?? (await _host.GetWpfXamlFileAsync(filePath, cancellationToken).ConfigureAwait(false))?.Text ?? string.Empty;
        var item = Find(entry.Handle);
        var start = Math.Min(entry.SpanStart, text.Length);
        var end = WordEnd(text, start, item?.Word ?? XamlWordKind.Name);
        var insert = item?.InsertText ?? entry.DisplayText;
        var caretFromEnd = item?.CaretFromEnd ?? 0;
        if (item is { IsAttributeName: true })
        {
            // Name="" with the caret inside, unless a character was typed to commit or an = follows already.
            var next = end;
            while (next < text.Length && text[next] is ' ' or '\t')
            {
                next++;
            }

            if (commitCharacter is null && (next >= text.Length || text[next] != '='))
            {
                insert += "=\"\"";
                caretFromEnd = 1;
            }
        }
        else if (commitCharacter == '=' && insert.EndsWith('='))
        {
            insert = insert[..^1];
            caretFromEnd = 0;
        }

        var caret = caretFromEnd > 0 ? start + insert.Length - caretFromEnd : (int?)null;
        return new CompletionCommit([new TextEdit(start, end - start, insert)], caret, IncludesCommitCharacter: false);
    }

    /// <summary>The tooltip of a completion item: the declaration and summary of what it stands for.</summary>
    /// <param name="entry">The item.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    public Task<IReadOnlyList<TaggedRun>> GetCompletionDescriptionAsync(CompletionEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Microsoft.CodeAnalysis.Compilation? compilation;
        XamlCompletionItem? item;
        lock (_gate)
        {
            var list = _lists.FirstOrDefault(l => l.Id == entry.Handle.Session);
            compilation = list.Compilation;
            item = list.Items is { } items && entry.Handle.Index < items.Count ? items[entry.Handle.Index] : null;
        }

        if (item?.Symbol is not { } symbol || compilation is null)
        {
            IReadOnlyList<TaggedRun> plain = item?.Description is { Length: > 0 } description ? [new TaggedRun(description, TaggedTextKind.Text)] : [];
            return Task.FromResult(plain);
        }

        var runs = XamlNavigation.Describe(symbol, item.Description?.StartsWith("attached", StringComparison.Ordinal) == true);
        if (XamlDocumentation.Summary(symbol, compilation, cancellationToken) is { Length: > 0 } summary)
        {
            runs.Add(new TaggedRun("\n", TaggedTextKind.LineBreak));
            runs.Add(new TaggedRun(summary, TaggedTextKind.Text));
        }

        if (WpfDiagnostics.IsNotImplemented(symbol))
        {
            runs.Add(new TaggedRun("\n", TaggedTextKind.LineBreak));
            runs.Add(new TaggedRun("Not implemented by OpenSilver: it does nothing in the web preview.", TaggedTextKind.Text));
        }

        return Task.FromResult<IReadOnlyList<TaggedRun>>(runs);
    }

    /// <summary>The Quick Info of the name at a position.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="position">The position.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    public async Task<QuickInfoResult?> GetQuickInfoAsync(string filePath, int position, CancellationToken cancellationToken = default)
    {
        if (await TargetAsync(filePath, position, cancellationToken).ConfigureAwait(false) is not var (semantics, target))
        {
            return null;
        }

        return XamlNavigation.QuickInfo(semantics, target, cancellationToken);
    }

    /// <summary>Where the name at a position is defined: a type or member, a handler in the code-behind, a resource or a named element.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="position">The position.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    public async Task<DefinitionResult?> FindDefinitionAsync(string filePath, int position, CancellationToken cancellationToken = default)
    {
        if (await TargetAsync(filePath, position, cancellationToken).ConfigureAwait(false) is not var (semantics, target))
        {
            return null;
        }

        return XamlNavigation.Definition(semantics, target);
    }

    /// <summary>The live errors and warnings of a XAML file.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="cancellationToken">Cancels the analysis.</param>
    public async Task<IReadOnlyList<BuildDiagnostic>> GetDiagnosticsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return await _host.GetWpfXamlFileAsync(filePath, cancellationToken).ConfigureAwait(false) is { } file
            ? XamlAnalyzer.Analyze(XamlSemantics.For(file), cancellationToken)
            : [];
    }

    /// <summary>The light bulb's actions at a range: adding an event handler the code-behind does not have yet.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="start">The start of the range.</param>
    /// <param name="length">Its length.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    public async Task<IReadOnlyList<CodeActionItem>> GetCodeActionsAsync(string filePath, int start, int length, CancellationToken cancellationToken = default)
    {
        if (await MissingHandlerAsync(filePath, start, cancellationToken).ConfigureAwait(false) is not { } missing)
        {
            return [];
        }

        return [new CodeActionItem($"{ActionPrefix}{start}|{filePath}", $"Add {missing.Name} to {Path.GetFileName(missing.Tree.FilePath)}", null, CodeActionKind.QuickFix, WpfDiagnosticIds.MissingEventHandler)];
    }

    /// <summary>Whether an id is one of the light bulb's actions this class made.</summary>
    /// <param name="id">The action's id.</param>
    public static bool IsCodeAction(string id) => id?.StartsWith(ActionPrefix, StringComparison.Ordinal) == true;

    /// <summary>The edits of a light bulb action: the new handler, written into the code-behind's class.</summary>
    /// <param name="id">The action's id, from <see cref="GetCodeActionsAsync"/>.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    public async Task<IReadOnlyList<DocumentEdit>> GetCodeActionEditsAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        var parts = id.Split('|', 3);
        if (!IsCodeAction(id) || parts.Length != 3 || !int.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out var start)
            || await MissingHandlerAsync(parts[2], start, cancellationToken).ConfigureAwait(false) is not { } missing)
        {
            return [];
        }

        var text = (await missing.Tree.GetTextAsync(cancellationToken).ConfigureAwait(false)).ToString();
        var close = missing.Class.CloseBraceToken.SpanStart;
        var lineStart = text.LastIndexOf('\n', Math.Max(0, close - 1)) + 1;
        var indent = new string(' ', Math.Max(0, close - lineStart)) + "    ";
        var parameters = string.Join(", ", missing.Parameters.Select((p, i) => $"{p.Type.ToMinimalDisplayString(missing.Model, close)} {(i == 0 ? "sender" : i == 1 ? "e" : p.Name)}"));
        var method = $"\n{indent}private void {missing.Name}({parameters})\n{indent}{{\n{indent}}}\n";
        var insertAt = text.LastIndexOf('\n', Math.Max(0, close - 1));
        insertAt = insertAt < 0 ? close : insertAt;
        return [new DocumentEdit(missing.Tree.FilePath, DocumentEditKind.Modified, [new TextEdit(insertAt, 0, method.TrimEnd('\n'))])];
    }

    private async Task<(XamlSemantics Semantics, XamlTarget Target)?> TargetAsync(string filePath, int position, CancellationToken cancellationToken)
    {
        if (await _host.GetWpfXamlFileAsync(filePath, cancellationToken).ConfigureAwait(false) is not { } file)
        {
            return null;
        }

        var semantics = XamlSemantics.For(file);
        var caret = XamlCaret.At(semantics.Document, position);
        return XamlNavigation.TargetAt(semantics, caret, cancellationToken) is { } target ? (semantics, target) : null;
    }

    /// <summary>The handler an event attribute's value at a position names that the code-behind's class does not have.</summary>
    private async Task<MissingHandler?> MissingHandlerAsync(string filePath, int position, CancellationToken cancellationToken)
    {
        if (await _host.GetWpfXamlFileAsync(filePath, cancellationToken).ConfigureAwait(false) is not { } file)
        {
            return null;
        }

        var semantics = XamlSemantics.For(file);
        var caret = XamlCaret.At(semantics.Document, position);
        if (caret is not { Kind: XamlCaretKind.AttributeValue, Element: { } element, Attribute: { } attribute }
            || semantics.MemberOf(element, attribute) is not { IsEvent: true } member
            || attribute.Value.Trim() is not { Length: > 0 } name
            || !Microsoft.CodeAnalysis.CSharp.SyntaxFacts.IsValidIdentifier(name)
            || semantics.Class is not { } @class
            || @class.GetMembers(name).Length > 0)
        {
            return null;
        }

        foreach (var reference in @class.DeclaringSyntaxReferences)
        {
            if (reference.SyntaxTree.FilePath.Contains(nameof(WpfXamlGenerator), StringComparison.Ordinal) || reference.GetSyntax(cancellationToken) is not ClassDeclarationSyntax declaration)
            {
                continue;
            }

            var parameters = (member.Type as INamedTypeSymbol)?.DelegateInvokeMethod?.Parameters ?? [];
            return new MissingHandler(name, reference.SyntaxTree, declaration, semantics.Types.Compilation.GetSemanticModel(reference.SyntaxTree), parameters);
        }

        return null;
    }

    private static IReadOnlyList<char> CommitCharacters(XamlCompletionItem item) => item.Word switch
    {
        XamlWordKind.Name when item.IsAttributeName => [' ', '=', '/', '>'],
        XamlWordKind.Name => [' ', '>', '/'],
        XamlWordKind.ExtensionPart when item.InsertText.EndsWith('=') => ['='],
        XamlWordKind.ExtensionPart => [',', '}'],
        _ => [],
    };

    private static int WordEnd(string text, int start, XamlWordKind word)
    {
        var end = start;
        while (end < text.Length && word switch
        {
            XamlWordKind.Name => XamlText.IsNameChar(text[end]),
            XamlWordKind.Value => text[end] is not ('"' or '\'' or '<'),
            _ => text[end] is not (',' or '=' or '}' or '{' or ' ' or '"' or '\'' or '.' or '<'),
        })
        {
            end++;
        }

        return end;
    }

    private long Remember(IReadOnlyList<XamlCompletionItem> items, Microsoft.CodeAnalysis.Compilation compilation)
    {
        lock (_gate)
        {
            var id = ++_lastList;
            _lists.AddFirst((id, items, compilation));
            while (_lists.Count > RememberedLists)
            {
                _lists.RemoveLast();
            }

            return id;
        }
    }

    private XamlCompletionItem? Find(CompletionHandle handle)
    {
        lock (_gate)
        {
            var list = _lists.FirstOrDefault(l => l.Id == handle.Session);
            return list.Items is { } items && handle.Index >= 0 && handle.Index < items.Count ? items[handle.Index] : null;
        }
    }

    /// <summary>An event handler to add to the code-behind.</summary>
    private sealed record MissingHandler(string Name, SyntaxTree Tree, ClassDeclarationSyntax Class, SemanticModel Model, IReadOnlyList<IParameterSymbol> Parameters);
}
