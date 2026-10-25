using System.Globalization;
using System.Text;

namespace TabStudio.LanguageServers.Text;

/// <summary>A tab stop of an expanded snippet: a place, with the placeholder text the user types over.</summary>
/// <param name="Index">The tab stop's number; 0 is where the caret ends up.</param>
/// <param name="Start">Its offset in the expanded text.</param>
/// <param name="Length">The length of its placeholder text, 0 for a plain tab stop.</param>
public readonly record struct SnippetTabStop(int Index, int Start, int Length);

/// <summary>A snippet turned into text, with its tab stops.</summary>
/// <param name="Text">The text to insert.</param>
/// <param name="TabStops">The tab stops in the order Tab visits them: 1, 2, 3 and so on, then 0. Each index once.</param>
public sealed record SnippetExpansion(string Text, IReadOnlyList<SnippetTabStop> TabStops)
{
    /// <summary>Where the caret goes first: the first tab stop, the final one, or the end of the text.</summary>
    public SnippetTabStop FirstStop => TabStops.Count > 0 ? TabStops[0] : new SnippetTabStop(0, Text.Length, 0);
}

/// <summary>
/// Expands the snippet syntax of the Language Server Protocol and VS Code: tab stops (<c>$1</c>, <c>${2}</c>),
/// placeholders (<c>${1:name}</c>, nested), choices (<c>${1|a,b|}</c>, the first is used), variables
/// (<c>$TM_FILENAME</c>, <c>${NAME:default}</c>) and the escapes <c>\$</c>, <c>\}</c> and <c>\\</c>.
/// </summary>
/// <remarks>
/// A tab stop that appears more than once mirrors the text of its first placeholder. Variables take their value
/// from the resolver, or their default; transformations (<c>${NAME/regex/format/}</c>) insert the plain value.
/// Text that is not valid snippet syntax is inserted as it is.
/// </remarks>
public static class SnippetParser
{
    /// <summary>Expands a snippet.</summary>
    /// <param name="snippet">The snippet text.</param>
    /// <param name="resolveVariable">Gives the value of a variable such as <c>TM_FILENAME</c>, or <see langword="null"/> when it has none.</param>
    public static SnippetExpansion Expand(string snippet, Func<string, string?>? resolveVariable = null)
    {
        ArgumentNullException.ThrowIfNull(snippet);
        var parser = new Parser(snippet, resolveVariable ?? (_ => null));
        parser.ParseUntil(closing: null);
        return parser.ToExpansion();
    }

    /// <summary>Whether text contains snippet syntax, so inserting it as plain text would differ.</summary>
    /// <param name="text">The text.</param>
    public static bool HasSyntax(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Contains('$', StringComparison.Ordinal) || text.Contains('\\', StringComparison.Ordinal);
    }

    private sealed class Parser(string source, Func<string, string?> resolveVariable)
    {
        private readonly StringBuilder _text = new();
        private readonly Dictionary<int, SnippetTabStop> _stops = [];
        private readonly Dictionary<int, string> _values = [];
        private int _position;

        public void ParseUntil(char? closing, bool inChoice = false)
        {
            while (_position < source.Length)
            {
                var character = source[_position];
                if (closing is { } close && character == close)
                {
                    return;
                }

                switch (character)
                {
                    case '\\' when _position + 1 < source.Length && IsEscapable(source[_position + 1], inChoice):
                        _text.Append(source[_position + 1]);
                        _position += 2;
                        break;
                    case '$' when TryParseDollar():
                        break;
                    default:
                        _text.Append(character);
                        _position++;
                        break;
                }
            }
        }

        public SnippetExpansion ToExpansion()
        {
            var ordered = _stops.Values
                .OrderBy(stop => stop.Index == 0 ? int.MaxValue : stop.Index)
                .ToList();
            return new SnippetExpansion(_text.ToString(), ordered);
        }

        private static bool IsEscapable(char character, bool inChoice) =>
            character is '$' or '}' or '\\' || (inChoice && character is ',' or '|');

        /// <summary>Parses what starts at a <c>$</c>; leaves everything as it was when it is not snippet syntax.</summary>
        private bool TryParseDollar()
        {
            var start = _position;
            var textLength = _text.Length;
            _position++;
            if (_position >= source.Length)
            {
                return Fail(start, textLength);
            }

            if (char.IsAsciiDigit(source[_position]))
            {
                AddTabStop(ReadNumber(), _text.Length, 0, mirror: true);
                return true;
            }

            if (IsVariableStart(source[_position]))
            {
                AppendVariable(ReadName(), null);
                return true;
            }

            if (source[_position] != '{')
            {
                return Fail(start, textLength);
            }

            _position++;
            if (_position < source.Length && char.IsAsciiDigit(source[_position]))
            {
                return ParseBracedTabStop(start, textLength);
            }

            if (_position < source.Length && IsVariableStart(source[_position]))
            {
                return ParseBracedVariable(start, textLength);
            }

            return Fail(start, textLength);
        }

        private bool ParseBracedTabStop(int start, int textLength)
        {
            var index = ReadNumber();
            if (_position >= source.Length)
            {
                return Fail(start, textLength);
            }

            switch (source[_position])
            {
                case '}':
                    _position++;
                    AddTabStop(index, _text.Length, 0, mirror: true);
                    return true;
                case ':':
                {
                    _position++;
                    var placeholderStart = _text.Length;
                    ParseUntil('}');
                    if (_position >= source.Length)
                    {
                        return Fail(start, textLength);
                    }

                    _position++;
                    AddTabStop(index, placeholderStart, _text.Length - placeholderStart, mirror: false);
                    return true;
                }

                case '|':
                {
                    _position++;
                    var choiceStart = _text.Length;
                    var choices = new StringBuilder();
                    var first = true;
                    while (_position < source.Length && !(source[_position] == '|' && _position + 1 < source.Length && source[_position + 1] == '}'))
                    {
                        var character = source[_position];
                        if (character == '\\' && _position + 1 < source.Length && IsEscapable(source[_position + 1], inChoice: true))
                        {
                            if (first)
                            {
                                choices.Append(source[_position + 1]);
                            }

                            _position += 2;
                            continue;
                        }

                        if (character == ',')
                        {
                            first = false;
                        }
                        else if (first)
                        {
                            choices.Append(character);
                        }

                        _position++;
                    }

                    if (_position >= source.Length)
                    {
                        return Fail(start, textLength);
                    }

                    _position += 2;
                    _text.Append(choices);
                    AddTabStop(index, choiceStart, _text.Length - choiceStart, mirror: false);
                    return true;
                }

                default:
                    return Fail(start, textLength);
            }
        }

        private bool ParseBracedVariable(int start, int textLength)
        {
            var name = ReadName();
            if (_position >= source.Length)
            {
                return Fail(start, textLength);
            }

            switch (source[_position])
            {
                case '}':
                    _position++;
                    AppendVariable(name, null);
                    return true;
                case ':':
                {
                    _position++;
                    var defaultStart = _text.Length;
                    ParseUntil('}');
                    if (_position >= source.Length)
                    {
                        return Fail(start, textLength);
                    }

                    _position++;
                    var fallback = _text.ToString(defaultStart, _text.Length - defaultStart);
                    _text.Length = defaultStart;
                    AppendVariable(name, fallback);
                    return true;
                }

                case '/':
                    // A transformation: skip the regular expression, format and options up to the closing brace.
                    while (_position < source.Length && source[_position] != '}')
                    {
                        _position += source[_position] == '\\' ? 2 : 1;
                    }

                    if (_position >= source.Length)
                    {
                        return Fail(start, textLength);
                    }

                    _position++;
                    AppendVariable(name, null);
                    return true;
                default:
                    return Fail(start, textLength);
            }
        }

        private void AppendVariable(string name, string? fallback) =>
            _text.Append(resolveVariable(name) ?? fallback ?? string.Empty);

        /// <summary>Records a tab stop; a later plain occurrence of the same index repeats the first one's text.</summary>
        private void AddTabStop(int index, int start, int length, bool mirror)
        {
            if (_stops.ContainsKey(index))
            {
                if (mirror && _values.TryGetValue(index, out var value))
                {
                    _text.Append(value);
                }

                return;
            }

            _stops[index] = new SnippetTabStop(index, start, length);
            _values[index] = _text.ToString(start, length);
        }

        private int ReadNumber()
        {
            var start = _position;
            while (_position < source.Length && char.IsAsciiDigit(source[_position]))
            {
                _position++;
            }

            return int.TryParse(source.AsSpan(start, _position - start), NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0;
        }

        private string ReadName()
        {
            var start = _position;
            while (_position < source.Length && (char.IsAsciiLetterOrDigit(source[_position]) || source[_position] == '_'))
            {
                _position++;
            }

            return source[start.._position];
        }

        private static bool IsVariableStart(char character) => char.IsAsciiLetter(character) || character == '_';

        /// <summary>Gives up on syntax that is not valid: the <c>$</c> becomes text, and parsing goes on after it.</summary>
        private bool Fail(int start, int textLength)
        {
            _text.Length = textLength;
            _position = start + 1;
            _text.Append('$');
            return true;
        }
    }
}
