using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using TabStudio.App.Editing;
using TabStudio.App.Theming;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Views;

/// <summary>
/// The Output panel: a read-only editor that follows the end of the output, and an input box that is focused
/// whenever the running program waits for a line.
/// </summary>
public sealed partial class OutputView : UserControl
{
    private OutputViewModel? _viewModel;

    /// <summary>Creates the view.</summary>
    public OutputView()
    {
        InitializeComponent();
        Editor.Options.EnableHyperlinks = false;
        Editor.Options.EnableEmailHyperlinks = false;

        // Following the end of the output keeps the last lines at the bottom of the panel; scrolling below the
        // document would leave only the empty last line in view.
        Editor.Options.AllowScrollBelowDocument = false;
        Editor.TextArea.Caret.CaretBrush = Avalonia.Media.Brushes.Transparent;
        Editor.TextArea.TextView.ElementGenerators.Add(new OutputLinkGenerator(() => _viewModel, () => ThemeBrushes.Get("Brush.Link")));
        Input.KeyDown += OnInputKeyDown;
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.TextAppended -= OnTextAppended;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as OutputViewModel;
        Editor.TextArea.TextView.LineTransformers.Clear();
        if (_viewModel is { } viewModel)
        {
            Editor.Document = viewModel.Document;
            Editor.TextArea.TextView.LineTransformers.Add(new OutputColorizer(() => viewModel.Segments));
            viewModel.TextAppended += OnTextAppended;
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnTextAppended(object? sender, EventArgs e)
    {
        Editor.TextArea.TextView.Redraw();
        Editor.ScrollToEnd();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OutputViewModel.IsWaitingForInput) && _viewModel?.IsWaitingForInput == true)
        {
            Dispatcher.UIThread.Post(() => Input.Focus(), DispatcherPriority.Background);
        }
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _viewModel?.SubmitInputCommand.CanExecute(null) == true)
        {
            _viewModel.SubmitInputCommand.Execute(null);
            e.Handled = true;
        }
    }
}
