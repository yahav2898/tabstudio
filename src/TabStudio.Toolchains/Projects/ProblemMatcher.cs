using System.Globalization;
using System.Text.RegularExpressions;
using TabStudio.Toolchains.Catalog;

namespace TabStudio.Toolchains.Projects;

/// <summary>A file location found in a line of output.</summary>
/// <param name="File">The file as printed: absolute, or relative to the folder the program ran in.</param>
/// <param name="Line">The 1-based line.</param>
/// <param name="Column">The 1-based column, when printed.</param>
/// <param name="Start">Where the location's text starts in the output line.</param>
/// <param name="Length">How long the location's text is.</param>
/// <param name="Message">A message printed with it, such as a compiler error's, when the pattern has one.</param>
/// <param name="Severity">The compiler's severity, when printed.</param>
public sealed record OutputLocation(string File, int Line, int? Column, int Start, int Length, string? Message = null, string? Severity = null);

/// <summary>The error that ended a program, for the Error List: its message and where it happened.</summary>
/// <param name="Message">The message, such as <c>NameError: name 'x' is not defined</c>.</param>
/// <param name="Location">Where, in the user's own code when the output names a file of the workspace.</param>
public sealed record RunError(string Message, OutputLocation? Location);

/// <summary>
/// Finds file locations in a toolchain's output with its catalog patterns, such as the frames of a Python
/// traceback or a Node.js stack trace, so the Output panel can link them and the Error List can list the error.
/// </summary>
public sealed class ProblemMatcher
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);
    private readonly List<Regex> _patterns;
    private readonly Regex? _error;

    /// <summary>Creates a matcher for a toolchain.</summary>
    /// <param name="toolchain">The toolchain.</param>
    public ProblemMatcher(ToolchainDefinition toolchain)
    {
        ArgumentNullException.ThrowIfNull(toolchain);
        _patterns = [.. toolchain.ProblemPatterns.Select(p => new Regex(p.Pattern, RegexOptions.CultureInvariant, MatchTimeout))];
        _error = toolchain.ErrorPattern is { } error ? new Regex(error, RegexOptions.CultureInvariant, MatchTimeout) : null;
    }

    /// <summary>The locations in one line, left to right, without overlaps.</summary>
    /// <param name="line">A line of output, without its newline.</param>
    public IReadOnlyList<OutputLocation> Match(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var result = new List<OutputLocation>();
        foreach (var pattern in _patterns)
        {
            try
            {
                foreach (Match match in pattern.Matches(line))
                {
                    var file = match.Groups["file"];
                    if (!file.Success || !int.TryParse(match.Groups["line"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 1)
                    {
                        continue;
                    }

                    var column = int.TryParse(match.Groups["column"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var c) && c > 0 ? c : (int?)null;
                    var message = match.Groups["message"] is { Success: true } m ? m.Value : null;
                    if (!result.Any(r => r.Start < match.Index + match.Length && match.Index < r.Start + r.Length))
                    {
                        // A file:// URL, as Node.js prints an ES module's frames, escapes spaces and other characters (%20).
                        var path = match.Groups["url"].Success ? Uri.UnescapeDataString(file.Value) : file.Value;
                        result.Add(new OutputLocation(path, number, column, match.Index, match.Length, message, match.Groups["severity"].Success ? match.Groups["severity"].Value : null));
                    }
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // A pathological line; it simply gets no links.
            }
        }

        result.Sort((a, b) => a.Start.CompareTo(b.Start));
        return result;
    }

    /// <summary>Compiler errors with a message on their location line, followed by any runtime error.</summary>
    /// <param name="lines">Output from one build or run.</param>
    /// <param name="isUserFile">Whether a printed path belongs to the workspace.</param>
    public IReadOnlyList<RunError> FindErrors(IReadOnlyList<string> lines, Func<string, bool> isUserFile)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(isUserFile);
        var errors = lines.SelectMany(Match)
            .Where(l => l.Message is { Length: > 0 } && l.Severity?.ToLowerInvariant() is null or "error" or "e" or "fatal error" or "parse error" && isUserFile(l.File))
            .Select(l => new RunError(l.Message!, l))
            // One error printed twice, as PHP's "PHP Parse error:" and "Parse error:" lines, is listed once.
            .DistinctBy(e => (e.Message, e.Location?.File, e.Location?.Line, e.Location?.Column))
            .ToList();
        if (errors.Count == 0 && FindError(lines, isUserFile) is { } runtime)
        {
            errors.Add(runtime);
        }

        return errors;
    }

    /// <summary>
    /// The error that ended a run, from its whole output: the last line that states an error, at the nearest
    /// location in the user's code: after the message for a stack trace that follows it (Node.js), before it for
    /// a traceback that leads up to it (Python).
    /// </summary>
    /// <param name="lines">The run's output, line by line.</param>
    /// <param name="isUserFile">Whether a printed path is one of the workspace's files.</param>
    public RunError? FindError(IReadOnlyList<string> lines, Func<string, bool> isUserFile)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(isUserFile);
        if (_error is null)
        {
            return null;
        }

        for (var i = lines.Count - 1; i >= 0; i--)
        {
            Match match;
            try
            {
                match = _error.Match(lines[i].Trim());
            }
            catch (RegexMatchTimeoutException)
            {
                continue;
            }

            if (!match.Success)
            {
                continue;
            }

            var message = match.Groups["message"].Value.Trim();

            // A message that ends with a colon goes on on the next line, as a Rust panic's does since Rust 1.73.
            if (message.EndsWith(':') && i + 1 < lines.Count && lines[i + 1].Trim() is { Length: > 0 } rest)
            {
                message = $"{message} {rest}";
            }

            // The location can be on the message's own line, as a panic's is ("panicked at src/main.rs:2:5:").
            var location = Match(lines[i]).FirstOrDefault(l => isUserFile(l.File));
            for (var j = i + 1; j < Math.Min(lines.Count, i + 30) && location is null; j++)
            {
                location = Match(lines[j]).FirstOrDefault(l => isUserFile(l.File));
            }

            for (var j = i - 1; j >= 0 && location is null; j--)
            {
                location = Match(lines[j]).LastOrDefault(l => isUserFile(l.File));
            }

            return new RunError(message, location);
        }

        return null;
    }
}
