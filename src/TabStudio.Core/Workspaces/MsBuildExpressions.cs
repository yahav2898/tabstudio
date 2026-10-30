using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace TabStudio.Core.Workspaces;

/// <summary>
/// Expands MSBuild property expressions and evaluates conditions as MSBuild does during evaluation: <c>$(Name)</c>,
/// property functions such as <c>$(TargetFramework.StartsWith('net4'))</c>,
/// <c>$([MSBuild]::IsTargetFrameworkCompatible('$(TargetFramework)', 'net8.0'))</c> or
/// <c>$([System.IO.Path]::Combine($(Root), 'src'))</c>, and conditions with <c>and</c>, <c>or</c>, <c>!</c>,
/// parentheses, <c>==</c>, <c>!=</c>, <c>&lt;</c>, <c>&gt;</c>, <c>&lt;=</c>, <c>&gt;=</c>, <c>Exists</c> and
/// <c>HasTrailingSlash</c>.
/// </summary>
/// <remarks>
/// As in MSBuild, a property nothing sets is empty, comparisons ignore case, numbers and versions compare by value,
/// and a property function's <see langword="bool"/> result is <c>True</c> or <c>False</c>. Functions that need the build
/// (registry, item transforms) or that this does not know make an expression one that cannot be evaluated:
/// <see cref="Expand"/> gives <see langword="null"/> and <see cref="Condition"/> gives <see langword="null"/> unless the
/// rest of the condition decides it, as <c>false and $([Unknown]::F())</c> is false.
/// </remarks>
internal static class MsBuildExpressions
{
    private const int MaxDepth = 16;

    /// <summary>Expands every <c>$(…)</c> in a text.</summary>
    /// <param name="text">The text, such as a property's value or an item's <c>Include</c>.</param>
    /// <param name="properties">The properties known at that point; keys are case-insensitive.</param>
    /// <param name="thisFile">The file the text is in, which <c>$(MSBuildThisFile…)</c> names; <see langword="null"/> for the project.</param>
    /// <param name="directory">The folder relative paths in functions such as <c>Exists</c> start from.</param>
    /// <returns>The expanded text; <see langword="null"/> when it uses a function that cannot be evaluated here.</returns>
    /// <param name="unknownProperties">
    /// Whether a property nothing sets expands to nothing, as in MSBuild (<see langword="true"/>), or makes the text one
    /// that cannot be expanded here (<see langword="false"/>), as for an item's path, where the property may be one the
    /// SDK's targets set, which TabStudio does not run, and <c>$(Unset)/**/*.cs</c> would name every file on the device.
    /// </param>
    public static string? Expand(string text, IReadOnlyDictionary<string, string> properties, string? thisFile, string directory, bool unknownProperties = true) =>
        new Context(properties, thisFile, directory) { UnknownIsEmpty = unknownProperties }.Expand(text, 0);

    /// <summary>Evaluates a condition.</summary>
    /// <param name="condition">The condition's text.</param>
    /// <param name="properties">The properties known at that point; keys are case-insensitive.</param>
    /// <param name="thisFile">The file the condition is in; <see langword="null"/> for the project.</param>
    /// <param name="directory">The folder relative paths in <c>Exists</c> start from.</param>
    /// <returns>The result; <see langword="null"/> when it cannot be evaluated here.</returns>
    public static bool? Condition(string condition, IReadOnlyDictionary<string, string> properties, string? thisFile, string directory)
    {
        if (string.IsNullOrWhiteSpace(condition))
        {
            return true;
        }

        var context = new Context(properties, thisFile, directory);
        try
        {
            var parser = new ConditionParser(condition, context);
            var result = parser.ParseExpression();
            return parser.AtEnd ? result.AsBoolean() : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>The value of a condition operand: a string, or one that cannot be evaluated.</summary>
    private readonly record struct Operand(string? Text, bool? Flag = null)
    {
        public static Operand Unknown => new(null);

        public bool? AsBoolean()
        {
            if (Flag is { } value)
            {
                return value;
            }

            return Text?.Trim().ToUpperInvariant() switch
            {
                "TRUE" or "ON" or "YES" or "!FALSE" or "!OFF" or "!NO" => true,
                "FALSE" or "OFF" or "NO" or "!TRUE" or "!ON" or "!YES" => false,
                _ => null,
            };
        }

        public string? AsText() => Text ?? (Flag is { } value ? (value ? "true" : "false") : null);
    }

    /// <summary>A recursive-descent parser for MSBuild conditions, which evaluates as it parses.</summary>
    private sealed class ConditionParser(string text, Context context)
    {
        private int _position;

        public bool AtEnd
        {
            get
            {
                SkipSpace();
                return _position >= text.Length;
            }
        }

        public Operand ParseExpression()
        {
            var left = ParseAnd();
            while (TryKeyword("or"))
            {
                var right = ParseAnd();
                left = (left.AsBoolean(), right.AsBoolean()) switch
                {
                    (true, _) or (_, true) => new Operand(null, true),
                    (false, false) => new Operand(null, false),
                    _ => Operand.Unknown,
                };
            }

            return left;
        }

        private Operand ParseAnd()
        {
            var left = ParseNot();
            while (TryKeyword("and"))
            {
                var right = ParseNot();
                left = (left.AsBoolean(), right.AsBoolean()) switch
                {
                    (false, _) or (_, false) => new Operand(null, false),
                    (true, true) => new Operand(null, true),
                    _ => Operand.Unknown,
                };
            }

            return left;
        }

        private Operand ParseNot()
        {
            SkipSpace();
            if (Peek() == '!' && Peek(1) != '=')
            {
                _position++;
                var inner = ParseNot();
                return inner.AsBoolean() is { } value ? new Operand(null, !value) : Operand.Unknown;
            }

            return ParseComparison();
        }

        private Operand ParseComparison()
        {
            var left = ParseFactor();
            SkipSpace();
            var op = Peek() switch
            {
                '=' when Peek(1) == '=' => "==",
                '!' when Peek(1) == '=' => "!=",
                '<' when Peek(1) == '=' => "<=",
                '>' when Peek(1) == '=' => ">=",
                '<' => "<",
                '>' => ">",
                _ => null,
            };
            if (op is null)
            {
                return left;
            }

            _position += op.Length;
            var right = ParseFactor();
            if (left.AsText() is not { } a || right.AsText() is not { } b)
            {
                return Operand.Unknown;
            }

            return op switch
            {
                "==" => new Operand(null, AreEqual(a, b)),
                "!=" => new Operand(null, !AreEqual(a, b)),
                _ => Compare(a, b) is { } order
                    ? new Operand(null, op switch { "<" => order < 0, ">" => order > 0, "<=" => order <= 0, _ => order >= 0 })
                    : Operand.Unknown,
            };
        }

        private Operand ParseFactor()
        {
            SkipSpace();
            var c = Peek();
            if (c == '(')
            {
                _position++;
                var inner = ParseExpression();
                SkipSpace();
                Expect(')');
                return inner;
            }

            if (c is '\'' or '"' or '`')
            {
                var quoted = ReadQuoted(c);
                return context.Expand(quoted, 0) is { } value ? new Operand(value) : Operand.Unknown;
            }

            if (c is '$' or '@' or '%')
            {
                var start = _position;
                SkipReference();
                var reference = text[start.._position];
                return context.Expand(reference, 0) is { } value && !value.Contains("@(", StringComparison.Ordinal) && !value.Contains("%(", StringComparison.Ordinal)
                    ? new Operand(value)
                    : Operand.Unknown;
            }

            // A function call (Exists, HasTrailingSlash) or a bare word or number.
            var wordStart = _position;
            while (_position < text.Length && (char.IsLetterOrDigit(text[_position]) || text[_position] is '.' or '_' or '-' or '+'))
            {
                _position++;
            }

            if (_position == wordStart)
            {
                throw new FormatException($"Unexpected '{c}' in a condition.");
            }

            var word = text[wordStart.._position];
            SkipSpace();
            if (Peek() == '(')
            {
                _position++;
                var argument = ParseFunctionArgument();
                SkipSpace();
                Expect(')');
                if (argument is null)
                {
                    return Operand.Unknown;
                }

                if (word.Equals("Exists", StringComparison.OrdinalIgnoreCase))
                {
                    return new Operand(null, context.Exists(argument));
                }

                if (word.Equals("HasTrailingSlash", StringComparison.OrdinalIgnoreCase))
                {
                    return new Operand(null, argument.EndsWith('/') || argument.EndsWith('\\'));
                }

                return Operand.Unknown;
            }

            return new Operand(word);
        }

        /// <summary>The argument of <c>Exists(...)</c>: a quoted string or a property reference, expanded.</summary>
        private string? ParseFunctionArgument()
        {
            SkipSpace();
            var c = Peek();
            string raw;
            if (c is '\'' or '"' or '`')
            {
                raw = ReadQuoted(c);
            }
            else
            {
                var start = _position;
                var depth = 0;
                while (_position < text.Length && (depth > 0 || text[_position] != ')'))
                {
                    if (text[_position] == '(')
                    {
                        depth++;
                    }
                    else if (text[_position] == ')')
                    {
                        depth--;
                    }

                    _position++;
                }

                raw = text[start.._position].Trim();
            }

            return context.Expand(raw, 0)?.Trim();
        }

        private void SkipReference()
        {
            // $( … ), @( … ) or %( … ), with nested parentheses and quotes.
            _position++;
            if (Peek() != '(')
            {
                throw new FormatException("A reference without parentheses.");
            }

            _position = Context.ClosingParenthesis(text, _position) is var close and >= 0 ? close + 1 : throw new FormatException("An unclosed reference.");
        }

        private string ReadQuoted(char quote)
        {
            var start = ++_position;
            while (_position < text.Length && text[_position] != quote)
            {
                if (text[_position] is '$' or '@' or '%' && _position + 1 < text.Length && text[_position + 1] == '(')
                {
                    var close = Context.ClosingParenthesis(text, _position + 1);
                    _position = close < 0 ? text.Length : close + 1;
                    continue;
                }

                _position++;
            }

            if (_position >= text.Length)
            {
                throw new FormatException("An unclosed string.");
            }

            return text[start.._position++];
        }

        private bool TryKeyword(string keyword)
        {
            SkipSpace();
            if (_position + keyword.Length <= text.Length
                && string.Compare(text, _position, keyword, 0, keyword.Length, StringComparison.OrdinalIgnoreCase) == 0
                && (_position + keyword.Length == text.Length || !char.IsLetterOrDigit(text[_position + keyword.Length])))
            {
                _position += keyword.Length;
                return true;
            }

            return false;
        }

        private void Expect(char c)
        {
            if (Peek() != c)
            {
                throw new FormatException($"Expected '{c}'.");
            }

            _position++;
        }

        private char Peek(int offset = 0) => _position + offset < text.Length ? text[_position + offset] : '\0';

        private void SkipSpace()
        {
            while (_position < text.Length && char.IsWhiteSpace(text[_position]))
            {
                _position++;
            }
        }

        private static bool AreEqual(string a, string b)
        {
            if (TryNumber(a, out var x) && TryNumber(b, out var y))
            {
                return x == y;
            }

            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static int? Compare(string a, string b)
        {
            if (TryNumber(a, out var x) && TryNumber(b, out var y))
            {
                return x.CompareTo(y);
            }

            return Version.TryParse(a.Trim(), out var v) && Version.TryParse(b.Trim(), out var w) ? v.CompareTo(w) : null;
        }

        private static bool TryNumber(string candidate, out double value)
        {
            var trimmed = candidate.Trim();
            if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                && long.TryParse(trimmed.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex))
            {
                value = hex;
                return true;
            }

            return double.TryParse(trimmed, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
        }
    }

    /// <summary>What an expansion sees: the properties, the file it is in and the folder paths start from.</summary>
    private sealed class Context(IReadOnlyDictionary<string, string> properties, string? thisFile, string directory)
    {
        public string Directory => directory;

        /// <summary>Whether a property nothing sets is empty; otherwise the expansion fails.</summary>
        public bool UnknownIsEmpty { get; init; } = true;

        private bool _missing;

        /// <summary>The folder of the file being evaluated, where <c>GetPathOfFileAbove</c> starts without a folder.</summary>
        public string ThisDirectory => thisFile is null ? directory : Path.GetDirectoryName(thisFile) ?? directory;

        public string? Expand(string text, int depth)
        {
            if (depth > MaxDepth)
            {
                return null;
            }

            var start = text.IndexOf("$(", StringComparison.Ordinal);
            if (start < 0)
            {
                return text;
            }

            var builder = new StringBuilder(text.Length);
            var position = 0;
            while (start >= 0)
            {
                builder.Append(text, position, start - position);
                var close = ClosingParenthesis(text, start + 1);
                if (close < 0)
                {
                    // An unclosed reference is text, as MSBuild leaves it.
                    builder.Append(text, start, text.Length - start);
                    return builder.ToString();
                }

                var value = EvaluateReference(text[(start + 2)..close], depth);
                if (value is null || _missing)
                {
                    return null;
                }

                builder.Append(value);
                position = close + 1;
                start = text.IndexOf("$(", position, StringComparison.Ordinal);
            }

            builder.Append(text, position, text.Length - position);
            return builder.ToString();
        }

        public bool Exists(string path)
        {
            if (path.Length == 0)
            {
                return false;
            }

            var full = FullPath(path);
            return File.Exists(full) || System.IO.Directory.Exists(full);
        }

        public string FullPath(string path)
        {
            var normalized = path.Replace('\\', Path.DirectorySeparatorChar);
            try
            {
                return Path.GetFullPath(Path.IsPathRooted(normalized) ? normalized : Path.Combine(directory, normalized));
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return normalized;
            }
        }

        /// <summary>The index of the parenthesis that closes the one at <paramref name="open"/>, outside quotes; -1 when there is none.</summary>
        public static int ClosingParenthesis(string text, int open)
        {
            var depth = 0;
            var quote = '\0';
            for (var i = open; i < text.Length; i++)
            {
                var c = text[i];
                if (quote != '\0')
                {
                    if (c == quote)
                    {
                        quote = '\0';
                    }

                    continue;
                }

                switch (c)
                {
                    case '\'' or '"' or '`':
                        quote = c;
                        break;
                    case '(':
                        depth++;
                        break;
                    case ')':
                        if (--depth == 0)
                        {
                            return i;
                        }

                        break;
                }
            }

            return -1;
        }

        /// <summary>The value of what is inside one <c>$( … )</c>.</summary>
        private string? EvaluateReference(string body, int depth)
        {
            body = body.Trim();
            if (body.Length == 0)
            {
                return string.Empty;
            }

            object? value;
            int position;
            if (body[0] == '[')
            {
                // A static function: [Type]::Member(args) or [Type]::Property.
                var typeEnd = body.IndexOf(']', StringComparison.Ordinal);
                if (typeEnd < 0 || !body.AsSpan(typeEnd + 1).StartsWith("::", StringComparison.Ordinal))
                {
                    return null;
                }

                var type = body[1..typeEnd].Trim();
                position = typeEnd + 3;
                var member = ReadMember(body, ref position, depth, out var arguments);
                if (member is null)
                {
                    return null;
                }

                value = MsBuildFunctions.CallStatic(type, member, arguments, this);
            }
            else
            {
                var nameEnd = 0;
                while (nameEnd < body.Length && (char.IsLetterOrDigit(body[nameEnd]) || body[nameEnd] is '_' or '-'))
                {
                    nameEnd++;
                }

                var name = body[..nameEnd];
                if (name.Length == 0)
                {
                    return null;
                }

                if (body.AsSpan(nameEnd).TrimStart() is [':', ..])
                {
                    // $(Registry:…) has no value here, as on a computer without that key.
                    return string.Empty;
                }

                value = Property(name);
                position = nameEnd;
            }

            // Members chained on the value: .StartsWith('net4'), .Length, .Major, .ToString('yyyy').
            while (value is not null && position < body.Length)
            {
                while (position < body.Length && char.IsWhiteSpace(body[position]))
                {
                    position++;
                }

                if (position >= body.Length)
                {
                    break;
                }

                if (body[position] != '.')
                {
                    return null;
                }

                position++;
                var member = ReadMember(body, ref position, depth, out var arguments);
                if (member is null)
                {
                    return null;
                }

                value = MsBuildFunctions.CallInstance(value, member, arguments);
            }

            return value is null ? null : MsBuildFunctions.ToText(value);
        }

        /// <summary>Reads a member name and, when it is called, its arguments, each expanded; <see langword="null"/> when they cannot be.</summary>
        private string? ReadMember(string body, ref int position, int depth, out List<string>? arguments)
        {
            arguments = null;
            var start = position;
            while (position < body.Length && (char.IsLetterOrDigit(body[position]) || body[position] == '_'))
            {
                position++;
            }

            var member = body[start..position];
            if (member.Length == 0)
            {
                return null;
            }

            while (position < body.Length && char.IsWhiteSpace(body[position]))
            {
                position++;
            }

            if (position >= body.Length || body[position] != '(')
            {
                return member;
            }

            var close = ClosingParenthesis(body, position);
            if (close < 0)
            {
                return null;
            }

            arguments = [];
            foreach (var raw in SplitArguments(body[(position + 1)..close]))
            {
                var argument = raw.Trim();
                if (argument.Length >= 2 && argument[0] is '\'' or '"' or '`' && argument[^1] == argument[0])
                {
                    argument = argument[1..^1];
                }

                if (Expand(argument, depth + 1) is not { } expanded)
                {
                    return null;
                }

                arguments.Add(expanded);
            }

            position = close + 1;
            return member;
        }

        private static List<string> SplitArguments(string text)
        {
            var parts = new List<string>();
            if (text.Trim().Length == 0)
            {
                return parts;
            }

            var depth = 0;
            var quote = '\0';
            var start = 0;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (quote != '\0')
                {
                    if (c == quote)
                    {
                        quote = '\0';
                    }

                    continue;
                }

                switch (c)
                {
                    case '\'' or '"' or '`':
                        quote = c;
                        break;
                    case '(':
                        depth++;
                        break;
                    case ')':
                        depth--;
                        break;
                    case ',' when depth == 0:
                        parts.Add(text[start..i]);
                        start = i + 1;
                        break;
                }
            }

            parts.Add(text[start..]);
            return parts;
        }

        private string Property(string name)
        {
            if (thisFile is not null && name.StartsWith("MSBuildThisFile", StringComparison.OrdinalIgnoreCase))
            {
                var thisFileValue = name.ToUpperInvariant() switch
                {
                    "MSBUILDTHISFILEDIRECTORY" => (Path.GetDirectoryName(thisFile) ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                    "MSBUILDTHISFILEDIRECTORYNOROOT" => NoRoot((Path.GetDirectoryName(thisFile) ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar),
                    "MSBUILDTHISFILEFULLPATH" => thisFile,
                    "MSBUILDTHISFILENAME" => Path.GetFileNameWithoutExtension(thisFile),
                    "MSBUILDTHISFILEEXTENSION" => Path.GetExtension(thisFile),
                    "MSBUILDTHISFILE" => Path.GetFileName(thisFile),
                    _ => null,
                };
                if (thisFileValue is not null)
                {
                    return thisFileValue;
                }
            }

            if (properties.TryGetValue(name, out var value))
            {
                return value;
            }

            _missing |= !UnknownIsEmpty;
            return string.Empty;
        }

        private static string NoRoot(string path) => path[(Path.GetPathRoot(path)?.Length ?? 0)..];
    }

    /// <summary>The property functions MSBuild offers that projects use during evaluation.</summary>
    private static class MsBuildFunctions
    {
        public static string ToText(object value) => value switch
        {
            string text => text,
            bool flag => flag ? "True" : "False",
            string[] parts => string.Join(';', parts),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };

        public static object? CallStatic(string type, string member, List<string>? arguments, Context context)
        {
            var args = arguments ?? [];
            var name = type.Trim();
            if (name.StartsWith("System.", StringComparison.OrdinalIgnoreCase))
            {
                name = name["System.".Length..];
            }

            return name.ToUpperInvariant() switch
            {
                "MSBUILD" => MsBuild(member, args, context),
                "IO.PATH" => PathFunction(member, args, context),
                "IO.FILE" => member.ToUpperInvariant() switch
                {
                    "EXISTS" when args.Count == 1 => File.Exists(context.FullPath(args[0])),
                    "READALLTEXT" when args.Count == 1 => ReadAllText(context.FullPath(args[0])),
                    _ => null,
                },
                "IO.DIRECTORY" => member.ToUpperInvariant() switch
                {
                    "EXISTS" when args.Count == 1 => Directory.Exists(context.FullPath(args[0])),
                    _ => null,
                },
                "STRING" => member.ToUpperInvariant() switch
                {
                    "COPY" when args.Count == 1 => args[0],
                    "ISNULLOREMPTY" when args.Count == 1 => args[0].Length == 0,
                    "ISNULLORWHITESPACE" when args.Count == 1 => string.IsNullOrWhiteSpace(args[0]),
                    "CONCAT" => string.Concat(args),
                    "EMPTY" => string.Empty,
                    "JOIN" when args.Count >= 1 => string.Join(args[0], args.Skip(1)),
                    _ => null,
                },
                "VERSION" => member.ToUpperInvariant() switch
                {
                    "PARSE" when args.Count == 1 && Version.TryParse(args[0].Trim(), out var version) => version,
                    _ => null,
                },
                "ENVIRONMENT" => member.ToUpperInvariant() switch
                {
                    "GETENVIRONMENTVARIABLE" when args.Count == 1 => Environment.GetEnvironmentVariable(args[0]) ?? string.Empty,
                    "NEWLINE" => Environment.NewLine,
                    "PROCESSORCOUNT" => Environment.ProcessorCount,
                    "IS64BITOPERATINGSYSTEM" or "IS64BITPROCESS" => Environment.Is64BitProcess,
                    _ => null,
                },
                "RUNTIME.INTEROPSERVICES.RUNTIMEINFORMATION" => member.ToUpperInvariant() switch
                {
                    "OSARCHITECTURE" => RuntimeInformation.OSArchitecture.ToString(),
                    "PROCESSARCHITECTURE" => RuntimeInformation.ProcessArchitecture.ToString(),
                    "FRAMEWORKDESCRIPTION" => RuntimeInformation.FrameworkDescription,
                    _ => null,
                },
                "TEXT.REGULAREXPRESSIONS.REGEX" => RegexFunction(member, args),
                "DATETIME" => member.ToUpperInvariant() switch
                {
                    "NOW" => DateTime.Now,
                    "UTCNOW" => DateTime.UtcNow,
                    "TODAY" => DateTime.Today,
                    _ => null,
                },
                "MATH" => MathFunction(member, args),
                "GUID" => member.Equals("NewGuid", StringComparison.OrdinalIgnoreCase) ? Guid.NewGuid().ToString() : null,
                "CONVERT" => member.ToUpperInvariant() switch
                {
                    "TOINT32" when args.Count == 1 && int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) => number,
                    "TOSTRING" when args.Count == 1 => args[0],
                    _ => null,
                },
                _ => null,
            };
        }

        public static object? CallInstance(object value, string member, List<string>? arguments)
        {
            var args = arguments ?? [];
            var upper = member.ToUpperInvariant();
            switch (value)
            {
                case Version version:
                    return upper switch
                    {
                        "MAJOR" => version.Major,
                        "MINOR" => version.Minor,
                        "BUILD" => version.Build,
                        "REVISION" => version.Revision,
                        "TOSTRING" when args.Count == 0 => version.ToString(),
                        "TOSTRING" when args.Count == 1 && int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var fields) && fields is >= 0 and <= 4 => version.ToString(fields),
                        _ => null,
                    };
                case DateTime date:
                    return upper switch
                    {
                        "YEAR" => date.Year,
                        "MONTH" => date.Month,
                        "DAY" => date.Day,
                        "TOSTRING" when args.Count == 0 => date.ToString(CultureInfo.InvariantCulture),
                        "TOSTRING" when args.Count == 1 => date.ToString(args[0], CultureInfo.InvariantCulture),
                        _ => null,
                    };
                case bool or int or double:
                    return upper == "TOSTRING" ? ToText(value) : null;
                case string[] parts:
                    return upper == "LENGTH" ? parts.Length : null;
            }

            var text = ToText(value);
            return upper switch
            {
                "LENGTH" => text.Length,
                "STARTSWITH" when args.Count >= 1 => text.StartsWith(args[0], Comparison(args)),
                "ENDSWITH" when args.Count >= 1 => text.EndsWith(args[0], Comparison(args)),
                "CONTAINS" when args.Count >= 1 => text.Contains(args[0], Comparison(args)),
                "EQUALS" when args.Count >= 1 => string.Equals(text, args[0], Comparison(args)),
                "INDEXOF" when args.Count == 1 => text.IndexOf(args[0], StringComparison.Ordinal),
                "LASTINDEXOF" when args.Count == 1 => text.LastIndexOf(args[0], StringComparison.Ordinal),
                "REPLACE" when args.Count == 2 && args[0].Length > 0 => text.Replace(args[0], args[1], StringComparison.Ordinal),
                "TRIM" when args.Count == 0 => text.Trim(),
                "TRIM" => text.Trim([.. string.Concat(args)]),
                "TRIMSTART" when args.Count == 0 => text.TrimStart(),
                "TRIMSTART" => text.TrimStart([.. string.Concat(args)]),
                "TRIMEND" when args.Count == 0 => text.TrimEnd(),
                "TRIMEND" => text.TrimEnd([.. string.Concat(args)]),
                "TOLOWER" or "TOLOWERINVARIANT" => text.ToLowerInvariant(),
                "TOUPPER" or "TOUPPERINVARIANT" => text.ToUpperInvariant(),
                "SUBSTRING" => Substring(text, args),
                "SPLIT" when args.Count >= 1 => text.Split([.. string.Concat(args.Take(1))]),
                "PADLEFT" when args.Count >= 1 && int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width) =>
                    text.PadLeft(width, args.Count > 1 && args[1].Length > 0 ? args[1][0] : ' '),
                "PADRIGHT" when args.Count >= 1 && int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width) =>
                    text.PadRight(width, args.Count > 1 && args[1].Length > 0 ? args[1][0] : ' '),
                "REMOVE" when args.Count >= 1 => Remove(text, args),
                "TOSTRING" => text,
                _ => null,
            };
        }

        private static StringComparison Comparison(List<string> args) =>
            args.Count > 1 && args[1].Contains("IgnoreCase", StringComparison.OrdinalIgnoreCase) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        private static string? Substring(string text, List<string> args)
        {
            if (args.Count == 0 || !int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var start) || start < 0 || start > text.Length)
            {
                return null;
            }

            if (args.Count == 1)
            {
                return text[start..];
            }

            return int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var length) && length >= 0 && start + length <= text.Length
                ? text.Substring(start, length)
                : null;
        }

        private static string? Remove(string text, List<string> args)
        {
            if (!int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var start) || start < 0 || start > text.Length)
            {
                return null;
            }

            if (args.Count == 1)
            {
                return text[..start];
            }

            return int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) && count >= 0 && start + count <= text.Length
                ? text.Remove(start, count)
                : null;
        }

        private static string? ReadAllText(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static object? MsBuild(string member, List<string> args, Context context)
        {
            switch (member.ToUpperInvariant())
            {
                case "GETPATHOFFILEABOVE" when args.Count is 1 or 2:
                    return MsBuildImports.GetPathOfFileAbove(args[0], args.Count == 2 && args[1].Length > 0 ? context.FullPath(args[1]) : context.ThisDirectory);
                case "GETDIRECTORYNAMEOFFILEABOVE" when args.Count == 2:
                    return MsBuildImports.GetDirectoryNameOfFileAbove(context.FullPath(args[0]), args[1]);
                case "ISOSPLATFORM" or "ISOSPLATFORM2" when args.Count == 1:
                    return IsWindows(args[0]);
                case "ISOSUNIXLIKE":
                    return false;
                case "ISTARGETFRAMEWORKCOMPATIBLE" when args.Count == 2:
                    return TargetFrameworks.IsCompatible(args[0], args[1]);
                case "GETTARGETFRAMEWORKIDENTIFIER" when args.Count == 1:
                    return TargetFrameworks.Parse(args[0])?.Identifier ?? string.Empty;
                case "GETTARGETFRAMEWORKVERSION" when args.Count is 1 or 2:
                    return TargetFrameworks.Parse(args[0]) is { } framework ? VersionText(framework.Version, args) : string.Empty;
                case "GETTARGETPLATFORMIDENTIFIER" when args.Count == 1:
                    return TargetFrameworks.Parse(args[0])?.Platform ?? string.Empty;
                case "GETTARGETPLATFORMVERSION" when args.Count is 1 or 2:
                    return TargetFrameworks.Parse(args[0]) is { PlatformVersion: { } platformVersion } ? VersionText(platformVersion, args) : string.Empty;
                case "VERSIONEQUALS" or "VERSIONNOTEQUALS" or "VERSIONGREATERTHAN" or "VERSIONGREATERTHANOREQUALS" or "VERSIONLESSTHAN" or "VERSIONLESSTHANOREQUALS" when args.Count == 2:
                    return CompareVersions(member.ToUpperInvariant(), args[0], args[1]);
                case "ENSURETRAILINGSLASH" when args.Count == 1:
                    return args[0].Length == 0 || args[0].EndsWith('/') || args[0].EndsWith('\\') ? args[0] : args[0] + Path.DirectorySeparatorChar;
                case "NORMALIZEPATH" when args.Count >= 1:
                    return context.FullPath(Path.Combine([.. args.Select(a => a.Replace('\\', Path.DirectorySeparatorChar))]));
                case "NORMALIZEDIRECTORY" when args.Count >= 1:
                    return context.FullPath(Path.Combine([.. args.Select(a => a.Replace('\\', Path.DirectorySeparatorChar))])).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                case "MAKERELATIVE" when args.Count == 2:
                    return MakeRelative(context.FullPath(args[0]), context.FullPath(args[1]), args[1]);
                case "VALUEORDEFAULT" when args.Count == 2:
                    return args[0].Length > 0 ? args[0] : args[1];
                case "ADD" or "SUBTRACT" or "MULTIPLY" or "DIVIDE" or "MODULO" when args.Count == 2:
                    return Arithmetic(member.ToUpperInvariant(), args[0], args[1]);
                case "BITWISEOR" or "BITWISEAND" when args.Count == 2
                    && long.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var a)
                    && long.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var b):
                    return (member.Equals("BitwiseOr", StringComparison.OrdinalIgnoreCase) ? a | b : a & b).ToString(CultureInfo.InvariantCulture);
                case "ESCAPE" when args.Count == 1:
                    return Uri.EscapeDataString(args[0]);
                case "UNESCAPE" when args.Count == 1:
                    return Uri.UnescapeDataString(args[0]);
                case "AREFEATURESENABLED":
                    return true;
                case "ISRUNNINGFROMVISUALSTUDIO":
                    return false;
                case "GETMSBUILDEXTENSIONSPATH" or "GETTOOLSDIRECTORY32" or "GETTOOLSDIRECTORY64" or "GETCURRENTTOOLSDIRECTORY" or "GETMSBUILDSDKSPATH":
                    return string.Empty;
                case "FILTERTARGETFRAMEWORKS" when args.Count == 2:
                    return string.Join(';', args[0].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Where(f => args[1].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .Any(filter => TargetFrameworks.Parse(f) is { } x && TargetFrameworks.Parse(filter) is { } y
                                && string.Equals(x.Identifier, y.Identifier, StringComparison.OrdinalIgnoreCase) && x.Version == y.Version)));
                case "STABLESTRINGHASH" when args.Count >= 1:
                    return StableHash(args[0]);
                default:
                    return null;
            }
        }

        private static string VersionText(Version version, List<string> args)
        {
            var fields = args.Count == 2 && int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ? count : 2;
            var parts = new[] { version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0) };
            return string.Join('.', parts.Take(Math.Clamp(fields, 1, 4)).Select(p => p.ToString(CultureInfo.InvariantCulture)));
        }

        /// <summary>MSBuild's platform checks answer as on Windows, where Visual Studio evaluates the project.</summary>
        private static bool IsWindows(string platform) => platform.Trim().Equals("windows", StringComparison.OrdinalIgnoreCase);

        private static bool? CompareVersions(string function, string a, string b)
        {
            if (!TryVersion(a, out var x) || !TryVersion(b, out var y))
            {
                return null;
            }

            var order = x.CompareTo(y);
            return function switch
            {
                "VERSIONEQUALS" => order == 0,
                "VERSIONNOTEQUALS" => order != 0,
                "VERSIONGREATERTHAN" => order > 0,
                "VERSIONGREATERTHANOREQUALS" => order >= 0,
                "VERSIONLESSTHAN" => order < 0,
                _ => order <= 0,
            };
        }

        private static bool TryVersion(string text, out Version version)
        {
            var trimmed = text.Trim().TrimStart('v', 'V');
            var dash = trimmed.IndexOfAny(['-', '+']);
            if (dash >= 0)
            {
                trimmed = trimmed[..dash];
            }

            if (!trimmed.Contains('.', StringComparison.Ordinal))
            {
                trimmed += ".0";
            }

            return Version.TryParse(trimmed, out version!);
        }

        private static string? Arithmetic(string function, string a, string b)
        {
            if (!double.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) || !double.TryParse(b, NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            {
                return null;
            }

            double? result = function switch
            {
                "ADD" => x + y,
                "SUBTRACT" => x - y,
                "MULTIPLY" => x * y,
                "DIVIDE" when y != 0 => x / y,
                "MODULO" when y != 0 => x % y,
                _ => null,
            };
            return result?.ToString(CultureInfo.InvariantCulture);
        }

        private static string MakeRelative(string basePath, string path, string original)
        {
            var baseFolder = basePath.EndsWith(Path.DirectorySeparatorChar) ? basePath : basePath + Path.DirectorySeparatorChar;
            var relative = Path.GetRelativePath(baseFolder, path);
            return original.EndsWith('/') || original.EndsWith('\\') ? relative.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar : relative;
        }

        private static int StableHash(string text)
        {
            // MSBuild's FNV-1a over the UTF-16 characters, as its StableStringHash.
            unchecked
            {
                var hash = (int)2166136261;
                foreach (var c in text)
                {
                    hash = (hash ^ c) * 16777619;
                }

                return hash;
            }
        }

        private static object? PathFunction(string member, List<string> args, Context context)
        {
            static string Native(string path) => path.Replace('\\', Path.DirectorySeparatorChar);
            try
            {
                return member.ToUpperInvariant() switch
                {
                    "COMBINE" when args.Count >= 1 => Path.Combine([.. args.Select(Native)]),
                    "GETFILENAME" when args.Count == 1 => Path.GetFileName(Native(args[0])),
                    "GETFILENAMEWITHOUTEXTENSION" when args.Count == 1 => Path.GetFileNameWithoutExtension(Native(args[0])),
                    "GETDIRECTORYNAME" when args.Count == 1 => Path.GetDirectoryName(Native(args[0])) ?? string.Empty,
                    "GETEXTENSION" when args.Count == 1 => Path.GetExtension(Native(args[0])),
                    "GETFULLPATH" when args.Count == 1 => context.FullPath(args[0]),
                    "ISPATHROOTED" when args.Count == 1 => Path.IsPathRooted(Native(args[0])),
                    "HASEXTENSION" when args.Count == 1 => Path.HasExtension(Native(args[0])),
                    "CHANGEEXTENSION" when args.Count == 2 => Path.ChangeExtension(Native(args[0]), args[1]),
                    "DIRECTORYSEPARATORCHAR" => Path.DirectorySeparatorChar.ToString(),
                    "GETTEMPPATH" => Path.GetTempPath(),
                    _ => null,
                };
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static object? RegexFunction(string member, List<string> args)
        {
            try
            {
                return member.ToUpperInvariant() switch
                {
                    "ISMATCH" when args.Count == 2 => Regex.IsMatch(args[0], args[1], RegexOptions.None, TimeSpan.FromSeconds(1)),
                    "REPLACE" when args.Count == 3 => Regex.Replace(args[0], args[1], args[2], RegexOptions.None, TimeSpan.FromSeconds(1)),
                    "MATCH" when args.Count == 2 => Regex.Match(args[0], args[1], RegexOptions.None, TimeSpan.FromSeconds(1)).Value,
                    "ESCAPE" when args.Count == 1 => Regex.Escape(args[0]),
                    _ => null,
                };
            }
            catch (Exception ex) when (ex is ArgumentException or RegexMatchTimeoutException)
            {
                return null;
            }
        }

        private static object? MathFunction(string member, List<string> args)
        {
            if (args.Count != 2
                || !double.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var a)
                || !double.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var b))
            {
                return null;
            }

            return member.ToUpperInvariant() switch
            {
                "MAX" => Math.Max(a, b),
                "MIN" => Math.Min(a, b),
                _ => null,
            };
        }
    }

    /// <summary>Target framework monikers, as NuGet reads them, for the property functions that take them.</summary>
    internal static class TargetFrameworks
    {
        /// <summary>A parsed framework: <c>.NETCoreApp</c> 8.0 with platform <c>android</c> 34.0 for <c>net8.0-android34.0</c>.</summary>
        internal sealed record Framework(string Identifier, Version Version, string Platform, Version? PlatformVersion);

        /// <summary>Parses a short framework name; <see langword="null"/> when it is not one.</summary>
        public static Framework? Parse(string moniker)
        {
            var text = moniker.Trim().ToLowerInvariant();
            if (text.Length == 0)
            {
                return null;
            }

            var platform = string.Empty;
            Version? platformVersion = null;
            var dash = text.IndexOf('-', StringComparison.Ordinal);
            if (dash >= 0)
            {
                var platformText = text[(dash + 1)..];
                text = text[..dash];
                var digit = platformText.AsSpan().IndexOfAnyInRange('0', '9');
                platform = digit < 0 ? platformText : platformText[..digit];
                if (digit >= 0 && TryParseVersion(platformText[digit..], dotted: true, out var parsed))
                {
                    platformVersion = parsed;
                }
            }

            (string Prefix, string Identifier)[] identifiers =
            [
                ("netcoreapp", ".NETCoreApp"), ("netstandard", ".NETStandard"), ("net", string.Empty), ("uap", "UAP"),
                ("monoandroid", "MonoAndroid"), ("xamarinios", "Xamarin.iOS"), ("netnative", ".NETNative"),
            ];
            foreach (var (prefix, identifier) in identifiers)
            {
                if (!text.StartsWith(prefix, StringComparison.Ordinal) || text.Length == prefix.Length || !char.IsAsciiDigit(text[prefix.Length]))
                {
                    continue;
                }

                var digits = text[prefix.Length..];
                var dotted = digits.Contains('.', StringComparison.Ordinal);
                if (!TryParseVersion(digits, dotted, out var version))
                {
                    return null;
                }

                var resolved = identifier.Length > 0 ? identifier : dotted || version.Major >= 5 ? ".NETCoreApp" : ".NETFramework";
                return new Framework(resolved, version, platform, platformVersion);
            }

            return null;
        }

        /// <summary>Whether a project for <paramref name="target"/> can use assets for <paramref name="candidate"/>, as NuGet decides.</summary>
        public static bool? IsCompatible(string target, string candidate)
        {
            if (Parse(target) is not { } t || Parse(candidate) is not { } c)
            {
                return null;
            }

            if (c.Platform.Length > 0 && !string.Equals(c.Platform, t.Platform, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(t.Identifier, c.Identifier, StringComparison.OrdinalIgnoreCase))
            {
                return c.Version <= t.Version;
            }

            // .NET Standard assets serve .NET (Core) and .NET Framework of the versions that implement them.
            if (c.Identifier == ".NETStandard")
            {
                return t.Identifier switch
                {
                    ".NETCoreApp" => c.Version <= (t.Version.Major >= 3 ? new Version(2, 1) : new Version(2, 0)),
                    ".NETFramework" => c.Version <= (t.Version >= new Version(4, 6, 1) ? new Version(2, 0) : new Version(1, 4)),
                    _ => false,
                };
            }

            return false;
        }

        private static bool TryParseVersion(string digits, bool dotted, out Version version)
        {
            if (dotted)
            {
                var text = digits.Contains('.', StringComparison.Ordinal) ? digits : digits + ".0";
                return Version.TryParse(text, out version!);
            }

            // net48 is 4.8, net472 is 4.7.2, netstandard2 is 2.0.
            if (digits.Length == 0 || !digits.All(char.IsAsciiDigit))
            {
                version = new Version();
                return false;
            }

            var parts = digits.Select(d => d - '0').ToList();
            while (parts.Count < 2)
            {
                parts.Add(0);
            }

            version = parts.Count switch
            {
                2 => new Version(parts[0], parts[1]),
                3 => new Version(parts[0], parts[1], parts[2]),
                _ => new Version(parts[0], parts[1], parts[2], parts[3]),
            };
            return true;
        }
    }
}
