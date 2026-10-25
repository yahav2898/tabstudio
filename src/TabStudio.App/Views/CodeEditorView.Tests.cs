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
    /// Adds the testing group to the editor's context menu, after the items other features put there. The item
    /// names what runs, such as "Run Test 'Adds'", and is hidden where there are no tests.
    /// </summary>
    private void InstallTestCommands()
    {
        if (!_document.FileType.IsCSharp)
        {
            return;
        }

        if (_editor.ContextFlyout is not MenuFlyout menu)
        {
            menu = new MenuFlyout();
            _editor.ContextFlyout = menu;
        }

        _testsSeparator = new Separator();
        _runTestsItem = new MenuItem { Header = "Run Tests" };
        _runTestsItem.Click += OnRunTestsClick;
        menu.Items.Add(_testsSeparator);
        menu.Items.Add(_runTestsItem);
        menu.Opening += OnContextMenuOpeningForTests;
    }

    private void OnContextMenuOpeningForTests(object? sender, EventArgs e)
    {
        if (sender is not MenuFlyout menu || _runTestsItem is null || _testsSeparator is null)
        {
            return;
        }

        var tests = CaretTestFinder.Find(_editor.Document.Text, _document.FilePath, _editor.CaretOffset);
        _runTestsItem.IsVisible = tests is not null;
        _runTestsItem.Header = tests?.MenuText ?? "Run Tests";

        // The separator only separates: it shows when items of other features come before the testing group.
        var separatorIndex = menu.Items.IndexOf(_testsSeparator);
        _testsSeparator.IsVisible = tests is not null && menu.Items.Take(separatorIndex).OfType<MenuItem>().Any(i => i.IsVisible);

        // Nothing to offer here: do not open an empty menu.
        if (e is CancelEventArgs cancel && !menu.Items.OfType<MenuItem>().Any(i => i.IsVisible))
        {
            cancel.Cancel = true;
        }
    }

    private void OnRunTestsClick(object? sender, RoutedEventArgs e) =>
        RaiseEvent(new RoutedEventArgs(RunTestsRequestedEvent, this));
}
