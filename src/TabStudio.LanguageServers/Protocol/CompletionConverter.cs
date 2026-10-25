using System.Text.Json;
using System.Text.Json.Nodes;
using TabStudio.LanguageServers.Documents;
using TabStudio.LanguageServers.Features;
using TabStudio.LanguageServers.Text;

namespace TabStudio.LanguageServers.Protocol;

/// <summary>
/// Reads completion results: a list or an array, item defaults applied, edits turned into offsets, snippets
/// expanded with the caret and selection on their first placeholder.
/// </summary>
internal static class CompletionConverter
{
    /// <summary>The items of a <c>CompletionItem[] | CompletionList | null</c> result.</summary>
    /// <param name="result">The result.</param>
    /// <param name="snapshot">The text the request was about.</param>
    /// <param name="offset">The caret of the request.</param>
    /// <param name="serverCommitCharacters">The characters that commit every item of this server.</param>
    public static LspCompletionList? ToList(JsonElement result, TextSnapshot snapshot, int offset, IReadOnlyList<char> serverCommitCharacters)
    {
        JsonElement items;
        CompletionItemDefaultsDto? defaults = null;
        var isIncomplete = false;
        switch (result.ValueKind)
        {
            case JsonValueKind.Array:
                items = result;
                break;
            case JsonValueKind.Object:
                var list = result.Deserialize(LspJsonContext.Default.CompletionListDto);
                if (list is null || list.Items.ValueKind != JsonValueKind.Array)
                {
                    return null;
                }

                items = list.Items;
                defaults = list.ItemDefaults;
                isIncomplete = list.IsIncomplete;
                break;
            default:
                return null;
        }

        var word = WordBefore(snapshot.Text, offset);
        var converted = new List<LspCompletionItem>();
        foreach (var item in items.EnumerateArray())
        {
            try
            {
                if (ToItem(item, defaults, serverCommitCharacters, snapshot, word) is { } entry)
                {
                    converted.Add(entry);
                }
            }
            catch (JsonException)
            {
                // An item the server described wrongly is left out.
            }
        }

        return new LspCompletionList(converted, isIncomplete);
    }

    /// <summary>An item with what <c>completionItem/resolve</c> added: documentation, detail and extra edits.</summary>
    /// <param name="item">The item as it was listed.</param>
    /// <param name="resolved">The resolved item's JSON.</param>
    /// <param name="snapshot">The current text, for the extra edits.</param>
    public static LspCompletionItem WithResolved(LspCompletionItem item, JsonElement resolved, TextSnapshot snapshot)
    {
        if (resolved.ValueKind != JsonValueKind.Object || resolved.Deserialize(LspJsonContext.Default.CompletionItemDto) is not { } dto)
        {
            return item;
        }

        return new LspCompletionItem(resolved, item.SnapshotVersion)
        {
            Label = item.Label,
            LabelDetail = dto.LabelDetails?.Detail ?? item.LabelDetail,
            LabelDescription = dto.LabelDetails?.Description ?? item.LabelDescription,
            Detail = dto.Detail ?? item.Detail,
            Kind = item.Kind,
            Documentation = LspConvert.ToMarkup(dto.Documentation, stringIsMarkdown: false) ?? item.Documentation,
            FilterText = item.FilterText,
            SortText = item.SortText,
            IsPreselected = item.IsPreselected,
            IsDeprecated = item.IsDeprecated,
            CommitCharacters = item.CommitCharacters,
            Edit = item.Edit,
            AdditionalEdits = dto.AdditionalTextEdits is { Count: > 0 } extra ? LspConvert.ToTextEdits(extra, snapshot) : item.AdditionalEdits,
            HasCommand = item.HasCommand || dto.Command is not null,
        };
    }

    /// <summary>The identifier being typed before an offset: letters, digits, <c>_</c> and <c>$</c>.</summary>
    public static TextSpan WordBefore(string text, int offset)
    {
        offset = Math.Clamp(offset, 0, text.Length);
        var start = offset;
        while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] is '_' or '$'))
        {
            start--;
        }

        return TextSpan.FromBounds(start, offset);
    }

    private static LspCompletionItem? ToItem(JsonElement raw, CompletionItemDefaultsDto? defaults, IReadOnlyList<char> serverCommitCharacters, TextSnapshot snapshot, TextSpan word)
    {
        if (raw.ValueKind != JsonValueKind.Object || raw.Deserialize(LspJsonContext.Default.CompletionItemDto) is not { Label: { } label } dto)
        {
            return null;
        }

        var format = dto.InsertTextFormat ?? defaults?.InsertTextFormat ?? LspValues.InsertTextFormat.PlainText;
        var (span, newText) = ReadEdit(dto, defaults, snapshot, word);
        var isSnippet = format == LspValues.InsertTextFormat.Snippet;
        var caret = newText.Length;
        var selection = 0;
        if (isSnippet)
        {
            var expansion = SnippetParser.Expand(newText);
            newText = expansion.Text;
            caret = expansion.FirstStop.Start;
            selection = expansion.FirstStop.Length;
        }

        var commitCharacters = (dto.CommitCharacters ?? defaults?.CommitCharacters)?
            .Where(c => c.Length == 1)
            .Select(c => c[0])
            .ToList()
            ?? [.. serverCommitCharacters];

        return new LspCompletionItem(WithDefaultData(raw, defaults), snapshot.Version)
        {
            Label = label,
            LabelDetail = dto.LabelDetails?.Detail,
            LabelDescription = dto.LabelDetails?.Description,
            Detail = dto.Detail,
            Kind = dto.Kind is { } kind && Enum.IsDefined((LspCompletionItemKind)kind) ? (LspCompletionItemKind)kind : LspCompletionItemKind.Unknown,
            Documentation = LspConvert.ToMarkup(dto.Documentation, stringIsMarkdown: false),
            FilterText = dto.FilterText ?? label,
            SortText = dto.SortText ?? label,
            IsPreselected = dto.Preselect == true,
            IsDeprecated = dto.Deprecated == true || (dto.Tags?.Contains(LspValues.ItemTag.Deprecated) ?? false),
            CommitCharacters = commitCharacters,
            Edit = new LspCompletionEdit(span, newText, caret, selection, isSnippet),
            AdditionalEdits = dto.AdditionalTextEdits is { Count: > 0 } extra ? LspConvert.ToTextEdits(extra, snapshot) : [],
            HasCommand = dto.Command is not null,
        };
    }

    /// <summary>The range an item replaces and its text: its own edit, the list's default range, or the typed word.</summary>
    private static (TextSpan Span, string Text) ReadEdit(CompletionItemDto item, CompletionItemDefaultsDto? defaults, TextSnapshot snapshot, TextSpan word)
    {
        if (item.TextEdit is { ValueKind: JsonValueKind.Object } edit)
        {
            if (edit.TryGetProperty("range", out _) && edit.Deserialize(LspJsonContext.Default.TextEditDto) is { Range: { } range } plain)
            {
                return (LspConvert.ToSpan(snapshot, range), plain.NewText ?? string.Empty);
            }

            if (edit.Deserialize(LspJsonContext.Default.InsertReplaceEditDto) is { Insert: { } insert } insertReplace)
            {
                return (LspConvert.ToSpan(snapshot, insert), insertReplace.NewText ?? string.Empty);
            }
        }

        var text = item.TextEditText ?? item.InsertText ?? item.Label;
        if (defaults?.EditRange is { ValueKind: JsonValueKind.Object } defaultRange)
        {
            var range = defaultRange.TryGetProperty("insert", out var insert)
                ? insert.Deserialize(LspJsonContext.Default.RangeDto)
                : defaultRange.Deserialize(LspJsonContext.Default.RangeDto);
            if (range is { Start: not null, End: not null })
            {
                return (LspConvert.ToSpan(snapshot, range), text);
            }
        }

        return (word, text);
    }

    /// <summary>The item's JSON with the list's default <c>data</c>, which the item needs when it is resolved.</summary>
    private static JsonElement WithDefaultData(JsonElement raw, CompletionItemDefaultsDto? defaults)
    {
        if (defaults?.Data is not { } data || raw.TryGetProperty("data", out _) || JsonObject.Create(raw) is not { } item)
        {
            return raw.Clone();
        }

        item["data"] = JsonNode.Parse(data.GetRawText());
        using var document = JsonDocument.Parse(item.ToJsonString());
        return document.RootElement.Clone();
    }
}
