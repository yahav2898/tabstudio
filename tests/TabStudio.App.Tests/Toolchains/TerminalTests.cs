using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Controls;
using TabStudio.App.Services;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Testing;
using TabStudio.Toolchains.Terminals;

namespace TabStudio.App.Tests.Toolchains;

/// <summary>The Terminal panel, over the computer's own shell: commands run, keys and colors arrive.</summary>
public sealed class TerminalTests
{
    [AvaloniaFact]
    public async Task The_terminal_runs_a_command_in_the_workspace_folder()
    {
        using var fixture = new WorkspaceFixture();
        using var folder = new TempDirectory();
        folder.Write("notes.txt", "tablet\n");
        var workspace = fixture.Workspace;
        await workspace.LoadAsync(folder.Path);

        workspace.ShowTerminalCommand.Execute(null);
        var terminal = workspace.Terminal;
        Assert.True(workspace.IsTerminalSelected);
        await UiTestHelpers.WaitUntilAsync(() => terminal.IsRunning, because: "the shell");

        terminal.SendText("cat notes.txt; echo sum=$((6*7))\r");

        await UiTestHelpers.WaitUntilAsync(() => terminal.Emulator.ScreenText.Contains("sum=42", StringComparison.Ordinal), because: "the command's output");
        Assert.Contains("\ntablet\n", "\n" + terminal.Emulator.ScreenText + "\n", StringComparison.Ordinal);

        terminal.SendText("exit\r");
        await UiTestHelpers.WaitUntilAsync(() => !terminal.IsRunning, because: "the shell to exit");
        Assert.Contains("Press Enter to start a new one.", terminal.Emulator.ScreenText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Ctrl_C_stops_the_running_command()
    {
        using var fixture = new WorkspaceFixture();
        var workspace = fixture.Workspace;
        await workspace.LoadAsync(fixture.Paths.Projects);
        workspace.ShowTerminalCommand.Execute(null);
        var terminal = workspace.Terminal;
        await UiTestHelpers.WaitUntilAsync(() => terminal.IsRunning, because: "the shell");

        terminal.SendText("sleep 30; echo slept\r");
        await Task.Delay(500);
        terminal.ToggleControlCommand.Execute(null);
        terminal.SendText("c");
        terminal.SendText("echo after-$((1+1))\r");

        await UiTestHelpers.WaitUntilAsync(() => terminal.Emulator.ScreenText.Contains("after-2", StringComparison.Ordinal), TimeSpan.FromSeconds(20), "the shell after Ctrl+C");
        Assert.DoesNotContain(terminal.Emulator.ScreenText.Split('\n'), l => l.Trim() == "slept");
        Assert.False(terminal.IsControlLatched);
    }

    [AvaloniaFact]
    public async Task Screenshot_of_the_terminal()
    {
        using var temp = new TempDirectory();
        var paths = new AppPaths(temp.Path);
        using var services = AppServices.Create(paths);
        var main = services.GetRequiredService<MainViewModel>();
        var window = new MainWindow { DataContext = main, Width = 1280, Height = 800 };
        window.Show();
        await main.OpenWorkspaceAsync(new ProjectTemplates(paths).Create(ProjectTemplates.PythonId, "Greeter"));
        var workspace = main.Workspace!;
        await UiTestHelpers.WaitUntilAsync(() => workspace.ActiveDocument is not null, because: "main.py");

        // A taller dock, as a user drags the splitter up to work in the Terminal.
        var editorArea = window.GetVisualDescendants().OfType<Avalonia.Controls.Grid>().Single(g => g.Name == "EditorArea");
        editorArea.RowDefinitions[4].Height = new Avalonia.Controls.GridLength(330);
        workspace.ShowTerminalCommand.Execute(null);
        var terminal = workspace.Terminal;
        await UiTestHelpers.WaitUntilAsync(() => terminal.IsRunning, because: "the shell");
        var view = window.GetVisualDescendants().OfType<TerminalView>().Single();
        await UiTestHelpers.WaitUntilAsync(() => view.Bounds.Width > 100 && terminal.Emulator.Columns > 40, because: "the terminal to fit its panel");

        // A short prompt, then what a session looks like: colors, a listing, a program.
        terminal.SendText("PS1='\\[\\e[1;32m\\]\\W\\[\\e[0m\\] \\$ '; clear\r");
        await UiTestHelpers.WaitUntilAsync(() => terminal.Emulator.ScreenText.StartsWith("Greeter $", StringComparison.Ordinal), because: "the prompt");
        terminal.SendText("printf '\\e[1;36mTabStudio\\e[0m terminal: \\e[33m256\\e[0m colors, \\e[4munderline\\e[0m, \\e[7minverse\\e[0m\\n'\r");
        terminal.SendText("ls -la\r");
        terminal.SendText("python3 main.py\r");
        await UiTestHelpers.WaitUntilAsync(() => terminal.Emulator.ScreenText.Contains("What is your name?", StringComparison.Ordinal), because: "the program");
        view.Focus();
        window.KeyTextInput("Ada");
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await UiTestHelpers.WaitUntilAsync(() => terminal.Emulator.ScreenText.Contains("Hello, Ada!", StringComparison.Ordinal), because: "typed input");
        await Task.Delay(200);
        await UiTestHelpers.WaitUntilAsync(() => true);

        UiTestHelpers.SaveScreenshot(window, "33-terminal");

        var line = Enumerable.Range(0, terminal.Emulator.Rows).Select(terminal.Emulator.ScreenLine).First(l => l.Text.StartsWith("TabStudio terminal", StringComparison.Ordinal));
        Assert.Equal(TerminalColor.Indexed(6), line[0].Style.Foreground);
        Assert.True(line[0].Style.Has(TerminalAttributes.Bold));

        // Ctrl+R, Ctrl+E and Ctrl+K are the shell's: the IDE's chords no longer swallow them and wait for a second key.
        foreach (var key in new[] { PhysicalKey.R, PhysicalKey.E, PhysicalKey.K })
        {
            view.Focus();
            window.KeyPressQwerty(key, RawInputModifiers.Control);
            await UiTestHelpers.WaitUntilAsync(() => true);
            Assert.DoesNotContain("Waiting for second key of chord", workspace.StatusText, StringComparison.Ordinal);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        }

        workspace.Dispose();
    }
}
