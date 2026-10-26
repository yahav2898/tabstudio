using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace TabStudio.Core.Workspaces;

/// <summary>
/// Evaluates the simple MSBuild conditions repositories put on item groups, such as
/// <c>'$(IncludePollyUsings)' == 'true'</c> or <c>'$(TargetFramework)' != 'net462' and Exists('x.props')</c>: string
/// comparisons with <c>==</c> and <c>!=</c>, <c>Exists(...)</c>, <c>!</c>, <c>and</c> and <c>or</c>. As in MSBuild an
/// unknown property is empty and comparisons ignore case. A condition with anything else, such as a property function,
/// cannot be evaluated here and counts as true, as TabStudio treated every condition before.
/// </summary>
public static partial class MsBuildConditions
{
    /// <summary>Evaluates a condition.</summary>
    /// <param name="condition">The condition's text.</param>
    /// <param name="properties">The properties known at that point.</param>
    /// <param name="baseDirectory">The folder relative paths in <c>Exists</c> start from.</param>
    /// <returns>The result, or <see langword="null"/> when the condition is not one this evaluates.</returns>
    public static bool? Evaluate(string condition, IReadOnlyDictionary<string, string> properties, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(properties);
        if (condition.Contains("$([", StringComparison.Ordinal) || condition.Contains("@(", StringComparison.Ordinal) || condition.Contains("%(", StringComparison.Ordinal))
        {
            return null;
        }

        var expanded = PropertyReference().Replace(condition, m => properties.TryGetValue(m.Groups["name"].Value, out var value) ? value : string.Empty);
        bool? any = false;
        foreach (var alternative in Split(expanded, "or"))
        {
            bool? all = true;
            foreach (var term in Split(alternative, "and"))
            {
                var value = Term(term.Trim(), baseDirectory);
                all = value is null || all is null ? (value == false || all == false ? false : null) : all.Value && value.Value;
            }

            any = all == true || any == true ? true : all is null || any is null ? null : false;
        }

        return any;
    }

    /// <summary>
    /// Removes the elements of a props or targets file whose condition, or whose group's condition, is false, so that
    /// only the items MSBuild would add remain. Conditions that cannot be evaluated keep their elements.
    /// </summary>
    /// <param name="root">The file's root element; it is changed.</param>
    /// <param name="properties">The project's properties.</param>
    /// <param name="baseDirectory">The file's folder.</param>
    public static void RemoveFalse(XElement root, IReadOnlyDictionary<string, string> properties, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(root);
        foreach (var element in root.Descendants().Where(e => e.Attribute("Condition") is { } c && Evaluate(c.Value, properties, baseDirectory) == false).ToList())
        {
            if (element.Parent is not null)
            {
                element.Remove();
            }
        }
    }

    private static bool? Term(string term, string baseDirectory)
    {
        if (term.Length == 0)
        {
            return null;
        }

        if (term.StartsWith('(') && term.EndsWith(')'))
        {
            return null;
        }

        if (term.StartsWith('!'))
        {
            return Term(term[1..].Trim(), baseDirectory) is { } inner ? !inner : null;
        }

        if (ExistsCall().Match(term) is { Success: true } exists)
        {
            var path = exists.Groups["path"].Value.Trim();
            if (path.Length == 0)
            {
                return false;
            }

            var full = Path.IsPathRooted(path) ? path : Path.Combine(baseDirectory, path.Replace('\\', Path.DirectorySeparatorChar));
            return File.Exists(full) || Directory.Exists(full);
        }

        if (Comparison().Match(term) is { Success: true } comparison)
        {
            var equal = string.Equals(Unquote(comparison.Groups["left"].Value), Unquote(comparison.Groups["right"].Value), StringComparison.OrdinalIgnoreCase);
            return comparison.Groups["op"].Value == "==" ? equal : !equal;
        }

        return null;
    }

    private static string Unquote(string operand)
    {
        var trimmed = operand.Trim();
        return trimmed.Length >= 2 && trimmed[0] == '\'' && trimmed[^1] == '\'' ? trimmed[1..^1] : trimmed;
    }

    /// <summary>Splits on a keyword outside quotes, as in <c>a and b</c>.</summary>
    private static List<string> Split(string text, string keyword)
    {
        var parts = new List<string>();
        var start = 0;
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\'')
            {
                quoted = !quoted;
            }
            else if (!quoted && i + keyword.Length + 2 <= text.Length && char.IsWhiteSpace(text[i])
                && string.Compare(text, i + 1, keyword, 0, keyword.Length, StringComparison.OrdinalIgnoreCase) == 0
                && char.IsWhiteSpace(text[i + keyword.Length + 1]))
            {
                parts.Add(text[start..i]);
                start = i + keyword.Length + 1;
            }
        }

        parts.Add(text[start..]);
        return parts;
    }

    [GeneratedRegex(@"\$\((?<name>[A-Za-z_][A-Za-z0-9_.\-]*)\)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex PropertyReference();

    [GeneratedRegex(@"^Exists\(\s*'(?<path>[^']*)'\s*\)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, 100)]
    private static partial Regex ExistsCall();

    [GeneratedRegex(@"^(?<left>'[^']*'|[^\s=!']+)\s*(?<op>==|!=)\s*(?<right>'[^']*'|[^\s=!']+)$", RegexOptions.CultureInvariant, 100)]
    private static partial Regex Comparison();
}
