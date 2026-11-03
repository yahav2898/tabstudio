using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using TabStudio.App.Input;

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

    /// <summary>
    /// Escape closes the dialog that shows over the IDE or the start screen, as its Close, Cancel or Done button does:
    /// TabStudio Link, Join a Swarm, Share Session, Join Live Session, Add a device and the others drawn over a scrim
    /// answered only a tap, and a keyboard user was left in them.
    /// </summary>
    private void OnDialogKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.Escape || e.KeyModifiers != KeyModifiers.None || TopDialogClose() is not { } close)
        {
            return;
        }

        if (close.Command is { } command)
        {
            if (command.CanExecute(close.CommandParameter))
            {
                command.Execute(close.CommandParameter);
            }
        }
        else
        {
            close.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }

        e.Handled = true;
    }

    /// <summary>The button that closes the topmost dialog shown over a scrim, or <see langword="null"/> when none shows.</summary>
    internal Button? TopDialogClose()
    {
        if (!this.TryFindResource("Brush.Scrim", ActualThemeVariant, out var scrim) || scrim is null)
        {
            return null;
        }

        var dialog = this.GetVisualDescendants().OfType<Control>().LastOrDefault(control => control.IsEffectivelyVisible && control switch
        {
            Panel panel => ReferenceEquals(panel.Background, scrim),
            Border border => ReferenceEquals(border.Background, scrim),
            _ => false,
        });
        return dialog?.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.IsEffectivelyVisible && button.IsEffectivelyEnabled
            && (button.Content as string ?? ToolTip.GetTip(button) as string) is { } text
            && (text is "Close" or "Cancel" or "Done" or "Not now" or "Not Now" || text.StartsWith("Close ", StringComparison.Ordinal)));
    }

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not null)
        {
            InputModality.Track(topLevel);

            // On the top level and tunnelling: a dialog closes while the editor behind it has the keyboard focus, and
            // when nothing has it, as after a tap on a button that then hid itself, when keys go to the top level alone.
            topLevel.AddHandler(KeyDownEvent, OnDialogKeyDown, RoutingStrategies.Tunnel);
        }

        _insets = topLevel?.InsetsManager;
        _inputPane = topLevel?.InputPane;
        if (_insets is not null)
        {
            _insets.SafeAreaChanged += OnInsetsChanged;
            ActualThemeVariantChanged += OnThemeChanged;
            UpdateSystemBars();
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
        if (TopLevel.GetTopLevel(this) is { } topLevel)
        {
            InputModality.Untrack(topLevel);
            topLevel.RemoveHandler(KeyDownEvent, OnDialogKeyDown);
        }

        if (_insets is not null)
        {
            _insets.SafeAreaChanged -= OnInsetsChanged;
            ActualThemeVariantChanged -= OnThemeChanged;
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

    private void OnThemeChanged(object? sender, EventArgs e) => UpdateSystemBars();

    /// <summary>
    /// Gives the system bars the window's color in the current theme, so the IDE reaches the edges of the screen;
    /// Avalonia makes their icons dark on the light theme.
    /// </summary>
    private void UpdateSystemBars()
    {
        if (_insets is not null && this.TryFindResource("Brush.Window", ActualThemeVariant, out var brush) && brush is ISolidColorBrush window)
        {
            _insets.SystemBarColor = window.Color;
        }
    }

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
