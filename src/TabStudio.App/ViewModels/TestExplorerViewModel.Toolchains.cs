using TabStudio.Core.Workspaces;
using TabStudio.TestRunner;
using TabStudio.Toolchains.Testing;

namespace TabStudio.App.ViewModels;

/// <summary>
/// The tests of PHP, Kotlin, Java and Swift projects: found in their source when the workspace opens, and run by their
/// own runners (PHPUnit, Gradle, Maven, <c>swift test</c>), whose JUnit XML reports become results.
/// </summary>
public sealed partial class TestExplorerViewModel
{
    /// <summary>Shows the tests found in a toolchain project's source.</summary>
    /// <param name="project">The project.</param>
    /// <param name="tests">Its tests.</param>
    public void SetToolchainSourceTests(ProjectInfo project, IReadOnlyList<ToolchainTestCase> tests)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(tests);
        var classes = tests
            .GroupBy(t => t.ClassName, StringComparer.Ordinal)
            .Select(group =>
            {
                var first = group.First();
                var dot = group.Key.LastIndexOf('.');
                return new SourceTestClass
                {
                    FullName = group.Key,
                    Name = dot < 0 ? group.Key : group.Key[(dot + 1)..],
                    NamespaceName = dot < 0 ? string.Empty : group.Key[..dot],
                    Framework = TestFramework.External,
                    Location = new TestSourceLocation(first.File, first.ClassLine),
                    Methods = [.. group.DistinctBy(t => t.MethodName, StringComparer.Ordinal).Select(t => new SourceTestMethod
                    {
                        FullyQualifiedName = t.Id,
                        TypeFullName = t.ClassName,
                        MethodName = t.MethodName,
                        DisplayName = t.MethodName,
                        Framework = TestFramework.External,
                        Location = new TestSourceLocation(t.File, t.Line),
                    })],
                };
            })
            .ToList();
        SetSourceTests(project, classes);
    }

    /// <summary>Shows the results of a toolchain project's test run.</summary>
    /// <param name="project">The project.</param>
    /// <param name="results">The results in its runner's reports.</param>
    /// <param name="wholeProject">Whether every test of the project ran, so tests without a result are no longer there.</param>
    /// <param name="elapsed">How long the run took.</param>
    /// <remarks>Called on the UI thread, after the runner ends.</remarks>
    public void ReportToolchainResults(ProjectInfo project, IReadOnlyList<ToolchainTestResult> results, bool wholeProject, TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(results);
        var known = Find(project.FilePath);
        if (known is null)
        {
            known = new ProjectTests(project.FilePath, project.Name);
            _projects.Add(known);
        }

        var converted = results.GroupBy(r => r.Id, StringComparer.Ordinal).Select(g => ConvertToolchainResult(project, g.Last())).ToArray();
        var ids = converted.Select(r => r.Test.Id).ToHashSet(StringComparer.Ordinal);
        known.Built = wholeProject
            ? [.. converted.Select(r => TestEntry.FromBuild(project.FilePath, project.Name, r.Test))]
            : [.. (known.Built ?? []).Where(e => !ids.Contains(e.Id)), .. converted.Select(r => TestEntry.FromBuild(project.FilePath, project.Name, r.Test))];
        HasTestProjects = _projects.Count > 0;
        MergeEntries();
        if (IsRunning)
        {
            ReportQueued(project.FilePath, converted.Select(r => r.Test).ToArray());
        }

        foreach (var result in converted)
        {
            ReportFinished(project.FilePath, result);
        }

        if (IsRunning)
        {
            _runSummaries.Add(new TestRunSummary { Total = converted.Length, Results = converted, Duration = elapsed });
        }

        RefreshCommands();
    }

    private static TestResult ConvertToolchainResult(ProjectInfo project, ToolchainTestResult result)
    {
        var location = result.File is { Length: > 0 } file && result.Line is { } line
            ? new TestSourceLocation(Path.GetFullPath(file, project.DirectoryPath), line) : null;
        var test = new TestCase
        {
            Id = result.Id,
            FullyQualifiedName = result.ClassName + "." + result.MethodName,
            DisplayName = result.Name,
            Framework = TestFramework.External,
            AssemblyName = project.Name,
            TypeFullName = result.ClassName,
            MethodName = result.MethodName,
            DataRow = result.Name == result.MethodName ? null : result.Name[result.MethodName.Length..].Trim(),
            Location = location,
            SkipReason = result.Outcome == ToolchainTestOutcome.Skipped ? result.Message ?? "Skipped" : null,
        };
        return new TestResult
        {
            Test = test,
            Outcome = result.Outcome switch
            {
                ToolchainTestOutcome.Passed => TestOutcome.Passed,
                ToolchainTestOutcome.Failed => TestOutcome.Failed,
                ToolchainTestOutcome.Skipped => TestOutcome.Skipped,
                _ => TestOutcome.Errored,
            },
            Duration = result.Duration,
            Message = result.Outcome == ToolchainTestOutcome.Skipped ? null : result.Message,
            FailureLocation = result.Outcome is ToolchainTestOutcome.Failed or ToolchainTestOutcome.Errored ? location : null,
        };
    }
}
