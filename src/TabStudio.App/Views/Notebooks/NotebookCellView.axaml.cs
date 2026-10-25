using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AvaloniaEdit.Editing;
using TabStudio.App.ViewModels.Notebooks;

namespace TabStudio.App.Views.Notebooks;

/// <summary>
/// One cell of the notebook view: the run button and execution count in the gutter, the editor or rendered
/// Markdown, the toolbar of the selected cell, compile errors and outputs. Tapping a cell selects it;
/// double-tapping rendered Markdown edits it.
/// </summary>
public sealed partial class NotebookCellView : UserControl
{
    /// <summary>Creates the view. Its data context is a <see cref="NotebookCellViewModel"/>.</summary>
    public NotebookCellView()
    {
        InitializeComponent();
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        Rendered.DoubleTapped += OnRenderedDoubleTapped;
    }

    private NotebookCellViewModel? Cell => DataContext as NotebookCellViewModel;

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Cell is not { } cell)
        {
            return;
        }

        cell.Notebook.SelectedCell = cell;

        // Tapping outside an editor or a button puts the keyboard in command mode, where keys act on cells.
        var source = e.Source as Visual;
        var inEditor = source?.FindAncestorOfType<TextArea>(includeSelf: true) is not null;
        var onButton = source?.FindAncestorOfType<Button>(includeSelf: true) is not null;
        var inText = source?.FindAncestorOfType<SelectableTextBlock>(includeSelf: true) is not null;
        if (!inEditor && !onButton && !inText)
        {
            this.FindAncestorOfType<NotebookView>()?.Focus();
        }
    }

    private void OnRenderedDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (Cell is { IsMarkdown: true } cell)
        {
            cell.Notebook.EditCell(cell);
            e.Handled = true;
        }
    }
}
