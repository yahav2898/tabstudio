using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TabStudio.App.ViewModels;
using TabStudio.Forms.Design.View;

namespace TabStudio.App.Views;

/// <summary>
/// The tab of a Windows Forms form's code file: the designer, with the Toolbox and the Properties, or the code, which
/// F7 and Shift+F7 switch between. A tool can be dragged from the Toolbox onto the form; a long press or a
/// right-click on the form opens its menu; Enter commits a property's text and Shift+Enter starts a new line of it.
/// </summary>
internal sealed partial class FormsDesignerView : UserControl, IDisposable
{
    /// <summary>How far a finger drags a tool before it leaves the Toolbox.</summary>
    private const double DragThreshold = 12;

    private readonly FormsDesignerViewModel _designer;
    private readonly CodeEditorView _editor;
    private ToolboxItemViewModel? _dragTool;
    private Point _dragStart;
    private bool _dragging;
    private bool _sized;
    private bool _disposed;

    /// <summary>Creates the tab of a form.</summary>
    /// <param name="document">The form's code file.</param>
    /// <param name="designer">Its designer.</param>
    public FormsDesignerView(DocumentViewModel document, FormsDesignerViewModel designer)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(designer);
        InitializeComponent();
        _designer = designer;
        DataContext = designer;
        _editor = new CodeEditorView(document);
        CodeHost.Content = _editor;
        var canvas = designer.Canvas;
        canvas.Bind(DesignCanvas.BackgroundProperty, this.GetResourceObservable("Brush.Window"));
        CanvasScroller.Content = canvas;
        canvas.MenuRequested += OnMenuRequested;
        designer.PropertyChanged += OnDesignerPropertyChanged;

        // Tools are dragged with the pointer the Toolbox's button captured; the tunnel sees the release before the
        // button does, so a drag does not also arm the tool.
        ToolboxList.AddHandler(PointerPressedEvent, OnToolPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        ToolboxList.AddHandler(PointerMovedEvent, OnToolPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        ToolboxList.AddHandler(PointerReleasedEvent, OnToolPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        ToolboxList.AddHandler(PointerCaptureLostEvent, (_, _) => EndToolDrag(), RoutingStrategies.Bubble, handledEventsToo: true);
        PropertiesPane.AddHandler(KeyDownEvent, OnPropertyKeyDown, RoutingStrategies.Tunnel);
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
        _designer.Canvas.MenuRequested -= OnMenuRequested;
        CanvasScroller.Content = null;
        _editor.Dispose();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // Back on the tab, the form shows what its designer file says now, edited in its own tab or not.
        if (change.Property == IsVisibleProperty && IsVisible && !_disposed && _designer.IsDesign)
        {
            _designer.Refresh();
        }
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        // F7 shows the code and Shift+F7 the designer, as in Visual Studio.
        if (e.Key == Key.F7 && (e.KeyModifiers & ~KeyModifiers.Shift) == KeyModifiers.None)
        {
            _designer.Mode = (e.KeyModifiers & KeyModifiers.Shift) != 0 ? FormsDesignerMode.Design : FormsDesignerMode.Code;
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);

        // On a narrow screen, such as a tablet held upright, the form gets the width; the panels open from the toolbar.
        if (!_sized && e.NewSize.Width > 0)
        {
            _sized = true;
            if (e.NewSize.Width < 900)
            {
                _designer.IsToolboxOpen = false;
                _designer.IsPropertiesOpen = false;
            }
        }
    }

    private void OnDesignerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FormsDesignerViewModel.Mode) && _designer.IsDesign)
        {
            _designer.Canvas.Focus();
        }
    }

    /// <summary>
    /// Enter commits a property's text, which otherwise commits when the box loses focus, and adds the strip item typed;
    /// Shift+Enter starts a new line of a text that can have several, such as a label's.
    /// </summary>
    private void OnPropertyKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (e.KeyModifiers & KeyModifiers.Shift) == 0
            && e.Source is Visual source && source.FindAncestorOfType<TextBox>(includeSelf: true) is { } box)
        {
            if (box == NewItemBox)
            {
                // Enter adds the item typed, as in Visual Studio's "Type Here".
                _designer.AddStripItemCommand.Execute(null);
            }
            else
            {
                BindingOperations.GetBindingExpressionBase(box, TextBox.TextProperty)?.UpdateSource();
            }

            e.Handled = true;
        }
    }

    private void OnEventDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: FormsEventViewModel row })
        {
            row.OpenCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>The designer's menu, at a long press or a right-click on the form.</summary>
    private void OnMenuRequested(object? sender, Point point)
    {
        var flyout = new MenuFlyout
        {
            Items =
            {
                Item("Cut", _designer.CutCommand),
                Item("Copy", _designer.CopyCommand),
                Item("Paste", _designer.PasteCommand),
                Item("Delete", _designer.DeleteCommand),
                new Separator(),
                Item("Bring to Front", _designer.BringToFrontCommand),
                Item("Send to Back", _designer.SendToBackCommand),
                new Separator(),
                Item("Select Form", _designer.SelectFormCommand),
                Item("View Code", _designer.ShowCodeCommand),
                Item("Properties", new CommunityToolkit.Mvvm.Input.RelayCommand(() =>
                {
                    _designer.IsPropertiesOpen = true;
                    _designer.ShowsEvents = false;
                })),
            },
        };
        flyout.ShowAt(_designer.Canvas, showAtPointer: true);

        static MenuItem Item(string header, System.Windows.Input.ICommand command) => new() { Header = header, Command = command };
    }

    private void OnToolPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Visual source && source.FindAncestorOfType<Button>(includeSelf: true) is { DataContext: ToolboxItemViewModel tool })
        {
            _dragTool = tool;
            _dragStart = e.GetPosition(DragLayer);
            _dragging = false;
        }
    }

    private void OnToolPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragTool is null)
        {
            return;
        }

        var point = e.GetPosition(DragLayer);
        if (!_dragging && LeavesToolbox(point - _dragStart, e.Pointer.Type))
        {
            _dragging = true;
            DragGhostText.Text = _dragTool.Name;
            DragGhost.IsVisible = true;
        }

        if (_dragging)
        {
            Canvas.SetLeft(DragGhost, point.X + 12);
            Canvas.SetTop(DragGhost, point.Y + 12);
            e.PreventGestureRecognition();
            e.Handled = true;
        }
    }

    private void OnToolPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragTool is not { } tool)
        {
            return;
        }

        if (_dragging)
        {
            // Dropped on the form: the tool is added there. Anywhere else, nothing happens.
            var canvas = _designer.Canvas;
            var point = e.GetPosition(canvas);
            if (new Rect(canvas.Bounds.Size).Contains(point))
            {
                canvas.DropTool(tool.Type, point);
            }

            e.Handled = true;
            e.Pointer.Capture(null);
        }

        EndToolDrag();
    }

    /// <summary>
    /// Whether a press on a tool has become a drag: with the mouse, a move in any direction; with a finger, a move
    /// towards the form, as a move up or down scrolls the Toolbox.
    /// </summary>
    private static bool LeavesToolbox(Vector moved, PointerType pointer) => pointer == PointerType.Mouse
        ? Math.Abs(moved.X) > DragThreshold || Math.Abs(moved.Y) > DragThreshold
        : Math.Abs(moved.X) > DragThreshold && Math.Abs(moved.X) > Math.Abs(moved.Y);

    private void EndToolDrag()
    {
        _dragTool = null;
        _dragging = false;
        DragGhost.IsVisible = false;
    }
}
