using System.Collections;
using Microsoft.Build.Framework;
using TabStudio.Core.Diagnostics;

namespace TabStudio.WindowsCompatibility.Building.Wpf;

/// <summary>
/// What an MSBuild task logs to, outside MSBuild: errors and warnings become TabStudio diagnostics with their file
/// and line, the way the Error List shows MSBuild's.
/// </summary>
/// <param name="projectFile">The project the task builds, for diagnostics without a file.</param>
/// <param name="projectName">The project's name.</param>
internal sealed class TaskBuildEngine(string projectFile, string projectName) : IBuildEngine
{
    private readonly List<BuildDiagnostic> _diagnostics = [];
    private readonly List<string> _messages = [];

    /// <summary>The errors and warnings logged so far.</summary>
    public IReadOnlyList<BuildDiagnostic> Diagnostics => _diagnostics;

    /// <summary>The important messages logged so far, for the build log.</summary>
    public IReadOnlyList<string> Messages => _messages;

    /// <inheritdoc />
    public bool ContinueOnError => false;

    /// <inheritdoc />
    public int LineNumberOfTaskNode => 0;

    /// <inheritdoc />
    public int ColumnNumberOfTaskNode => 0;

    /// <inheritdoc />
    public string ProjectFileOfTaskNode => projectFile;

    /// <inheritdoc />
    public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs) => false;

#pragma warning disable CS0618 // IBuildEngine still declares the custom event method MSBuild has made obsolete.
    /// <inheritdoc />
    public void LogCustomEvent(CustomBuildEventArgs e)
    {
    }
#pragma warning restore CS0618

    /// <inheritdoc />
    public void LogErrorEvent(BuildErrorEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        _diagnostics.Add(Diagnostic(DiagnosticSeverityLevel.Error, e.Code, e.Message, e.File, e.LineNumber, e.ColumnNumber));
    }

    /// <inheritdoc />
    public void LogWarningEvent(BuildWarningEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        _diagnostics.Add(Diagnostic(DiagnosticSeverityLevel.Warning, e.Code, e.Message, e.File, e.LineNumber, e.ColumnNumber));
    }

    /// <inheritdoc />
    public void LogMessageEvent(BuildMessageEventArgs e)
    {
        if (e is { Importance: MessageImportance.High, Message: { } message })
        {
            _messages.Add(message);
        }
    }

    private BuildDiagnostic Diagnostic(DiagnosticSeverityLevel severity, string? code, string? message, string? file, int line, int column)
    {
        var location = string.IsNullOrEmpty(file)
            ? null
            : new DiagnosticLocation(Path.GetFullPath(file, Path.GetDirectoryName(projectFile)!), 0, 0, Math.Max(1, line), Math.Max(1, column));
        return new BuildDiagnostic(string.IsNullOrEmpty(code) ? "MC0000" : code, severity, message ?? string.Empty, location, projectName);
    }
}
