using System.Text;
using System.Text.RegularExpressions;

namespace TabStudio.Services.Compose;

/// <summary>Compose variable substitution; $$ is kept for the service's shell.</summary>
public static partial class ComposeInterpolation
{
    public static string Expand(string text, IReadOnlyDictionary<string, string?> environment)
    {
        var result = new StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '$' || i + 1 == text.Length) { result.Append(text[i]); continue; }
            if (text[i + 1] == '$') { result.Append('$'); i++; continue; }
            string expression;
            if (text[i + 1] == '{')
            {
                var start = i + 2;
                var depth = 1;
                var end = start;
                for (; end < text.Length; end++)
                {
                    if (text[end] == '{') depth++;
                    if (text[end] == '}' && --depth == 0) break;
                }
                if (end == text.Length) throw new ComposeException("Unclosed Compose environment expression.");
                expression = text[start..end];
                i = end;
            }
            else
            {
                var match = Variable().Match(text, i + 1);
                if (!match.Success || match.Index != i + 1) { result.Append('$'); continue; }
                expression = match.Value;
                i += match.Length;
            }

            var parsed = Expression().Match(expression);
            if (!parsed.Success) throw new ComposeException("Invalid Compose environment expression.");
            var name = parsed.Groups[1].Value;
            var op = parsed.Groups[2].Value;
            var fallback = parsed.Groups[3].Value;
            environment.TryGetValue(name, out var value);
            var defined = value is not null && (!op.StartsWith(':') || value.Length > 0);
            result.Append(op switch
            {
                "-" or ":-" => defined ? value : Expand(fallback, environment),
                "+" or ":+" => defined ? Expand(fallback, environment) : "",
                "?" or ":?" => defined ? value : throw new ComposeException($"Required Compose variable '{name}' is missing."),
                _ => value ?? "",
            });
        }
        return result.ToString();
    }

    [GeneratedRegex("[A-Za-z_][A-Za-z0-9_]*")]
    private static partial Regex Variable();
    [GeneratedRegex("^([A-Za-z_][A-Za-z0-9_]*)(:?[-+?])?(.*)$", RegexOptions.Singleline)]
    private static partial Regex Expression();
}
