using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AvaloniaEdit.Editing;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Views.DesktopMode;

/// <summary>
/// The companion's typing bar, for a tablet without a keyboard: its field takes the on-screen keyboard's typing,
/// which <see cref="DesktopTypingRelay"/> makes on the big screen, and its buttons press the keys an on-screen
/// keyboard lacks. Enter, Tab and Backspace on an empty field go to the big screen too.
/// </summary>
public sealed partial class DesktopKeyboardView : UserControl
{
    private DesktopTypingRelay? _relay;
    private bool _clearing;

    /// <summary>Creates the bar.</summary>
    public DesktopKeyboardView()
    {
        InitializeComponent();
        Field.AddHandler(KeyDownEvent, OnFieldKeyDown, RoutingStrategies.Tunnel);
        Field.PropertyChanged += OnFieldPropertyChanged;
        Field.GotFocus += (_, _) => ShowTarget();
        Field.LostFocus += (_, _) => Clear();
    }

    /// <summary>What the bar types into on the big screen, for tests.</summary>
    internal InputElement? Target => _relay?.Target;

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        var tablet = this.FindAncestorOfType<MainView>();
        _relay = new DesktopTypingRelay(() => tablet?.DesktopScreen?.KeyboardTarget);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty && IsVisible)
        {
            ShowTarget();
        }
    }

    private void OnFieldPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextBox.TextProperty && !_clearing)
        {
            _relay?.TextChanged(Field.Text);
        }
    }

    /// <summary>Keys the field has no use for go to the big screen.</summary>
    private void OnFieldKeyDown(object? sender, KeyEventArgs e)
    {
        var forward = e.Key switch
        {
            Key.Enter or Key.Tab or Key.Escape or Key.Up or Key.Down => true,
            Key.Back or Key.Left or Key.Right => string.IsNullOrEmpty(Field.Text),
            _ => false,
        };

        if (forward)
        {
            Press(e.Key, e.KeyModifiers);
            e.Handled = true;
        }
    }

    private void OnKeyClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: Key key })
        {
            Press(key, KeyModifiers.None);
        }
    }

    /// <summary>Presses a key on the big screen, which moves its caret, so the field starts again empty.</summary>
    private void Press(Key key, KeyModifiers modifiers)
    {
        _relay?.SendKey(key, modifiers);
        Clear();
    }

    private void Clear()
    {
        _clearing = true;
        Field.Text = string.Empty;
        _clearing = false;
        _relay?.Reset();
    }

    /// <summary>Says in the empty field where its typing goes.</summary>
    private void ShowTarget() => Field.Watermark = _relay?.Target switch
    {
        TextArea area when area.FindAncestorOfType<CodeEditorView>()?.DataContext is DocumentViewModel document =>
            $"Type into {document.FileName} on the big screen",
        TextBox => "Type into the box selected on the big screen",
        _ => "Click into an editor on the big screen to type there",
    };
}
