using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Text;
using TabStudio.Compilation.Razor;
using RoslynCompilation = Microsoft.CodeAnalysis.Compilation;

namespace TabStudio.Compilation.Features;

/// <summary>
/// Completion in Razor files: C# inside <c>@code</c> blocks and expressions comes from the C# the Razor compiler
/// generated, through its <c>#line</c> mappings; in markup, the components the file can use after <c>&lt;</c>, a
/// component's parameters (and <c>@bind-</c> for those with a <c>Changed</c> callback) inside its tag, and the
/// directives after <c>@</c> at the start of a line.
/// </summary>
public sealed partial class CompletionFeature
{
    /// <summary>The handle of items that are not Roslyn's, such as directives and component names.</summary>
    private const long MarkupSession = -1;

    private static readonly string[] ComponentDirectives =
        ["page", "using", "inject", "code", "inherits", "implements", "layout", "attribute", "namespace", "typeparam", "rendermode", "preservewhitespace", "functions"];

    private static readonly string[] ViewDirectives =
        ["page", "model", "using", "inject", "inherits", "functions", "section", "addTagHelper", "removeTagHelper", "tagHelperPrefix", "namespace", "implements", "attribute"];

    private static readonly string[] ControlKeywords = ["if", "foreach", "for", "while", "switch", "do", "try", "lock"];

    private static readonly string[] ComponentDirectiveAttributes = ["@key", "@ref", "@rendermode"];

    private static readonly string[] ElementDirectiveAttributes =
    [
        "@onclick", "@onchange", "@oninput", "@onsubmit", "@onkeydown", "@onkeyup", "@onfocus", "@onblur", "@onmouseover",
        "@onmouseout", "@bind", "@bind-value", "@bind:event", "@bind:format", "@bind:after", "@key", "@ref", "@formname", "@attributes",
    ];

    /// <summary>Completion at a position of a Razor file.</summary>
    private async Task<CompletionResult> GetRazorCompletionsAsync(string filePath, int position, CompletionTrigger trigger, CancellationToken cancellationToken)
    {
        if (await _host.GetRazorGeneratedDocumentAsync(filePath, cancellationToken).ConfigureAwait(false) is not { } razor)
        {
            return CompletionResult.Empty;
        }

        var (generated, text) = razor;
        if (await generated.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false) is not { } tree
            || await generated.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false) is not { } compilation)
        {
            return CompletionResult.Empty;
        }

        var caret = Math.Clamp(position, 0, text.Length);
        var context = RazorMarkupContext.At(text, caret);
        var isComponentFile = RazorProjects.IsComponent(filePath);
        switch (context.Kind)
        {
            case RazorMarkupKind.TagName when isComponentFile:
                return Markup(context, RazorComponents.InScope(compilation, tree)
                    .Select(c => new MarkupItem(c.Name, c.ContainingNamespace.ToDisplayString(), SymbolGlyph.Class)));

            case RazorMarkupKind.AttributeName:
                return Markup(context, Attributes(compilation, tree, context, isComponentFile));

            case RazorMarkupKind.Directive when IsAtLineStart(text, context.WordStart):
                return Markup(context, Directives(isComponentFile));
        }

        if (RazorSourceMap.ToGenerated(tree, filePath, text, caret, cancellationToken) is { } mapped)
        {
            return await CSharpInRazorAsync(generated, mapped, caret - mapped, trigger, cancellationToken).ConfigureAwait(false);
        }

        return context.Kind == RazorMarkupKind.Directive ? Markup(context, Directives(isComponentFile)) : CompletionResult.Empty;
    }

    /// <summary>Whether typing a character in a Razor file should open the list: in a tag, after @, or while typing a word.</summary>
    private bool ShouldTriggerRazorCompletion(string filePath, int position, char typedCharacter)
    {
        if (typedCharacter is '<' or '@' or '.')
        {
            return true;
        }

        if (char.IsLetter(typedCharacter) || typedCharacter == '_')
        {
            return true;
        }

        // A space in a component's tag offers its parameters.
        return typedCharacter == ' '
            && _host.TryGetRazorText(filePath) is { } text
            && RazorMarkupContext.At(text, position) is { Kind: RazorMarkupKind.AttributeName, TagName: { Length: > 0 } tag }
            && char.IsUpper(tag[0]);
    }

    /// <summary>
    /// The edit that commits an item in a Razor file: the item's text over the word being typed, as it is now.
    /// </summary>
    private async Task<CompletionCommit> GetRazorCompletionChangeAsync(string filePath, CompletionEntry entry, CancellationToken cancellationToken)
    {
        var text = await _host.GetRazorTextAsync(filePath, cancellationToken).ConfigureAwait(false);
        return Fallback(entry, text?.ToString());
    }

    /// <summary>C# completion in the generated C#, with its spans moved back into the Razor file.</summary>
    private async Task<CompletionResult> CSharpInRazorAsync(Document generated, int position, int offset, CompletionTrigger trigger, CancellationToken cancellationToken)
    {
        if (CompletionService.GetService(generated) is not { } service)
        {
            return CompletionResult.Empty;
        }

        var list = await service.GetCompletionsAsync(generated, position, ToRoslyn(trigger), cancellationToken: cancellationToken).ConfigureAwait(false);
        var items = list.ItemsList;
        if (items.Count == 0)
        {
            return CompletionResult.Empty;
        }

        var session = Remember(generated, items);
        var entries = new List<CompletionEntry>(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            var entry = ToEntry(items[i], new CompletionHandle(session, i));
            entries.Add(entry with { SpanStart = entry.SpanStart + offset });
        }

        entries.Sort(CompareEntries);
        return new CompletionResult(entries, list.Span.Start + offset, list.Span.Length, list.SuggestionModeItem is not null);
    }

    /// <summary>The attributes of the tag the caret is in: a component's parameters and directive attributes.</summary>
    private static IEnumerable<MarkupItem> Attributes(
        RoslynCompilation compilation, SyntaxTree generated, RazorMarkupContext context, bool isComponentFile)
    {
        if (!isComponentFile || context.TagName is not { Length: > 0 } tag)
        {
            return [];
        }

        if (RazorComponents.Find(compilation, generated, tag) is not { } component)
        {
            // An HTML element: Blazor's directive attributes, once the user starts typing one.
            return context.WordLength == 0 ? [] : ElementDirectiveAttributes.Select(a => new MarkupItem(a, "directive attribute", SymbolGlyph.Keyword));
        }

        var parameters = RazorComponents.Parameters(component);
        var names = parameters.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        return parameters
            .Select(p => new MarkupItem(p.Name, p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), SymbolGlyph.Property))
            .Concat(parameters
                .Where(p => !RazorComponents.IsEventCallback(p) && names.Contains(p.Name + "Changed"))
                .Select(p => new MarkupItem("@bind-" + p.Name, $"binds {p.Name} both ways", SymbolGlyph.Property)))
            .Concat(ComponentDirectiveAttributes.Select(a => new MarkupItem(a, "directive attribute", SymbolGlyph.Keyword)));
    }

    private static IEnumerable<MarkupItem> Directives(bool isComponentFile) =>
        (isComponentFile ? ComponentDirectives : ViewDirectives)
            .Select(d => new MarkupItem("@" + d, "directive", SymbolGlyph.Keyword))
            .Concat(ControlKeywords.Select(k => new MarkupItem("@" + k, "C# statement", SymbolGlyph.Keyword)));

    /// <summary>A list of markup items over the word at the caret.</summary>
    private static CompletionResult Markup(RazorMarkupContext context, IEnumerable<MarkupItem> items)
    {
        var entries = items
            .DistinctBy(i => i.Name, StringComparer.Ordinal)
            .OrderBy(i => i.Name.TrimStart('@'), StringComparer.OrdinalIgnoreCase)
            .Select((item, index) => new CompletionEntry(
                new CompletionHandle(MarkupSession, index),
                item.Name,
                string.Empty,
                string.Empty,
                item.Name,
                item.Name.TrimStart('@'),
                item.Description,
                item.Kind,
                IsPreselected: false,
                context.WordStart,
                context.WordLength))
            .ToList();
        return entries.Count == 0 ? CompletionResult.Empty : new CompletionResult(entries, context.WordStart, context.WordLength, IsSuggestionMode: false);
    }

    /// <summary>Whether only white space comes before a position on its line, where directives go.</summary>
    private static bool IsAtLineStart(SourceText text, int position)
    {
        var line = text.Lines.GetLineFromPosition(position);
        for (var i = line.Start; i < position; i++)
        {
            if (!char.IsWhiteSpace(text[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>An item of a markup completion list: its text, the note beside it and its icon.</summary>
    private readonly record struct MarkupItem(string Name, string Description, SymbolGlyph Kind);
}
