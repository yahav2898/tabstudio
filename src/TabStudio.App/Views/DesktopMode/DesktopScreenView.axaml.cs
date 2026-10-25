using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AvaloniaEdit.Editing;
using TabStudio.App.Input;

namespace TabStudio.App.Views.DesktopMode;

/// <summary>
/// The root of the external display in desktop mode: it holds the IDE, which is used there with a keyboard and a
/// mouse, and remembers what had the keyboard focus in it, to give the focus back after the tablet borrowed it.
/// </summary>
/// <remarks>
/// Avalonia keeps one keyboard focus for the whole app, whichever window or view it is in. Keys typed on a keyboard
/// connected to the tablet reach Android's focused window, which is the tablet's, and Avalonia then delivers them to
/// the focused element, here on the display. So the IDE on the display gets the keyboard as long as one of its
/// elements has the focus, which this view sees to.
/// </remarks>
public sealed partial class DesktopScreenView : UserControl
{
    private IInputElement? _lastFocused;
    private TopLevel? _topLevel;

    /// <summary>Creates the view.</summary>
    public DesktopScreenView()
    {
        InitializeComponent();
        InputModality.SetPrefersKeyboard(this, true);
        AddHandler(GotFocusEvent, OnGotFocus, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <summary>The IDE, while the display shows it.</summary>
    public Control? Ide => Host.Child;

    /// <summary>Puts the IDE on the display, or takes it off with <see langword="null"/>.</summary>
    /// <param name="ide">The IDE.</param>
    internal void Show(Control? ide)
    {
        Host.Child = ide;
        if (ide is null)
        {
            _lastFocused = null;
        }
    }

    /// <summary>Gives the keyboard focus back to what had it on the display, or to the active document's editor.</summary>
    internal void RestoreKeyboardFocus()
    {
        if (_lastFocused is InputElement { IsEffectivelyVisible: true, IsEffectivelyEnabled: true } last
            && this.IsVisualAncestorOf(last)
            && last.Focus())
        {
            return;
        }

        foreach (var editor in this.GetVisualDescendants().OfType<TextArea>())
        {
            if (editor.IsEffectivelyVisible && editor.Focus())
            {
                return;
            }
        }
    }

    /// <summary>
    /// Runs the IDE's shortcut for a key that reached a window with nothing focused, as the IDE would have with the
    /// focus: F5, Ctrl+Shift+B and the others are key bindings of its views.
    /// </summary>
    /// <param name="e">The key.</param>
    /// <returns>Whether a shortcut ran.</returns>
    internal bool RunKeyBinding(KeyEventArgs e)
    {
        Visual? start = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        if (start is null || !this.IsVisualAncestorOf(start))
        {
            start = Host.Child;
        }

        for (var current = start; current is not null; current = current.GetVisualParent())
        {
            if (current is not InputElement { KeyBindings.Count: > 0 } element)
            {
                continue;
            }

            foreach (var binding in element.KeyBindings)
            {
                if (binding.Gesture?.Matches(e) == true && binding.Command is { } command && command.CanExecute(binding.CommandParameter))
                {
                    command.Execute(binding.CommandParameter);
                    return true;
                }
            }
        }

        return false;
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel is not null)
        {
            InputModality.Track(_topLevel);
            _topLevel.AddHandler(KeyDownEvent, OnUnfocusedKeyDown, RoutingStrategies.Bubble);
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_topLevel is not null)
        {
            InputModality.Untrack(_topLevel);
            _topLevel.RemoveHandler(KeyDownEvent, OnUnfocusedKeyDown);
            _topLevel = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>A key on the display's own window with nothing focused, such as after a click on an empty area.</summary>
    private void OnUnfocusedKeyDown(object? sender, KeyEventArgs e)
    {
        if (!e.Handled && ReferenceEquals(e.Source, _topLevel))
        {
            RestoreKeyboardFocus();
            e.Handled = RunKeyBinding(e);
        }
    }

    private void OnGotFocus(object? sender, GotFocusEventArgs e) => _lastFocused = e.Source as IInputElement;
}
