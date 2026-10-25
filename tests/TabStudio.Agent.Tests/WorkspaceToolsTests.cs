using System.Text.Json.Nodes;
using TabStudio.Agent.Tools;
using TabStudio.Testing;

namespace TabStudio.Agent.Tests;

public sealed class WorkspaceToolsTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly FolderWorkspace _workspace;
    private readonly ChangeSet _changes = new();
    private readonly WorkspaceTools _tools;

    public WorkspaceToolsTests()
    {
        _workspace = new FolderWorkspace(_temp);
        _tools = new WorkspaceTools(_workspace, _changes);
        _temp.Write("src/App/Program.cs", "using Lib;\nConsole.WriteLine(Greeter.Greet());\n");
        _temp.Write("src/Lib/Greeter.cs", "namespace Lib;\npublic static class Greeter\n{\n    public static string Greet() => \"Hello\";\n}\n");
        _temp.Write("README.md", "# Demo\n");
    }

    private Task<ToolOutcome> Call(string name, string json) =>
        _tools.All.Single(t => t.Definition.Name == name).InvokeAsync((JsonObject)JsonNode.Parse(json)!, CancellationToken.None);

    [Fact]
    public async Task Lists_files_by_folder_and_pattern()
    {
        Assert.Equal("README.md\nsrc/App/Program.cs\nsrc/Lib/Greeter.cs", (await Call("list_files", "{}")).Content);
        Assert.Equal("src/Lib/Greeter.cs", (await Call("list_files", """{"folder":"src/Lib"}""")).Content);
        Assert.Equal("src/App/Program.cs\nsrc/Lib/Greeter.cs", (await Call("list_files", """{"pattern":"*.cs"}""")).Content);
        Assert.Equal("No files match.", (await Call("list_files", """{"folder":"docs"}""")).Content);
    }

    [Fact]
    public async Task Reads_files_with_line_numbers_and_ranges()
    {
        var all = await Call("read_file", """{"path":"src/Lib/Greeter.cs"}""");
        Assert.Contains("     4\t    public static string Greet() => \"Hello\";", all.Content, StringComparison.Ordinal);

        var range = await Call("read_file", """{"path":"src/Lib/Greeter.cs","start_line":2,"end_line":3}""");
        Assert.StartsWith("     2\tpublic static class Greeter\n     3\t{\n", range.Content, StringComparison.Ordinal);
        Assert.Contains("more lines", range.Content, StringComparison.Ordinal);

        Assert.True((await Call("read_file", """{"path":"missing.cs"}""")).IsError);
        Assert.True((await Call("read_file", """{"path":"../outside.txt"}""")).IsError);
    }

    [Fact]
    public async Task Searches_with_regular_expressions()
    {
        var result = await Call("search", """{"pattern":"Greet\\(\\)","file_pattern":"**/*.cs"}""");

        Assert.Equal("src/App/Program.cs:2: Console.WriteLine(Greeter.Greet());\nsrc/Lib/Greeter.cs:4: public static string Greet() => \"Hello\";", result.Content);
        Assert.True((await Call("search", """{"pattern":"("}""")).IsError);
    }

    [Fact]
    public async Task Edits_are_pending_until_approved_and_are_read_back()
    {
        var edit = await Call("edit_file", """{"path":"src/Lib/Greeter.cs","old_text":"\"Hello\"","new_text":"\"Hello, tablet\""}""");

        Assert.False(edit.IsError);
        Assert.Contains("\"Hello\"", File.ReadAllText(_temp.Resolve("src/Lib/Greeter.cs")), StringComparison.Ordinal);
        Assert.Contains("Hello, tablet", (await Call("read_file", """{"path":"src/Lib/Greeter.cs"}""")).Content, StringComparison.Ordinal);
        var change = Assert.Single(_changes.Pending);
        Assert.Equal("src/Lib/Greeter.cs", change.RelativePath);
        Assert.Contains("\"Hello\"", change.Original, StringComparison.Ordinal);
        Assert.Contains("Hello, tablet", change.Proposed, StringComparison.Ordinal);

        // A second edit to the same file keeps the original from before the first.
        await Call("edit_file", """{"path":"src/Lib/Greeter.cs","old_text":"Hello, tablet","new_text":"Hi"}""");
        Assert.Contains("\"Hello\"", Assert.Single(_changes.Pending).Original, StringComparison.Ordinal);

        // Editing it back removes the change.
        await Call("edit_file", """{"path":"src/Lib/Greeter.cs","old_text":"\"Hi\"","new_text":"\"Hello\""}""");
        Assert.Empty(_changes.Pending);
    }

    [Fact]
    public async Task Edits_must_match_exactly_once()
    {
        Assert.True((await Call("edit_file", """{"path":"src/Lib/Greeter.cs","old_text":"Goodbye","new_text":"x"}""")).IsError);
        Assert.True((await Call("edit_file", """{"path":"src/Lib/Greeter.cs","old_text":"public","new_text":"internal"}""")).IsError);
        Assert.False((await Call("edit_file", """{"path":"src/Lib/Greeter.cs","old_text":"public","new_text":"internal","replace_all":true}""")).IsError);
        Assert.DoesNotContain("public", _changes.Pending.Single().Proposed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Edits_to_files_with_windows_line_endings_apply()
    {
        _temp.Write("crlf.txt", "one\r\ntwo\r\nthree\r\n");

        var edit = await Call("edit_file", """{"path":"crlf.txt","old_text":"one\ntwo","new_text":"one\n2"}""");

        Assert.False(edit.IsError, edit.Content);
        Assert.Equal("one\r\n2\r\nthree\r\n", _changes.Pending.Single().Proposed);
    }

    [Fact]
    public async Task New_and_deleted_files_show_up_in_listings()
    {
        await Call("create_file", """{"path":"src/Lib/Farewell.cs","content":"namespace Lib;\n"}""");
        await Call("delete_file", """{"path":"README.md"}""");

        Assert.Equal("src/App/Program.cs\nsrc/Lib/Farewell.cs\nsrc/Lib/Greeter.cs", (await Call("list_files", "{}")).Content);
        Assert.True((await Call("read_file", """{"path":"README.md"}""")).IsError);
        Assert.True(File.Exists(_temp.Resolve("README.md")));
        Assert.Equal(2, _changes.Pending.Count);
    }

    [Fact]
    public async Task Diagnostics_and_tests_see_the_pending_changes()
    {
        await Call("create_file", """{"path":"New.cs","content":"class New { int x = \"no\"; }"}""");
        var file = _temp.Resolve("New.cs");
        _workspace.Diagnostics.Add(new TabStudio.Core.Diagnostics.BuildDiagnostic(
            "CS0029", TabStudio.Core.Diagnostics.DiagnosticSeverityLevel.Error, "Cannot implicitly convert type 'string' to 'int'",
            new TabStudio.Core.Diagnostics.DiagnosticLocation(file, 20, 4, 1, 21)));

        var diagnostics = await Call("get_diagnostics", "{}");

        Assert.Equal("1 errors, 0 warnings\nNew.cs(1,21): error CS0029: Cannot implicitly convert type 'string' to 'int'\n", diagnostics.Content);
        Assert.True(_workspace.LastOverlay!.ContainsKey(file));
        Assert.Equal("3 passed, 0 failed (Lib)", (await Call("run_tests", """{"filter":"Lib"}""")).Content);
    }

    public void Dispose() => _temp.Dispose();
}
