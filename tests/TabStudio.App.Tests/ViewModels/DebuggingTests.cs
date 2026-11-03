using Avalonia.Headless.XUnit;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>The debugger in the IDE, driven through the workspace's commands with the sample solution.</summary>
public sealed class DebuggingTests
{
    // Lines of the sample's Program.cs.
    private const int FirstStatement = 12;
    private const int ReadName = 13;
    private const int CreateGreeter = 15;
    private const int Greet = 16;
    private const int AfterLanguages = 27;

    // Line of the sample's Greeter.cs that builds the greeting.
    private const int BuildGreeting = 25;

    [AvaloniaFact]
    public async Task F5_stops_at_a_breakpoint_shows_the_locals_steps_over_and_continues_to_the_end()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        var program = workspace.ActiveDocument!;
        fixture.AnswerInput("Ada");
        workspace.Breakpoints.Toggle(program.FilePath, CreateGreeter);

        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        await WaitForPauseAsync(workspace, CreateGreeter);
        await WaitForLocalAsync(workspace, "name");

        Assert.Equal(IdeState.Paused, workspace.State);
        Assert.True(workspace.IsDebugging);
        Assert.Equal("Continue", workspace.StartDebuggingLabel);
        Assert.Equal(BottomPanel.Locals, workspace.SelectedPanel);
        Assert.True(workspace.ShowsCallStackBeside);
        Assert.StartsWith("Paused at a breakpoint", workspace.StatusText, StringComparison.Ordinal);
        Assert.Equal("<top-level statements>", workspace.CallStack.Frames[0].MethodName);
        Assert.Equal(CreateGreeter, LineOf(program, program.CurrentStatement));
        Assert.Equal("\"Ada\"", workspace.Locals.Tree.Rows.Single(r => r.Name == "name").Value);

        await workspace.StepOverCommand.ExecuteAsync(null);
        await WaitForPauseAsync(workspace, Greet);
        await WaitForLocalAsync(workspace, "greeter");

        Assert.Equal("Paused · Program.cs, line 16", workspace.StatusText);
        Assert.Equal(Greet, LineOf(program, program.CurrentStatement));

        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        await WaitForExitAsync(workspace);

        var output = workspace.Output.Text;
        Assert.Contains("Ada! This program was compiled and run on your tablet.", output, StringComparison.Ordinal);
        Assert.Contains("The program 'HelloTablet' has exited with code 0 (0x0).", output, StringComparison.Ordinal);
        Assert.Equal(IdeState.Ready, workspace.State);
        Assert.Equal(workspace.RunLabel, workspace.StartDebuggingLabel);
        Assert.Null(program.CurrentStatement);
        Assert.Empty(workspace.CallStack.Frames);
        Assert.Empty(workspace.Locals.Tree.Rows);
        Assert.Equal(BottomPanel.Output, workspace.SelectedPanel);
        Assert.Null(Assert.Single(workspace.Breakpoints.All).Binding);
    }

    [AvaloniaFact]
    public async Task A_breakpoint_on_a_line_without_code_moves_to_the_next_statement_or_is_hollow()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        var program = workspace.ActiveDocument!;
        fixture.AnswerInput("Ada");
        var blankLine = workspace.Breakpoints.Toggle(program.FilePath, CreateGreeter - 1)!;
        var afterTheLastStatement = workspace.Breakpoints.Toggle(program.FilePath, program.Document.LineCount)!;

        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        await WaitForPauseAsync(workspace, CreateGreeter);

        Assert.Equal(CreateGreeter, blankLine.Line);
        Assert.Equal(BreakpointGlyph.Normal, blankLine.Glyph);
        Assert.Equal(BreakpointGlyph.Unbound, afterTheLastStatement.Glyph);
        Assert.Equal(new[] { blankLine, afterTheLastStatement }, program.Breakpoints);

        workspace.StopDebuggingCommand.Execute(null);
        await WaitForExitAsync(workspace);

        Assert.Equal(BreakpointGlyph.Normal, afterTheLastStatement.Glyph);
    }

    [AvaloniaFact]
    public async Task F11_starts_at_the_first_statement_and_Shift_F5_stops_debugging()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;

        await workspace.StepIntoCommand.ExecuteAsync(null);
        await WaitForPauseAsync(workspace, FirstStatement);
        await workspace.StepIntoCommand.ExecuteAsync(null);
        await WaitForPauseAsync(workspace, ReadName);

        workspace.StopDebuggingCommand.Execute(null);
        await WaitForExitAsync(workspace);

        Assert.Equal(IdeState.Ready, workspace.State);
        Assert.Equal("Debugging stopped", workspace.StatusText);
        Assert.Contains("The program 'HelloTablet' has exited with code -1 (0xffffffff).", workspace.Output.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Break_All_pauses_a_running_loop()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        workspace.ActiveDocument!.Document.Text = """
            var counter = 0;
            Console.WriteLine("Counting");
            while (true)
            {
                counter++;
            }
            """;

        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        Assert.True(workspace.IsDebuggeeRunning);

        // Once it has printed, the program is in its loop, where counter is assigned.
        await UiTestHelpers.WaitUntilAsync(() => workspace.Output.Text.Contains("Counting", StringComparison.Ordinal), because: "the loop to start");
        workspace.BreakAllCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.IsPaused, because: "Break All");
        await workspace.PauseShown;

        Assert.StartsWith("Paused (Break All)", workspace.StatusText, StringComparison.Ordinal);
        Assert.Contains(workspace.Locals.Tree.Rows, r => r.Name == "counter");

        workspace.StopDebuggingCommand.Execute(null);
        await WaitForExitAsync(workspace);
    }

    [AvaloniaFact]
    public async Task Restart_stops_the_program_and_debugs_it_again()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        fixture.AnswerInput("Ada");
        workspace.Breakpoints.Toggle(workspace.ActiveDocument!.FilePath, CreateGreeter);
        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        await WaitForPauseAsync(workspace, CreateGreeter);
        var builds = 0;
        workspace.PropertyChanged += (_, e) => builds += e.PropertyName == nameof(WorkspaceViewModel.State) && workspace.State == IdeState.Busy ? 1 : 0;

        workspace.RestartCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => builds == 1 && workspace.IsPaused, because: "the restarted program to pause");
        await workspace.PauseShown;

        Assert.Equal(CreateGreeter, workspace.CallStack.Frames[0].Location?.Line);
        workspace.StopDebuggingCommand.Execute(null);
        await WaitForExitAsync(workspace);
    }

    [AvaloniaFact]
    public async Task Run_to_cursor_starts_the_program_and_stops_at_the_caret_line()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        fixture.AnswerInput("Ada");
        workspace.ActiveDocument!.CaretLine = Greet;

        await workspace.RunToCursorCommand.ExecuteAsync(null);
        await WaitForPauseAsync(workspace, Greet);

        Assert.StartsWith("Paused at the cursor", workspace.StatusText, StringComparison.Ordinal);
        workspace.StopDebuggingCommand.Execute(null);
        await WaitForExitAsync(workspace);
    }

    [AvaloniaFact]
    public async Task An_unhandled_exception_pauses_at_the_line_that_threw_with_the_exception_helper()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        var program = workspace.ActiveDocument!;
        program.Document.Text = ThrowingProgram;

        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => program.ExceptionHelper is not null, because: "the exception helper");

        var helper = program.ExceptionHelper!;
        Assert.Equal("System.InvalidOperationException", helper.ExceptionType);
        Assert.Equal("Sequence contains no elements", helper.Message);
        Assert.Contains("at <top-level statements> in Program.cs:line 3", helper.Details, StringComparison.Ordinal);
        Assert.Equal(3, LineOf(program, program.CurrentStatement));
        Assert.Equal("Exception Unhandled: System.InvalidOperationException", workspace.StatusText);

        helper.ContinueCommand.Execute(null);
        await WaitForExitAsync(workspace);

        Assert.Equal(IdeState.Failed, workspace.State);
        Assert.Contains("The program 'HelloTablet' has exited with code -1 (0xffffffff).", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Null(program.ExceptionHelper);
    }

    [AvaloniaFact]
    public async Task Ctrl_F5_runs_without_the_debugger_and_ignores_breakpoints()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        fixture.AnswerInput("Ada");
        workspace.Breakpoints.Toggle(workspace.ActiveDocument!.FilePath, CreateGreeter);
        var paused = false;
        workspace.PropertyChanged += (_, e) => paused |= e.PropertyName == nameof(WorkspaceViewModel.IsPaused) && workspace.IsPaused;

        await workspace.RunCommand.ExecuteAsync(null);

        Assert.False(paused);
        Assert.Contains("has exited with code 0", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Equal(IdeState.Ready, workspace.State);
    }

    [AvaloniaFact]
    public async Task The_call_stack_shows_callers_and_choosing_one_marks_its_call_in_green()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        fixture.AnswerInput("Ada");
        var greeterPath = Path.Combine(workspace.Workspace!.Projects.Single(p => p.Name == "Greetings").DirectoryPath, "Greeter.cs");
        workspace.Breakpoints.Toggle(greeterPath, BuildGreeting);

        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        await WaitForPauseAsync(workspace, BuildGreeting);

        Assert.Equal(new[] { "Greeter.Greet()", "<top-level statements>" }, workspace.CallStack.Frames.Select(f => f.MethodName));
        var greeter = workspace.ActiveDocument!;
        Assert.Equal("Greeter.cs", greeter.FileName);
        Assert.Equal(BuildGreeting, LineOf(greeter, greeter.CurrentStatement));

        workspace.CallStack.Activate(workspace.CallStack.Frames[1]);
        await UiTestHelpers.WaitUntilAsync(() => workspace.ActiveDocument?.FileName == "Program.cs", because: "the caller's file");

        var program = workspace.ActiveDocument!;
        Assert.Equal(Greet, LineOf(program, program.CallerStatement));
        Assert.Null(program.CurrentStatement);
        Assert.Equal(BuildGreeting, LineOf(greeter, greeter.CurrentStatement));
        Assert.True(workspace.CallStack.Frames[1].IsActiveCaller);
        Assert.Contains("Greeter.Greet()", workspace.Locals.Note, StringComparison.Ordinal);

        workspace.ShowNextStatementCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.ActiveDocument?.FileName == "Greeter.cs", because: "the current statement");
        Assert.Null(program.CallerStatement);

        workspace.StopDebuggingCommand.Execute(null);
        await WaitForExitAsync(workspace);
    }

    [AvaloniaFact]
    public async Task Watches_and_DataTips_show_values_of_the_paused_program()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        var program = workspace.ActiveDocument!;
        fixture.AnswerInput("Ada");
        await workspace.Watch.AddAsync("languages[0].Name");
        await workspace.Watch.AddAsync("missing");
        workspace.Breakpoints.Toggle(program.FilePath, AfterLanguages);

        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        await WaitForPauseAsync(workspace, AfterLanguages);
        await UiTestHelpers.WaitUntilAsync(
            () => workspace.Watch.Tree.Rows.Any(r => r.Name == "languages[0].Name" && r.Value == "\"C#\""),
            because: "the watch values");

        Assert.True(workspace.Watch.Tree.Rows.Single(r => r.Name == "missing").IsError);

        var dataTip = await ((IEditorDebugger)workspace).EvaluateDataTipAsync("languages");
        Assert.NotNull(dataTip);
        var root = Assert.Single(dataTip.Rows);
        Assert.Equal("Length = 4", root.Value);
        await dataTip.ToggleCommand.ExecuteAsync(root);
        Assert.Equal(new[] { "languages", "[0]", "[1]", "[2]", "[3]" }, dataTip.Rows.Select(r => r.Name));
        Assert.Null(await ((IEditorDebugger)workspace).EvaluateDataTipAsync("nothing.here"));

        workspace.StopDebuggingCommand.Execute(null);
        await WaitForExitAsync(workspace);
    }

    [AvaloniaFact]
    public async Task Breakpoints_follow_edits_and_come_back_with_the_session()
    {
        using var first = new WorkspaceFixture();
        var solution = await first.OpenSampleAsync();
        var program = first.Workspace.ActiveDocument!;
        program.CaretLine = CreateGreeter;

        first.Workspace.ToggleBreakpointCommand.Execute(null);
        var breakpoint = Assert.Single(first.Workspace.Breakpoints.All);
        Assert.Same(breakpoint, Assert.Single(program.Breakpoints));

        program.Document.Insert(0, "// one\n// two\n");
        Assert.Equal(CreateGreeter + 2, breakpoint.Line);
        program.Document.Remove(0, "// one\n// two\n".Length);
        Assert.Equal(CreateGreeter, breakpoint.Line);

        first.Workspace.Breakpoints.SetCondition(breakpoint, "name != null");
        await first.Workspace.Watch.AddAsync("name");
        first.Workspace.SaveSessionNow();

        using var second = new WorkspaceViewModel(first.Compiler, first.DeviceStatus, first.Profile, new AvaloniaUiDispatcher(), first.Sessions, first.Paths);
        await second.LoadAsync(solution, first.Sessions.Load());

        var restored = Assert.Single(second.Breakpoints.All);
        Assert.Equal(CreateGreeter, restored.Line);
        Assert.Equal("name != null", restored.Condition);
        Assert.Equal(BreakpointGlyph.Conditional, restored.Glyph);
        var reopened = second.ActiveDocument!;
        Assert.Same(restored, Assert.Single(reopened.Breakpoints));
        Assert.Equal(new[] { "name" }, second.Watch.Expressions);

        second.DeleteAllBreakpointsCommand.Execute(null);
        Assert.Empty(second.Breakpoints.All);
        Assert.Empty(reopened.Breakpoints);
    }

    [AvaloniaFact]
    public async Task A_CSharp_breakpoint_with_a_hit_count_stops_on_that_hit_and_the_editor_offers_it()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        var program = workspace.ActiveDocument!;
        program.Document.Text = """
            var total = 0;
            for (var i = 0; i < 5; i++)
            {
                total += i;
            }
            Console.WriteLine(total);
            """;
        var options = (IEditorBreakpointOptions)workspace;
        Assert.True(options.HasBreakpointOptions(program.FilePath));
        Assert.False(options.HasLogpoints(program.FilePath));

        var edit = options.EditHitCountAsync(program.FilePath, 4);
        await UiTestHelpers.WaitUntilAsync(() => workspace.Prompt.IsOpen, because: "the hit count prompt");
        workspace.Prompt.Text = "> 3";
        workspace.Prompt.ConfirmCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.Prompt.IsOpen && workspace.Prompt.Message.Contains("is not a hit count", StringComparison.Ordinal), because: "the prompt to refuse > 3");
        workspace.Prompt.Text = "= 3";
        workspace.Prompt.ConfirmCommand.Execute(null);
        await edit;

        var breakpoint = Assert.Single(workspace.Breakpoints.All);
        Assert.Equal("= 3", breakpoint.HitCount);
        Assert.Equal(BreakpointGlyph.Conditional, breakpoint.Glyph);

        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        await WaitForPauseAsync(workspace, 4);
        await WaitForLocalAsync(workspace, "i");
        Assert.Equal("2", workspace.Locals.Tree.Rows.Single(r => r.Name == "i").Value);

        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        await WaitForExitAsync(workspace);
        Assert.Contains("10", workspace.Output.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task The_call_stack_lists_the_programs_threads_and_shows_the_one_chosen()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        var program = workspace.ActiveDocument!;
        program.Document.Text = """
            var running = true;
            var worker = new Thread(() =>
            {
                var spins = 0;
                while (Volatile.Read(ref running))
                {
                    spins++;
                }
            }) { Name = "Worker", IsBackground = true };
            worker.Start();
            Thread.Sleep(300);
            Console.WriteLine("paused here");
            running = false;
            worker.Join();
            """;
        workspace.Breakpoints.Toggle(program.FilePath, 12);

        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        await WaitForPauseAsync(workspace, 12);
        await UiTestHelpers.WaitUntilAsync(() => workspace.CallStack.HasThreads, because: "the worker thread");

        var worker = Assert.Single(workspace.CallStack.Threads, t => t.Name == "Worker");
        Assert.NotEqual(worker, workspace.CallStack.SelectedThread);
        workspace.CallStack.SelectedThread = worker;
        await UiTestHelpers.WaitUntilAsync(() => workspace.CallStack.Frames.FirstOrDefault()?.Location?.Line is 5 or 7, because: "the worker's frames");
        Assert.Contains(workspace.CallStack.Frames[0].MethodName, workspace.Locals.Note ?? string.Empty, StringComparison.Ordinal);

        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        await WaitForExitAsync(workspace);
        Assert.Empty(workspace.CallStack.Threads);
    }

    [AvaloniaFact]
    public async Task Stop_cancels_a_build_in_progress_says_so_and_the_next_build_runs()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;

        var build = workspace.BuildCommand.ExecuteAsync(null);
        Assert.True(workspace.StopDebuggingCommand.CanExecute(null));
        workspace.StopDebuggingCommand.Execute(null);
        await build;

        Assert.Contains("========== Build: cancelled ==========", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Equal(IdeState.Ready, workspace.State);
        Assert.Equal("Build cancelled", workspace.StatusText);
        Assert.False(workspace.StopDebuggingCommand.CanExecute(null));

        await workspace.BuildCommand.ExecuteAsync(null);
        Assert.Contains(" succeeded, 0 failed", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Equal("Build succeeded", workspace.StatusText);
    }

    private const string ThrowingProgram = """
        var numbers = new List<int>();
        Console.WriteLine("Looking for the first number");
        var first = numbers.First();
        Console.WriteLine(first);
        """;

    private static int? LineOf(DocumentViewModel document, ExecutionPoint? point) =>
        point is null ? null : document.Document.GetLineByOffset(point.Start).LineNumber;

    /// <summary>Waits until the program pauses at a line and the IDE has shown the pause.</summary>
    internal static async Task WaitForPauseAsync(WorkspaceViewModel workspace, int line)
    {
        await UiTestHelpers.WaitUntilAsync(
            () => workspace.IsPaused && workspace.CallStack.Frames.Count > 0 && workspace.CallStack.Frames[0].Location?.Line == line,
            because: $"a pause at line {line}");
        await workspace.PauseShown;
    }

    private static Task WaitForLocalAsync(WorkspaceViewModel workspace, string name) =>
        UiTestHelpers.WaitUntilAsync(
            () => !workspace.Locals.Tree.IsStale && workspace.Locals.Tree.Rows.Any(r => r.Name == name),
            because: $"the local '{name}'");

    /// <summary>Waits until the debugged program has ended.</summary>
    internal static Task WaitForExitAsync(WorkspaceViewModel workspace) =>
        UiTestHelpers.WaitUntilAsync(() => !workspace.IsDebugging && workspace.State is IdeState.Ready or IdeState.Failed, because: "the program to end");
}
