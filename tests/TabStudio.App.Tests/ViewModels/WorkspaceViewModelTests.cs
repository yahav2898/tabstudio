using Avalonia.Headless.XUnit;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.Core.Diagnostics;

namespace TabStudio.App.Tests.ViewModels;

public sealed class WorkspaceViewModelTests
{
    [AvaloniaFact]
    public async Task Opening_the_sample_shows_its_projects_and_opens_Program_cs()
    {
        using var fixture = new WorkspaceFixture();

        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;

        Assert.Equal("HelloTablet", workspace.Title);
        Assert.Equal("HelloTablet", workspace.StartupProject?.Name);
        Assert.Equal("Program.cs", workspace.ActiveDocument?.FileName);
        Assert.Contains(workspace.Explorer.Rows, r => r.Name == "Greetings" && r.IsProject);
        Assert.Contains(workspace.Compatibility, r => r.Project == "HelloTablet" && r.IsReady);
        Assert.Equal(IdeState.Ready, workspace.State);
    }

    [AvaloniaFact]
    public async Task The_sample_has_no_errors()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var document = fixture.Workspace.ActiveDocument!;

        await document.AnalyzeNowAsync();
        await UiTestHelpers.WaitUntilAsync(() => document.Classifications.Count > 0, because: "classification");

        Assert.DoesNotContain(document.Diagnostics, d => d.Severity == DiagnosticSeverityLevel.Error);
        Assert.Contains(document.Classifications, c => c.Classification == "class name");
    }

    [AvaloniaFact]
    public async Task Typing_a_mistake_shows_an_error_in_the_editor_and_the_Error_List()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var document = fixture.Workspace.ActiveDocument!;

        var offset = document.Document.Text.IndexOf("greeter.Greet()", StringComparison.Ordinal);
        document.Document.Replace(offset, "greeter".Length, "greeterr");
        await document.AnalyzeNowAsync();

        Assert.Contains(document.Diagnostics, d => d.Id == "CS0103");
        Assert.Contains(fixture.Workspace.ErrorList.Items, i => i.Code == "CS0103" && i.File == "Program.cs");
        Assert.True(document.IsDirty);
        Assert.Equal("Program.cs*", document.Title);
    }

    [AvaloniaFact]
    public async Task F5_builds_and_runs_the_program_with_console_input()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        fixture.AnswerInput("Ada");

        await workspace.RunCommand.ExecuteAsync(null);
        var output = workspace.Output.Text;

        Assert.Contains("========== Build: 1 succeeded, 0 failed ==========", output, StringComparison.Ordinal);
        Assert.Contains("What's your name? Ada", output, StringComparison.Ordinal);
        Assert.Contains("Ada! This program was compiled and run on your tablet.", output, StringComparison.Ordinal);
        Assert.Contains("has exited with code 0", output, StringComparison.Ordinal);
        Assert.Equal(IdeState.Ready, workspace.State);
    }

    [AvaloniaFact]
    public async Task A_build_error_stops_F5_and_shows_the_Error_List()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        workspace.ActiveDocument!.Document.Insert(0, "this does not compile;\n");

        await workspace.RunCommand.ExecuteAsync(null);

        Assert.Equal(IdeState.Failed, workspace.State);
        Assert.Equal(BottomPanel.ErrorList, workspace.SelectedPanel);
        Assert.True(workspace.ErrorList.ErrorCount > 0);
        Assert.Contains("0 succeeded, 1 failed", workspace.Output.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Format_document_fixes_the_layout_as_one_undo_step()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var greeter = fixture.Workspace.Workspace!.Projects.Single(p => p.Name == "Greetings").DirectoryPath;
        var document = (await fixture.Workspace.OpenDocumentAsync(Path.Combine(greeter, "Greeter.cs")))!;
        document.Document.Insert(0, "   ");

        Assert.True(await document.FormatAsync());
        Assert.StartsWith("namespace Greetings;", document.Document.Text, StringComparison.Ordinal);

        document.Document.UndoStack.Undo();
        Assert.StartsWith("   namespace", document.Document.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Saving_writes_the_file_and_clears_the_asterisk()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var document = fixture.Workspace.ActiveDocument!;
        document.Document.Insert(0, "// saved\n");

        await fixture.Workspace.SaveCommand.ExecuteAsync(null);

        Assert.False(document.IsDirty);
        Assert.StartsWith("// saved\n", await File.ReadAllTextAsync(document.FilePath), StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task The_session_restores_open_documents_and_unsaved_text()
    {
        string solution;
        using (var first = new WorkspaceFixture())
        {
            solution = await first.OpenSampleAsync();
            first.Workspace.ActiveDocument!.Document.Insert(0, "// not saved yet\n");
            first.Workspace.SaveSessionNow();

            using var second = new WorkspaceViewModel(first.Compiler, first.DeviceStatus, first.Profile, new AvaloniaUiDispatcher(), first.Sessions);
            await second.LoadAsync(solution, first.Sessions.Load());

            Assert.Equal("Program.cs", second.ActiveDocument?.FileName);
            Assert.True(second.ActiveDocument!.IsDirty);
            Assert.StartsWith("// not saved yet", second.ActiveDocument.Document.Text, StringComparison.Ordinal);
        }
    }

    [AvaloniaFact]
    public async Task A_warm_device_builds_on_one_thread_and_says_so()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();

        fixture.DeviceStatus.SetThermal(ThermalState.Warm);
        await UiTestHelpers.WaitUntilAsync(() => fixture.Workspace.IsThrottled, because: "the thermal update");

        Assert.Contains("one core", fixture.Workspace.ThermalText, StringComparison.Ordinal);
        fixture.AnswerInput("Ada");
        await fixture.Workspace.RunCommand.ExecuteAsync(null);
        Assert.Contains("1 succeeded", fixture.Workspace.Output.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Memory_pressure_keeps_the_workspace_working()
    {
        using var fixture = new WorkspaceFixture(new DeviceInfo(3L * 1024 * 1024 * 1024, 4, IsLowRamDevice: true));
        await fixture.OpenSampleAsync();
        Assert.Equal(DeviceClass.Compact, fixture.Profile.Class);

        fixture.DeviceStatus.RaiseMemoryPressure(MemoryPressure.Critical);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var document = fixture.Workspace.ActiveDocument!;
        await document.AnalyzeNowAsync();

        Assert.DoesNotContain(document.Diagnostics, d => d.Severity == DiagnosticSeverityLevel.Error);
        Assert.Equal("Lite mode · 3 GB", fixture.Workspace.DeviceText);
    }

    [AvaloniaFact]
    public async Task Quick_open_finds_files_by_name()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();

        fixture.Workspace.QuickOpenText = "greet";

        var result = Assert.Single(fixture.Workspace.QuickOpenResults);
        Assert.Equal("Greeter.cs", result.Name);
        await fixture.Workspace.OpenQuickOpenItemCommand.ExecuteAsync(result);
        Assert.Equal("Greeter.cs", fixture.Workspace.ActiveDocument?.FileName);
    }
}
