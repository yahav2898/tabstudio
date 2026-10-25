using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TabStudio.App.Services.Agent;
using TabStudio.App.ViewModels;
using TabStudio.App.ViewModels.Agent;
using TabStudio.App.Views;
using TabStudio.Core.Diagnostics;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>The agent tool window against a scripted Claude: settings, proposals, Keep and Undo, and Fix with Agent.</summary>
public sealed class AgentPanelTests
{
    private const string GreeterPath = "src/Greetings/Greeter.cs";
    private const string OldGreeting = "This program was compiled and run on your tablet.";
    private const string NewGreeting = "Built and run on a tablet.";

    [AvaloniaFact]
    public void Without_an_API_key_the_panel_asks_for_one_and_keeps_it()
    {
        using var fixture = new WorkspaceFixture();
        var agent = fixture.Workspace.Agent;

        Assert.True(agent.IsSettingsOpen);
        Assert.False(agent.HasApiKey);

        agent.ApiKeyText = "sk-ant-test";
        agent.InstructionsText = "Use file-scoped namespaces.";
        agent.SaveSettingsCommand.Execute(null);

        Assert.False(agent.IsSettingsOpen);
        Assert.True(agent.HasApiKey);
        var saved = new AgentSettingsStore(fixture.Paths).Load();
        Assert.Equal("sk-ant-test", saved.ApiKey);
        Assert.Equal("Use file-scoped namespaces.", saved.Instructions);
    }

    [AvaloniaFact]
    public void MCP_server_lines_need_a_name_and_an_address()
    {
        using var fixture = new WorkspaceFixture();
        var agent = fixture.Workspace.Agent;
        agent.ApiKeyText = "sk-ant-test";
        agent.McpServersText = "issues";

        agent.SaveSettingsCommand.Execute(null);

        Assert.True(agent.IsSettingsOpen);
        Assert.Contains("issues", agent.ErrorText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task A_proposed_edit_waits_as_a_diff_until_it_is_kept()
    {
        var claude = new FakeClaude()
            .ToolCall("toolu_1", "read_file", new JsonObject { ["path"] = GreeterPath }, text: "I'll look at the greeter.")
            .ToolCall("toolu_2", "edit_file", new JsonObject { ["path"] = GreeterPath, ["old_text"] = OldGreeting, ["new_text"] = NewGreeting })
            .Text("I shortened the greeting.");
        using var http = new HttpClient(claude);
        using var fixture = new WorkspaceFixture(agentHttp: http);
        var root = Path.GetDirectoryName(await fixture.OpenSampleAsync())!;
        var greeter = Path.Combine(root, "src", "Greetings", "Greeter.cs");
        var document = await fixture.Workspace.OpenDocumentAsync(greeter);
        var agent = SignedIn(fixture);

        agent.InputText = "Make the greeting shorter";
        await agent.SendCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("sk-ant-test", claude.ApiKeys[0]);
        Assert.IsType<UserMessageViewModel>(agent.Items[0]);
        Assert.Contains(agent.Items.OfType<ToolCallViewModel>(), c => c.Description == $"Edit {GreeterPath}" && c.IsDone && !c.IsError);
        Assert.Equal("I shortened the greeting.", agent.Items.OfType<AssistantMessageViewModel>().Last().Text);
        Assert.Contains("in ·", agent.UsageText, StringComparison.Ordinal);

        var change = Assert.Single(agent.Changes);
        Assert.Equal("1 file changed", agent.ChangesTitle);
        Assert.Equal("Edited", change.KindLabel);
        Assert.Contains(change.Lines, l => l.IsAdded && l.Text.Contains(NewGreeting, StringComparison.Ordinal));
        Assert.Contains(change.Lines, l => l.IsRemoved && l.Text.Contains(OldGreeting, StringComparison.Ordinal));
        Assert.Contains(OldGreeting, await File.ReadAllTextAsync(greeter), StringComparison.Ordinal);
        Assert.Contains(OldGreeting, document!.Document.Text, StringComparison.Ordinal);

        await agent.KeepCommand.ExecuteAsync(change);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(agent.Changes);
        Assert.Contains(NewGreeting, document.Document.Text, StringComparison.Ordinal);
        Assert.False(document.IsDirty);
        Assert.Contains(NewGreeting, await File.ReadAllTextAsync(greeter), StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Undo_drops_a_proposal_and_leaves_the_file_alone()
    {
        var claude = new FakeClaude()
            .ToolCall("toolu_1", "create_file", new JsonObject { ["path"] = "src/Greetings/Farewell.cs", ["content"] = "namespace Greetings;\n" })
            .Text("Added a file.");
        using var http = new HttpClient(claude);
        using var fixture = new WorkspaceFixture(agentHttp: http);
        var root = Path.GetDirectoryName(await fixture.OpenSampleAsync())!;
        var agent = SignedIn(fixture);

        await agent.AskAsync("Add a farewell");
        Dispatcher.UIThread.RunJobs();
        var change = Assert.Single(agent.Changes);
        Assert.Equal("Added", change.KindLabel);

        agent.UndoCommand.Execute(change);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(agent.Changes);
        Assert.False(File.Exists(Path.Combine(root, "src", "Greetings", "Farewell.cs")));
    }

    [AvaloniaFact]
    public async Task Fix_with_Agent_from_the_Error_List_shows_the_agent_and_asks_about_the_error()
    {
        var claude = new FakeClaude().Text("The variable is misspelled; here is the fix.");
        using var http = new HttpClient(claude);
        using var fixture = new WorkspaceFixture(agentHttp: http);
        var root = Path.GetDirectoryName(await fixture.OpenSampleAsync())!;
        SignedIn(fixture);
        var program = Path.Combine(root, "src", "HelloTablet", "Program.cs");
        var diagnostic = new BuildDiagnostic("CS0103", DiagnosticSeverityLevel.Error, "The name 'nam' does not exist in the current context", new DiagnosticLocation(program, Start: 0, Length: 3, Line: 15, Column: 27), "HelloTablet");
        var item = new ErrorItemViewModel(diagnostic);

        Assert.True(item.CanFixWithAgent);
        fixture.Workspace.ErrorList.FixWithAgentCommand.Execute(item);
        await UiTestHelpers.WaitUntilAsync(() => !fixture.Workspace.Agent.IsBusy && fixture.Workspace.Agent.Items.Count >= 2, because: "the agent's answer");

        Assert.True(fixture.Workspace.IsAgentSelected);
        var asked = Assert.IsType<UserMessageViewModel>(fixture.Workspace.Agent.Items[0]).Text;
        Assert.Contains("CS0103: The name 'nam' does not exist", asked, StringComparison.Ordinal);
        Assert.Contains("In src/HelloTablet/Program.cs, line 15, column 27.", asked, StringComparison.Ordinal);
        Assert.Contains("Project: HelloTablet.", asked, StringComparison.Ordinal);
        Assert.Contains("CS0103", claude.Requests[0].ToJsonString(), StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task A_request_while_the_agent_is_busy_waits_in_the_input_box()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var agent = fixture.Workspace.Agent;

        // Without a key the request cannot be sent yet: it waits for the user, and the settings open.
        await agent.AskAsync("Explain Program.cs");

        Assert.Equal("Explain Program.cs", agent.InputText);
        Assert.True(agent.IsSettingsOpen);
        Assert.Empty(agent.Items);
    }

    [AvaloniaFact]
    public async Task Screenshot_of_the_agent_with_a_proposed_change()
    {
        var claude = new FakeClaude()
            .ToolCall("toolu_1", "read_file", new JsonObject { ["path"] = GreeterPath }, text: "I'll look at how the greeting is built.")
            .ToolCall("toolu_2", "edit_file", new JsonObject { ["path"] = GreeterPath, ["old_text"] = OldGreeting, ["new_text"] = NewGreeting })
            .ToolCall("toolu_3", "get_diagnostics", [])
            .Text("I shortened the greeting in Greeter.cs. The solution still builds without errors. Keep the change to apply it.");
        using var http = new HttpClient(claude);
        using var fixture = new WorkspaceFixture(agentHttp: http);
        await fixture.OpenSampleAsync();
        var agent = SignedIn(fixture);
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = fixture.Workspace } };
        window.Show();
        fixture.Workspace.ShowAgentCommand.Execute(null);

        await agent.AskAsync("Make the greeting shorter, it doesn't fit on a phone.");
        Dispatcher.UIThread.RunJobs();
        agent.Changes[0].IsExpanded = true;

        UiTestHelpers.SaveScreenshot(window, "agent-01-proposed-change");

        Assert.True(fixture.Workspace.IsAgentSelected);
        Assert.Single(agent.Changes);

        // Each answer streams in two pieces, and shows as one message however the UI thread interleaves them.
        Assert.Equal(
            ["I'll look at how the greeting is built.", "I shortened the greeting in Greeter.cs. The solution still builds without errors. Keep the change to apply it."],
            agent.Items.OfType<AssistantMessageViewModel>().Select(m => m.Text));
        window.Close();
    }

    private static AgentPanelViewModel SignedIn(WorkspaceFixture fixture)
    {
        var agent = fixture.Workspace.Agent;
        agent.ApiKeyText = "sk-ant-test";
        agent.SaveSettingsCommand.Execute(null);
        return agent;
    }
}
