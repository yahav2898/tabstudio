using System.Globalization;
using System.Windows.Forms;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TabStudio.Forms.Hosting;

/// <summary>
/// The keyboard on the desktop: keys go to the active window first, so the app's KeyDown, KeyPress, shortcuts and
/// dialog keys work; a key the app does not take goes on to the native text box when a field is edited. Keys stay in
/// the desktop, so Tab and the arrows move between the app's controls rather than out of the preview, but function
/// keys and shortcuts with Ctrl or Alt the app does not take reach TabStudio, such as Shift+F5 to stop the app.
/// </summary>
internal sealed partial class FormsDesktop
{
    /// <summary>How long the desktop waits for the app to say whether it takes a key.</summary>
    private static readonly TimeSpan KeyTimeout = TimeSpan.FromMilliseconds(150);

    private void InitializeKeyboard()
    {
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnKeyDownBubble, RoutingStrategies.Bubble);
        AddHandler(KeyUpEvent, OnKeyUpTunnel, RoutingStrategies.Tunnel);
        AddHandler(TextInputEvent, OnTextInputTunnel, RoutingStrategies.Tunnel);
    }

    /// <summary>Whether TabStudio gets a key the app did not take: function keys and shortcuts with Ctrl or Alt.</summary>
    private static bool BelongsToIde(Keys keys) =>
        (keys & (Keys.Control | Keys.Alt)) != 0 || (keys & Keys.KeyCode) is >= Keys.F1 and <= Keys.F24;

    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        var keys = KeyMapping.ToKeys(e.Key, e.KeyModifiers);
        if (keys == Keys.None || Runtime is not { } runtime)
        {
            return;
        }

        var handled = false;
        runtime.Send(() => handled = runtime.ActiveWindow?.KeyDown(keys) ?? false, KeyTimeout);
        if (handled)
        {
            e.Handled = true;
            return;
        }

        // A field's Tab moves focus between the app's controls, unless the field types tabs.
        if (_editor.IsKeyboardFocusWithin && (keys & Keys.KeyCode) == Keys.Tab && !(_editor.Field is { Multiline: true, AcceptsTab: true } && (keys & Keys.Control) == 0))
        {
            e.Handled = true;
        }
    }

    private void OnKeyDownBubble(object? sender, KeyEventArgs e)
    {
        // The native text box had the key and did not use it: it stays in the desktop all the same.
        var keys = KeyMapping.ToKeys(e.Key, e.KeyModifiers);
        if (keys != Keys.None && !BelongsToIde(keys))
        {
            e.Handled = true;
        }
    }

    private void OnKeyUpTunnel(object? sender, KeyEventArgs e)
    {
        var keys = KeyMapping.ToKeys(e.Key, e.KeyModifiers);
        if (keys == Keys.None || Runtime is not { } runtime)
        {
            return;
        }

        runtime.Post(() => runtime.ActiveWindow?.KeyUp(keys));
        if (!BelongsToIde(keys) && !_editor.IsKeyboardFocusWithin)
        {
            e.Handled = true;
        }
    }

    private void OnTextInputTunnel(object? sender, TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text) || Runtime is not { } runtime)
        {
            return;
        }

        var text = e.Text;
        if (!_editor.IsKeyboardFocusWithin)
        {
            // Typed on a control that is not a text field: KeyPress, as in Windows.
            runtime.Post(() => runtime.ActiveWindow?.TextInput(text));
            e.Handled = true;
            return;
        }

        // KeyPress handlers can change or refuse what is typed into the field before it reaches the box.
        string? accepted = text;
        if (!runtime.Send(() => accepted = runtime.ActiveWindow?.TextInput(text), KeyTimeout))
        {
            accepted = text;
        }

        accepted = _editor.Field?.Casing switch
        {
            CharacterCasing.Upper => accepted?.ToUpper(CultureInfo.CurrentCulture),
            CharacterCasing.Lower => accepted?.ToLower(CultureInfo.CurrentCulture),
            _ => accepted,
        };

        if (accepted is null)
        {
            e.Handled = true;
        }
        else if (!string.Equals(accepted, text, StringComparison.Ordinal))
        {
            e.Text = accepted;
        }
    }
}
