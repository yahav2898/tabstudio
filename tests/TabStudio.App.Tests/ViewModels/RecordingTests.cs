using Avalonia.Headless.XUnit;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Tests.ViewModels;

public sealed class RecordingTests
{
    internal const string Loop = """
        int total = 0;
        for (int iteration = 0; iteration < 4; iteration++)
        {
            total += iteration;
        }
        Console.WriteLine(total);
        """;

    [AvaloniaFact]
    public async Task A_loop_can_be_scrubbed_and_stepped_back_after_exit_with_historical_watch_and_DataTips()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        workspace.ActiveDocument!.Document.Text = Loop;
        await workspace.Watch.AddAsync("total");
        await workspace.StartWithRecordingCommand.ExecuteAsync(null);
        await DebuggingTests.WaitForExitAsync(workspace);
        Assert.True(workspace.HasRecording);
        Assert.Equal(64, workspace.RecordingBufferMegabytes);
        Assert.True(workspace.StepBackCommand.CanExecute(null));

        await workspace.StepBackCommand.ExecuteAsync(null);
        Assert.True(workspace.IsHistorical);
        Assert.True(workspace.Locals.Tree.IsHistorical);
        Assert.True(workspace.Watch.Tree.IsHistorical);
        Assert.Contains("Historical", workspace.StatusText, StringComparison.Ordinal);
        Assert.All(workspace.CallStack.Frames, f => Assert.Contains("historical", f.MethodName, StringComparison.Ordinal));

        await workspace.JumpToLastWriteCommand.ExecuteAsync("total");
        Assert.Equal("6", workspace.Locals.Tree.Rows.Single(r => r.Name == "total").Value);
        await workspace.JumpToLastWriteCommand.ExecuteAsync("total");
        Assert.Equal("3", workspace.Locals.Tree.Rows.Single(r => r.Name == "total").Value);
        Assert.Equal("3", workspace.Watch.Tree.Rows.Single(r => r.Name == "total").Value);
        var tip = await ((IEditorDebugger)workspace).EvaluateDataTipAsync("total");
        Assert.True(tip!.IsHistorical);
        Assert.Equal("3", Assert.Single(tip.Rows).Value);

        workspace.RecordingPosition = 0;
        await workspace.PauseShown;
        Assert.Contains("step 1", workspace.StatusText, StringComparison.Ordinal);
        Assert.Equal(IdeState.Ready, workspace.State);
    }

    [AvaloniaFact]
    public async Task A_live_pause_stays_paused_while_history_is_selected_and_F5_continues_from_live()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        workspace.ActiveDocument!.Document.Text = Loop;
        workspace.Breakpoints.Toggle(workspace.ActiveDocument.FilePath, 6);
        await workspace.StartWithRecordingCommand.ExecuteAsync(null);
        await DebuggingTests.WaitForPauseAsync(workspace, 6);
        await workspace.StepBackCommand.ExecuteAsync(null);
        await workspace.JumpToLastWriteCommand.ExecuteAsync("total");
        await workspace.JumpToLastWriteCommand.ExecuteAsync("total");
        Assert.Equal("3", workspace.Locals.Tree.Rows.Single(r => r.Name == "total").Value);
        Assert.True(workspace.IsPaused);

        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        await DebuggingTests.WaitForExitAsync(workspace);
        Assert.Contains("6", workspace.Output.Text, StringComparison.Ordinal);
        Assert.True(workspace.HasRecording);
        Assert.False(workspace.IsHistorical);
    }

    [AvaloniaFact]
    public async Task A_crash_automatically_selects_the_faulty_write_and_Reverse_Continue_finds_it_after_exit()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        workspace.ActiveDocument!.Document.Text = """
            int divisor = 2;
            divisor = 0;
            int unrelated = 100;
            Console.WriteLine(unrelated / divisor);
            """;
        await workspace.StartWithRecordingCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.IsHistorical, because: "the exception's recorded cause");
        await workspace.PauseShown;
        Assert.Equal(2, workspace.CallStack.Frames[0].Location!.Line);
        Assert.Equal("0", workspace.Locals.Tree.Rows.Single(r => r.Name == "divisor").Value);

        await workspace.ReturnToLiveCommand.ExecuteAsync(null);
        Assert.False(workspace.IsHistorical);
        Assert.Equal(4, workspace.CallStack.Frames[0].Location!.Line);
        Assert.NotNull(workspace.ActiveDocument.ExceptionHelper);

        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        await DebuggingTests.WaitForExitAsync(workspace);
        Assert.Equal(IdeState.Failed, workspace.State);
        await workspace.ReverseContinueCommand.ExecuteAsync(null);
        Assert.Equal(2, workspace.CallStack.Frames[0].Location!.Line);
        Assert.True(workspace.IsHistorical);
    }

    [AvaloniaFact]
    public async Task Buffer_size_persists_but_a_normal_F5_never_enables_recording()
    {
        using var fixture = new WorkspaceFixture();
        var solution = await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        workspace.ActiveDocument!.Document.Text = "Console.WriteLine(42);";
        workspace.RecordingBufferMegabytes = 8;
        workspace.SaveSessionNow();
        Assert.Equal(8, fixture.Sessions.Load()!.RecordingBufferMegabytes);
        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        await DebuggingTests.WaitForExitAsync(workspace);
        Assert.False(workspace.HasRecording);
        Assert.False(workspace.StepBackCommand.CanExecute(null));
    }
}
