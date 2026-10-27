using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Live;
using TabStudio.Live.Sessions;

namespace TabStudio.App.Tests.Live;

/// <summary>
/// TabStudio Live in the IDE, with two and five participants in one process over the real transport: each has its own
/// identity, its own Link and Live services and its own workspace, as on separate tablets, and they meet on the loopback
/// address through the same TLS connections and pairing as on Wi-Fi.
/// </summary>
public sealed class LiveSessionTests
{
    // Lines of the sample's Program.cs.
    private const int CreateGreeter = 15;
    private const int Greet = 16;

    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    [AvaloniaFact]
    public async Task Two_devices_type_at_once_and_end_with_the_same_text()
    {
        await using var host = await LiveParticipant.HostAsync();
        await using var guest = await LiveParticipant.JoinAsync(host, "Noam's phone");
        var hostProgram = host.Workspace.ActiveDocument!;
        Assert.Equal("Program.cs", hostProgram.FileName);
        var guestProgram = await guest.OpenSharedAsync(guest.PathOf(host, hostProgram.FilePath));
        Assert.Equal(hostProgram.Document.Text, guestProgram.Document.Text);

        // Both type a line at once, a key at a time, the host at the top and the guest at the bottom.
        const string hostLine = "// Dana was here\n";
        const string guestLine = "\n// Noam was here too";
        for (var i = 0; i < Math.Max(hostLine.Length, guestLine.Length); i++)
        {
            if (i < hostLine.Length)
            {
                hostProgram.Document.Insert(i, hostLine[i].ToString(CultureInfo.InvariantCulture));
            }

            if (i < guestLine.Length)
            {
                guestProgram.Document.Insert(guestProgram.Document.TextLength, guestLine[i].ToString(CultureInfo.InvariantCulture));
            }

            if (i % 4 == 0)
            {
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            }
        }

        await UiTestHelpers.WaitUntilAsync(
            () => hostProgram.Document.Text == guestProgram.Document.Text && hostProgram.Document.Text.Contains(guestLine, StringComparison.Ordinal),
            Timeout,
            "both devices to show both lines");
        Assert.StartsWith(hostLine, hostProgram.Document.Text, StringComparison.Ordinal);
        Assert.EndsWith(guestLine, guestProgram.Document.Text, StringComparison.Ordinal);
        Assert.True(hostProgram.IsDirty);

        // Undo takes back only the guest's own line, on both devices.
        Assert.True(guestProgram.Live!.Undo());
        while (guestProgram.Document.Text.Contains("Noam", StringComparison.Ordinal) && guestProgram.Live.Undo())
        {
        }

        await UiTestHelpers.WaitUntilAsync(
            () => hostProgram.Document.Text == guestProgram.Document.Text && !hostProgram.Document.Text.Contains("Noam", StringComparison.Ordinal),
            Timeout,
            "the guest's undo to reach the host");
        Assert.StartsWith(hostLine, hostProgram.Document.Text, StringComparison.Ordinal);

        // Each sees the other's caret, in the other's color, with the other's name.
        guestProgram.Live.ReportPlace(10, 14, 1);
        await UiTestHelpers.WaitUntilAsync(() => hostProgram.Live!.Carets.Any(c => c.Name == "Noam's phone" && c.Head == 14), Timeout, "the guest's caret on the host");
        var caret = hostProgram.Live!.Carets.Single();
        Assert.Equal((10, 14), (caret.Anchor, caret.Head));
        Assert.NotEqual(host.Session.Self!.Color, guest.Session.Self!.Color);
        Assert.Equal(guest.Session.Self.Color, caret.Color);
        Assert.Contains(hostProgram.LiveAvatars, a => a.Name == "Noam's phone");

        // A guest's save is the host's save, and every copy of the file changes.
        Assert.True(await guest.Workspace.SaveAllCoreAsync());
        await UiTestHelpers.WaitUntilAsync(() => !hostProgram.IsDirty && !guestProgram.IsDirty, Timeout, "the save on both devices");
        Assert.Equal(hostProgram.Document.Text, File.ReadAllText(hostProgram.FilePath));
        await UiTestHelpers.WaitUntilAsync(() => File.ReadAllText(guestProgram.FilePath) == hostProgram.Document.Text, Timeout, "the guest's copy of the file");
    }

    [AvaloniaFact]
    public async Task Five_devices_type_at_once_and_end_with_the_same_text()
    {
        await using var host = await LiveParticipant.HostAsync();
        var guests = new List<LiveParticipant>();
        try
        {
            foreach (var name in new[] { "Noam's phone", "Maya's tablet", "Amit's tablet", "Lior's phone" })
            {
                guests.Add(await LiveParticipant.JoinAsync(host, name));
            }

            var hostProgram = host.Workspace.ActiveDocument!;
            var documents = new List<DocumentViewModel> { hostProgram };
            foreach (var guest in guests)
            {
                documents.Add(await guest.OpenSharedAsync(guest.PathOf(host, hostProgram.FilePath)));
            }

            Assert.All(documents, d => Assert.Equal(hostProgram.Document.Text, d.Document.Text));
            await UiTestHelpers.WaitUntilAsync(
                () => guests.All(g => g.Workspace.LivePanel!.Participants.Count == 5),
                Timeout,
                "everyone to list five participants");

            // Everyone types a word into the same line at once, a key at a time.
            var length = hostProgram.Document.TextLength;
            var words = documents.Select((_, i) => $"/*{i}*/").ToList();
            for (var key = 0; key < words[0].Length; key++)
            {
                for (var i = 0; i < documents.Count; i++)
                {
                    var document = documents[i].Document;
                    var at = Math.Min(document.GetLineByNumber(CreateGreeter - 1).Offset + key, document.TextLength);
                    document.Insert(at, words[i][key].ToString(CultureInfo.InvariantCulture));
                }

                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            }

            await UiTestHelpers.WaitUntilAsync(
                () => documents.All(d => d.Document.Text == hostProgram.Document.Text)
                    && documents.All(d => d.Live!.Live.Text.PendingCount == 0)
                    && hostProgram.Document.TextLength == length + (documents.Count * words[0].Length),
                Timeout,
                "the five devices to agree");
            Assert.All(documents, d => Assert.Equal(hostProgram.Document.Text, d.Document.Text));
        }
        finally
        {
            foreach (var guest in guests)
            {
                await guest.DisposeAsync();
            }
        }
    }

    [AvaloniaFact]
    public async Task Follow_shows_the_followed_document_and_place_until_the_follower_types()
    {
        await using var host = await LiveParticipant.HostAsync();
        await using var guest = await LiveParticipant.JoinAsync(host, "Noam's phone");
        var hostProgram = host.Workspace.ActiveDocument!;
        await guest.OpenSharedAsync(guest.PathOf(host, hostProgram.FilePath));

        guest.Workspace.FollowLive(LiveHost.HostId);
        Assert.Equal(LiveHost.HostId, guest.Workspace.LiveFollowed);
        Assert.Equal("Following Dana's tablet", guest.Workspace.LivePanel!.StatusText);

        // The host opens another file: the follower's IDE opens it too.
        var greeterPath = Directory.GetFiles(host.Workspace.Workspace!.RootDirectory, "Greeter.cs", SearchOption.AllDirectories).Single();
        var hostGreeter = await host.OpenSharedAsync(greeterPath);
        await UiTestHelpers.WaitUntilAsync(() => guest.Workspace.ActiveDocument?.FileName == "Greeter.cs" && guest.Workspace.ActiveDocument.Live is not null, Timeout, "the follower to open Greeter.cs");
        var guestGreeter = guest.Workspace.ActiveDocument!;

        // The host selects and scrolls: the follower is shown the same.
        LivePresence? shown = null;
        guestGreeter.Live!.FollowMoved += (_, presence) => shown = presence;
        hostGreeter.Live!.ReportPlace(40, 52, 9);
        await UiTestHelpers.WaitUntilAsync(() => shown is { TopLine: 9 }, Timeout, "the host's place on the follower");
        Assert.Equal((40, 52), guestGreeter.Live.OffsetsOf(shown!));
        Assert.Equal(LiveHost.HostId, guest.Workspace.LiveFollowed);

        // The follower types, and follows no more.
        guestGreeter.Live.StopFollowing();
        Assert.Null(guest.Workspace.LiveFollowed);
        await host.Workspace.OpenDocumentAsync(hostProgram.FilePath);
        hostProgram.Live!.ReportPlace(0, 0, 1);
        await Task.Delay(300);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal("Greeter.cs", guest.Workspace.ActiveDocument?.FileName);
    }

    [AvaloniaFact]
    public async Task A_guests_breakpoint_pauses_the_hosts_program_and_both_show_the_pause()
    {
        await using var host = await LiveParticipant.HostAsync();
        await using var guest = await LiveParticipant.JoinAsync(host, "Noam's phone");
        AnswerInput(host.Workspace, "Ada");
        var hostProgram = host.Workspace.ActiveDocument!;
        var guestProgram = await guest.OpenSharedAsync(guest.PathOf(host, hostProgram.FilePath));

        // The guest sets the breakpoint; it is the host's, and every participant's.
        guest.Workspace.Breakpoints.Toggle(guestProgram.FilePath, CreateGreeter);
        await UiTestHelpers.WaitUntilAsync(() => host.Workspace.Breakpoints.Find(hostProgram.FilePath, CreateGreeter) is not null, Timeout, "the breakpoint on the host");
        Assert.False(guest.Workspace.RunCommand.CanExecute(null));

        await host.Workspace.StartDebuggingCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => host.Workspace.IsPaused && LineOf(hostProgram) == CreateGreeter, Timeout, "the host to pause at the breakpoint");
        await UiTestHelpers.WaitUntilAsync(
            () => guest.Workspace.IsPaused && LineOf(guestProgram) == CreateGreeter && guest.Workspace.Locals.Tree.Rows.Any(r => r.Name == "name"),
            Timeout,
            "the guest to show the same pause");
        Assert.Equal("<top-level statements>", guest.Workspace.CallStack.Frames[0].MethodName);
        Assert.Equal(guestProgram.FilePath, guest.Workspace.CallStack.Frames[0].Location!.FilePath);
        Assert.Equal("\"Ada\"", guest.Workspace.Locals.Tree.Rows.Single(r => r.Name == "name").Value);
        Assert.StartsWith("Dana's tablet: Paused at a breakpoint", guest.Workspace.StatusText, StringComparison.Ordinal);

        // The host decides who steps: not this guest, yet.
        await guest.Workspace.StepOverCommand.ExecuteAsync(null);
        Assert.Contains("has not allowed you", guest.Workspace.StatusText, StringComparison.Ordinal);
        Assert.True(host.Workspace.IsPaused);

        host.Host.SetCanStep(guest.Guest.SelfId, canStep: true);
        await UiTestHelpers.WaitUntilAsync(() => guest.Guest.Self is { CanStep: true }, Timeout, "the guest to be let step");
        await guest.Workspace.StepOverCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => host.Workspace.IsPaused && LineOf(hostProgram) == Greet, Timeout, "the host to step to the next line");
        await UiTestHelpers.WaitUntilAsync(() => guest.Workspace.IsPaused && LineOf(guestProgram) == Greet, Timeout, "the guest to show the step");

        await guest.Workspace.StartDebuggingCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => host.Workspace.State == IdeState.Ready, Timeout, "the program to end");
        await UiTestHelpers.WaitUntilAsync(
            () => guest.Workspace.State == IdeState.Ready && guest.Workspace.Output.Text.Contains("Ada! This program was compiled", StringComparison.Ordinal),
            Timeout,
            "the guest to show the end and the output");
        Assert.Null(guestProgram.CurrentStatement);
    }

    [AvaloniaFact]
    public async Task A_guest_who_only_watches_cannot_change_the_text_and_a_removed_guest_leaves()
    {
        await using var host = await LiveParticipant.HostAsync();
        await using var guest = await LiveParticipant.JoinAsync(host, "Noam's phone");
        var hostProgram = host.Workspace.ActiveDocument!;
        var guestProgram = await guest.OpenSharedAsync(guest.PathOf(host, hostProgram.FilePath));
        var before = hostProgram.Document.Text;

        host.Host.SetRole(guest.Guest.SelfId, LiveRole.Viewer);
        await UiTestHelpers.WaitUntilAsync(() => guestProgram.IsLiveReadOnly, Timeout, "the guest's editor to be read-only");
        Assert.True(guest.Workspace.LivePanel!.IsViewer);

        // Typing anyway, as the editor's read-only mode would not allow: the text is put back, and the host never changes.
        guestProgram.Document.Insert(0, "// a viewer typed this\n");
        await UiTestHelpers.WaitUntilAsync(() => guestProgram.Document.Text == before, Timeout, "the viewer's typing to be put back");
        await Task.Delay(300);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal(before, hostProgram.Document.Text);
        Assert.False(await guest.Workspace.SaveAllCoreAsync() && guestProgram.IsDirty);

        // The host lets the guest edit again, then removes it.
        host.Host.SetRole(guest.Guest.SelfId, LiveRole.Editor);
        await UiTestHelpers.WaitUntilAsync(() => !guestProgram.IsLiveReadOnly, Timeout, "the guest's editor to take typing again");
        string? reason = null;
        guest.Live.GuestEnded += (_, why) => reason = why;
        await host.Host.RemoveAsync(guest.Guest.SelfId);
        await UiTestHelpers.WaitUntilAsync(() => reason is not null && !guest.Workspace.IsLiveGuest, Timeout, "the guest to leave the session");
        Assert.Contains("removed you", reason, StringComparison.Ordinal);
        Assert.Null(guestProgram.Live);
        await UiTestHelpers.WaitUntilAsync(() => host.Workspace.LivePanel!.Participants.Count == 1, Timeout, "the host to list only itself");

        // Its copy of the workspace is deleted, as the app does when the session ends.
        var copy = guest.Live.CopiesDirectory;
        guest.Workspace.Dispose();
        guest.Live.DeleteCopies();
        Assert.False(Directory.Exists(copy));
    }

    [AvaloniaFact]
    public async Task Two_devices_editing_together_in_the_real_shell()
    {
        await using var host = await LiveParticipant.HostAsync();
        await using var guest = await LiveParticipant.JoinAsync(host, "Noam's phone");
        var hostWindow = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = host.Workspace } };
        var guestWindow = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = guest.Workspace } };
        hostWindow.Show();
        guestWindow.Show();
        try
        {
            var hostProgram = host.Workspace.ActiveDocument!;
            var guestProgram = await guest.OpenSharedAsync(guest.PathOf(host, hostProgram.FilePath));
            host.Workspace.LivePanel!.CloseShareCommand.Execute(null);
            host.Workspace.SelectSideBarPanelCommand.Execute(SideBarPanel.Live);
            guest.Workspace.SelectSideBarPanelCommand.Execute(SideBarPanel.Live);

            // Each types on its own line; each sees the other's text and caret.
            var hostLine = hostProgram.Document.GetLineByNumber(CreateGreeter);
            hostProgram.Document.Insert(hostLine.Offset, "    // Dana: the greeter comes next\n");
            var guestLine = guestProgram.Document.GetLineByNumber(Greet + 1);
            guestProgram.Document.Insert(guestLine.Offset, "    // Noam: then it greets\n");
            await UiTestHelpers.WaitUntilAsync(() => hostProgram.Document.Text == guestProgram.Document.Text && hostProgram.Document.Text.Contains("Noam:", StringComparison.Ordinal), Timeout, "both edits on both devices");

            // Each selects in its own editor, which says where it is as a person's selection does.
            var noam = hostProgram.Document.Text.IndexOf("then it greets", StringComparison.Ordinal);
            var dana = guestProgram.Document.Text.IndexOf("the greeter comes", StringComparison.Ordinal);
            host.Workspace.IsTouchBarCollapsed = true;
            guest.Workspace.IsTouchBarCollapsed = true;
            await UiTestHelpers.WaitUntilAsync(() => FindEditor(hostWindow) is not null && FindEditor(guestWindow) is not null, Timeout, "both editors");
            FindEditor(guestWindow)!.Editor.Select(noam, "then it greets".Length);
            FindEditor(hostWindow)!.Editor.CaretOffset = dana;
            await UiTestHelpers.WaitUntilAsync(
                () => hostProgram.Live!.Carets is [{ Anchor: var anchor, Head: var end }] && anchor == noam && end == noam + "then it greets".Length
                    && guestProgram.Live!.Carets is [{ Head: var head }] && head == dana
                    && hostProgram.HasLiveAvatars && guestProgram.HasLiveAvatars,
                Timeout,
                "each device to show the other's caret");
            await UiTestHelpers.WaitUntilAsync(
                () => FindEditor(hostWindow)?.LivePresence?.Shown.Count == 1 && FindEditor(guestWindow)?.LivePresence?.Shown.Count == 1,
                Timeout,
                "the editors to draw the carets");

            UiTestHelpers.SaveScreenshot(hostWindow, "live-01-host-editing-with-a-guest");
            UiTestHelpers.SaveScreenshot(guestWindow, "live-02-guest-editing-with-the-host");

            host.Workspace.LivePanel!.OpenShare();
            UiTestHelpers.SaveScreenshot(hostWindow, "live-03-share-session");
            host.Workspace.LivePanel.CloseShareCommand.Execute(null);
        }
        finally
        {
            hostWindow.Close();
            guestWindow.Close();
        }
    }

    private static CodeEditorView? FindEditor(Window window) =>
        Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<CodeEditorView>().FirstOrDefault(v => v.IsEffectivelyVisible);

    private static int? LineOf(DocumentViewModel document) =>
        document.CurrentStatement is { } point ? document.Document.GetLineByOffset(point.Start).LineNumber : null;

    private static void AnswerInput(WorkspaceViewModel workspace, string line)
    {
        var output = workspace.Output;
        output.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(OutputViewModel.IsWaitingForInput) && output.IsWaitingForInput)
            {
                output.InputText = line;
                output.SubmitInputCommand.Execute(null);
            }
        };
    }
}
