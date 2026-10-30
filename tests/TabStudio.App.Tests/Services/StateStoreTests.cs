using TabStudio.App.Services;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Services;

public sealed class StateStoreTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly AppPaths _paths;

    public StateStoreTests()
    {
        _paths = new AppPaths(_temp.Resolve("data"));
    }

    [Fact]
    public void An_unreadable_session_is_kept_aside_instead_of_being_overwritten()
    {
        // The session holds the only copy of unsaved text after Android ended the app; a damaged one was replaced by
        // the next save without a trace.
        Directory.CreateDirectory(Path.GetDirectoryName(_paths.SessionFile)!);
        File.WriteAllText(_paths.SessionFile, "{ \"WorkspacePath\": \"/w\", \"Documents\": [ { \"Path\": \"a.cs\", \"UnsavedText\": \"mine");

        Assert.Null(new SessionStore(_paths).Load());

        var kept = Assert.Single(Directory.EnumerateFiles(Path.GetDirectoryName(_paths.SessionFile)!, "session.unreadable-*.json"));
        Assert.Contains("mine", File.ReadAllText(kept), StringComparison.Ordinal);
        Assert.False(File.Exists(_paths.SessionFile));
    }

    [Fact]
    public void Recent_workspaces_are_newest_first_and_survive_a_restart()
    {
        var first = _temp.Write("one/One.slnx", "<Solution />");
        var second = _temp.Write("two/Two.slnx", "<Solution />");
        var recent = new RecentWorkspaces(_paths);
        recent.Add(new RecentWorkspace(first, "One", "1 project", DateTimeOffset.Now.AddDays(-1)));
        recent.Add(new RecentWorkspace(second, "Two", "2 projects", DateTimeOffset.Now));
        recent.Add(new RecentWorkspace(first, "One", "1 project", DateTimeOffset.Now));

        var reloaded = new RecentWorkspaces(_paths).GetAll();

        Assert.Equal(new[] { "One", "Two" }, reloaded.Select(r => r.Name));
    }

    [Fact]
    public void Recent_workspaces_whose_files_are_gone_are_hidden()
    {
        var recent = new RecentWorkspaces(_paths);
        recent.Add(new RecentWorkspace(_temp.Resolve("missing/Missing.slnx"), "Missing", "", DateTimeOffset.Now));

        Assert.Empty(recent.GetAll());
    }

    [Fact]
    public void A_damaged_recent_list_starts_empty_instead_of_failing()
    {
        File.WriteAllText(_paths.RecentWorkspacesFile, "{ not json");

        Assert.Empty(new RecentWorkspaces(_paths).GetAll());
    }

    [Fact]
    public void Sessions_round_trip_with_unsaved_text()
    {
        var solution = _temp.Write("app/App.slnx", "<Solution />");
        var store = new SessionStore(_paths);
        var session = new SessionState(solution, [new SessionDocument(_temp.Resolve("app/Program.cs"), 12, "unsaved")], _temp.Resolve("app/Program.cs"), null);

        store.Save(session);
        var loaded = new SessionStore(_paths).Load();

        Assert.NotNull(loaded);
        Assert.Equal(solution, loaded.WorkspacePath);
        var document = Assert.Single(loaded.Documents);
        Assert.Equal("unsaved", document.UnsavedText);
        Assert.Equal(12, document.CaretOffset);
    }

    [Fact]
    public void A_cleared_session_is_gone()
    {
        var store = new SessionStore(_paths);
        store.Save(new SessionState(_temp.Write("App.slnx", "<Solution />"), [], null, null));

        store.Clear();

        Assert.Null(store.Load());
    }

    public void Dispose() => _temp.Dispose();
}
