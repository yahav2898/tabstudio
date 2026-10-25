using System.Globalization;
using TabStudio.Core.Diagnostics;
using TabStudio.Notebooks.Outputs;

namespace TabStudio.Notebooks.Execution;

/// <summary>Turns compile errors and exceptions into the error outputs saved with a notebook.</summary>
internal static class CellErrors
{
    /// <summary>The name of the error output for a cell that did not compile.</summary>
    public const string CompilationErrorName = "CompilationError";

    /// <summary>Every stack frame in a cell's own code is in a class with this prefix.</summary>
    private const string SubmissionFrameMarker = "Submission#";

    /// <summary>An error output listing a cell's errors as <c>(line,column): error ID: message</c>.</summary>
    public static ErrorOutput FromDiagnostics(IEnumerable<BuildDiagnostic> diagnostics)
    {
        var lines = diagnostics.Where(d => d.Severity == DiagnosticSeverityLevel.Error).Select(Describe).ToList();
        return new ErrorOutput(CompilationErrorName, string.Join('\n', lines), lines);
    }

    /// <summary>
    /// An error output for an exception, with its stack trace cut after the last frame in a cell's code, so it
    /// does not show TabStudio's own frames.
    /// </summary>
    public static ErrorOutput FromException(Exception exception)
    {
        var lines = exception.ToString().ReplaceLineEndings("\n").Split('\n');
        var last = Array.FindLastIndex(lines, line => line.Contains(SubmissionFrameMarker, StringComparison.Ordinal));
        var traceback = last >= 0 ? lines[..(last + 1)] : lines;
        return new ErrorOutput(exception.GetType().FullName ?? exception.GetType().Name, exception.Message, traceback);
    }

    private static string Describe(BuildDiagnostic diagnostic) => diagnostic.Location is { } location
        ? string.Create(CultureInfo.InvariantCulture, $"({location.Line},{location.Column}): error {diagnostic.Id}: {diagnostic.Message}")
        : $"error {diagnostic.Id}: {diagnostic.Message}";
}
