using System.ComponentModel;
using System.Drawing;

namespace System.Windows.Forms;

/// <summary>
/// The workspace of an MDI parent form, which <see cref="Form.IsMdiContainer"/> adds: it fills what the form's
/// docked menus and tool bars leave, and holds the MDI child forms, each with a title bar to move, maximize and close it.
/// </summary>
[ToolboxItem(false)]
public sealed class MdiClient : Control
{
    private const int MinimizedWidth = 160;
    private Form? _active;

    /// <summary>Creates the workspace, docked to fill its form.</summary>
    public MdiClient()
    {
        Dock = DockStyle.Fill;
        BackColor = SystemColors.AppWorkspace;
        SetStyle(ControlStyles.Selectable, false);
    }

    /// <summary>The MDI children, front to back.</summary>
    public Form[] MdiChildren => [.. Controls.OfType<Form>()];

    /// <summary>The child shown in front, which has the active title bar.</summary>
    internal Form? ActiveChild => _active is { IsDisposed: false } active && active.Parent == this ? active : null;

    /// <summary>Arranges the children: cascaded from the top-left corner, or tiled to share the workspace.</summary>
    /// <param name="value">The arrangement.</param>
    public void LayoutMdi(MdiLayout value)
    {
        var children = Controls.OfType<Form>().Where(child => child.VisibleSelf && child.WindowState != FormWindowState.Minimized).Reverse().ToList();
        var area = ClientRectangle;
        if (value == MdiLayout.ArrangeIcons)
        {
            ArrangeMinimized();
            return;
        }

        if (children.Count == 0 || area.Width <= 0 || area.Height <= 0)
        {
            return;
        }

        foreach (var child in children.Where(child => child.WindowState == FormWindowState.Maximized))
        {
            child.WindowState = FormWindowState.Normal;
        }

        switch (value)
        {
            case MdiLayout.Cascade:
            {
                var step = Form.MdiCaptionHeight + 2;
                var size = new Size(Math.Max(200, area.Width * 3 / 5), Math.Max(150, area.Height * 3 / 5));
                for (var i = 0; i < children.Count; i++)
                {
                    var offset = step * (i % Math.Max(1, (area.Height - size.Height) / step + 1));
                    children[i].Bounds = new Rectangle(offset, offset, Math.Min(size.Width, area.Width), Math.Min(size.Height, area.Height));
                    children[i].BringToFront();
                }

                break;
            }

            case MdiLayout.TileHorizontal:
            {
                // One above the other, as Windows' "Tile Horizontally" does.
                var height = area.Height / children.Count;
                for (var i = 0; i < children.Count; i++)
                {
                    var bottom = i == children.Count - 1 ? area.Height : (i + 1) * height;
                    children[i].Bounds = new Rectangle(0, i * height, area.Width, bottom - (i * height));
                }

                break;
            }

            case MdiLayout.TileVertical:
            {
                var width = area.Width / children.Count;
                for (var i = 0; i < children.Count; i++)
                {
                    var right = i == children.Count - 1 ? area.Width : (i + 1) * width;
                    children[i].Bounds = new Rectangle(i * width, 0, right - (i * width), area.Height);
                }

                break;
            }
        }

        if (ActiveChild is { } active)
        {
            active.BringToFront();
        }
    }

    /// <summary>Adds a child and places it where Windows places a new MDI child: cascaded below the last one.</summary>
    /// <param name="child">The child.</param>
    internal void AddChild(Form child)
    {
        if (child.StartPosition is FormStartPosition.WindowsDefaultLocation or FormStartPosition.WindowsDefaultBounds)
        {
            var step = Form.MdiCaptionHeight + 2;
            var index = Controls.OfType<Form>().Count();
            var offset = ClientSize.Height > step * 4 ? step * (index % Math.Max(1, (ClientSize.Height / 2) / step)) : 0;
            child.Location = new Point(offset, offset);
        }
        else if (child.StartPosition is FormStartPosition.CenterParent or FormStartPosition.CenterScreen)
        {
            child.Location = new Point(Math.Max(0, (ClientSize.Width - child.Width) / 2), Math.Max(0, (ClientSize.Height - child.Height) / 2));
        }

        Controls.Add(child);
        child.BringToFront();
    }

    /// <summary>Makes a child the active one: in front, with the active title bar.</summary>
    /// <param name="child">The child.</param>
    internal void Activate(Form? child)
    {
        if (child is not null && child.Parent != this)
        {
            return;
        }

        child?.BringToFront();
        if (child == ActiveChild)
        {
            return;
        }

        var previous = ActiveChild;
        _active = child;
        previous?.Invalidate();
        if (previous is { IsDisposed: false })
        {
            previous.DispatchDeactivate();
        }

        if (child is not null)
        {
            child.Invalidate();
            child.DispatchActivated();
        }

        (Parent as Form)?.DispatchMdiChildActivate();
        UpdateWindowList();
    }

    /// <summary>Takes a child out, when it closes, and activates the next one.</summary>
    /// <param name="child">The child.</param>
    internal void RemoveChild(Form child)
    {
        var wasActive = ActiveChild == child;
        Controls.Remove(child);
        if (wasActive)
        {
            _active = null;
            Activate(Controls.OfType<Form>().FirstOrDefault(form => form.VisibleSelf));
        }
        else
        {
            UpdateWindowList();
        }
    }

    /// <summary>Activates the next child when the active one is hidden.</summary>
    /// <param name="child">The hidden child.</param>
    internal void ChildHidden(Form child)
    {
        if (ActiveChild == child)
        {
            _active = null;
            Activate(Controls.OfType<Form>().FirstOrDefault(form => form != child && form.VisibleSelf));
        }
        else
        {
            UpdateWindowList();
        }
    }

    /// <summary>The bounds of a maximized child: the whole workspace.</summary>
    internal Rectangle MaximizedBounds => ClientRectangle;

    /// <summary>The bounds of a minimized child: a title bar along the bottom of the workspace.</summary>
    /// <param name="child">The child.</param>
    internal Rectangle MinimizedBounds(Form child)
    {
        var minimized = Controls.OfType<Form>().Where(form => form != child && form.WindowState == FormWindowState.Minimized).Count();
        var height = Form.MdiCaptionHeight + 2;
        var perRow = Math.Max(1, ClientSize.Width / MinimizedWidth);
        return new Rectangle((minimized % perRow) * MinimizedWidth, ClientSize.Height - (height * (1 + (minimized / perRow))), MinimizedWidth, height);
    }

    /// <inheritdoc />
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        foreach (var child in Controls.OfType<Form>())
        {
            if (child.WindowState == FormWindowState.Maximized)
            {
                child.Bounds = MaximizedBounds;
            }
        }

        ArrangeMinimized();
    }

    /// <summary>Lists the children in the window list menu item of the parent's menu, as Windows does.</summary>
    internal void UpdateWindowList()
    {
        if ((Parent as Form)?.MainMenuStrip?.MdiWindowListItem is not { } item)
        {
            return;
        }

        foreach (var old in item.DropDownItems.OfType<ToolStripItem>().Where(entry => entry.Tag is MdiWindowListEntry).ToList())
        {
            item.DropDownItems.Remove(old);
        }

        var children = Controls.OfType<Form>().Where(child => child.VisibleSelf).Reverse().ToList();
        if (children.Count == 0)
        {
            return;
        }

        if (item.DropDownItems.Count > 0)
        {
            item.DropDownItems.Add(new ToolStripSeparator { Tag = MdiWindowListEntry.Instance });
        }

        // Windows lists children in the order they were opened, the active one checked.
        var opened = Controls.OfType<Form>().Where(child => child.VisibleSelf).OrderBy(child => child.MdiOrder).ToList();
        for (var i = 0; i < opened.Count && i < 9; i++)
        {
            var child = opened[i];
            var entry = new ToolStripMenuItem($"&{i + 1} {child.Text}") { Tag = MdiWindowListEntry.Instance, Checked = child == ActiveChild };
            entry.Click += (_, _) =>
            {
                if (child.WindowState == FormWindowState.Minimized)
                {
                    child.WindowState = FormWindowState.Normal;
                }

                child.Activate();
            };
            item.DropDownItems.Add(entry);
        }
    }

    private void ArrangeMinimized()
    {
        var index = 0;
        var height = Form.MdiCaptionHeight + 2;
        var perRow = Math.Max(1, ClientSize.Width / MinimizedWidth);
        foreach (var child in Controls.OfType<Form>().Where(form => form.WindowState == FormWindowState.Minimized).Reverse())
        {
            child.Bounds = new Rectangle((index % perRow) * MinimizedWidth, ClientSize.Height - (height * (1 + (index / perRow))), MinimizedWidth, height);
            index++;
        }
    }

    private sealed class MdiWindowListEntry
    {
        public static MdiWindowListEntry Instance { get; } = new();
    }
}
