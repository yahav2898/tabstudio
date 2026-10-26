using Avalonia.Headless.XUnit;
using Avalonia.Controls;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Testing;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.App.Tests.Toolchains;

public sealed class EmbeddedTestWorkflowTests
{
    [AvaloniaFact]
    public async Task Native_host_harness_results_are_captured_beyond_the_Output_process_tail_limit()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        using var fixture = new WorkspaceFixture();
        using var project = new TempDirectory();
        project.Write("platformio.ini", "[env:uno]\nboard=uno\nplatform=atmelavr\n");
        var source = project.Write("host_tests.c", "#include <stdio.h>\nint main(void){for(int i=0;i<600;i++) printf(\"test_sensor.c:%d:test_%d:PASS\\n\",i+1,i); puts(\"600 Tests 0 Failures 0 Ignored\"); return 0;}\n");
        var binary = project.Resolve("host_tests");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var compiled = await CommandRunner.RunAsync(new HostRuntime(), new ProcessStartSpec
        {
            Program = "cc", Arguments = [source, "-o", binary], WorkingDirectory = project.Path,
        }, null, timeout.Token);
        Assert.True(compiled.Succeeded, compiled.Output);
        project.Write(".tabstudio/embedded-tests.json", "{\"format\":\"Unity\",\"executable\":\"host_tests\",\"hostId\":\"linux-host\"}");
        var workspace = fixture.Workspace;
        workspace.SetUpProjectsWhenTheyOpen = false;
        await workspace.LoadAsync(project.Path);
        await workspace.RunEmbeddedTestsCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(600, workspace.Tests.PassedCount);
        Assert.Equal(0, workspace.Tests.FailedCount);
        Assert.Equal(IdeState.Ready, workspace.State);
        Assert.True(workspace.IsTestExplorerSelected);
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = workspace } };
        window.Show();
        try
        {
            UiTestHelpers.SaveScreenshot(window, "embedded-02-host-tests");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task An_actual_decoded_board_log_is_imported_through_the_real_command_and_explicit_identity()
    {
        using var fixture = new WorkspaceFixture();
        using var project = new TempDirectory();
        project.Write("platformio.ini", "[env:uno]\nboard=uno\nplatform=atmelavr\n");
        var path = project.Write("board-results.txt", "test/sensor.c:12:test_ready:PASS\ntest/sensor.c:20:test_fault:FAIL:Expected ready\n2 Tests 1 Failures 0 Ignored\n");
        var workspace = fixture.Workspace;
        workspace.SetUpProjectsWhenTheyOpen = false;
        await workspace.LoadAsync(project.Path);
        var importing = workspace.ImportEmbeddedTestResultsCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.Prompt.IsOpen && workspace.Prompt.Title == "Import embedded test results", TimeSpan.FromSeconds(10));
        workspace.Prompt.Text = path;
        workspace.Prompt.ConfirmCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.Prompt.IsOpen && workspace.Prompt.Title == "Test result format", TimeSpan.FromSeconds(10));
        workspace.Prompt.SelectedIndex = 0;
        workspace.Prompt.ConfirmCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.Prompt.IsOpen && workspace.Prompt.Title == "Test board identity", TimeSpan.FromSeconds(10));
        workspace.Prompt.Text = "USB-serial-ABC123";
        workspace.Prompt.ConfirmCommand.Execute(null);
        await importing.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal((2, 1, 1), (workspace.Tests.TotalCount, workspace.Tests.PassedCount, workspace.Tests.FailedCount));
        Assert.True(workspace.IsTestExplorerSelected);
        Assert.Contains(workspace.Tests.Entries, e => e.DisplayName.Contains("USB-serial-ABC123", StringComparison.Ordinal));
        Assert.Equal(TestState.Failed, workspace.Tests.Entries.Single(e => e.MethodName == "test_fault").State);
    }
}
