using System.Globalization;
using System.Text.RegularExpressions;

namespace TabStudio.Toolchains.Python;

/// <summary>
/// The Python 3 versions a project accepts, from a PEP 440 specifier such as <c>&gt;=3.9,&lt;3.12</c>, Poetry's
/// <c>^3.10</c> and <c>~3.10</c>, or a plain version such as <c>3.11</c> or <c>3.11.4</c>. Only CPython 3 is
/// considered, at the level of minor versions: <see cref="Allows(int)"/> tells whether some release of
/// 3.<i>minor</i> satisfies the specifier.
/// </summary>
public sealed partial class PythonVersionRange
{
    private const int AnyPatch = 9999;
    private const int Below = -1;
    private const int Above = int.MaxValue - 1;
    private readonly Bound _lowest;
    private readonly Bound _highest;
    private readonly HashSet<int> _excluded;

    private PythonVersionRange(Bound lowest, Bound highest, HashSet<int> excluded, string text)
    {
        _lowest = lowest;
        _highest = highest;
        _excluded = excluded;
        Text = text;
    }

    /// <summary>The specifier as the project wrote it, such as <c>&gt;=3.9</c>.</summary>
    public string Text { get; }

    /// <summary>
    /// The one minor version the range allows, such as 11 for <c>3.11</c> or <c>==3.11.4</c>, or
    /// <see langword="null"/> when it allows several.
    /// </summary>
    public int? ExactMinor => _lowest.Minor == _highest.Minor && _lowest.Minor >= 0 && Allows(_lowest.Minor) ? _lowest.Minor : null;

    /// <summary>Reads a specifier, or returns <see langword="null"/> when it names no Python 3 version.</summary>
    /// <param name="text">The specifier, such as <c>&gt;=3.9,&lt;3.12</c>, <c>^3.10</c> or <c>3.11</c>.</param>
    public static PythonVersionRange? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var lowest = new Bound(Below, 0, Inclusive: true);
        var highest = new Bound(Above, AnyPatch, Inclusive: true);
        var excluded = new HashSet<int>();
        var clauses = text.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (clauses.Length == 0 || text.Contains("||", StringComparison.Ordinal))
        {
            // Alternatives ("^3.8 || ^3.9") cannot be one interval; the project's own tools resolve them.
            return null;
        }

        foreach (var raw in MergeOperators(clauses))
        {
            if (Clause().Match(raw) is not { Success: true } clause)
            {
                return null;
            }

            var op = clause.Groups["op"].Value;
            var parts = clause.Groups["version"].Value.Split('.');
            var wildcard = parts[^1] == "*";
            if (wildcard)
            {
                parts = parts[..^1];
            }

            var numbers = parts.Select(p => int.Parse(p, CultureInfo.InvariantCulture)).ToArray();
            var major = numbers[0];
            var minor = major < 3 ? Below : major > 3 ? Above : numbers.Length > 1 ? numbers[1] : 0;
            var patch = major == 3 && numbers.Length > 2 ? numbers[2] : 0;
            var hasMinor = major == 3 && numbers.Length > 1;
            var hasPatch = major == 3 && numbers.Length > 2;
            if (major != 3 && op is "" or "==" or "===" or "~=" or "^" or "~")
            {
                return null;
            }

            switch (op)
            {
                case ">=":
                    lowest = Bound.Max(lowest, new(minor, patch, Inclusive: true));
                    break;
                case ">":
                    lowest = Bound.Max(lowest, new(minor, hasPatch ? patch : AnyPatch, Inclusive: false));
                    break;
                case "<=":
                    highest = Bound.Min(highest, new(minor, hasPatch ? patch : AnyPatch, Inclusive: true));
                    break;
                case "<":
                    highest = Bound.Min(highest, new(minor, patch, Inclusive: false));
                    break;
                case "!=":
                    if (wildcard || !hasPatch)
                    {
                        excluded.Add(minor);
                    }

                    break;
                case "^":
                    // Poetry's caret: compatible within the major version.
                    lowest = Bound.Max(lowest, new(minor, patch, Inclusive: true));
                    highest = Bound.Min(highest, new(Above, 0, Inclusive: false));
                    break;
                case "~":
                case "~=" when hasPatch:
                    // Poetry's tilde, and ~=3.10.2: the same minor version.
                    lowest = Bound.Max(lowest, new(minor, patch, Inclusive: true));
                    highest = Bound.Min(highest, new(minor, AnyPatch, Inclusive: true));
                    break;
                case "~=":
                    // ~=3.10: 3.10 or any later 3.x.
                    lowest = Bound.Max(lowest, new(minor, patch, Inclusive: true));
                    break;
                default:
                    // ==, === or a bare version: that release; with a wildcard or without a patch, that minor version.
                    if (!hasMinor)
                    {
                        break;
                    }

                    lowest = Bound.Max(lowest, new(minor, wildcard || !hasPatch ? 0 : patch, Inclusive: true));
                    highest = Bound.Min(highest, new(minor, wildcard || !hasPatch ? AnyPatch : patch, Inclusive: true));
                    break;
            }
        }

        return new PythonVersionRange(lowest, highest, excluded, text.Trim());
    }

    /// <summary>Whether some release of Python 3.<paramref name="minor"/> satisfies the range.</summary>
    /// <param name="minor">The minor version, such as 11 for Python 3.11.</param>
    public bool Allows(int minor)
    {
        if (minor < 0 || _excluded.Contains(minor))
        {
            return false;
        }

        // The releases of 3.minor run from 3.minor.0 to 3.minor.AnyPatch; the range must overlap them.
        var low = Compare(minor, AnyPatch, _lowest);
        var high = Compare(minor, 0, _highest);
        return (low > 0 || (low == 0 && _lowest.Inclusive)) && (high < 0 || (high == 0 && _highest.Inclusive));
    }

    /// <inheritdoc />
    public override string ToString() => Text;

    private static int Compare(int minor, int patch, Bound bound) =>
        minor != bound.Minor ? minor.CompareTo(bound.Minor) : patch.CompareTo(bound.Patch);

    /// <summary>Joins operators written apart from their versions, as in <c>&gt;= 3.9</c>.</summary>
    private static IEnumerable<string> MergeOperators(string[] clauses)
    {
        for (var i = 0; i < clauses.Length; i++)
        {
            if (OperatorOnly().IsMatch(clauses[i]) && i + 1 < clauses.Length)
            {
                yield return clauses[i] + clauses[++i];
            }
            else
            {
                yield return clauses[i];
            }
        }
    }

    /// <summary>One end of the range: a minor version and a patch, and whether it is included.</summary>
    private readonly record struct Bound(int Minor, int Patch, bool Inclusive)
    {
        public static Bound Max(Bound a, Bound b) =>
            (a.Minor, a.Patch).CompareTo((b.Minor, b.Patch)) is var order && (order > 0 || (order == 0 && !a.Inclusive)) ? a : b;

        public static Bound Min(Bound a, Bound b) =>
            (a.Minor, a.Patch).CompareTo((b.Minor, b.Patch)) is var order && (order < 0 || (order == 0 && !a.Inclusive)) ? a : b;
    }

    [GeneratedRegex(@"^(?<op>===|==|!=|<=|>=|~=|<|>|\^|~)?v?(?<version>\d+(\.\d+)*(\.\*)?)([a-z]+\d*)?$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex Clause();

    [GeneratedRegex(@"^(===|==|!=|<=|>=|~=|<|>|\^|~)$", RegexOptions.CultureInvariant)]
    private static partial Regex OperatorOnly();
}
