using System.Drawing;
using System.Windows.Forms;

namespace TabStudio.Forms.Runtime;

/// <summary>
/// The menu Windows gives a text box that has no context menu of its own: Undo, Cut, Copy, Paste, Delete and Select
/// All, each enabled when it can act. A right-click or a long press on the text box opens it.
/// </summary>
internal static class TextEditMenu
{
    /// <summary>Opens the menu for a text box.</summary>
    /// <param name="box">The text box.</param>
    /// <param name="window">Its window.</param>
    /// <param name="point">Where, in the window's client coordinates.</param>
    public static void Show(TextBoxBase box, FormWindow window, Point point)
    {
        var editable = !box.ReadOnly && box.Enabled;
        var selected = box.SelectionLength > 0;
        var hidden = box is TextBox { PasswordChar: not '\0' } or TextBox { UseSystemPasswordChar: true };
        var menu = new ContextMenuStrip();
        menu.Items.Add(Item("&Undo", editable && box.CanUndo, box.Undo));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Item("Cu&t", editable && selected && !hidden, box.Cut));
        menu.Items.Add(Item("&Copy", selected && !hidden, box.Copy));
        menu.Items.Add(Item("&Paste", editable && Clipboard.ContainsText(), box.Paste));
        menu.Items.Add(Item("&Delete", editable && selected, () => box.SelectedText = string.Empty));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Item("Select &All", box.TextLength > 0 && !(selected && box.SelectionLength == box.TextLength), box.SelectAll));
        menu.Closed += (_, _) => window.Runtime.Post(menu.Dispose);
        menu.ShowAt(box, window, point);
    }

    private static ToolStripMenuItem Item(string text, bool enabled, Action action)
    {
        var item = new ToolStripMenuItem(text) { Enabled = enabled };
        item.Click += (_, _) => action();
        return item;
    }
}
