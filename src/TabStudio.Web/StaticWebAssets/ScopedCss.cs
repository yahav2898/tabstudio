using System.Text;
using TabStudio.Core.Diagnostics;

namespace TabStudio.Web.StaticWebAssets;

/// <summary>A problem in a scoped CSS file, such as an <c>@import</c>, which scoped CSS cannot have.</summary>
/// <param name="Position">The zero-based offset in the file.</param>
/// <param name="Line">The one-based line.</param>
/// <param name="Column">The one-based column.</param>
/// <param name="Message">What is wrong.</param>
public sealed record ScopedCssError(int Position, int Line, int Column, string Message);

/// <summary>A stylesheet with its scope applied, and its problems.</summary>
/// <param name="Css">The rewritten stylesheet.</param>
/// <param name="Errors">The problems; the SDK fails a build that has any.</param>
public sealed record ScopedCssResult(string Css, IReadOnlyList<ScopedCssError> Errors);

/// <summary>
/// Applies a CSS scope to a component's stylesheet the way the SDK's <c>RewriteCss</c> task does, so a
/// <c>Counter.razor.css</c> styles only the elements of <c>Counter.razor</c>, which the Razor compiler marks with the
/// same scope attribute.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>Every selector gets <c>[scope]</c> after its last compound selector, before a pseudo-element: <c>a::before</c> becomes <c>a[b-1234567890]::before</c>.</item>
///   <item><c>::deep</c> ends the scoped part of a selector and is removed: <c>.row ::deep a</c> becomes <c>.row[b-1234567890]  a</c>.</item>
///   <item>Keyframes the file declares are renamed with the scope, and so are the <c>animation</c> declarations that use them.</item>
///   <item>Rules inside <c>@media</c>, <c>@supports</c>, <c>@container</c> and <c>@layer</c> are scoped; other at-rules are left alone.</item>
///   <item><c>@import</c> is an error, because the loading order of scoped files is undefined.</item>
/// </list>
/// </remarks>
public static class ScopedCss
{
    /// <summary>The message the SDK gives for an <c>@import</c> in a scoped CSS file.</summary>
    public const string ImportNotAllowed =
        "@import rules are not supported within scoped CSS files because the loading order would be undefined. @import may only be placed in non-scoped CSS files.";

    private const string DeepCombinator = "::deep";

    private static readonly HashSet<string> NestingAtRules = new(StringComparer.OrdinalIgnoreCase)
    {
        "media", "supports", "container", "layer", "document", "-moz-document", "starting-style",
    };

    private static readonly HashSet<string> LegacyPseudoElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "after", "before", "first-letter", "first-line",
    };

    /// <summary>Applies a scope to a stylesheet.</summary>
    /// <param name="css">The stylesheet.</param>
    /// <param name="scope">The scope, such as <c>b-nz1yiwl0um</c>.</param>
    public static ScopedCssResult Rewrite(string css, string scope)
    {
        ArgumentNullException.ThrowIfNull(css);
        ArgumentException.ThrowIfNullOrEmpty(scope);
        var rewriter = new Rewriter(css, scope);
        return rewriter.Run();
    }

    /// <summary>One pass over a stylesheet, collecting the edits that apply the scope.</summary>
    private sealed class Rewriter(string css, string scope)
    {
        private readonly List<(int Position, int Order, int DeleteLength, string Insert)> _edits = [];
        private readonly List<ScopedCssError> _errors = [];
        private HashSet<string> _keyframes = [];

        public ScopedCssResult Run()
        {
            _keyframes = FindKeyframeNames();
            Rules(0, css.Length);

            var result = new StringBuilder(css.Length + (_edits.Count * (scope.Length + 2)));
            var copied = 0;
            foreach (var edit in _edits.OrderBy(e => e.Position).ThenBy(e => e.Order))
            {
                if (edit.Position < copied)
                {
                    continue;
                }

                result.Append(css, copied, edit.Position - copied).Append(edit.Insert);
                copied = edit.Position + edit.DeleteLength;
            }

            result.Append(css, copied, css.Length - copied);
            return new ScopedCssResult(result.ToString(), _errors);
        }

        /// <summary>The rules of a stylesheet or of an at-rule's block.</summary>
        private void Rules(int start, int end)
        {
            var i = start;
            while (true)
            {
                i = SkipBlank(i, end);
                if (i >= end)
                {
                    return;
                }

                if (css[i] == '}')
                {
                    i++;
                    continue;
                }

                if (css[i] == '@')
                {
                    i = AtRule(i, end);
                    continue;
                }

                var stop = FindTopLevel(i, end, "{;}");
                if (stop < 0)
                {
                    return;
                }

                if (css[stop] != '{')
                {
                    i = stop + 1;
                    continue;
                }

                SelectorList(i, stop);
                var close = MatchingBrace(stop, end);
                Declarations(stop + 1, close);
                i = close + 1;
            }
        }

        private int AtRule(int at, int end)
        {
            var nameEnd = IdentifierEnd(at + 1, end);
            var name = css[(at + 1)..nameEnd];
            var stop = FindTopLevel(nameEnd, end, "{;");
            if (string.Equals(name, "import", StringComparison.OrdinalIgnoreCase))
            {
                var (line, column) = LineAndColumn(at);
                _errors.Add(new ScopedCssError(at, line, column, ImportNotAllowed));
            }

            if (stop < 0)
            {
                return end;
            }

            if (css[stop] == ';')
            {
                return stop + 1;
            }

            var close = MatchingBrace(stop, end);
            if (IsKeyframes(name))
            {
                var identifierStart = SkipBlank(nameEnd, stop);
                var identifierEnd = IdentifierEnd(identifierStart, stop);
                if (identifierEnd > identifierStart)
                {
                    Insert(identifierEnd, "-" + scope);
                }
            }
            else if (NestingAtRules.Contains(name))
            {
                Rules(stop + 1, close);
            }

            return close + 1;
        }

        /// <summary>A rule's selectors, separated by commas outside parentheses and brackets.</summary>
        private void SelectorList(int start, int end)
        {
            var selectorStart = start;
            while (selectorStart < end)
            {
                var comma = FindTopLevel(selectorStart, end, ",");
                var selectorEnd = comma < 0 ? end : comma;
                Selector(selectorStart, selectorEnd);
                selectorStart = selectorEnd + 1;
            }
        }

        /// <summary>Puts the scope after a selector's last compound selector, or before its <c>::deep</c>.</summary>
        private void Selector(int start, int end)
        {
            var compounds = new List<(int Start, int End, int InsertAt)>();
            var i = start;
            while (i < end)
            {
                i = SkipBlank(i, end);
                if (i >= end)
                {
                    break;
                }

                if (css[i] is '>' or '+' or '~')
                {
                    i++;
                    continue;
                }

                var compoundStart = i;
                var insertAt = -1;
                while (i < end && !char.IsWhiteSpace(css[i]) && css[i] is not ('>' or '+' or '~') && !IsCommentStart(i))
                {
                    switch (css[i])
                    {
                        case '[':
                            i = Closing(i, end, '[', ']') + 1;
                            break;
                        case '(':
                            i = Closing(i, end, '(', ')') + 1;
                            break;
                        case '"' or '\'':
                            i = StringEnd(i, end);
                            break;
                        case '\\':
                            i = Math.Min(i + 2, end);
                            break;
                        case ':':
                            var pseudoStart = i;
                            var isElement = i + 1 < end && css[i + 1] == ':';
                            i += isElement ? 2 : 1;
                            var nameEnd = IdentifierEnd(i, end);
                            if (insertAt < 0 && (isElement || LegacyPseudoElements.Contains(css[i..nameEnd])))
                            {
                                insertAt = pseudoStart;
                            }

                            i = nameEnd;
                            if (i < end && css[i] == '(')
                            {
                                i = Closing(i, end, '(', ')') + 1;
                            }

                            break;
                        default:
                            i++;
                            break;
                    }
                }

                compounds.Add((compoundStart, Math.Min(i, end), insertAt));
            }

            var deep = compounds.FindIndex(c => string.CompareOrdinal(css, c.Start, DeepCombinator, 0, DeepCombinator.Length) == 0);
            var scoped = deep < 0 ? compounds.Count - 1 : deep - 1;
            if (scoped >= 0)
            {
                var compound = compounds[scoped];
                Insert(compound.InsertAt >= 0 ? compound.InsertAt : compound.End, $"[{scope}]");
            }
            else if (deep >= 0)
            {
                // A leading ::deep would leave the rule unscoped, so the scope goes where it was.
                Insert(compounds[deep].Start, $"[{scope}]");
            }

            if (deep >= 0)
            {
                _edits.Add((compounds[deep].Start, _edits.Count, DeepCombinator.Length, string.Empty));
            }
        }

        /// <summary>A rule's declarations: the names of this file's keyframes in <c>animation</c> get the scope.</summary>
        private void Declarations(int start, int end)
        {
            var i = start;
            while (true)
            {
                i = SkipBlank(i, end);
                if (i >= end)
                {
                    return;
                }

                var stop = FindTopLevel(i, end, ":;{");
                if (stop < 0)
                {
                    return;
                }

                if (css[stop] == '{')
                {
                    // A nested rule, such as &[open] { ... }: like the SDK, its selector is left alone, but the
                    // animations it declares still use the file's scoped keyframes.
                    var close = MatchingBrace(stop, end);
                    Declarations(stop + 1, close);
                    i = close + 1;
                    continue;
                }

                if (css[stop] == ';')
                {
                    i = stop + 1;
                    continue;
                }

                var property = css[i..stop].Trim();
                var valueEnd = FindTopLevel(stop + 1, end, ";");
                valueEnd = valueEnd < 0 ? end : valueEnd;
                if (_keyframes.Count > 0
                    && (string.Equals(property, "animation", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(property, "animation-name", StringComparison.OrdinalIgnoreCase)))
                {
                    AnimationNames(stop + 1, valueEnd);
                }

                i = valueEnd + 1;
            }
        }

        private void AnimationNames(int start, int end)
        {
            var i = start;
            while (i < end)
            {
                if (css[i] == '(')
                {
                    i = Closing(i, end, '(', ')') + 1;
                }
                else if (css[i] is '"' or '\'')
                {
                    i = StringEnd(i, end);
                }
                else if (IsIdentifierChar(css[i]))
                {
                    var identifierEnd = IdentifierEnd(i, end);
                    if (identifierEnd < end && css[identifierEnd] == '(')
                    {
                        // A function, such as cubic-bezier(...): not a name.
                        i = Closing(identifierEnd, end, '(', ')') + 1;
                        continue;
                    }

                    if (_keyframes.Contains(css[i..identifierEnd]))
                    {
                        Insert(identifierEnd, "-" + scope);
                    }

                    i = identifierEnd;
                }
                else
                {
                    i++;
                }
            }
        }

        /// <summary>The names of the keyframes declared anywhere in the file, which are case-sensitive.</summary>
        private HashSet<string> FindKeyframeNames()
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            var i = 0;
            while (i < css.Length)
            {
                if (IsCommentStart(i))
                {
                    i = CommentEnd(i, css.Length);
                }
                else if (css[i] is '"' or '\'')
                {
                    i = StringEnd(i, css.Length);
                }
                else if (css[i] == '@')
                {
                    var nameEnd = IdentifierEnd(i + 1, css.Length);
                    if (IsKeyframes(css[(i + 1)..nameEnd]))
                    {
                        var identifierStart = SkipBlank(nameEnd, css.Length);
                        var identifierEnd = IdentifierEnd(identifierStart, css.Length);
                        if (identifierEnd > identifierStart)
                        {
                            names.Add(css[identifierStart..identifierEnd]);
                        }
                    }

                    i = nameEnd;
                }
                else
                {
                    i++;
                }
            }

            return names;
        }

        private void Insert(int position, string text) => _edits.Add((position, _edits.Count, 0, text));

        private static bool IsKeyframes(string atRuleName) => atRuleName.EndsWith("keyframes", StringComparison.OrdinalIgnoreCase);

        /// <summary>Skips white space and comments.</summary>
        private int SkipBlank(int i, int end)
        {
            while (i < end)
            {
                if (char.IsWhiteSpace(css[i]))
                {
                    i++;
                }
                else if (IsCommentStart(i))
                {
                    i = CommentEnd(i, end);
                }
                else
                {
                    break;
                }
            }

            return i;
        }

        /// <summary>The first of some characters outside strings, comments, parentheses and brackets, or -1.</summary>
        private int FindTopLevel(int i, int end, string stops)
        {
            while (i < end)
            {
                var c = css[i];
                if (IsCommentStart(i))
                {
                    i = CommentEnd(i, end);
                }
                else if (c is '"' or '\'')
                {
                    i = StringEnd(i, end);
                }
                else if (c == '(')
                {
                    i = Closing(i, end, '(', ')') + 1;
                }
                else if (c == '[')
                {
                    i = Closing(i, end, '[', ']') + 1;
                }
                else if (c == '\\')
                {
                    i += 2;
                }
                else if (stops.Contains(c, StringComparison.Ordinal))
                {
                    return i;
                }
                else
                {
                    i++;
                }
            }

            return -1;
        }

        /// <summary>The <c>}</c> that closes the block opened at a <c>{</c>, or the end.</summary>
        private int MatchingBrace(int open, int end) => Closing(open, end, '{', '}');

        /// <summary>The character that closes a group, skipping strings and comments, or the end when it never closes.</summary>
        private int Closing(int open, int end, char opening, char closing)
        {
            var depth = 0;
            var i = open;
            while (i < end)
            {
                var c = css[i];
                if (IsCommentStart(i))
                {
                    i = CommentEnd(i, end);
                    continue;
                }

                if (c is '"' or '\'')
                {
                    i = StringEnd(i, end);
                    continue;
                }

                if (c == '\\')
                {
                    i += 2;
                    continue;
                }

                if (c == opening)
                {
                    depth++;
                }
                else if (c == closing && --depth == 0)
                {
                    return i;
                }

                i++;
            }

            return end;
        }

        private bool IsCommentStart(int i) => i + 1 < css.Length && css[i] == '/' && css[i + 1] == '*';

        private int CommentEnd(int i, int end)
        {
            var close = css.IndexOf("*/", i + 2, StringComparison.Ordinal);
            return close < 0 || close + 2 > end ? end : close + 2;
        }

        /// <summary>Just past the quote that closes a string, or the end of the line or text when it is not closed.</summary>
        private int StringEnd(int i, int end)
        {
            var quote = css[i];
            i++;
            while (i < end && css[i] != quote && css[i] != '\n')
            {
                i += css[i] == '\\' ? 2 : 1;
            }

            return Math.Min(i + 1, end);
        }

        private int IdentifierEnd(int i, int end)
        {
            while (i < end && (IsIdentifierChar(css[i]) || css[i] == '\\'))
            {
                i += css[i] == '\\' ? 2 : 1;
            }

            return Math.Min(i, end);
        }

        private static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c is '-' or '_' || c > 127;

        private (int Line, int Column) LineAndColumn(int position)
        {
            var line = 1;
            var lineStart = 0;
            for (var i = 0; i < position; i++)
            {
                if (css[i] == '\n')
                {
                    line++;
                    lineStart = i + 1;
                }
            }

            return (line, position - lineStart + 1);
        }
    }

    /// <summary>An error as an entry of the Error List, on the scoped CSS file.</summary>
    /// <param name="filePath">The scoped CSS file.</param>
    /// <param name="error">The error.</param>
    /// <param name="projectName">The project the file belongs to.</param>
    public static BuildDiagnostic ToDiagnostic(string filePath, ScopedCssError error, string? projectName)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(error);
        return new BuildDiagnostic(
            WebDiagnosticIds.ScopedCss,
            DiagnosticSeverityLevel.Error,
            error.Message,
            new DiagnosticLocation(filePath, error.Position, 1, error.Line, error.Column),
            projectName);
    }
}
