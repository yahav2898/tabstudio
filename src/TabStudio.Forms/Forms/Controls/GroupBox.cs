using System.ComponentModel;
using System.Drawing;
using TabStudio.Forms.Rendering;

namespace System.Windows.Forms;

/// <summary>A frame with a caption around a group of controls, such as radio buttons that exclude each other.</summary>
[DefaultProperty(nameof(Text))]
public class GroupBox : Control
{
    /// <summary>Creates a group box.</summary>
    public GroupBox()
    {
        SetStyle(ControlStyles.ContainerControl | ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
    }

    /// <summary>How the group box sizes itself when AutoSize is on.</summary>
    [DefaultValue(AutoSizeMode.GrowOnly)]
    public AutoSizeMode AutoSizeMode { get; set; } = AutoSizeMode.GrowOnly;

    /// <summary>The look of the frame. Kept for compatibility.</summary>
    public FlatStyle FlatStyle { get; set; } = FlatStyle.Standard;

    /// <summary>Whether text is drawn with GDI+. Kept for compatibility.</summary>
    public bool UseCompatibleTextRendering { get; set; }

    /// <summary>The area inside the frame, below the caption, inside the padding.</summary>
    public override Rectangle DisplayRectangle
    {
        get
        {
            var client = ClientSize;
            var caption = Font.Height;
            var padding = Padding;
            return new Rectangle(padding.Left, caption + padding.Top, Math.Max(client.Width - padding.Horizontal, 0), Math.Max(client.Height - caption - padding.Vertical, 0));
        }
    }

    /// <inheritdoc />
    protected override Size DefaultSize => new(200, 100);

    /// <summary>The group's mnemonic moves focus to its first control.</summary>
    /// <param name="charCode">The character.</param>
    protected internal override bool ProcessMnemonic(char charCode)
    {
        if (CanProcessMnemonic() && IsMnemonic(charCode, Text))
        {
            SelectNextControl(null, forward: true, tabStopOnly: true, nested: true, wrap: false);
            return true;
        }

        return base.ProcessMnemonic(charCode);
    }

    /// <inheritdoc />
    protected override Padding DefaultPadding => new(3);

    /// <inheritdoc />
    internal override Size GetPreferredSizeCore(Size proposedSize)
    {
        var content = base.GetPreferredSizeCore(proposedSize);
        var caption = (int)Math.Ceiling(TextMetrics.Measure(Graphics.Prefixed(Text, System.Drawing.Text.HotkeyPrefix.Hide), Font.Spec).Width) + 16;
        content = new Size(Math.Max(content.Width, caption), content.Height + Font.Height);
        return AutoSizeMode == AutoSizeMode.GrowOnly ? new Size(Math.Max(content.Width, Width), Math.Max(content.Height, Height)) : content;
    }

    /// <inheritdoc />
    internal override void PaintContent(Graphics g)
    {
        var text = Graphics.Prefixed(Text, System.Drawing.Text.HotkeyPrefix.Hide);
        var lineY = Font.Height / 2;
        var textWidth = text.Length == 0 ? 0 : (int)Math.Ceiling(TextMetrics.Measure(text, Font.Spec).Width);
        var size = ClientSize;
        using (var pen = new Pen(Theme.GroupBorder))
        {
            if (textWidth > 0)
            {
                g.DrawLine(pen, 0, lineY, 6, lineY);
                g.DrawLine(pen, Math.Min(size.Width - 1, 8 + textWidth + 2), lineY, size.Width - 1, lineY);
            }
            else
            {
                g.DrawLine(pen, 0, lineY, size.Width - 1, lineY);
            }

            g.DrawLine(pen, 0, lineY, 0, size.Height - 1);
            g.DrawLine(pen, size.Width - 1, lineY, size.Width - 1, size.Height - 1);
            g.DrawLine(pen, 0, size.Height - 1, size.Width - 1, size.Height - 1);
        }

        if (textWidth > 0)
        {
            g.DrawText(text, Font, Enabled ? ForeColor : Theme.DisabledText, new RectangleF(8, 0, Math.Min(textWidth + 1, size.Width - 10), Font.Height), new TextOptions(StringAlignment.Near, StringAlignment.Near, Wrap: false, Ellipsis: true, Clip: true));
        }
    }
}
