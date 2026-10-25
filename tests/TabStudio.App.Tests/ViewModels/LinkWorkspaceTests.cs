using System.Globalization;
using Avalonia.Headless.XUnit;
using TabStudio.App.Services.Link;
using TabStudio.Compilation.Features;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>
/// The IDE with TabStudio Link, linked to a host in the same process on the loopback address: the host's answers match
/// this device's, programs built there run here, and losing the host falls back to this device until it is back.
/// </summary>
public sealed class LinkWorkspaceTests
{
    private const string HostName = "Test host";

    [AvaloniaFact]
    public async Task A_linked_host_finds_the_same_errors_as_this_device()
    {
        using var local = new WorkspaceFixture();
        using var tablet = new WorkspaceFixture(link: true);
        await using var host = tablet.Link!.CreateLoopbackHost(HostName);
        await local.OpenSampleAsync();
        await tablet.OpenSampleAsync();
        await LinkAsync(tablet, host);

        var expected = await MistypeAsync(local);
        var requests = host.RequestCount;
        var actual = await MistypeAsync(tablet);

        Assert.Contains(actual, d => d.StartsWith("CS0103 ", StringComparison.Ordinal));
        Assert.Equal(expected, actual);
        Assert.True(host.RequestCount > requests, "The host analyzed the document.");
        Assert.True(tablet.Workspace.IsLinkedToHost);
        Assert.StartsWith(HostName, tablet.Workspace.LinkStatusText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Completion_and_rename_on_a_linked_host_match_this_device()
    {
        using var local = new WorkspaceFixture();
        using var tablet = new WorkspaceFixture(link: true);
        await using var host = tablet.Link!.CreateLoopbackHost(HostName);
        await local.OpenSampleAsync();
        await tablet.OpenSampleAsync();
        await LinkAsync(tablet, host);

        var localItems = await CompleteAfterGreeterAsync(local);
        var linkedItems = await CompleteAfterGreeterAsync(tablet);
        var localRename = await RenameGreetAsync(local);
        var linkedRename = await RenameGreetAsync(tablet);

        Assert.Contains("Greet", linkedItems);
        Assert.Equal(localItems, linkedItems);
        Assert.True(linkedRename.Count >= 2, "Greet is declared in one file and called in another.");
        Assert.Equal(localRename, linkedRename);
    }

    [AvaloniaFact]
    public async Task F5_builds_on_the_linked_host_and_runs_the_program_here()
    {
        using var tablet = new WorkspaceFixture(link: true);
        await using var host = tablet.Link!.CreateLoopbackHost(HostName);
        await tablet.OpenSampleAsync();
        await LinkAsync(tablet, host);
        tablet.AnswerInput("Ada");
        var requests = host.RequestCount;

        await tablet.Workspace.RunCommand.ExecuteAsync(null);
        var output = tablet.Workspace.Output.Text;

        Assert.Contains("========== Build: 1 succeeded, 0 failed ==========", output, StringComparison.Ordinal);
        Assert.Contains("Ada! This program was compiled and run on your tablet.", output, StringComparison.Ordinal);
        Assert.Contains("has exited with code 0", output, StringComparison.Ordinal);
        Assert.True(host.RequestCount > requests, "The host built the program.");
        Assert.True(tablet.Workspace.IsLinkedToHost);
    }

    [AvaloniaFact]
    public async Task Losing_the_host_falls_back_to_this_device_and_the_link_comes_back_by_itself()
    {
        using var tablet = new WorkspaceFixture(link: true);
        await using var host = tablet.Link!.CreateLoopbackHost(HostName);
        await tablet.OpenSampleAsync();
        await LinkAsync(tablet, host);
        var workspace = tablet.Workspace;
        var language = Assert.IsType<LinkedLanguageService>(workspace.LanguageService);

        await host.StopAsync();
        await UiTestHelpers.WaitUntilAsync(() => !language.IsLinked && workspace.IsLinkReconnecting, because: "the fallback to this device");
        var whileAway = await MistypeAsync(tablet);

        Assert.Equal($"Reconnecting to {HostName}", workspace.LinkStatusText);
        Assert.Equal($"Lost {HostName} · compiling on this device", workspace.StatusText);
        Assert.Contains(whileAway, d => d.StartsWith("CS0103 ", StringComparison.Ordinal));

        var requests = host.RequestCount;
        host.Restart();
        await UiTestHelpers.WaitUntilAsync(() => language.IsLinked && workspace.IsLinkedToHost, TimeSpan.FromSeconds(90), "the link to come back");
        var document = workspace.ActiveDocument!;
        await document.AnalyzeNowAsync();

        // The edit made while the host was away reached it.
        Assert.Contains(document.Diagnostics, d => d.Id == "CS0103");
        Assert.True(host.RequestCount > requests, "The host loaded and analyzed the workspace again.");
        Assert.Equal($"Linked to {HostName}", workspace.StatusText);
    }

    /// <summary>Pairs the fixture's link with the host, as scanning its QR code does, and waits until the workspace follows.</summary>
    internal static async Task LinkAsync(WorkspaceFixture tablet, LinkLoopbackHost host)
    {
        var language = Assert.IsType<LinkedLanguageService>(tablet.Workspace.LanguageService);
        Assert.True(await tablet.Link!.PairAsync(host.Invitation), tablet.Link.ClientError);
        await UiTestHelpers.WaitUntilAsync(() => language.IsLinked && tablet.Workspace.IsLinkedToHost, because: "the workspace to move to the host");
    }

    /// <summary>Misspells <c>greeter</c> in Program.cs and returns the document's errors, without paths.</summary>
    private static async Task<List<string>> MistypeAsync(WorkspaceFixture fixture)
    {
        var document = fixture.Workspace.ActiveDocument!;
        var offset = document.Document.Text.IndexOf("greeter.Greet()", StringComparison.Ordinal);
        document.Document.Replace(offset, "greeter".Length, "greeterr");
        await document.AnalyzeNowAsync();
        return [.. document.Diagnostics.Select(d => string.Create(CultureInfo.InvariantCulture, $"{d.Id} {d.Location?.Line}:{d.Location?.Column} {d.Message}"))];
    }

    private static async Task<List<string>> CompleteAfterGreeterAsync(WorkspaceFixture fixture)
    {
        var document = fixture.Workspace.ActiveDocument!;
        var position = document.Document.Text.IndexOf("greeter.Greet()", StringComparison.Ordinal) + "greeter.".Length;
        var result = await fixture.Workspace.LanguageService!.Features.GetCompletionsAsync(document.FilePath, position, CompletionTrigger.Invoke, CancellationToken.None);
        return [.. result.Items.Select(i => i.DisplayText)];
    }

    private static async Task<List<string>> RenameGreetAsync(WorkspaceFixture fixture)
    {
        var document = fixture.Workspace.ActiveDocument!;
        var root = fixture.Workspace.Workspace!.RootDirectory;
        var position = document.Document.Text.IndexOf("greeter.Greet()", StringComparison.Ordinal) + "greeter.".Length;
        var result = await fixture.Workspace.LanguageService!.Features.GetRenameEditsAsync(document.FilePath, position, "SayHello", null, CancellationToken.None);
        Assert.True(result.Success, result.Error);
        return [.. result.Edits.Select(e => $"{Path.GetRelativePath(root, e.FilePath)}: {string.Join(", ", e.Edits)}").Order(StringComparer.Ordinal)];
    }
}
