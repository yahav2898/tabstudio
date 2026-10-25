namespace System.Windows.Forms;

/// <summary>An object with a window handle, such as a control or a form.</summary>
public interface IWin32Window
{
    /// <summary>The window handle.</summary>
    IntPtr Handle { get; }
}

/// <summary>A control that contains other controls and keeps track of the active one, such as a form.</summary>
public interface IContainerControl
{
    /// <summary>The control that has focus in the container.</summary>
    Control? ActiveControl { get; set; }

    /// <summary>Makes a control the active control.</summary>
    /// <param name="active">The control.</param>
    /// <returns>Whether the control became active.</returns>
    bool ActivateControl(Control active);
}

/// <summary>A control that acts as a button: a form's accept and cancel buttons.</summary>
public interface IButtonControl
{
    /// <summary>The result the button gives its form when clicked.</summary>
    DialogResult DialogResult { get; set; }

    /// <summary>Tells the button it is or is not its form's default button.</summary>
    /// <param name="value">Whether it is the default button.</param>
    void NotifyDefault(bool value);

    /// <summary>Clicks the button.</summary>
    void PerformClick();
}
