using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Microsoft.CodeAnalysis;
using TabStudio.App.Previews;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Compilation;
using TabStudio.Compilation.HotReload;

namespace TabStudio.App.Tests.ViewModels;

public sealed class HotReloadTests
{
    private const string ConsoleSource = """
        Console.WriteLine("Started once");
        while (Console.ReadLine() != null)
        {
            Console.WriteLine(Probe.Message());
        }
        public static class Probe
        {
            public static string Message() => "before";
        }
        """;

    [AvaloniaFact]
    public async Task Toolbar_applies_unsaved_console_edits_without_restarting_and_on_save_applies_a_second_generation()
    {
        Assert.True(HotReloadSession.IsSupported, "Launch tests with DOTNET_MODIFIABLE_ASSEMBLIES=debug.");
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        var document = workspace.ActiveDocument!;
        document.Document.Text = ConsoleSource;
        var run = workspace.RunCommand.ExecuteAsync(null);
        try
        {
            await UiTestHelpers.WaitUntilAsync(() => workspace.Output.IsWaitingForInput || run.IsCompleted, because: "the console app to wait for input");
            Assert.False(run.IsCompleted, workspace.Output.Text);
            document.Document.Text = ConsoleSource.Replace("before", "after", StringComparison.Ordinal);
            Assert.True(document.IsDirty);

            await workspace.ApplyHotReloadCommand.ExecuteAsync(null);
            Assert.Equal(1, workspace.HotReloadGeneration);
            Assert.True(document.IsDirty); // Applying edits doesn't force the user to save.
            Submit(workspace, "first");
            await UiTestHelpers.WaitUntilAsync(() => workspace.Output.Text.Contains("after", StringComparison.Ordinal), because: "the edited method to print its new output");

            workspace.HotReloadOnSave = true;
            document.Document.Text = ConsoleSource.Replace("before", "second", StringComparison.Ordinal);
            await workspace.SaveCommand.ExecuteAsync(null);
            Assert.Equal(2, workspace.HotReloadGeneration);
            Submit(workspace, "second");
            await UiTestHelpers.WaitUntilAsync(() => workspace.Output.Text.Contains("second", StringComparison.Ordinal), because: "the next generation to print");
            Assert.Equal(1, workspace.Output.Text.Split("Started once", StringSplitOptions.None).Length - 1);
            Assert.False(run.IsCompleted);
        }
        finally
        {
            workspace.StopCommand.Execute(null);
            await run.WaitAsync(TimeSpan.FromSeconds(15));
        }
    }

    [AvaloniaFact]
    public async Task Continue_applies_edits_at_a_breakpoint_and_rude_edits_keep_the_program_paused()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        var document = workspace.ActiveDocument!;
        document.Document.Text = """
            public static class Program
            {
                public static void Main()
                {
                    Console.WriteLine(Probe.Message());
                }
            }
            public static class Probe
            {
                public static string Message() => "before";
            }
            """;
        workspace.Breakpoints.Toggle(document.FilePath, 5);
        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        try
        {
            await UiTestHelpers.WaitUntilAsync(() => workspace.IsPaused, because: "the breakpoint before the call");
            await workspace.PauseShown;
            var original = document.CurrentText;
            document.Document.Text = original.Replace("string Message()", "string Message(int value)", StringComparison.Ordinal)
                .Replace("Probe.Message()", "Probe.Message(0)", StringComparison.Ordinal);
            await workspace.StartDebuggingCommand.ExecuteAsync(null);
            Assert.True(workspace.IsPaused);
            Assert.True(workspace.HotReloadNeedsRestart);
            Assert.Contains("restart", workspace.StatusText, StringComparison.OrdinalIgnoreCase);

            document.Document.Text = original.Replace("before", "after", StringComparison.Ordinal);
            await workspace.StartDebuggingCommand.ExecuteAsync(null);
            await UiTestHelpers.WaitUntilAsync(() => !workspace.IsDebugging, because: "Continue to finish the original run");
            Assert.Contains("after", workspace.Output.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("before", workspace.Output.Text, StringComparison.Ordinal);
        }
        finally
        {
            workspace.StopDebuggingCommand.Execute(null);
        }
    }

    [AvaloniaFact]
    public void Avalonia_refresh_recreates_content_from_the_same_loaded_assembly_and_preserves_static_state()
    {
        Assert.True(HotReloadSession.IsSupported, "Launch tests with DOTNET_MODIFIABLE_ASSEMBLIES=debug.");
        var compiler = new CSharpCompiler();
        var references = Directory.GetFiles(AppContext.BaseDirectory, "Avalonia*.dll")
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToArray();
        var source = """
            using Avalonia;
            using Avalonia.Controls;
            using Avalonia.Controls.ApplicationLifetimes;
            public class App : Application
            {
                public static int Views;
                public override void OnFrameworkInitializationCompleted()
                {
                    Views++;
                    ((ISingleViewApplicationLifetime)ApplicationLifetime!).MainView = new TextBlock { Text = Message() };
                }
                public static string Message() => "before";
            }
            """;
        CompiledAssembly Compile(string text)
        {
            var result = compiler.Compile(new CompilationRequest
            {
                AssemblyName = "HotReloadAvalonia",
                Kind = AssemblyKind.Library,
                AdditionalReferences = references,
                Documents = [new SourceDocument("App.cs", text)],
            });
            Assert.True(result.Success, string.Join('\n', result.Diagnostics));
            return result.Assembly!;
        }

        var original = Compile(source);
        using var app = AvaloniaAppHost.Start(original, [], []);
        using var session = new HotReloadSession(original, [], app.EntryAssembly);
        var assembly = app.EntryAssembly;
        var before = app.MainView;
        Assert.Equal("before", Assert.IsType<TextBlock>(before).Text);

        Assert.Equal(HotReloadStatus.Applied, session.Apply([Compile(source.Replace("before", "after", StringComparison.Ordinal))]).Status);
        app.RefreshContent();

        Assert.Same(assembly, app.EntryAssembly);
        Assert.NotSame(before, app.MainView);
        Assert.Equal("after", Assert.IsType<TextBlock>(app.MainView).Text);
        Assert.Equal(2, assembly.GetType("App")!.GetField("Views")!.GetValue(null));

        Assert.Equal(HotReloadStatus.Applied, session.Apply([Compile(source)]).Status);
        app.RefreshContent();
        Assert.Same(assembly, app.EntryAssembly);
        Assert.Equal("before", Assert.IsType<TextBlock>(app.MainView).Text);
        Assert.Equal(3, assembly.GetType("App")!.GetField("Views")!.GetValue(null));
    }

    [AvaloniaFact]
    public async Task Workspace_exposes_the_shortcut_and_rude_edit_restart_action()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var view = new WorkspaceView { DataContext = fixture.Workspace };
        Assert.Contains(view.KeyBindings, b => b.Gesture.ToString() == "Alt+F10" && ReferenceEquals(b.Command, fixture.Workspace.ApplyHotReloadCommand));
        var menu = view.GetLogicalDescendants().OfType<Menu>().First();
        var onSave = MenuItems(menu).Single(item => Equals(item.Header, "Hot Reload C# on Save"));
        onSave.SetCurrentValue(MenuItem.IsCheckedProperty, true);
        Assert.True(fixture.Workspace.HotReloadOnSave);
        onSave.SetCurrentValue(MenuItem.IsCheckedProperty, false);
        Assert.False(fixture.Workspace.HotReloadOnSave);
    }

    private static IEnumerable<MenuItem> MenuItems(ItemsControl parent)
    {
        foreach (var item in parent.Items.OfType<MenuItem>())
        {
            yield return item;
            foreach (var child in MenuItems(item)) yield return child;
        }
    }

    [AvaloniaFact]
    public async Task Saving_a_Visual_Basic_file_applies_Hot_Reload_as_saving_a_CSharp_file_does()
    {
        // Hot Reload on Save looked only at .cs files; Alt+F10 already applied Visual Basic edits.
        using var fixture = new WorkspaceFixture();
        var workspace = fixture.Workspace;
        var solution = new ProjectTemplates(fixture.Paths).Create(ProjectTemplates.VisualBasicConsoleId, "Greeter");
        await workspace.LoadAsync(solution);
        var program = Path.Combine(Path.GetDirectoryName(solution)!, "Greeter", "Program.vb");
        var document = (await workspace.OpenDocumentAsync(program))!;
        var run = workspace.RunCommand.ExecuteAsync(null);
        try
        {
            await UiTestHelpers.WaitUntilAsync(() => workspace.Output.IsWaitingForInput || run.IsCompleted, because: "the program to ask for a name");
            Assert.False(run.IsCompleted, workspace.Output.Text);
            workspace.HotReloadOnSave = true;
            document.Document.Text = document.Document.Text.Replace("This is Visual Basic", "Saved and reloaded", StringComparison.Ordinal);
            await workspace.SaveCommand.ExecuteAsync(null);
            Assert.Equal(1, workspace.HotReloadGeneration);
            Submit(workspace, "Ada");
            await UiTestHelpers.WaitUntilAsync(() => workspace.Output.Text.Contains("Hello, Ada! Saved and reloaded", StringComparison.Ordinal), because: "the edited function to run");
        }
        finally
        {
            workspace.StopCommand.Execute(null);
            await run.WaitAsync(TimeSpan.FromSeconds(15));
        }
    }

    private static void Submit(WorkspaceViewModel workspace, string text)
    {
        workspace.Output.InputText = text;
        workspace.Output.SubmitInputCommand.Execute(null);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Recording_requires_restart_for_edits_but_unchanged_Continue_preserves_live_execution_and_history(bool lineOnly)
    {
        Assert.True(HotReloadSession.IsSupported, "Launch tests with DOTNET_MODIFIABLE_ASSEMBLIES=debug.");
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        var document = workspace.ActiveDocument!;
        const string source = """
            public static class Program
            {
                public static void Main()
                {
                    int value = 1;
                    Console.WriteLine(Probe.Message(value));
                }
            }
            public static class Probe
            {
                public static string Message(int value) => "before";
            }
            """;
        document.Document.Text = source;
        workspace.Breakpoints.Toggle(document.FilePath, 6);
        await workspace.StartWithRecordingCommand.ExecuteAsync(null);
        try
        {
            await DebuggingTests.WaitForPauseAsync(workspace, 6);
            document.Document.Text = lineOnly ? "\n" + source : source.Replace("before", "after", StringComparison.Ordinal);
            await workspace.ApplyHotReloadCommand.ExecuteAsync(null);
            Assert.True(workspace.HotReloadNeedsRestart);
            Assert.Contains("while recording", workspace.StatusText, StringComparison.Ordinal);
            Assert.True(workspace.IsPaused);
            Assert.Equal(0, workspace.HotReloadGeneration);

            await workspace.StartDebuggingCommand.ExecuteAsync(null);
            Assert.True(workspace.IsPaused);
            await workspace.StepBackCommand.ExecuteAsync(null);
            Assert.True(workspace.IsHistorical);
            await workspace.ReturnToLiveCommand.ExecuteAsync(null);
            Assert.Equal(6, workspace.CallStack.Frames[0].Location!.Line);

            document.Document.Text = source;
            await workspace.StartDebuggingCommand.ExecuteAsync(null);
            await DebuggingTests.WaitForExitAsync(workspace);
            Assert.Contains("before", workspace.Output.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("after", workspace.Output.Text, StringComparison.Ordinal);
            Assert.True(workspace.HasRecording);
            await workspace.StepBackCommand.ExecuteAsync(null);
            Assert.True(workspace.IsHistorical);
        }
        finally
        {
            workspace.StopDebuggingCommand.Execute(null);
        }
    }

    [AvaloniaFact]
    public async Task Restart_to_apply_edits_starts_a_new_recording_and_runs_the_edited_code()
    {
        Assert.True(HotReloadSession.IsSupported, "Launch tests with DOTNET_MODIFIABLE_ASSEMBLIES=debug.");
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        var document = workspace.ActiveDocument!;
        document.Document.Text = """
            public static class Program
            {
                public static void Main()
                {
                    int value = 1;
                    Console.WriteLine(Probe.Message(value));
                }
            }
            public static class Probe
            {
                public static string Message(int value) => "before";
            }
            """;
        workspace.Breakpoints.Toggle(document.FilePath, 6);
        await workspace.StartWithRecordingCommand.ExecuteAsync(null);
        try
        {
            await DebuggingTests.WaitForPauseAsync(workspace, 6);
            document.Document.Text = document.CurrentText.Replace("before", "after", StringComparison.Ordinal);
            await workspace.ApplyHotReloadCommand.ExecuteAsync(null);
            Assert.True(workspace.HotReloadNeedsRestart);
            Assert.True(document.IsDirty);

            workspace.RestartForHotReloadCommand.Execute(null);
            await UiTestHelpers.WaitUntilAsync(() => !document.IsDirty && workspace.IsPaused,
                because: "the restarted recording's saved build and breakpoint");
            await workspace.PauseShown;
            Assert.True(workspace.HasRecording);
            await workspace.StartDebuggingCommand.ExecuteAsync(null);
            await DebuggingTests.WaitForExitAsync(workspace);
            Assert.Contains("after", workspace.Output.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("before", workspace.Output.Text, StringComparison.Ordinal);
            Assert.True(workspace.HasRecording);
            await workspace.StepBackCommand.ExecuteAsync(null);
            Assert.True(workspace.IsHistorical);
        }
        finally
        {
            workspace.StopDebuggingCommand.Execute(null);
        }
    }
}
