using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TabStudio.App.Services;

namespace TabStudio.App.Views;

/// <summary>
/// "Run Tests" in the code editor's context menu (long-press on touch, right-click with a mouse): runs the data row,
/// test, class or file at the caret, like Ctrl+R, T.
/// </summary>
public sealed partial class CodeEditorView
{
    /// <summary>Raised, bubbling, when the user chooses "Run Tests". The workspace view runs the tests at the caret.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> RunTestsRequestedEvent =
        RoutedEvent.Register<CodeEditorView, RoutedEventArgs>(nameof(RunTestsRequested), RoutingStrategies.Bubble);

    private Separator? _testsSeparator;
    private MenuItem? _runTestsItem;

    /// <summary>Raised when the user chooses "Run Tests" in the editor's context menu.</summary>
    public event EventHandler<RoutedEventArgs>? RunTestsRequested
    {
        add => AddHandler(RunTestsRequestedEvent, value);
        remove => RemoveHandler(RunTestsRequestedEvent, value);
    }

    /// <summary>
    /// Adds the testing group to the editor's context menu, after the navigation commands and before the clipboard
    /// ones. The item names what runs, such as "Run Test 'Adds'", and is hidden where there are no tests.
    /// </summary>
    private void InstallTestCommands()
    {
        if (!_document.FileType.IsRoslyn)
        {
            return;
        }

        if (_editor.ContextMenu is not { } menu)
        {
            menu = new ContextMenu();
            _editor.ContextMenu = menu;
        }

        _runTestsItem = new MenuItem { Header = "Run Tests", IsVisible = false };
        _runTestsItem.Click += OnRunTestsClick;
        _testsSeparator = new Separator { IsVisible = false };
        var index = _cutItem is not null && menu.Items.IndexOf(_cutItem) is >= 0 and var cut ? cut : menu.Items.Count;
        menu.Items.Insert(index, _testsSeparator);
        menu.Items.Insert(index, _runTestsItem);
        menu.Opening += OnContextMenuOpeningForTests;
    }

    private void OnContextMenuOpeningForTests(object? sender, CancelEventArgs e)
    {
        if (_runTestsItem is null || _testsSeparator is null)
        {
            return;
        }

        var tests = CaretTestFinder.Find(_editor.Document.Text, _document.FilePath, _editor.CaretOffset);
        _runTestsItem.IsVisible = tests is not null;
        _runTestsItem.Header = tests?.MenuText ?? "Run Tests";
        _testsSeparator.IsVisible = tests is not null;
    }

    private void OnRunTestsClick(object? sender, RoutedEventArgs e) =>
        RaiseEvent(new RoutedEventArgs(RunTestsRequestedEvent, this));
}
