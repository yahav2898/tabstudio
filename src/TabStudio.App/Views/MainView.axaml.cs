using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TabStudio.App.Views;

/// <summary>
/// The app's root view: the start screen or the IDE. On Android it keeps the content clear of the status and
/// navigation bars, which the system draws over the app, and of the on-screen keyboard.
/// </summary>
public sealed partial class MainView : UserControl
{
    private IInsetsManager? _insets;
    private IInputPane? _inputPane;

    /// <summary>Creates the view.</summary>
    public MainView()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        var topLevel = TopLevel.GetTopLevel(this);
        _insets = topLevel?.InsetsManager;
        _inputPane = topLevel?.InputPane;
        if (_insets is not null)
        {
            _insets.SafeAreaChanged += OnInsetsChanged;

            // System bars in the window's color, so the IDE reaches the edges of the screen.
            if (this.TryFindResource("Brush.Window", out var brush) && brush is ISolidColorBrush window)
            {
                _insets.SystemBarColor = window.Color;
            }
        }

        if (_inputPane is not null)
        {
            _inputPane.StateChanged += OnInputPaneChanged;
        }

        UpdatePadding();
    }

    /// <inheritdoc />
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        if (_insets is not null)
        {
            _insets.SafeAreaChanged -= OnInsetsChanged;
            _insets = null;
        }

        if (_inputPane is not null)
        {
            _inputPane.StateChanged -= OnInputPaneChanged;
            _inputPane = null;
        }

        base.OnUnloaded(e);
    }

    private void OnInsetsChanged(object? sender, SafeAreaChangedArgs e) => UpdatePadding();

    private void OnInputPaneChanged(object? sender, InputPaneStateEventArgs e) => UpdatePadding();

    /// <summary>
    /// Pads the view by the system bars and, when the app is drawn edge to edge, by the keyboard too: the
    /// activity resizes for the keyboard on its own otherwise. The keyboard sits above the navigation bar.
    /// </summary>
    private void UpdatePadding()
    {
        var safe = _insets?.SafeAreaPadding ?? default;
        var keyboard = _insets is { DisplaysEdgeToEdge: true } && _inputPane is { State: InputPaneState.Open } pane
            ? pane.OccludedRect.Height
            : 0;
        Padding = new Thickness(safe.Left, safe.Top, safe.Right, safe.Bottom + keyboard);
    }
}
