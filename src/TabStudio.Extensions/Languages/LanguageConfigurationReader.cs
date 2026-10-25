using System.Text.Json;
using System.Text.RegularExpressions;
using TabStudio.Extensions.Json;

namespace TabStudio.Extensions.Languages;

/// <summary>
/// Reads a language configuration file (<c>language-configuration.json</c>) as VS Code does: patterns may be strings
/// or <c>{ "pattern", "flags" }</c> objects, pairs may be arrays or objects, and parts that are missing get VS Code's
/// defaults (auto-closing pairs default to the brackets, surrounding pairs to the auto-closing pairs).
/// </summary>
public static class LanguageConfigurationReader
{
    /// <summary>Reads a language configuration. Rules that cannot be read are left out rather than failing the file.</summary>
    /// <param name="json">The file's text.</param>
    /// <exception cref="JsonException">The text is not JSON.</exception>
    public static LanguageConfiguration Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var document = Jsonc.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return LanguageConfiguration.Empty;
        }

        var comments = root.GetObject("comments");
        var brackets = Pairs(root.GetArray("brackets")).ToList();
        var autoClosing = root.GetElement("autoClosingPairs") is { ValueKind: JsonValueKind.Array } closing
            ? closing.EnumerateArray().Select(AutoClosing).OfType<AutoClosingPair>().ToList()
            : [.. brackets.Select(b => new AutoClosingPair(b.Open, b.Close))];
        var surrounding = root.GetElement("surroundingPairs") is { ValueKind: JsonValueKind.Array } surroundingPairs
            ? Pairs(surroundingPairs.EnumerateArray()).ToList()
            : [.. autoClosing.Select(p => new BracketPair(p.Open, p.Close))];

        return new LanguageConfiguration
        {
            LineComment = LineComment(comments?.GetElement("lineComment")),
            BlockComment = comments?.GetElement("blockComment") is { } block ? Pair(block) : null,
            Brackets = brackets,
            AutoClosingPairs = autoClosing,
            SurroundingPairs = surrounding,
            AutoCloseBefore = root.GetString("autoCloseBefore") ?? LanguageConfiguration.DefaultAutoCloseBefore,
            Folding = Folding(root.GetObject("folding")),
            WordPattern = Pattern(root.GetElement("wordPattern")),
            Indentation = Indentation(root.GetObject("indentationRules")),
            OnEnterRules = [.. root.GetArray("onEnterRules").Select(OnEnter).OfType<OnEnterRule>()],
        };
    }

    /// <summary>A pattern written as a string or as <c>{ "pattern": "...", "flags": "i" }</c>.</summary>
    internal static Regex? Pattern(JsonElement? element) => element switch
    {
        { ValueKind: JsonValueKind.String } text => JsRegex.TryCreate(text.GetString()),
        { ValueKind: JsonValueKind.Object } value => JsRegex.TryCreate(value.GetString("pattern"), value.GetString("flags")),
        _ => null,
    };

    /// <summary>The line comment, written as a string or, in newer files, as <c>{ "comment": "//" }</c>.</summary>
    private static string? LineComment(JsonElement? element) => element switch
    {
        { ValueKind: JsonValueKind.String } text when text.GetString() is { Length: > 0 } comment => comment,
        { ValueKind: JsonValueKind.Object } value when value.GetString("comment") is { Length: > 0 } comment => comment,
        _ => null,
    };

    private static IEnumerable<BracketPair> Pairs(IEnumerable<JsonElement> elements) =>
        elements.Select(Pair).OfType<BracketPair>();

    /// <summary>A pair written as <c>["{", "}"]</c> or <c>{ "open": "{", "close": "}" }</c>.</summary>
    private static BracketPair? Pair(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array && element.GetArrayLength() == 2
            && element[0].ValueKind == JsonValueKind.String && element[1].ValueKind == JsonValueKind.String)
        {
            var open = element[0].GetString()!;
            return open.Length > 0 ? new BracketPair(open, element[1].GetString()!) : null;
        }

        return element.GetString("open") is { Length: > 0 } opening && element.GetString("close") is { } closing
            ? new BracketPair(opening, closing)
            : null;
    }

    private static AutoClosingPair? AutoClosing(JsonElement element)
    {
        if (Pair(element) is not { } pair)
        {
            return null;
        }

        var notIn = TokenContexts.None;
        foreach (var context in element.ValueKind == JsonValueKind.Object ? element.GetStrings("notIn") : [])
        {
            notIn |= context switch
            {
                "string" => TokenContexts.String,
                "comment" => TokenContexts.Comment,
                "regex" => TokenContexts.RegularExpression,
                _ => TokenContexts.None,
            };
        }

        return new AutoClosingPair(pair.Open, pair.Close, notIn);
    }

    private static FoldingRules? Folding(JsonElement? folding)
    {
        if (folding is not { } rules)
        {
            return null;
        }

        var markers = rules.GetObject("markers");
        return new FoldingRules(
            rules.GetBoolean("offSide") ?? false,
            Pattern(markers?.GetElement("start")),
            Pattern(markers?.GetElement("end")));
    }

    private static IndentationRules? Indentation(JsonElement? rules) =>
        rules is { } value
            ? new IndentationRules(
                Pattern(value.GetElement("increaseIndentPattern")),
                Pattern(value.GetElement("decreaseIndentPattern")),
                Pattern(value.GetElement("indentNextLinePattern")),
                Pattern(value.GetElement("unIndentedLinePattern")))
            : null;

    private static OnEnterRule? OnEnter(JsonElement rule)
    {
        if (rule.ValueKind != JsonValueKind.Object || Pattern(rule.GetElement("beforeText")) is not { } before)
        {
            return null;
        }

        var action = rule.GetObject("action");
        var indent = (action?.GetString("indent") ?? action?.GetString("indentAction")) switch
        {
            "indent" => IndentAction.Indent,
            "indentOutdent" => IndentAction.IndentOutdent,
            "outdent" => IndentAction.Outdent,
            _ => IndentAction.None,
        };
        return new OnEnterRule(
            before,
            Pattern(rule.GetElement("afterText")),
            Pattern(rule.GetElement("previousLineText")),
            new EnterAction(indent, action?.GetString("appendText"), (int)(action?.GetInt64("removeText") ?? 0)));
    }
}
