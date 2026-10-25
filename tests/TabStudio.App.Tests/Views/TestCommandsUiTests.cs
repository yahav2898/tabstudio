using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Views;

/// <summary>
/// Running tests from the real views: the code editor's context menu and Visual Studio's key chords, in a window
/// rendered headlessly.
/// </summary>
public sealed class TestCommandsUiTests : IClassFixture<SharedToolchain>, IDisposable
{
    private static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(3);

    private readonly TempDirectory _temp = new();
    private readonly ServiceProvider _services;
    private readonly MainViewModel _main;

    public TestCommandsUiTests(SharedToolchain toolchain)
    {
        ArgumentNullException.ThrowIfNull(toolchain);
        _services = AppServices.Create(toolchain.Paths);
        _main = _services.GetRequiredService<MainViewModel>();
    }

    [AvaloniaFact]
    public async Task The_editor_context_menu_runs_the_test_at_the_caret()
    {
        var (_, workspace, editor) = await OpenTestFileAsync();
        editor.Editor.CaretOffset = CalculatorSolution.Tests.IndexOf("Adds()", StringComparison.Ordinal);
        var menu = Assert.IsType<MenuFlyout>(editor.Editor.ContextFlyout);

        menu.ShowAt(editor.Editor);
        Dispatcher.UIThread.RunJobs();
        var item = Assert.Single(menu.Items.OfType<MenuItem>(), i => i.IsVisible);
        Assert.Equal("Run Test 'Adds'", item.Header);
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        menu.Hide();
        await UiTestHelpers.WaitUntilAsync(() => workspace.Tests.Entries.Any(e => e.Result is not null) && !workspace.Tests.IsRunning, RunTimeout, "the test run");

        var ran = Assert.Single(workspace.Tests.Entries, e => e.Result is not null);
        Assert.Equal("Adds", ran.MethodName);
        Assert.Equal(TestState.Passed, ran.State);
    }

    [AvaloniaFact]
    public async Task Key_chords_show_the_Test_Explorer_and_run_all_tests()
    {
        var (window, workspace, editor) = await OpenTestFileAsync();
        workspace.SelectSidePanelCommand.Execute(SidePanel.SolutionExplorer);
        editor.Editor.TextArea.Focus();

        window.KeyPress(Key.E, RawInputModifiers.Control);
        window.KeyRelease(Key.E, RawInputModifiers.Control);
        window.KeyPress(Key.T, RawInputModifiers.None);
        window.KeyRelease(Key.T, RawInputModifiers.None);

        Assert.True(workspace.IsTestExplorerSelected);

        window.KeyPress(Key.R, RawInputModifiers.Control);
        window.KeyRelease(Key.R, RawInputModifiers.Control);

        Assert.StartsWith("(Ctrl+R) was pressed", workspace.StatusText, StringComparison.Ordinal);

        window.KeyPress(Key.A, RawInputModifiers.None);
        window.KeyRelease(Key.A, RawInputModifiers.None);
        await UiTestHelpers.WaitUntilAsync(() => workspace.Tests.PassedCount == 3 && !workspace.Tests.IsRunning, RunTimeout, "Run All");

        Assert.Equal((3, 1, 1), (workspace.Tests.PassedCount, workspace.Tests.FailedCount, workspace.Tests.SkippedCount));
        Assert.Contains("Test run finished: 5 Tests", workspace.Output.Text, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        _main.Workspace?.Dispose();
        _services.Dispose();
        _temp.Dispose();
    }

    private async Task<(Window Window, WorkspaceViewModel Workspace, CodeEditorView Editor)> OpenTestFileAsync()
    {
        var window = new MainWindow { DataContext = _main, Width = 1280, Height = 800 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        await _main.OpenWorkspaceAsync(CalculatorSolution.Write(_temp));
        var workspace = _main.Workspace!;
        await UiTestHelpers.WaitUntilAsync(() => workspace.Tests.TotalCount == 5, because: "the tests found in source");

        var document = (await workspace.OpenDocumentAsync(_temp.Resolve(CalculatorSolution.TestFile)))!;
        Dispatcher.UIThread.RunJobs();
        var editor = window.GetVisualDescendants().OfType<CodeEditorView>().Single(v => ReferenceEquals(v.DataContext, document));
        return (window, workspace, editor);
    }
}
