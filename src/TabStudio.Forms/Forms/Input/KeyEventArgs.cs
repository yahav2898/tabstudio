namespace System.Windows.Forms;

/// <summary>Data of the KeyDown and KeyUp events.</summary>
public class KeyEventArgs : EventArgs
{
    private bool _suppressKeyPress;

    /// <summary>Creates the data of a key event.</summary>
    /// <param name="keyData">The key code combined with the modifiers held.</param>
    public KeyEventArgs(Keys keyData) => KeyData = keyData;

    /// <summary>The key code combined with the modifiers held.</summary>
    public Keys KeyData { get; }

    /// <summary>The key code, without modifiers.</summary>
    public Keys KeyCode => KeyData & Keys.KeyCode;

    /// <summary>The modifiers held: Shift, Control and Alt.</summary>
    public Keys Modifiers => KeyData & Keys.Modifiers;

    /// <summary>The key code as a number.</summary>
    public int KeyValue => (int)(KeyData & Keys.KeyCode);

    /// <summary>Whether Alt was held.</summary>
    public virtual bool Alt => (KeyData & Keys.Alt) == Keys.Alt;

    /// <summary>Whether Ctrl was held.</summary>
    public bool Control => (KeyData & Keys.Control) == Keys.Control;

    /// <summary>Whether Shift was held.</summary>
    public virtual bool Shift => (KeyData & Keys.Shift) == Keys.Shift;

    /// <summary>Whether the event was handled, which stops the control's own handling of the key.</summary>
    public bool Handled { get; set; }

    /// <summary>Whether the KeyPress that would follow is suppressed. Setting it also sets <see cref="Handled"/>.</summary>
    public bool SuppressKeyPress
    {
        get => _suppressKeyPress;
        set
        {
            _suppressKeyPress = value;
            Handled = value;
        }
    }
}

/// <summary>Handles a KeyDown or KeyUp event.</summary>
/// <param name="sender">The control.</param>
/// <param name="e">The event data.</param>
public delegate void KeyEventHandler(object? sender, KeyEventArgs e);

/// <summary>Data of the KeyPress event: the character typed.</summary>
public class KeyPressEventArgs : EventArgs
{
    /// <summary>Creates the data of a KeyPress event.</summary>
    /// <param name="keyChar">The character typed.</param>
    public KeyPressEventArgs(char keyChar) => KeyChar = keyChar;

    /// <summary>The character typed; a handler may change it, as for upper-casing input.</summary>
    public char KeyChar { get; set; }

    /// <summary>Whether the event was handled, which keeps the character out of the control.</summary>
    public bool Handled { get; set; }
}

/// <summary>Handles a KeyPress event.</summary>
/// <param name="sender">The control.</param>
/// <param name="e">The event data.</param>
public delegate void KeyPressEventHandler(object? sender, KeyPressEventArgs e);

/// <summary>Data of the PreviewKeyDown event, raised before KeyDown.</summary>
public class PreviewKeyDownEventArgs : EventArgs
{
    /// <summary>Creates the data.</summary>
    /// <param name="keyData">The key code combined with the modifiers held.</param>
    public PreviewKeyDownEventArgs(Keys keyData) => KeyData = keyData;

    /// <summary>The key code combined with the modifiers held.</summary>
    public Keys KeyData { get; }

    /// <summary>The key code, without modifiers.</summary>
    public Keys KeyCode => KeyData & Keys.KeyCode;

    /// <summary>The modifiers held.</summary>
    public Keys Modifiers => KeyData & Keys.Modifiers;

    /// <summary>The key code as a number.</summary>
    public int KeyValue => (int)(KeyData & Keys.KeyCode);

    /// <summary>Whether Alt was held.</summary>
    public bool Alt => (KeyData & Keys.Alt) == Keys.Alt;

    /// <summary>Whether Ctrl was held.</summary>
    public bool Control => (KeyData & Keys.Control) == Keys.Control;

    /// <summary>Whether Shift was held.</summary>
    public bool Shift => (KeyData & Keys.Shift) == Keys.Shift;

    /// <summary>Whether the control receives the key in KeyDown, rather than the form using it to move focus.</summary>
    public bool IsInputKey { get; set; }
}

/// <summary>Handles a PreviewKeyDown event.</summary>
/// <param name="sender">The control.</param>
/// <param name="e">The event data.</param>
public delegate void PreviewKeyDownEventHandler(object? sender, PreviewKeyDownEventArgs e);
