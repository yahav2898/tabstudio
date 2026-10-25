using Avalonia.Input;
using Avalonia.Interactivity;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Views;

/// <summary>
/// Testing in the shell: Visual Studio's test shortcuts and the editor's "Run Tests" menu item.
/// </summary>
/// <remarks>
/// Ctrl+R, A runs all tests; Ctrl+R, F the failed ones; Ctrl+R, L repeats the last run; Ctrl+R, T runs the tests at
/// the editor's caret, or the selected tests while the Test Explorer has the focus; Ctrl+E, T shows the Test Explorer.
/// </remarks>
public sealed partial class WorkspaceView
{
    /// <summary>Registers the test shortcuts and listens for "Run Tests" from the editors. Called by the constructor, after the chord map is set up.</summary>
    private void InitializeTestExplorer()
    {
        _chords.Add(Key.R, KeyModifiers.Control, Key.A, () => _viewModel?.Tests.RunAllCommand);
        _chords.Add(Key.R, KeyModifiers.Control, Key.F, () => _viewModel?.Tests.RunFailedCommand);
        _chords.Add(Key.R, KeyModifiers.Control, Key.L, () => _viewModel?.Tests.RepeatLastRunCommand);
        _chords.Add(Key.R, KeyModifiers.Control, Key.T, () => TestExplorer.IsKeyboardFocusWithin
            ? _viewModel?.Tests.RunSelectedCommand
            : _viewModel?.RunTestsInContextCommand);
        _chords.Add(Key.E, KeyModifiers.Control, Key.T, () => _viewModel?.SelectSideBarPanelCommand, SideBarPanel.TestExplorer);
        AddHandler(CodeEditorView.RunTestsRequestedEvent, OnRunTestsRequested);
    }

    private void OnRunTestsRequested(object? sender, RoutedEventArgs e)
    {
        if (_viewModel?.RunTestsInContextCommand is { } command && command.CanExecute(null))
        {
            command.Execute(null);
        }

        e.Handled = true;
    }
}
