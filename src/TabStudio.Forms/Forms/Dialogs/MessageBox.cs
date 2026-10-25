using System.Drawing;
using System.Drawing.Drawing2D;
using TabStudio.Forms.Rendering;

namespace System.Windows.Forms;

/// <summary>
/// A message box: a dialog with text, an icon and buttons, shown inside the app preview over the app's windows.
/// <c>Show</c> waits until a button is pressed and returns which one.
/// </summary>
public static class MessageBox
{
    /// <summary>Shows text with an OK button.</summary>
    /// <param name="text">The text.</param>
    public static DialogResult Show(string? text) => Show(null, text, string.Empty, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);

    /// <summary>Shows text with a caption and an OK button.</summary>
    /// <param name="text">The text.</param>
    /// <param name="caption">The title.</param>
    public static DialogResult Show(string? text, string? caption) => Show(null, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);

    /// <summary>Shows text with a caption and buttons.</summary>
    /// <param name="text">The text.</param>
    /// <param name="caption">The title.</param>
    /// <param name="buttons">The buttons.</param>
    public static DialogResult Show(string? text, string? caption, MessageBoxButtons buttons) =>
        Show(null, text, caption, buttons, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);

    /// <summary>Shows text with a caption, buttons and an icon.</summary>
    /// <param name="text">The text.</param>
    /// <param name="caption">The title.</param>
    /// <param name="buttons">The buttons.</param>
    /// <param name="icon">The icon.</param>
    public static DialogResult Show(string? text, string? caption, MessageBoxButtons buttons, MessageBoxIcon icon) =>
        Show(null, text, caption, buttons, icon, MessageBoxDefaultButton.Button1);

    /// <summary>Shows text with a caption, buttons, an icon and a default button.</summary>
    /// <param name="text">The text.</param>
    /// <param name="caption">The title.</param>
    /// <param name="buttons">The buttons.</param>
    /// <param name="icon">The icon.</param>
    /// <param name="defaultButton">The button Enter presses.</param>
    public static DialogResult Show(string? text, string? caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton) =>
        Show(null, text, caption, buttons, icon, defaultButton);

    /// <summary>Shows text with a caption, buttons, an icon, a default button and options.</summary>
    /// <param name="text">The text.</param>
    /// <param name="caption">The title.</param>
    /// <param name="buttons">The buttons.</param>
    /// <param name="icon">The icon.</param>
    /// <param name="defaultButton">The button Enter presses.</param>
    /// <param name="options">Options, kept for compatibility.</param>
    public static DialogResult Show(string? text, string? caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton, MessageBoxOptions options) =>
        Show(null, text, caption, buttons, icon, defaultButton);

    /// <summary>Shows text over a window.</summary>
    /// <param name="owner">The window.</param>
    /// <param name="text">The text.</param>
    public static DialogResult Show(IWin32Window? owner, string? text) => Show(owner, text, string.Empty, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);

    /// <summary>Shows text with a caption over a window.</summary>
    /// <param name="owner">The window.</param>
    /// <param name="text">The text.</param>
    /// <param name="caption">The title.</param>
    public static DialogResult Show(IWin32Window? owner, string? text, string? caption) =>
        Show(owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);

    /// <summary>Shows text with a caption and buttons over a window.</summary>
    /// <param name="owner">The window.</param>
    /// <param name="text">The text.</param>
    /// <param name="caption">The title.</param>
    /// <param name="buttons">The buttons.</param>
    public static DialogResult Show(IWin32Window? owner, string? text, string? caption, MessageBoxButtons buttons) =>
        Show(owner, text, caption, buttons, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);

    /// <summary>Shows text with a caption, buttons and an icon over a window.</summary>
    /// <param name="owner">The window.</param>
    /// <param name="text">The text.</param>
    /// <param name="caption">The title.</param>
    /// <param name="buttons">The buttons.</param>
    /// <param name="icon">The icon.</param>
    public static DialogResult Show(IWin32Window? owner, string? text, string? caption, MessageBoxButtons buttons, MessageBoxIcon icon) =>
        Show(owner, text, caption, buttons, icon, MessageBoxDefaultButton.Button1);

    /// <summary>Shows text with a caption, buttons, an icon and a default button over a window.</summary>
    /// <param name="owner">The window.</param>
    /// <param name="text">The text.</param>
    /// <param name="caption">The title.</param>
    /// <param name="buttons">The buttons.</param>
    /// <param name="icon">The icon.</param>
    /// <param name="defaultButton">The button Enter presses.</param>
    /// <returns>The button pressed: OK, Cancel, Yes, No and so on.</returns>
    public static DialogResult Show(IWin32Window? owner, string? text, string? caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
    {
        using var box = new MessageBoxForm(text ?? string.Empty, caption ?? string.Empty, buttons, icon, defaultButton);
        return box.ShowDialog(owner);
    }

    /// <summary>Shows text with a caption, buttons, an icon, a default button and options over a window.</summary>
    /// <param name="owner">The window.</param>
    /// <param name="text">The text.</param>
    /// <param name="caption">The title.</param>
    /// <param name="buttons">The buttons.</param>
    /// <param name="icon">The icon.</param>
    /// <param name="defaultButton">The button Enter presses.</param>
    /// <param name="options">Options, kept for compatibility.</param>
    public static DialogResult Show(IWin32Window? owner, string? text, string? caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton, MessageBoxOptions options) =>
        Show(owner, text, caption, buttons, icon, defaultButton);
}

/// <summary>The dialog of a message box, laid out as Windows lays out its message boxes.</summary>
internal sealed class MessageBoxForm : Form
{
    private const int Inset = 24;
    private const int IconSize = 32;
    private const int MaxTextWidth = 420;
    private const int ButtonWidth = 88;
    private const int ButtonHeight = 26;
    private const int ButtonGap = 8;
    private const int StripHeight = 46;

    public MessageBoxForm(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
    {
        SuspendLayout();
        Text = caption;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.White;
        Font = SystemFonts.MessageBoxFont;

        var textLeft = Inset + (icon == MessageBoxIcon.None ? 0 : IconSize + 12);
        var measured = TextMetrics.Measure(text, Font.Spec, MaxTextWidth);
        var textSize = new Size((int)Math.Ceiling(measured.Width) + 4, (int)Math.Ceiling(measured.Height));
        var contentHeight = Math.Max(icon == MessageBoxIcon.None ? 0 : IconSize, textSize.Height);
        var choices = Choices(buttons);
        var buttonsWidth = (choices.Count * ButtonWidth) + ((choices.Count - 1) * ButtonGap) + 24;
        var width = Math.Max(Math.Max(textLeft + textSize.Width + Inset, buttonsWidth), 200);
        var height = Inset + contentHeight + Inset + StripHeight;
        ClientSize = new Size(width, height);

        if (icon != MessageBoxIcon.None)
        {
            Controls.Add(new MessageBoxGlyph(icon) { Bounds = new Rectangle(Inset, Inset + ((contentHeight - IconSize) / 2), IconSize, IconSize) });
        }

        Controls.Add(new Label
        {
            Text = text,
            UseMnemonic = false,
            AutoSize = false,
            Bounds = new Rectangle(textLeft, Inset + ((contentHeight - textSize.Height) / 2), textSize.Width, textSize.Height),
            BackColor = Color.White,
        });

        var strip = new Panel { Dock = DockStyle.Bottom, Height = StripHeight, BackColor = SystemColors.Control };
        Controls.Add(strip);
        var x = width - 12 - ButtonWidth;
        var created = new List<Button>();
        for (var i = choices.Count - 1; i >= 0; i--)
        {
            var (label, result) = choices[i];
            var button = new Button
            {
                Text = label,
                DialogResult = result,
                Bounds = new Rectangle(x, (StripHeight - ButtonHeight) / 2, ButtonWidth, ButtonHeight),
                TabIndex = i,
            };
            strip.Controls.Add(button);
            created.Insert(0, button);
            x -= ButtonWidth + ButtonGap;
        }

        var defaultIndex = defaultButton switch
        {
            MessageBoxDefaultButton.Button2 => 1,
            MessageBoxDefaultButton.Button3 => 2,
            MessageBoxDefaultButton.Button4 => 3,
            _ => 0,
        };
        var accept = created[Math.Min(defaultIndex, created.Count - 1)];
        AcceptButton = accept;
        ActiveControl = accept;
        if (created.Find(b => b.DialogResult == DialogResult.Cancel) is { } cancel)
        {
            CancelButton = cancel;
        }
        else if (created.Count == 1)
        {
            CancelButton = created[0];
        }
        else
        {
            ControlBox = false;
        }

        ResumeLayout(false);
        PerformLayout();
    }

    private static List<(string Label, DialogResult Result)> Choices(MessageBoxButtons buttons) => buttons switch
    {
        MessageBoxButtons.OKCancel => [("OK", DialogResult.OK), ("Cancel", DialogResult.Cancel)],
        MessageBoxButtons.AbortRetryIgnore => [("&Abort", DialogResult.Abort), ("&Retry", DialogResult.Retry), ("&Ignore", DialogResult.Ignore)],
        MessageBoxButtons.YesNoCancel => [("&Yes", DialogResult.Yes), ("&No", DialogResult.No), ("Cancel", DialogResult.Cancel)],
        MessageBoxButtons.YesNo => [("&Yes", DialogResult.Yes), ("&No", DialogResult.No)],
        MessageBoxButtons.RetryCancel => [("&Retry", DialogResult.Retry), ("Cancel", DialogResult.Cancel)],
        MessageBoxButtons.CancelTryContinue => [("Cancel", DialogResult.Cancel), ("&Try Again", DialogResult.TryAgain), ("&Continue", DialogResult.Continue)],
        _ => [("OK", DialogResult.OK)],
    };
}

/// <summary>The icon of a message box, drawn as Windows 10 and 11 draw them.</summary>
internal sealed class MessageBoxGlyph : Control
{
    private readonly MessageBoxIcon _icon;

    public MessageBoxGlyph(MessageBoxIcon icon)
    {
        _icon = icon;
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
    }

    /// <inheritdoc />
    internal override void PaintContent(Graphics g) => Draw(g, new Rectangle(Point.Empty, ClientSize), _icon);

    /// <summary>Draws a message box icon in a square.</summary>
    internal static void Draw(Graphics g, Rectangle box, MessageBoxIcon icon)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var font = new Font("Segoe UI", box.Height * 0.42f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        switch (icon)
        {
            case MessageBoxIcon.Warning:
            {
                using var fill = new SolidBrush(Color.FromArgb(252, 225, 0));
                using var edge = new Pen(Color.FromArgb(222, 183, 0), 1.5f);
                PointF[] triangle = [new(box.X + (box.Width / 2f), box.Y + 2), new(box.Right - 1, box.Bottom - 3), new(box.X + 1, box.Bottom - 3)];
                g.FillPolygon(fill, triangle);
                g.DrawPolygon(edge, triangle);
                g.DrawString("!", font, Brushes.Black, new RectangleF(box.X, box.Y + (box.Height * 0.12f), box.Width, box.Height), center);
                break;
            }

            case MessageBoxIcon.Error:
            {
                using var fill = new SolidBrush(Color.FromArgb(232, 17, 35));
                g.FillEllipse(fill, box.X + 1, box.Y + 1, box.Width - 2, box.Height - 2);
                using var cross = new Pen(Color.White, box.Width * 0.09f);
                var inset = box.Width * 0.32f;
                g.DrawLine(cross, box.X + inset, box.Y + inset, box.Right - inset, box.Bottom - inset);
                g.DrawLine(cross, box.Right - inset, box.Y + inset, box.X + inset, box.Bottom - inset);
                break;
            }

            default:
            {
                using var fill = new SolidBrush(Color.FromArgb(0, 120, 215));
                g.FillEllipse(fill, box.X + 1, box.Y + 1, box.Width - 2, box.Height - 2);
                g.DrawString(icon == MessageBoxIcon.Question ? "?" : "i", font, Brushes.White, new RectangleF(box.X, box.Y, box.Width, box.Height), center);
                break;
            }
        }
    }
}
