using Avalonia.Headless.XUnit;
using TabStudio.App.Tests.ViewModels;
using TabStudio.Testing;
using TabStudio.Toolchains.Processes;

namespace TabStudio.App.Tests.Toolchains;

public sealed class EmbeddedReplTerminalTests
{
    [AvaloniaFact]
    public async Task The_REPL_program_path_owns_the_real_PTY_until_the_client_stops()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        using var fixture = new WorkspaceFixture();
        using var project = new TempDirectory();
        var workspace = fixture.Workspace;
        await workspace.LoadAsync(project.Path);
        using var cancellation = new CancellationTokenSource();
        // cat is a real interactive native client fixture; this tests the REPL terminal lifecycle, not a board.
        var running = workspace.Terminal.RunProgramAsync(new ProcessStartSpec
        {
            Program = "cat", WorkingDirectory = project.Path,
        }, "MicroPython client fixture", cancellation.Token);
        await UiTestHelpers.WaitUntilAsync(() => workspace.Terminal.IsRunning, TimeSpan.FromSeconds(10));
        Assert.Equal("MicroPython client fixture", workspace.Terminal.Title);
        workspace.Terminal.SendText("client-received-text\r");
        await UiTestHelpers.WaitUntilAsync(() => workspace.Terminal.Emulator.ScreenText.Contains("client-received-text", StringComparison.Ordinal), TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        var exit = await running.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(exit.WasStopped);
        await UiTestHelpers.WaitUntilAsync(() => !workspace.Terminal.IsRunning, TimeSpan.FromSeconds(10));
        Assert.Contains("was stopped", workspace.Terminal.Emulator.ScreenText, StringComparison.Ordinal);
    }
}
