using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaEdit;
using AvaloniaEdit.Editing;

namespace TabStudio.App.Views.DesktopMode;

/// <summary>
/// Types on the big screen what the tablet's on-screen keyboard types into the companion's typing field, for when no
/// keyboard is connected: the on-screen keyboard can only type into a field on the tablet, which has the keyboard focus
/// while it shows.
/// </summary>
/// <remarks>
/// The field holds what was typed since it was last emptied, which is the text just before the caret on the big
/// screen. Each change of the field, which an on-screen keyboard makes a word or a letter at a time and corrects as it
/// likes, becomes the keys that make the same change there: Backspace for letters taken away and text input for letters
/// put in, with the arrow keys around a change before the end. So the editor on the big screen sees typing, with its
/// completion list, brackets and indentation, as it would from a keyboard.
/// </remarks>
/// <param name="target">The element on the big screen that gets the keys: what had the keyboard focus there.</param>
internal sealed class DesktopTypingRelay(Func<InputElement?> target)
{
    private string _typed = string.Empty;

    /// <summary>The element on the big screen that gets the keys, if there is one.</summary>
    public InputElement? Target => target();

    /// <summary>Starts again from an empty field: after a key that moved the caret on the big screen, or a new target.</summary>
    public void Reset() => _typed = string.Empty;

    /// <summary>The field's text changed: makes the same change before the caret on the big screen.</summary>
    /// <param name="text">The field's text now.</param>
    public void TextChanged(string? text)
    {
        text ??= string.Empty;
        var typed = _typed;
        _typed = text;
        if (target() is not { } element || text == typed)
        {
            return;
        }

        var (prefix, suffix) = Common(typed, text);
        var removed = typed.Length - prefix - suffix;
        var inserted = text.Substring(prefix, text.Length - prefix - suffix);
        Press(element, Key.Left, suffix);
        Press(element, Key.Back, removed);
        if (inserted.Length > 0)
        {
            element.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = inserted, Source = element });
        }

        Press(element, Key.Right, suffix);
        ShowCaret(element);
    }

    /// <summary>Presses a key on the big screen, such as Enter, Tab or an arrow, and starts again from an empty field.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">Shift, Ctrl or Alt held with it.</param>
    /// <returns>The key's event, to see whether anything handled it; <see langword="null"/> without a target.</returns>
    public KeyEventArgs? SendKey(Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        Reset();
        if (target() is not { } element)
        {
            return null;
        }

        var down = Press(element, key, modifiers);
        ShowCaret(element);
        return down;
    }

    /// <summary>The lengths of the beginning and the end two texts share, which do not overlap in either.</summary>
    internal static (int Prefix, int Suffix) Common(string before, string after)
    {
        var prefix = 0;
        var shortest = Math.Min(before.Length, after.Length);
        while (prefix < shortest && before[prefix] == after[prefix])
        {
            prefix++;
        }

        var suffix = 0;
        while (suffix < shortest - prefix && before[before.Length - 1 - suffix] == after[after.Length - 1 - suffix])
        {
            suffix++;
        }

        return (prefix, suffix);
    }

    private static void Press(InputElement element, Key key, int times)
    {
        for (var i = 0; i < times; i++)
        {
            Press(element, key, KeyModifiers.None);
        }
    }

    /// <summary>
    /// Presses a key on an element. The editor's own key bindings only act for the element that has the keyboard
    /// focus, which is the tablet's field, and a key the editor leaves goes on to the display's window, which moves
    /// the focus with Tab. So a key that the editor's own handlers, such as its completion list, leave unhandled runs
    /// the editor's action directly, as it reaches the editor.
    /// </summary>
    private static KeyEventArgs Press(InputElement element, Key key, KeyModifiers modifiers)
    {
        void RunInEditor(object? sender, KeyEventArgs e)
        {
            if (!e.Handled && sender is TextArea area && RunEditorKey(area, e.Key, e.KeyModifiers))
            {
                e.Handled = true;
            }
        }

        element.AddHandler(InputElement.KeyDownEvent, RunInEditor, RoutingStrategies.Bubble);
        KeyEventArgs down;
        try
        {
            down = Raise(element, InputElement.KeyDownEvent, key, modifiers);
        }
        finally
        {
            element.RemoveHandler(InputElement.KeyDownEvent, RunInEditor);
        }

        Raise(element, InputElement.KeyUpEvent, key, modifiers);
        return down;
    }

    /// <summary>
    /// Runs the editor's own action for a key a touch keyboard sends. Enter is typed as a line break, with the
    /// editor's indentation, because the editor's Enter command acts only while the editor has the keyboard focus.
    /// </summary>
    /// <returns>Whether the key has an action in the editor.</returns>
    private static bool RunEditorKey(TextArea area, Key key, KeyModifiers modifiers)
    {
        if (key == Key.Enter && modifiers == KeyModifiers.None)
        {
            area.PerformTextInput("\n");
            return true;
        }

        if (EditorCommand(key, modifiers) is not { } command)
        {
            return false;
        }

        command.Execute(null, area);
        return true;
    }

    /// <summary>The editor's command for a key a touch keyboard sends: editing keys and the arrows.</summary>
    private static RoutedCommand? EditorCommand(Key key, KeyModifiers modifiers) => (key, modifiers) switch
    {
        (Key.Back, KeyModifiers.None) => EditingCommands.Backspace,
        (Key.Delete, KeyModifiers.None) => EditingCommands.Delete,
        (Key.Tab, KeyModifiers.None) => EditingCommands.TabForward,
        (Key.Tab, KeyModifiers.Shift) => EditingCommands.TabBackward,
        (Key.Left, KeyModifiers.None) => EditingCommands.MoveLeftByCharacter,
        (Key.Right, KeyModifiers.None) => EditingCommands.MoveRightByCharacter,
        (Key.Up, KeyModifiers.None) => EditingCommands.MoveUpByLine,
        (Key.Down, KeyModifiers.None) => EditingCommands.MoveDownByLine,
        _ => null,
    };

    private static KeyEventArgs Raise(InputElement element, RoutedEvent<KeyEventArgs> routedEvent, Key key, KeyModifiers modifiers)
    {
        var e = new KeyEventArgs { RoutedEvent = routedEvent, Key = key, KeyModifiers = modifiers, Source = element };
        element.RaiseEvent(e);
        return e;
    }

    /// <summary>Keeps the editor's caret showing on the big screen while the tablet's field has the keyboard focus.</summary>
    private static void ShowCaret(InputElement element)
    {
        if (element is TextArea area)
        {
            area.Caret.Show();
            area.Caret.BringCaretToView();
        }
    }
}
