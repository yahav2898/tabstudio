using System.Collections;
using Microsoft.Build.Framework;

namespace TabStudio.Wpf.Compiler;

/// <summary>An error or warning the XAML compiler logged, with the file and position it named.</summary>
/// <param name="IsError">Whether it is an error.</param>
/// <param name="Code">Its code, often empty for OpenSilver's errors.</param>
/// <param name="Message">What it said.</param>
/// <param name="File">The file, or empty.</param>
/// <param name="Line">The one-based line, or 0.</param>
/// <param name="Column">The one-based column, or 0.</param>
internal sealed record LoggedProblem(bool IsError, string Code, string Message, string File, int Line, int Column);

/// <summary>
/// The MSBuild engine a task runs in, reduced to what OpenSilver's XAML compiler uses: it collects the errors,
/// warnings and messages the task logs, instead of a build log.
/// </summary>
/// <param name="projectFilePath">The project, as the task's node.</param>
internal sealed class XamlBuildEngine(string projectFilePath) : IBuildEngine
{
    private readonly List<LoggedProblem> _problems = [];
    private readonly List<string> _messages = [];

    /// <summary>The errors and warnings, in the order logged.</summary>
    public IReadOnlyList<LoggedProblem> Problems => _problems;

    /// <summary>The messages, such as the time each file took.</summary>
    public IReadOnlyList<string> Messages => _messages;

    /// <inheritdoc />
    public bool ContinueOnError => false;

    /// <inheritdoc />
    public int LineNumberOfTaskNode => 0;

    /// <inheritdoc />
    public int ColumnNumberOfTaskNode => 0;

    /// <inheritdoc />
    public string ProjectFileOfTaskNode => projectFilePath;

    /// <inheritdoc />
    public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs) => false;

    /// <inheritdoc />
#pragma warning disable CS0618 // The interface names the event type MSBuild marks as obsolete; nothing is done with it.
    public void LogCustomEvent(CustomBuildEventArgs e)
#pragma warning restore CS0618
    {
    }

    /// <inheritdoc />
    public void LogErrorEvent(BuildErrorEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        _problems.Add(new LoggedProblem(true, e.Code ?? string.Empty, e.Message ?? string.Empty, e.File ?? string.Empty, e.LineNumber, e.ColumnNumber));
    }

    /// <inheritdoc />
    public void LogWarningEvent(BuildWarningEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        _problems.Add(new LoggedProblem(false, e.Code ?? string.Empty, e.Message ?? string.Empty, e.File ?? string.Empty, e.LineNumber, e.ColumnNumber));
    }

    /// <inheritdoc />
    public void LogMessageEvent(BuildMessageEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.Message is { Length: > 0 } message)
        {
            _messages.Add(message);
        }
    }
}
