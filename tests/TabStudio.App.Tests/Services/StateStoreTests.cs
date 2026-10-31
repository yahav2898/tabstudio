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

    [Fact]
    public void Unsaved_text_of_a_workspace_that_is_gone_is_kept_in_the_recovered_folder()
    {
        // A workspace on storage that can no longer be read (a revoked permission, a removed card) was skipped at
        // startup, and its unsaved text was overwritten by the next workspace's session.
        var store = new SessionStore(_paths);
        var gone = _temp.Resolve("gone/App.slnx");
        store.Save(new SessionState(gone, [new SessionDocument(_temp.Resolve("gone/Program.cs"), 0, "my edits"), new SessionDocument(_temp.Resolve("gone/Saved.cs"), 0, null)], null, null));

        var reloaded = new SessionStore(_paths);
        Assert.Null(reloaded.Load());

        var kept = Assert.Single(reloaded.LastRecovered);
        Assert.StartsWith(reloaded.RecoveredFolder, kept, StringComparison.Ordinal);
        Assert.EndsWith(".cs", kept, StringComparison.Ordinal);
        Assert.Equal("my edits", File.ReadAllText(kept));
        Assert.False(File.Exists(_paths.SessionFile));
    }

    [Fact]
    public void A_restore_that_never_finished_is_reported_at_the_next_start()
    {
        // Out of memory while reopening a large solution on a small tablet ended the app at every start.
        var store = new SessionStore(_paths);
        Assert.Null(store.InterruptedRestore);

        store.BeginRestore("/w/App.slnx");
        Assert.Equal("/w/App.slnx", new SessionStore(_paths).InterruptedRestore);

        store.EndRestore();
        Assert.Null(new SessionStore(_paths).InterruptedRestore);
    }

    [Fact]
    public void A_session_without_documents_loads_with_none()
    {
        var solution = _temp.Write("app/App.slnx", "<Solution />");
        File.WriteAllText(_paths.SessionFile, $$"""{ "workspacePath": {{System.Text.Json.JsonSerializer.Serialize(solution)}} }""");

        var loaded = new SessionStore(_paths).Load();

        Assert.NotNull(loaded);
        Assert.Empty(loaded.Documents);
    }

    [Fact]
    public void A_background_save_that_finishes_late_does_not_replace_a_newer_session()
    {
        var solution = _temp.Write("app/App.slnx", "<Solution />");
        var store = new SessionStore(_paths);
        var older = store.NextSequence();
        var newer = store.NextSequence();

        store.Save(new SessionState(solution, [new SessionDocument(_temp.Resolve("app/Program.cs"), 0, "newer")], null, null), newer);
        store.Save(new SessionState(solution, [new SessionDocument(_temp.Resolve("app/Program.cs"), 0, "older")], null, null), older);

        Assert.Equal("newer", Assert.Single(new SessionStore(_paths).Load()!.Documents).UnsavedText);

        var late = store.NextSequence();
        store.Clear();
        store.Save(new SessionState(solution, [], null, null), late);
        Assert.Null(new SessionStore(_paths).Load());
    }

    public void Dispose() => _temp.Dispose();
}
