using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TabStudio.Agent;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Views;

/// <summary>
/// The tab of an Avalonia XAML file: the code editor and the live preview of the XAML, split side by side or one
/// above the other, or either one alone, as Visual Studio's Design, split and XAML buttons choose.
/// </summary>
public sealed partial class XamlDesignerView : UserControl, IDisposable
{
    private const double SplitterThickness = 6;

    private readonly XamlDesignerViewModel _designer;
    private readonly CodeEditorView _editor;
    private readonly Border _artboard = new() { ClipToBounds = true };
    private bool _disposed;

    /// <summary>Creates the tab of a XAML file.</summary>
    /// <param name="document">The XAML file.</param>
    /// <param name="designer">Its designer.</param>
    public XamlDesignerView(DocumentViewModel document, XamlDesignerViewModel designer)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(designer);
        InitializeComponent();
        _designer = designer;
        DataContext = designer;
        _artboard.Bind(Border.BorderBrushProperty, _artboard.GetResourceObservable("Brush.BorderStrong"));
        _editor = new CodeEditorView(document);
        EditorHost.Content = _editor;
        designer.PropertyChanged += OnDesignerPropertyChanged;
        designer.Sketch.Capture = SketchPad.ToPng;
        designer.Sketch.Converted += OnSketchConverted;
        SketchPad.StrokesChanged += OnStrokesChanged;
        ArrangePanes();
        PlacePreview();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _designer.PropertyChanged -= OnDesignerPropertyChanged;
        _designer.Sketch.Converted -= OnSketchConverted;
        _designer.Sketch.Capture = null;
        SketchPad.StrokesChanged -= OnStrokesChanged;
        _artboard.Child = null;
        _editor.Dispose();
    }

    private void OnDesignerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(XamlDesignerViewModel.Mode):
            case nameof(XamlDesignerViewModel.IsSideBySide):
                ArrangePanes();
                break;
            case nameof(XamlDesignerViewModel.Preview):
            case nameof(XamlDesignerViewModel.FitToPane):
            case nameof(XamlDesignerViewModel.DesignWidth):
            case nameof(XamlDesignerViewModel.DesignHeight):
                PlacePreview();
                break;
        }
    }

    private void OnStrokesChanged(object? sender, EventArgs e) => _designer.Sketch.HasStrokes = SketchPad.HasStrokes;

    private void OnSketchConverted(object? sender, SketchResult e) => SketchPad.Clear();

    private void OnUndoStroke(object? sender, RoutedEventArgs e) => SketchPad.Undo();

    private void OnClearSketch(object? sender, RoutedEventArgs e) => SketchPad.Clear();

    /// <summary>Lays the preview and the editor out for the mode: one cell, or two with a splitter between them.</summary>
    private void ArrangePanes()
    {
        Panes.RowDefinitions.Clear();
        Panes.ColumnDefinitions.Clear();
        var split = _designer.Mode == XamlDesignerMode.Split;
        DesignPane.IsVisible = _designer.Mode != XamlDesignerMode.Xaml;
        EditorHost.IsVisible = _designer.Mode != XamlDesignerMode.Design;
        Splitter.IsVisible = split;

        foreach (var pane in new Control[] { DesignPane, Splitter, EditorHost })
        {
            Grid.SetRow(pane, 0);
            Grid.SetColumn(pane, 0);
        }

        if (!split)
        {
            return;
        }

        // Visual Studio puts the design above the XAML; on a wide tablet, beside it reads better.
        if (_designer.IsSideBySide)
        {
            Panes.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star) { MinWidth = 160 });
            Panes.ColumnDefinitions.Add(new ColumnDefinition(SplitterThickness, GridUnitType.Pixel));
            Panes.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star) { MinWidth = 160 });
            Grid.SetColumn(Splitter, 1);
            Grid.SetColumn(EditorHost, 2);
            Splitter.ResizeDirection = GridResizeDirection.Columns;
        }
        else
        {
            Panes.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star) { MinHeight = 120 });
            Panes.RowDefinitions.Add(new RowDefinition(SplitterThickness, GridUnitType.Pixel));
            Panes.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star) { MinHeight = 120 });
            Grid.SetRow(Splitter, 1);
            Grid.SetRow(EditorHost, 2);
            Splitter.ResizeDirection = GridResizeDirection.Rows;
        }
    }

    /// <summary>
    /// Puts the preview on its artboard: filling the pane, or at the XAML's design size, scaled to fit the pane or at
    /// its real size with scroll bars.
    /// </summary>
    private void PlacePreview()
    {
        // The artboard can only have one parent; take it out of the old one first.
        switch (_artboard.Parent)
        {
            case Viewbox viewbox:
                viewbox.Child = null;
                break;
            case ScrollViewer scroll:
                scroll.Content = null;
                break;
        }

        Surface.Content = null;
        _artboard.Child = _designer.Preview;
        _artboard.Width = _designer.DesignWidth;
        _artboard.Height = _designer.DesignHeight;
        ZoomText.Text = _designer.FitToPane ? "Fit" : "100%";

        if (!_designer.HasDesignSize)
        {
            _artboard.BorderThickness = default;
            _artboard.Margin = default;
            Surface.Content = _artboard;
            return;
        }

        _artboard.BorderThickness = new Thickness(1);
        _artboard.Margin = new Thickness(16);
        _artboard.HorizontalAlignment = HorizontalAlignment.Center;
        _artboard.VerticalAlignment = VerticalAlignment.Center;
        Surface.Content = _designer.FitToPane
            ? new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Child = _artboard }
            : new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                Content = _artboard,
            };
    }
}
