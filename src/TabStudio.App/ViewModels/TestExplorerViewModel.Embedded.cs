using TabStudio.Core.Workspaces;
using TabStudio.TestRunner;
using TabStudio.Toolchains.Embedded.Testing;

namespace TabStudio.App.ViewModels;

public sealed partial class TestExplorerViewModel
{
    /// <summary>Shows native host/board test outcomes in the ordinary Test Explorer with per-board identities.</summary>
    /// <param name="project">The embedded project the native harness belongs to.</param>
    /// <param name="results">Final parsed results, including transport and incomplete-harness failures.</param>
    /// <remarks>Called on the UI thread, after the native command or device transport ends.</remarks>
    public void ReportEmbeddedResults(ProjectInfo project, IReadOnlyList<EmbeddedTestResult> results)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(results);
        var known = Find(project.FilePath);
        if (known is null)
        {
            known = new ProjectTests(project.FilePath, project.Name);
            _projects.Add(known);
        }
        var converted = results.GroupBy(r => r.Id, StringComparer.Ordinal).Select(g => ConvertEmbeddedResult(project, g.Last())).ToArray();
        var scopes = results.Select(r => (r.BoardId, Harness: r.Format.ToString())).ToHashSet();
        known.Built = [.. (known.Built ?? []).Where(entry => !scopes.Contains((
            entry.Traits.FirstOrDefault(t => t.Name == "Board")?.Value ?? "",
            entry.Traits.FirstOrDefault(t => t.Name == "Harness")?.Value ?? ""))),
            .. converted.Select(r => TestEntry.FromBuild(project.FilePath, project.Name, r.Test))];
        HasTestProjects = _projects.Count > 0;
        MergeEntries();
        foreach (var result in converted)
        {
            ReportFinished(project.FilePath, result);
        }
        RefreshCommands();
    }

    private static TestResult ConvertEmbeddedResult(ProjectInfo project, EmbeddedTestResult result)
    {
        var location = result.File is { } file && result.Line is { } line
            && !(file.Length > 2 && char.IsAsciiLetter(file[0]) && file[1] == ':' && !OperatingSystem.IsWindows())
            ? new TestSourceLocation(Path.GetFullPath(file, project.DirectoryPath), line) : null;
        var test = new TestCase
        {
            Id = result.Id,
            FullyQualifiedName = result.Format + "." + result.Name,
            DisplayName = result.Name + " [" + result.BoardId + "]",
            Framework = TestFramework.External,
            AssemblyName = project.Name,
            TypeFullName = "Embedded." + result.Format,
            MethodName = result.Name,
            Location = location,
            Traits = [new TestTrait("Board", result.BoardId), new TestTrait("Harness", result.Format.ToString())],
            SkipReason = result.Outcome == EmbeddedTestOutcome.Skipped ? result.Message : null,
        };
        return new TestResult
        {
            Test = test,
            Outcome = result.Outcome switch
            {
                EmbeddedTestOutcome.Passed => TestOutcome.Passed,
                EmbeddedTestOutcome.Failed => TestOutcome.Failed,
                EmbeddedTestOutcome.Skipped => TestOutcome.Skipped,
                _ => TestOutcome.Errored,
            },
            Duration = result.Duration,
            Message = result.Message,
            FailureLocation = result.Outcome is EmbeddedTestOutcome.Failed or EmbeddedTestOutcome.Errored ? location : null,
        };
    }
}
