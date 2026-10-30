using TabStudio.LiveTesting;
using TabStudio.LiveTesting.Results;
using TabStudio.TestRunner;

namespace TabStudio.App.ViewModels;

/// <summary>Live results: the editors' markers, the counts, opening tests, and including or excluding them.</summary>
public sealed partial class LiveTestingViewModel
{
    private static readonly TimeSpan RefreshDelay = TimeSpan.FromMilliseconds(150);
    private bool _refreshScheduled;

    /// <summary>Shows the markers of a document that just opened, while Live Unit Testing is on.</summary>
    /// <param name="document">The document.</param>
    public void OnDocumentOpened(DocumentViewModel document)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.LiveTesting = this;
        if (_engine is { } engine && document.FileType.IsRoslyn && !document.IsComparison)
        {
            document.ShowLiveTestCoverage(engine.Index.GetFile(document.FilePath));
        }
    }

    /// <summary>Updates every open document's markers and the counts from the latest results.</summary>
    public void RefreshNow()
    {
        _refreshScheduled = false;
        if (_engine is not { } engine)
        {
            return;
        }

        foreach (var document in _host.OpenDocuments.Where(d => d.FileType.IsRoslyn && !d.IsComparison))
        {
            document.ShowLiveTestCoverage(engine.Index.GetFile(document.FilePath));
        }

        var projects = _host.TestProjects.Select(p => p.FilePath).ToHashSet(StringComparer.Ordinal);
        var tests = engine.Index.Tests.Where(t => projects.Contains(t.Key.ProjectFilePath)).ToList();
        var included = tests.Where(t => Inclusion.IsIncluded(t.Key.ProjectFilePath, t.Test)).ToList();
        PassedCount = included.Count(t => t.Result?.Outcome == TestOutcome.Passed);
        FailedCount = included.Count(t => t.Result?.Outcome is TestOutcome.Failed or TestOutcome.Errored);
        ExcludedCount = tests.Count - included.Count;
    }

    /// <inheritdoc />
    public async Task GoToTestAsync(TestKey test)
    {
        var tests = _host.Tests;
        var entry = tests.Entries.FirstOrDefault(e => e.ProjectFilePath == test.ProjectFilePath && e.Id == test.TestId);
        if (entry is not null)
        {
            tests.Reveal(entry);
        }

        var location = entry?.Location ?? _engine?.Index.Tests.FirstOrDefault(t => t.Key == test)?.Test.Location;
        if (location is not null)
        {
            await _host.NavigateToAsync(location.FilePath, location.Line, location.Column);
        }
    }

    /// <summary>
    /// Includes or excludes a project, namespace, class, method or test. Excluded tests never run live and count
    /// for no line; an excluded project under test gets no markers. Included tests run at once.
    /// </summary>
    /// <param name="projectFilePath">The project.</param>
    /// <param name="name">What in the project, as Test Explorer names it, or an empty string for the whole project.</param>
    /// <param name="include">Whether to include it.</param>
    public void SetInclusion(string projectFilePath, string name, bool include)
    {
        ArgumentNullException.ThrowIfNull(projectFilePath);
        ArgumentNullException.ThrowIfNull(name);
        Inclusion.Set(projectFilePath, name, include);
        if (_engine is not { } engine)
        {
            return;
        }

        var affected = _host.Tests.Entries
            .Where(e => e.ProjectFilePath == projectFilePath && LiveTestInclusion.IsWithin(e.Id, name))
            .Select(e => e.Id)
            .ToList();
        if (include)
        {
            engine.Invalidate(projectFilePath, affected.Where(id => Inclusion.IsIncluded(projectFilePath, id)));
            RunAfter(TimeSpan.FromMilliseconds(100));
        }
        else
        {
            engine.Forget(projectFilePath, affected);
            if (name.Length == 0 && !_host.TestProjects.Any(p => p.FilePath == projectFilePath))
            {
                // A project under test: the next build leaves it without coverage.
                RunAfter(TimeSpan.FromMilliseconds(100));
            }
        }

        ScheduleRefresh();
    }

    /// <summary>Whether a test, class, namespace or project is included, for the context menus.</summary>
    /// <param name="projectFilePath">The project.</param>
    /// <param name="name">What in it, or an empty string for the whole project.</param>
    public bool IsIncluded(string projectFilePath, string name) => Inclusion.IsIncluded(projectFilePath, name);

    /// <summary>The name of a test for a status message.</summary>
    private string NameOf(TestKey test) =>
        _host.Tests.Entries.FirstOrDefault(e => e.ProjectFilePath == test.ProjectFilePath && e.Id == test.TestId)?.DisplayName ?? test.TestId;

    /// <summary>The engine's results changed, on any thread: refreshes the markers shortly, once for many results.</summary>
    private void OnIndexChanged(object? sender, EventArgs e) => _ui.Post(ScheduleRefresh);

    private void ScheduleRefresh()
    {
        if (_refreshScheduled || _engine is null)
        {
            return;
        }

        _refreshScheduled = true;
        _ = RefreshLaterAsync();
    }

    private async Task RefreshLaterAsync()
    {
        await Task.Delay(RefreshDelay);
        if (_refreshScheduled)
        {
            RefreshNow();
        }
    }
}
