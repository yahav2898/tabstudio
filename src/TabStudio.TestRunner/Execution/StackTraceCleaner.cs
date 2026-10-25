using System.Globalization;
using TabStudio.TestRunner.Reflection;

namespace TabStudio.TestRunner.Execution;

/// <summary>A trimmed stack trace and where the failure happened in the user's code.</summary>
/// <param name="Text">The trimmed stack trace, one frame per line.</param>
/// <param name="Frames">The frames of the trimmed trace.</param>
/// <param name="FailureLocation">The top user frame with a source location.</param>
internal sealed record CleanStackTrace(string? Text, IReadOnlyList<TestStackFrame> Frames, TestSourceLocation? FailureLocation);

/// <summary>
/// Trims a failed test's stack trace to the frames that help: the user's code and what it called, without the
/// assertion internals above it and the runner below it.
/// </summary>
/// <remarks>
/// <para>
/// Frames are read from <see cref="Exception.StackTrace"/>, which, unlike <see cref="System.Diagnostics.StackTrace"/>,
/// keeps the frames from before every <c>await</c> an async test went through. Assemblies are loaded with
/// their portable PDBs, so the runtime has already mapped each frame to a file and line.
/// </para>
/// <para>
/// A frame is user code when its type is one of the types compiled from the user's projects, including the
/// compiler-generated ones behind lambdas and async methods.
/// </para>
/// </remarks>
internal sealed class StackTraceCleaner(IReadOnlySet<string> userTypeNames)
{
    private const string AtPrefix = "at ";
    private const string InSeparator = " in ";
    private const string LineSeparator = ":line ";

    /// <summary>Builds the set of user type names, as stack traces print them, from the user's assemblies.</summary>
    /// <param name="types">Every type of the user's assemblies.</param>
    public static HashSet<string> TypeNamesOf(IEnumerable<Type> types) =>
        types.Select(t => (t.FullName ?? t.Name).Replace('+', '.')).ToHashSet(StringComparer.Ordinal);

    /// <summary>Cleans an exception's stack trace.</summary>
    /// <param name="exception">The exception, already unwrapped.</param>
    public CleanStackTrace Clean(Exception exception)
    {
        var frames = Parse(exception.StackTrace);
        var firstUser = frames.FindIndex(f => f.IsUserCode);
        var lastUser = frames.FindLastIndex(f => f.IsUserCode);

        List<TestStackFrame> kept;
        if (firstUser < 0)
        {
            kept = frames.Where(f => !IsRunnerFrame(f.Method)).ToList();
        }
        else
        {
            var start = 0;
            while (start < firstUser && IsFrameworkFrame(frames[start].Method))
            {
                start++;
            }

            kept = frames.GetRange(start, lastUser - start + 1);
        }

        var location = kept
            .Where(f => f.IsUserCode && f.FilePath is not null && f.Line is not null)
            .Select(f => new TestSourceLocation(f.FilePath!, f.Line!.Value))
            .FirstOrDefault();

        var text = kept.Count == 0 ? null : string.Join("\n", kept.Select(f => f.ToString()));
        return new CleanStackTrace(text, kept, location);
    }

    private List<TestStackFrame> Parse(string? stackTrace)
    {
        var frames = new List<TestStackFrame>();
        if (string.IsNullOrEmpty(stackTrace))
        {
            return frames;
        }

        foreach (var rawLine in stackTrace.Split('\n'))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith(AtPrefix, StringComparison.Ordinal))
            {
                // "--- End of stack trace from previous location ---" and anything else that is not a frame.
                continue;
            }

            var body = line[AtPrefix.Length..];
            string? file = null;
            int? lineNumber = null;
            var inIndex = body.LastIndexOf(InSeparator, StringComparison.Ordinal);
            if (inIndex >= 0)
            {
                var location = body[(inIndex + InSeparator.Length)..];
                var lineIndex = location.LastIndexOf(LineSeparator, StringComparison.Ordinal);
                if (lineIndex >= 0 &&
                    int.TryParse(location.AsSpan(lineIndex + LineSeparator.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
                {
                    file = location[..lineIndex];
                    lineNumber = parsed;
                    body = body[..inIndex];
                }
            }

            frames.Add(new TestStackFrame(body, file, lineNumber, IsUserMethod(body)));
        }

        return frames;
    }

    /// <summary>Whether a frame's method, such as <c>Ns.Type.Method(Int32 x)</c>, belongs to a user type.</summary>
    private bool IsUserMethod(string method)
    {
        var parenthesis = method.IndexOf('(', StringComparison.Ordinal);
        var name = parenthesis < 0 ? method : method[..parenthesis];
        var bracket = name.IndexOf('[', StringComparison.Ordinal);
        if (bracket >= 0)
        {
            name = name[..bracket];
        }

        for (var dot = name.LastIndexOf('.'); dot > 0; dot = name.LastIndexOf('.', dot - 1))
        {
            if (userTypeNames.Contains(name[..dot]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFrameworkFrame(string method) =>
        FrameworkNames.FrameworkFramePrefixes.Any(prefix => method.StartsWith(prefix, StringComparison.Ordinal));

    private static bool IsRunnerFrame(string method) =>
        method.StartsWith("TabStudio.TestRunner.", StringComparison.Ordinal) ||
        method.StartsWith("System.Reflection.", StringComparison.Ordinal) ||
        method.StartsWith("System.RuntimeMethodHandle.", StringComparison.Ordinal) ||
        method.StartsWith("System.Threading.", StringComparison.Ordinal);
}
