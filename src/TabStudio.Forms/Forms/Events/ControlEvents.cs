using System.ComponentModel;

namespace System.Windows.Forms;

/// <summary>Data of the Layout event: what changed.</summary>
public sealed class LayoutEventArgs : EventArgs
{
    /// <summary>Creates the data of a Layout event.</summary>
    /// <param name="affectedControl">The control whose change caused the layout.</param>
    /// <param name="affectedProperty">The property that changed.</param>
    public LayoutEventArgs(Control? affectedControl, string? affectedProperty)
    {
        AffectedComponent = affectedControl;
        AffectedProperty = affectedProperty;
    }

    /// <summary>Creates the data of a Layout event.</summary>
    /// <param name="affectedComponent">The component whose change caused the layout.</param>
    /// <param name="affectedProperty">The property that changed.</param>
    public LayoutEventArgs(IComponent? affectedComponent, string? affectedProperty)
    {
        AffectedComponent = affectedComponent;
        AffectedProperty = affectedProperty;
    }

    /// <summary>The component whose change caused the layout.</summary>
    public IComponent? AffectedComponent { get; }

    /// <summary>The control whose change caused the layout.</summary>
    public Control? AffectedControl => AffectedComponent as Control;

    /// <summary>The property that changed.</summary>
    public string? AffectedProperty { get; }
}

/// <summary>Handles a Layout event.</summary>
/// <param name="sender">The control.</param>
/// <param name="e">The event data.</param>
public delegate void LayoutEventHandler(object? sender, LayoutEventArgs e);

/// <summary>Data of the ControlAdded and ControlRemoved events.</summary>
public class ControlEventArgs : EventArgs
{
    /// <summary>Creates the data.</summary>
    /// <param name="control">The control added or removed.</param>
    public ControlEventArgs(Control? control) => Control = control;

    /// <summary>The control added or removed.</summary>
    public Control? Control { get; }
}

/// <summary>Handles a ControlAdded or ControlRemoved event.</summary>
/// <param name="sender">The container.</param>
/// <param name="e">The event data.</param>
public delegate void ControlEventHandler(object? sender, ControlEventArgs e);

/// <summary>Data of the FormClosing event, which can cancel the closing.</summary>
public class FormClosingEventArgs : CancelEventArgs
{
    /// <summary>Creates the data.</summary>
    /// <param name="closeReason">Why the form is closing.</param>
    /// <param name="cancel">Whether the closing is cancelled to begin with.</param>
    public FormClosingEventArgs(CloseReason closeReason, bool cancel)
        : base(cancel) => CloseReason = closeReason;

    /// <summary>Why the form is closing.</summary>
    public CloseReason CloseReason { get; }
}

/// <summary>Handles a FormClosing event.</summary>
/// <param name="sender">The form.</param>
/// <param name="e">The event data.</param>
public delegate void FormClosingEventHandler(object? sender, FormClosingEventArgs e);

/// <summary>Data of the FormClosed event.</summary>
public class FormClosedEventArgs : EventArgs
{
    /// <summary>Creates the data.</summary>
    /// <param name="closeReason">Why the form closed.</param>
    public FormClosedEventArgs(CloseReason closeReason) => CloseReason = closeReason;

    /// <summary>Why the form closed.</summary>
    public CloseReason CloseReason { get; }
}

/// <summary>Handles a FormClosed event.</summary>
/// <param name="sender">The form.</param>
/// <param name="e">The event data.</param>
public delegate void FormClosedEventHandler(object? sender, FormClosedEventArgs e);

/// <summary>Data of a Scroll event.</summary>
public class ScrollEventArgs : EventArgs
{
    /// <summary>Creates the data.</summary>
    /// <param name="type">What the scroll bar did.</param>
    /// <param name="newValue">The new position.</param>
    public ScrollEventArgs(ScrollEventType type, int newValue)
        : this(type, -1, newValue, ScrollOrientation.HorizontalScroll)
    {
    }

    /// <summary>Creates the data.</summary>
    /// <param name="type">What the scroll bar did.</param>
    /// <param name="oldValue">The old position.</param>
    /// <param name="newValue">The new position.</param>
    /// <param name="scroll">The scroll bar's orientation.</param>
    public ScrollEventArgs(ScrollEventType type, int oldValue, int newValue, ScrollOrientation scroll)
    {
        Type = type;
        OldValue = oldValue;
        NewValue = newValue;
        ScrollOrientation = scroll;
    }

    /// <summary>What the scroll bar did.</summary>
    public ScrollEventType Type { get; }

    /// <summary>The old position.</summary>
    public int OldValue { get; }

    /// <summary>The new position.</summary>
    public int NewValue { get; set; }

    /// <summary>The scroll bar's orientation.</summary>
    public ScrollOrientation ScrollOrientation { get; }
}

/// <summary>Handles a Scroll event.</summary>
/// <param name="sender">The control.</param>
/// <param name="e">The event data.</param>
public delegate void ScrollEventHandler(object? sender, ScrollEventArgs e);

/// <summary>Data of the ItemCheck event of a checked list box.</summary>
public class ItemCheckEventArgs : EventArgs
{
    /// <summary>Creates the data.</summary>
    /// <param name="index">The item's index.</param>
    /// <param name="newCheckValue">The state it is changing to.</param>
    /// <param name="currentValue">The state it has.</param>
    public ItemCheckEventArgs(int index, CheckState newCheckValue, CheckState currentValue)
    {
        Index = index;
        NewValue = newCheckValue;
        CurrentValue = currentValue;
    }

    /// <summary>The item's index.</summary>
    public int Index { get; }

    /// <summary>The state it is changing to; a handler may change it.</summary>
    public CheckState NewValue { get; set; }

    /// <summary>The state it has.</summary>
    public CheckState CurrentValue { get; }
}

/// <summary>Handles an ItemCheck event.</summary>
/// <param name="sender">The list.</param>
/// <param name="e">The event data.</param>
public delegate void ItemCheckEventHandler(object? sender, ItemCheckEventArgs e);

/// <summary>Represents a method with no parameters and no result, as Control.Invoke takes.</summary>
public delegate void MethodInvoker();

/// <summary>Data of the Invalidated event: the area to draw again.</summary>
public class InvalidateEventArgs : EventArgs
{
    /// <summary>Creates the data.</summary>
    /// <param name="invalidRect">The area to draw again, in client coordinates.</param>
    public InvalidateEventArgs(System.Drawing.Rectangle invalidRect) => InvalidRect = invalidRect;

    /// <summary>The area to draw again, in client coordinates.</summary>
    public System.Drawing.Rectangle InvalidRect { get; }
}

/// <summary>Handles an Invalidated event.</summary>
/// <param name="sender">The control.</param>
/// <param name="e">The event data.</param>
public delegate void InvalidateEventHandler(object? sender, InvalidateEventArgs e);
