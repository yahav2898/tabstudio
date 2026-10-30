using TabStudio.Link;
using TabStudio.Link.Networking;
using TabStudio.Live.Sessions;
using TabStudio.Swarm;

namespace TabStudio.Live.Tests;

/// <summary>Sessions over the real transport: joining, editing together, presence, permissions, removal and reconnection.</summary>
public sealed class SessionTests
{
    [Fact]
    public async Task Guests_join_with_the_QR_code_copy_the_workspace_and_see_everyone()
    {
        await using var live = await LiveLoopback.StartAsync(new Dictionary<string, string>
        {
            ["Hello.sln"] = "Microsoft Visual Studio Solution File",
            ["Hello/Program.cs"] = "Console.WriteLine(\"Hello\");\n",
            ["Hello/bin/Debug/Hello.dll"] = "binary",
        });
        var invitation = await live.On(() => live.Host.Invitation);
        Assert.NotNull(invitation);
        Assert.Equal(LinkRole.Live, invitation.Role);

        var dana = await live.JoinAsync("Dana's tablet");
        var noa = await live.JoinAsync("Noa's phone");

        // One QR code lets many devices join: the code stays while it is shown.
        Assert.Equal(invitation.Code, await live.On(() => live.Host.PairingCode));
        await LiveLoopback.UntilAsync(() => dana.On(() => dana.Session.Participants.Count == 3), "Dana to see three participants");
        var names = await dana.On(() => dana.Session.Participants.Select(p => p.Name).ToList());
        Assert.Equal(["Teacher's tablet", "Dana's tablet", "Noa's phone"], names);
        Assert.Equal(LiveRole.Editor, (await dana.On(() => dana.Session.Self))!.Role);
        Assert.Equal(LiveRole.Host, (await noa.On(() => noa.Session.Participants[0])).Role);

        var root = await dana.On(() => dana.Session.WorkspaceRoot!);
        Assert.Equal("Console.WriteLine(\"Hello\");\n", await File.ReadAllTextAsync(Path.Combine(root, "Hello", "Program.cs")));
        Assert.False(File.Exists(Path.Combine(root, "Hello", "bin", "Debug", "Hello.dll")));
        Assert.Equal(root, await dana.On(() => dana.Session.EntryPath));
        Assert.NotEqual(await dana.On(() => dana.Session.SelfId), await noa.On(() => noa.Session.SelfId));
    }

    [Fact]
    public async Task Concurrent_typing_on_three_devices_converges_everywhere()
    {
        await using var live = await LiveLoopback.StartAsync();
        var hostDocument = await live.ShareAsync("Program.cs");
        var dana = await live.JoinAsync("Dana");
        var noa = await live.JoinAsync("Noa");
        var danaDocument = await dana.OpenAsync("Program.cs");
        var noaDocument = await noa.OpenAsync("Program.cs");
        Assert.Equal("class Program\n{\n}\n", await dana.On(() => danaDocument.Text.ToString()));

        // Everyone types at once, at places of their own choosing, while the edits cross.
        var random = new Random(7);
        var typing = new List<Task>();
        foreach (var (on, document, word) in new (Func<Func<bool>, Task<bool>>, LiveDocument, string)[]
        {
            (work => live.On(work), hostDocument, "host"),
            (work => dana.On(work), danaDocument, "dana"),
            (work => noa.On(work), noaDocument, "noa"),
        })
        {
            var seed = random.Next();
            typing.Add(Task.Run(async () =>
            {
                var mine = new Random(seed);
                for (var i = 0; i < 40; i++)
                {
                    Assert.True(await on(() =>
                    {
                        var length = document.Text.Length;
                        if (length > 4 && mine.Next(4) == 0)
                        {
                            var at = mine.Next(length - 2);
                            return document.Edit(at, document.Text.Substring(at, 2), string.Empty);
                        }

                        return document.Edit(mine.Next(length + 1), string.Empty, $"{word}{i} ");
                    }));
                    await Task.Delay(mine.Next(3));
                }
            }));
        }

        await Task.WhenAll(typing);
        await LiveLoopback.UntilAsync(
            async () =>
            {
                var host = await live.On(() => hostDocument.Text.ToString());
                return host == await dana.On(() => danaDocument.Text.ToString()) && host == await noa.On(() => noaDocument.Text.ToString());
            },
            "the three copies to be the same");
        // Each device's own edits all arrived: every device has every participant's edits, and nothing waits.
        var versions = await live.On(() => hostDocument.Text.Version);
        Assert.Equal(await dana.On(() => danaDocument.Text.Version[dana.Session.SelfId]), versions[await dana.On(() => dana.Session.SelfId)]);
        Assert.Equal(await noa.On(() => noaDocument.Text.Version[noa.Session.SelfId]), versions[await noa.On(() => noa.Session.SelfId)]);
        Assert.Equal(0, await live.On(() => hostDocument.Text.PendingCount));
    }

    [Fact]
    public async Task Carets_and_selections_reach_the_others_and_stay_by_their_text()
    {
        await using var live = await LiveLoopback.StartAsync();
        var hostDocument = await live.ShareAsync("Program.cs");
        var dana = await live.JoinAsync("Dana");
        var noa = await live.JoinAsync("Noa");
        var danaDocument = await dana.OpenAsync("Program.cs");
        var noaDocument = await noa.OpenAsync("Program.cs");
        var danaId = await dana.On(() => dana.Session.SelfId);

        // Dana selects "Program".
        await dana.On(() => dana.Session.ReportPresence(danaDocument, 6, 13, 1));
        await LiveLoopback.UntilAsync(() => noa.On(() => noa.Session.PresenceOf(danaId) is not null), "Noa to see Dana's selection");
        var presence = (await noa.On(() => noa.Session.PresenceOf(danaId)))!;
        Assert.Equal("Program.cs", presence.Path);
        Assert.Equal(6, await noa.On(() => noaDocument.OffsetOf(presence.Anchor)));
        Assert.Equal(13, await noa.On(() => noaDocument.OffsetOf(presence.Head)));

        // The host types above it; Dana's selection stays on "Program".
        await live.On(() => hostDocument.Edit(0, string.Empty, "// hi\n"));
        await LiveLoopback.UntilAsync(() => noa.On(() => noaDocument.Text.ToString().StartsWith("// hi", StringComparison.Ordinal)), "Noa to get the host's line");
        Assert.Equal(12, await noa.On(() => noaDocument.OffsetOf(presence.Anchor)));
        Assert.Equal("Program", await noa.On(() => noaDocument.Text.Substring(12, 7)));
        Assert.NotNull(await live.On(() => live.Host.PresenceOf(danaId)));
    }

    [Fact]
    public async Task A_guest_that_only_watches_cannot_edit_and_its_edits_are_refused_by_the_host()
    {
        await using var live = await LiveLoopback.StartAsync(defaultRole: LiveRole.Viewer);
        var hostDocument = await live.ShareAsync("Program.cs");
        var noa = await live.JoinAsync("Noa");
        var document = await noa.OpenAsync("Program.cs");

        Assert.False(await noa.On(() => document.CanEdit));
        Assert.False(await noa.On(() => document.Edit(0, string.Empty, "hacked")));

        // A guest that edits anyway, past its own IDE, is refused by the host and loads the document again.
        var reloaded = new TaskCompletionSource();
        await noa.On(() => noa.Session.DocumentReloaded += (_, _) => reloaded.TrySetResult());
        await noa.On(() =>
        {
            var forced = document.Text.Insert(0, "hacked ")!;
            noa.Session.SendEdits(document, [forced]);
        });
        await reloaded.Task.WaitAsync(LiveLoopback.Patience);

        Assert.Equal("class Program\n{\n}\n", await live.On(() => hostDocument.Text.ToString()));
        Assert.Equal("class Program\n{\n}\n", await noa.On(() => document.Text.ToString()));
        await Assert.ThrowsAsync<LinkRemoteException>(() => noa.On(() => noa.Session.SaveAsync(document)));
        await Assert.ThrowsAsync<LinkRemoteException>(() => noa.On(() => noa.Session.SetBreakpointAsync("Program.cs", 1, null, remove: false)));

        // The host makes the guest an editor: now it edits.
        var noaId = await noa.On(() => noa.Session.SelfId);
        await live.On(() => live.Host.SetRole(noaId, LiveRole.Editor));
        await LiveLoopback.UntilAsync(() => noa.On(() => document.CanEdit), "Noa to become an editor");
        Assert.True(await noa.On(() => document.Edit(0, string.Empty, "// ")));
        await LiveLoopback.UntilAsync(() => live.On(() => hostDocument.Text.ToString().StartsWith("// class", StringComparison.Ordinal)), "the host to get Noa's edit");
    }

    [Fact]
    public async Task The_host_removes_a_guest_which_cannot_come_back_with_the_same_code()
    {
        await using var live = await LiveLoopback.StartAsync();
        var dana = await live.JoinAsync("Dana");
        var danaId = await dana.On(() => dana.Session.SelfId);
        var ended = new TaskCompletionSource<string>();
        await dana.On(() => dana.Session.Ended += (_, reason) => ended.TrySetResult(reason));

        await live.On(() => live.Host.RemoveAsync(danaId));

        Assert.Contains("removed you", await ended.Task.WaitAsync(LiveLoopback.Patience), StringComparison.Ordinal);
        Assert.Equal(LiveGuestState.Ended, await dana.On(() => dana.Session.State));
        Assert.DoesNotContain(await live.On(() => live.Host.Participants.ToList()), p => p.Id == danaId);

        // The same device, with the code still on the screen, is refused.
        var again = new LiveGuest(
            new LiveGuestOptions { Identity = dana.Identity, Name = "Dana", MirrorDirectory = Path.Combine(Path.GetTempPath(), "tabstudio-tests", Guid.NewGuid().ToString("N")) },
            dana.Dispatcher);
        var invitation = (await live.On(() => live.Host.Invitation))!;
        Assert.False(await dana.On(() => again.JoinAsync(LinkTarget.FromInvitation(invitation))));
        Assert.Contains("removed this device", await dana.On(() => again.Error), StringComparison.Ordinal);
        await again.DisposeAsync();
    }

    [Fact]
    public async Task A_guest_whose_connection_drops_reconnects_and_its_offline_edits_merge()
    {
        await using var live = await LiveLoopback.StartAsync();
        var hostDocument = await live.ShareAsync("Program.cs");
        var dana = await live.JoinAsync("Dana", reconnectDelays: [TimeSpan.FromSeconds(1)]);
        var document = await dana.OpenAsync("Program.cs");
        var danaId = await dana.On(() => dana.Session.SelfId);

        await live.On(() => live.Host.DropConnection(danaId));
        await LiveLoopback.UntilAsync(() => dana.On(() => dana.Session.State == LiveGuestState.Reconnecting), "Dana to notice");

        // Both edit while apart.
        Assert.True(await dana.On(() => document.Edit(0, string.Empty, "// offline\n")));
        await live.On(() => hostDocument.Edit(hostDocument.Text.Length, string.Empty, "// host\n"));

        await LiveLoopback.UntilAsync(() => dana.On(() => dana.Session.State == LiveGuestState.Joined), "Dana to reconnect");
        const string Expected = "// offline\nclass Program\n{\n}\n// host\n";
        await LiveLoopback.UntilAsync(() => live.On(() => hostDocument.Text.ToString() == Expected), "the host to get Dana's offline edit");
        await LiveLoopback.UntilAsync(() => dana.On(() => document.Text.ToString() == Expected), "Dana to get the host's edit");
        Assert.Equal(danaId, await dana.On(() => dana.Session.SelfId));
    }

    [Fact]
    public async Task A_class_of_thirty_joins_from_one_code_follows_the_presenter_and_raises_hands()
    {
        await using var live = await LiveLoopback.StartAsync(classroom: true);
        var invitation = (await live.On(() => live.Host.Invitation))!;
        var students = Enumerable.Range(1, 30).Select(i => live.CreateGuest($"Student {i}")).ToList();

        var joins = students.Select(s => s.On(() => s.Session.JoinAsync(LinkTarget.FromInvitation(invitation)))).ToList();
        var joined = await Task.WhenAll(joins).WaitAsync(TimeSpan.FromSeconds(120));
        Assert.All(joined, Assert.True);
        await LiveLoopback.UntilAsync(() => live.On(() => live.Host.ConnectedGuests == 30), "thirty students");

        var participants = await live.On(() => live.Host.Participants.ToList());
        Assert.True(await live.On(() => live.Host.IsClassroom));
        Assert.All(participants.Skip(1), p => Assert.Equal(LiveRole.Viewer, p.Role));
        Assert.True(participants[0].IsPresenter);

        // A student raises a hand; the teacher hands control to them, so they present and edit.
        var asker = students[11];
        var askerId = await asker.On(() => asker.Session.SelfId);
        await asker.On(() => asker.Session.RaiseHand(true));
        await LiveLoopback.UntilAsync(() => live.On(() => live.Host.Find(askerId)?.HandRaised == true), "the raised hand");
        Assert.True(await live.On(() => live.Host.HandControl(askerId)));
        await LiveLoopback.UntilAsync(() => students[0].On(() => students[0].Session.PresenterId == askerId), "the class to see the new presenter");
        var presenter = (await asker.On(() => asker.Session.Self))!;
        Assert.Equal(LiveRole.Editor, presenter.Role);
        Assert.True(presenter.IsPresenter);
        Assert.False(presenter.HandRaised);

        await live.On(() => live.Host.TakeControl());
        await LiveLoopback.UntilAsync(() => asker.On(() => asker.Session.Self?.Role == LiveRole.Viewer), "control to come back");
    }

    [Fact]
    public async Task The_host_decides_who_steps_while_everyone_sees_the_pause_and_its_values()
    {
        await using var live = await LiveLoopback.StartAsync();
        var dana = await live.JoinAsync("Dana");
        var danaId = await dana.On(() => dana.Session.SelfId);

        var pause = new LiveDebugState(
            7,
            "Paused at a breakpoint · Program.cs, line 2",
            [new SwarmFrame("Program.Main()", new TabStudio.Debugging.Symbols.DebugSourceSpan("Program.cs", 2, 1, 2, 2, 14, 1))],
            [new SwarmVariable("args", "string[0]", "string[]", 1, "args")],
            null);
        await live.On(() =>
        {
            live.Host.PublishRun(new LiveRunState(LiveRunPhase.Paused, pause.StatusText));
            live.Host.PublishDebug(pause);
            live.Host.PublishBreakpoints([new SwarmBreakpoint("Program.cs", 2)]);
        });
        await LiveLoopback.UntilAsync(() => dana.On(() => dana.Session.Debug?.PauseId == 7), "Dana to see the pause");
        Assert.Equal(LiveRunPhase.Paused, await dana.On(() => dana.Session.RunState.Phase));

        // The breakpoints are their own message, published after the pause, so they may land a moment later.
        await LiveLoopback.UntilAsync(() => dana.On(() => dana.Session.Breakpoints.Count > 0), "Dana to see the breakpoints");
        Assert.Equal("Program.cs", (await dana.On(() => dana.Session.Breakpoints))[0].FilePath);
        var children = await dana.On(() => dana.Session.GetVariablesAsync(7, "args"));
        Assert.Equal("args.Count", Assert.Single(children).Path);
        Assert.Equal("42", (await dana.On(() => dana.Session.EvaluateAsync(7, "answer"))).Value!.Value);
        Assert.Equal("42", (await dana.On(() => dana.Session.EvaluateAsync(7, "order.Lines[0].Price"))).Value!.Value);

        // A viewer reads variables but cannot run code on the host: a debug adapter's evaluation is Python's eval.
        await Assert.ThrowsAsync<LinkRemoteException>(() => dana.On(() => dana.Session.EvaluateAsync(7, "__import__('os').system('rm -rf ~')")));

        // Stepping is the host's to allow.
        await Assert.ThrowsAsync<LinkRemoteException>(() => dana.On(() => dana.Session.ControlDebuggerAsync(LiveDebugCommand.StepOver)));
        await live.On(() => live.Host.SetCanStep(danaId, true));
        await LiveLoopback.UntilAsync(() => dana.On(() => dana.Session.Self?.CanStep == true), "Dana to be allowed");
        await dana.On(() => dana.Session.ControlDebuggerAsync(LiveDebugCommand.StepOver));
        Assert.Equal((LiveDebugCommand.StepOver, "Dana"), await live.On(() => live.Workspace.DebugCommands.Single()));
        Assert.Equal("42", (await dana.On(() => dana.Session.EvaluateAsync(7, "Total(order)"))).Value!.Value);

        // An editor sets a breakpoint through the host.
        await dana.On(() => dana.Session.SetBreakpointAsync("Program.cs", 3, null, remove: false));
        var request = await live.On(() => live.Workspace.BreakpointRequests.Single());
        Assert.Equal(Path.Combine(live.Root, "Program.cs"), request.Path);
        Assert.Equal(3, request.Line);
        await Assert.ThrowsAsync<LinkRemoteException>(() => dana.On(() => dana.Session.SetBreakpointAsync("../outside.cs", 1, null, remove: false)));
    }

    [Fact]
    public async Task Output_the_Terminal_and_the_Error_List_reach_guests_and_a_late_guest_sees_the_last_of_them()
    {
        await using var live = await LiveLoopback.StartAsync();
        var dana = await live.JoinAsync("Dana");
        var output = new List<LiveOutput>();
        var terminal = new List<byte>();
        await dana.On(() =>
        {
            dana.Session.OutputWritten += (_, o) => output.Add(o);
            dana.Session.TerminalWritten += (_, data) => terminal.AddRange(data.ToArray());
        });

        await live.On(() =>
        {
            live.Host.WriteOutput("Build started\n", LiveOutputKind.Info);
            live.Host.WriteOutput("Hello", LiveOutputKind.Program);
            live.Host.WriteOutput(", world\n", LiveOutputKind.Program);
            live.Host.WriteTerminal("$ ls\r\n"u8);
            live.Host.PublishErrors([new LiveDiagnostic("CS0103", 3, "The name 'x' does not exist", "Program.cs", 10, 1, 2, 5, "Hello")]);
        });

        await LiveLoopback.UntilAsync(() => dana.On(() => string.Concat(output.Select(o => o.Text)) == "Build started\nHello, world\n"), "the Output");
        await LiveLoopback.UntilAsync(() => dana.On(() => terminal.Count == 6), "the Terminal");
        await LiveLoopback.UntilAsync(() => dana.On(() => dana.Session.Errors.Count == 1), "the Error List");
        Assert.Equal(LiveOutputKind.Info, (await dana.On(() => output[0])).Kind);

        // A guest that joins now sees the last of the Output and the Error List.
        var late = live.CreateGuest("Noa");
        var lateOutput = new List<LiveOutput>();
        await late.On(() => late.Session.OutputWritten += (_, o) => lateOutput.Add(o));
        var invitation = (await live.On(() => live.Host.Invitation))!;
        Assert.True(await late.On(() => late.Session.JoinAsync(LinkTarget.FromInvitation(invitation))));
        Assert.Equal("Build started\nHello, world\n", await late.On(() => string.Concat(lateOutput.Select(o => o.Text))));
        Assert.Equal("CS0103", (await late.On(() => late.Session.Errors[0])).Id);
    }

    [Fact]
    public async Task A_guest_saves_on_the_host_with_its_last_edits_and_cannot_open_files_that_are_not_shared()
    {
        await using var live = await LiveLoopback.StartAsync();
        var dana = await live.JoinAsync("Dana");
        var document = await dana.OpenAsync("Program.cs");
        var saved = new TaskCompletionSource<string>();
        await dana.On(() => dana.Session.DocumentSaved += (_, path) => saved.TrySetResult(path));

        Assert.True(await dana.On(() => document.Edit(document.Text.Length, string.Empty, "// saved\n")));
        await dana.On(() => dana.Session.SaveAsync(document));

        Assert.Equal("class Program\n{\n}\n// saved\n", await live.On(() => live.Workspace.Saved.Single()));
        Assert.Equal("Program.cs", await saved.Task.WaitAsync(LiveLoopback.Patience));
        Assert.Null(await dana.On(() => dana.Session.OpenDocumentAsync("../../etc/passwd")));
        Assert.Null(await dana.On(() => dana.Session.OpenDocumentAsync("Missing.cs")));
    }
}
