using System.ComponentModel;

namespace System.Windows.Forms;

/// <summary>Data about an item of a strip that was clicked.</summary>
public class ToolStripItemClickedEventArgs : EventArgs
{
    /// <summary>Creates the data.</summary>
    /// <param name="clickedItem">The item.</param>
    public ToolStripItemClickedEventArgs(ToolStripItem clickedItem)
    {
        ClickedItem = clickedItem;
    }

    /// <summary>The item clicked.</summary>
    public ToolStripItem ClickedItem { get; }
}

/// <summary>Handles a click on an item of a strip.</summary>
/// <param name="sender">The strip.</param>
/// <param name="e">The event data.</param>
public delegate void ToolStripItemClickedEventHandler(object? sender, ToolStripItemClickedEventArgs e);

/// <summary>Data about an item added to or removed from a strip.</summary>
public class ToolStripItemEventArgs : EventArgs
{
    /// <summary>Creates the data.</summary>
    /// <param name="item">The item.</param>
    public ToolStripItemEventArgs(ToolStripItem? item)
    {
        Item = item;
    }

    /// <summary>The item.</summary>
    public ToolStripItem? Item { get; }
}

/// <summary>Handles an item added to or removed from a strip.</summary>
/// <param name="sender">The strip.</param>
/// <param name="e">The event data.</param>
public delegate void ToolStripItemEventHandler(object? sender, ToolStripItemEventArgs e);

/// <summary>Data about a drop-down that is closing, which handlers can keep open.</summary>
public class ToolStripDropDownClosingEventArgs : CancelEventArgs
{
    /// <summary>Creates the data.</summary>
    /// <param name="reason">Why it closes.</param>
    public ToolStripDropDownClosingEventArgs(ToolStripDropDownCloseReason reason)
    {
        CloseReason = reason;
    }

    /// <summary>Why the drop-down closes.</summary>
    public ToolStripDropDownCloseReason CloseReason { get; }
}

/// <summary>Handles a drop-down that is closing.</summary>
/// <param name="sender">The drop-down.</param>
/// <param name="e">The event data.</param>
public delegate void ToolStripDropDownClosingEventHandler(object? sender, ToolStripDropDownClosingEventArgs e);

/// <summary>Data about a drop-down that closed.</summary>
public class ToolStripDropDownClosedEventArgs : EventArgs
{
    /// <summary>Creates the data.</summary>
    /// <param name="reason">Why it closed.</param>
    public ToolStripDropDownClosedEventArgs(ToolStripDropDownCloseReason reason)
    {
        CloseReason = reason;
    }

    /// <summary>Why the drop-down closed.</summary>
    public ToolStripDropDownCloseReason CloseReason { get; }
}

/// <summary>Handles a drop-down that closed.</summary>
/// <param name="sender">The drop-down.</param>
/// <param name="e">The event data.</param>
public delegate void ToolStripDropDownClosedEventHandler(object? sender, ToolStripDropDownClosedEventArgs e);
