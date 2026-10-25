using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace TabStudio.Forms.Rendering;

/// <summary>The look of a button-like part: a button, a drop-down arrow, a tab or a spin button.</summary>
internal enum PushState
{
    /// <summary>At rest.</summary>
    Normal,

    /// <summary>Under the pointer.</summary>
    Hot,

    /// <summary>Pressed.</summary>
    Pressed,

    /// <summary>Disabled.</summary>
    Disabled,
}

/// <summary>A direction an arrow glyph points in.</summary>
internal enum ArrowDirection
{
    /// <summary>Up.</summary>
    Up,

    /// <summary>Down.</summary>
    Down,

    /// <summary>Left.</summary>
    Left,

    /// <summary>Right.</summary>
    Right,
}

/// <summary>
/// Draws the parts of controls in the Windows visual styles: button faces, check boxes, radio buttons, edit borders,
/// arrows, scroll bars and text, with the colors of <see cref="Theme"/>.
/// </summary>
internal static class ThemePainter
{
    /// <summary>The size of a check box or radio button glyph.</summary>
    public const int GlyphSize = 13;

    /// <summary>Draws a button face and border.</summary>
    /// <param name="g">The surface.</param>
    /// <param name="bounds">The button's bounds.</param>
    /// <param name="state">The button's state.</param>
    /// <param name="emphasized">Whether it is the default button or has focus, which draws its border in the accent color.</param>
    /// <param name="face">A face color the app set, or <see langword="null"/> for the visual style's.</param>
    public static void DrawButton(Graphics g, Rectangle bounds, PushState state, bool emphasized, Color? face = null)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var (fill, border) = state switch
        {
            PushState.Hot => (Theme.ButtonHotFace, Theme.Accent),
            PushState.Pressed => (Theme.ButtonPressedFace, Theme.ButtonPressedBorder),
            PushState.Disabled => (Theme.ButtonDisabledFace, Theme.ButtonDisabledBorder),
            _ => (Theme.ButtonFace, emphasized ? Theme.Accent : Theme.ButtonBorder),
        };

        if (face is { } custom && state != PushState.Disabled)
        {
            fill = state == PushState.Pressed ? Blend(custom, Color.Black, 0.12f) : state == PushState.Hot ? Blend(custom, Color.White, 0.25f) : custom;
        }

        using var brush = new SolidBrush(fill);
        g.FillRectangle(brush, bounds);
        using var pen = new Pen(border);
        g.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
        if (emphasized && state == PushState.Normal && bounds.Width > 4 && bounds.Height > 4)
        {
            g.DrawRectangle(pen, bounds.X + 1, bounds.Y + 1, bounds.Width - 3, bounds.Height - 3);
        }
    }

    /// <summary>Draws a flat or popup button: a face of the button's color with a one-pixel border.</summary>
    /// <param name="g">The surface.</param>
    /// <param name="bounds">The button's bounds.</param>
    /// <param name="state">The button's state.</param>
    /// <param name="face">The face color.</param>
    /// <param name="border">The border color.</param>
    /// <param name="borderSize">The border width.</param>
    public static void DrawFlatButton(Graphics g, Rectangle bounds, PushState state, Color face, Color border, int borderSize)
    {
        var fill = state switch
        {
            PushState.Hot => Blend(face, Color.White, 0.3f),
            PushState.Pressed => Blend(face, Color.Black, 0.15f),
            _ => face,
        };
        using var brush = new SolidBrush(fill);
        g.FillRectangle(brush, bounds);
        if (borderSize > 0)
        {
            using var pen = new Pen(border);
            for (var i = 0; i < borderSize; i++)
            {
                g.DrawRectangle(pen, bounds.X + i, bounds.Y + i, bounds.Width - 1 - (2 * i), bounds.Height - 1 - (2 * i));
            }
        }
    }

    /// <summary>Draws a check box glyph.</summary>
    /// <param name="g">The surface.</param>
    /// <param name="box">The glyph's bounds, 13 pixels square.</param>
    /// <param name="state">Checked, unchecked or indeterminate.</param>
    /// <param name="push">The glyph's state.</param>
    public static void DrawCheckBox(Graphics g, Rectangle box, CheckState state, PushState push)
    {
        var border = push switch
        {
            PushState.Hot or PushState.Pressed => Theme.Accent,
            PushState.Disabled => Theme.ButtonDisabledBorder,
            _ => Theme.GlyphBorder,
        };
        var face = push == PushState.Pressed ? Theme.ButtonPressedFace : Theme.Window;
        using (var brush = new SolidBrush(face))
        {
            g.FillRectangle(brush, box);
        }

        using (var pen = new Pen(border))
        {
            g.DrawRectangle(pen, box.X, box.Y, box.Width - 1, box.Height - 1);
        }

        var glyph = push == PushState.Disabled ? Theme.DisabledText : push is PushState.Hot or PushState.Pressed ? Theme.Accent : Theme.Glyph;
        if (state == CheckState.Checked)
        {
            var smoothing = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(glyph, 1.6f);
            g.DrawLines(pen,
            [
                new PointF(box.X + 2.8f, box.Y + 6.6f),
                new PointF(box.X + 5.3f, box.Y + 9.1f),
                new PointF(box.X + 10.2f, box.Y + 3.6f),
            ]);
            g.SmoothingMode = smoothing;
        }
        else if (state == CheckState.Indeterminate)
        {
            using var brush = new SolidBrush(glyph);
            g.FillRectangle(brush, box.X + 3, box.Y + 3, box.Width - 6, box.Height - 6);
        }
    }

    /// <summary>Draws a radio button glyph.</summary>
    /// <param name="g">The surface.</param>
    /// <param name="box">The glyph's bounds, 13 pixels square.</param>
    /// <param name="isChecked">Whether it is checked.</param>
    /// <param name="push">The glyph's state.</param>
    public static void DrawRadio(Graphics g, Rectangle box, bool isChecked, PushState push)
    {
        var smoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var border = push switch
        {
            PushState.Hot or PushState.Pressed => Theme.Accent,
            PushState.Disabled => Theme.ButtonDisabledBorder,
            _ => Theme.GlyphBorder,
        };
        var face = push == PushState.Pressed ? Theme.ButtonPressedFace : Theme.Window;
        using (var brush = new SolidBrush(face))
        {
            g.FillEllipse(brush, box.X + 0.5f, box.Y + 0.5f, box.Width - 1, box.Height - 1);
        }

        using (var pen = new Pen(border))
        {
            g.DrawEllipse(pen, box.X + 0.5f, box.Y + 0.5f, box.Width - 1, box.Height - 1);
        }

        if (isChecked)
        {
            var glyph = push == PushState.Disabled ? Theme.DisabledText : push is PushState.Hot or PushState.Pressed ? Theme.Accent : Theme.Glyph;
            using var brush = new SolidBrush(glyph);
            g.FillEllipse(brush, box.X + 3.5f, box.Y + 3.5f, box.Width - 7, box.Height - 7);
        }

        g.SmoothingMode = smoothing;
    }

    /// <summary>Draws the border of a text box, list or other edit field.</summary>
    /// <param name="g">The surface, at the control's bounds.</param>
    /// <param name="bounds">The control's bounds.</param>
    /// <param name="style">The border style.</param>
    /// <param name="focused">Whether the field has focus, which draws the border in the accent color.</param>
    /// <param name="enabled">Whether the field is enabled.</param>
    public static void DrawEditBorder(Graphics g, Rectangle bounds, BorderStyle style, bool focused, bool enabled)
    {
        if (style == BorderStyle.None || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var color = !enabled ? Theme.EditDisabledBorder : focused ? Theme.Accent : style == BorderStyle.FixedSingle ? Color.FromArgb(100, 100, 100) : Theme.EditBorder;
        using var pen = new Pen(color);
        g.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
    }

    /// <summary>Draws the border of a panel or group: a single line, or a sunken three-dimensional edge.</summary>
    /// <param name="g">The surface, at the control's bounds.</param>
    /// <param name="bounds">The control's bounds.</param>
    /// <param name="style">The border style.</param>
    public static void DrawBorder(Graphics g, Rectangle bounds, BorderStyle style)
    {
        if (bounds.Width <= 1 || bounds.Height <= 1)
        {
            return;
        }

        switch (style)
        {
            case BorderStyle.FixedSingle:
            {
                using var pen = new Pen(Color.FromArgb(100, 100, 100));
                g.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
                break;
            }

            case BorderStyle.Fixed3D:
            {
                using var dark = new Pen(Color.FromArgb(160, 160, 160));
                using var darker = new Pen(Color.FromArgb(105, 105, 105));
                using var light = new Pen(Color.White);
                using var lighter = new Pen(Color.FromArgb(227, 227, 227));
                g.DrawLine(dark, bounds.X, bounds.Y, bounds.Right - 2, bounds.Y);
                g.DrawLine(dark, bounds.X, bounds.Y, bounds.X, bounds.Bottom - 2);
                g.DrawLine(darker, bounds.X + 1, bounds.Y + 1, bounds.Right - 3, bounds.Y + 1);
                g.DrawLine(darker, bounds.X + 1, bounds.Y + 1, bounds.X + 1, bounds.Bottom - 3);
                g.DrawLine(light, bounds.X, bounds.Bottom - 1, bounds.Right - 1, bounds.Bottom - 1);
                g.DrawLine(light, bounds.Right - 1, bounds.Y, bounds.Right - 1, bounds.Bottom - 1);
                g.DrawLine(lighter, bounds.X + 1, bounds.Bottom - 2, bounds.Right - 2, bounds.Bottom - 2);
                g.DrawLine(lighter, bounds.Right - 2, bounds.Y + 1, bounds.Right - 2, bounds.Bottom - 2);
                break;
            }
        }
    }

    /// <summary>The width of a border style, which the client area excludes.</summary>
    /// <param name="style">The border style.</param>
    public static int BorderWidth(BorderStyle style) => style switch
    {
        BorderStyle.FixedSingle => 1,
        BorderStyle.Fixed3D => 2,
        _ => 0,
    };

    /// <summary>Draws a chevron arrow, as drop-down buttons and scroll bars show.</summary>
    /// <param name="g">The surface.</param>
    /// <param name="bounds">The area the arrow is centered in.</param>
    /// <param name="direction">Where it points.</param>
    /// <param name="color">Its color.</param>
    public static void DrawArrow(Graphics g, Rectangle bounds, ArrowDirection direction, Color color)
    {
        var smoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var cx = bounds.X + (bounds.Width / 2f);
        var cy = bounds.Y + (bounds.Height / 2f);
        const float Half = 3.5f;
        const float Depth = 2f;
        PointF[] points = direction switch
        {
            ArrowDirection.Up => [new(cx - Half, cy + Depth), new(cx, cy - Depth), new(cx + Half, cy + Depth)],
            ArrowDirection.Left => [new(cx + Depth, cy - Half), new(cx - Depth, cy), new(cx + Depth, cy + Half)],
            ArrowDirection.Right => [new(cx - Depth, cy - Half), new(cx + Depth, cy), new(cx - Depth, cy + Half)],
            _ => [new(cx - Half, cy - Depth), new(cx, cy + Depth), new(cx + Half, cy - Depth)],
        };
        using var pen = new Pen(color, 1.2f);
        g.DrawLines(pen, points);
        g.SmoothingMode = smoothing;
    }

    /// <summary>Draws a dotted focus rectangle, as Windows shows on the control with keyboard focus.</summary>
    /// <param name="g">The surface.</param>
    /// <param name="bounds">The rectangle.</param>
    public static void DrawFocusRectangle(Graphics g, Rectangle bounds)
    {
        if (bounds.Width <= 1 || bounds.Height <= 1)
        {
            return;
        }

        using var pen = new Pen(Color.Black) { DashStyle = DashStyle.Dot };
        g.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
    }

    /// <summary>Draws a scroll bar.</summary>
    /// <param name="g">The surface.</param>
    /// <param name="bounds">The scroll bar's bounds.</param>
    /// <param name="vertical">Whether it is vertical.</param>
    /// <param name="thumb">The thumb's bounds.</param>
    /// <param name="pressed">Whether the thumb is being dragged.</param>
    /// <param name="enabled">Whether it can scroll.</param>
    public static void DrawScrollBar(Graphics g, Rectangle bounds, bool vertical, Rectangle thumb, bool pressed, bool enabled)
    {
        using (var track = new SolidBrush(Theme.ScrollTrack))
        {
            g.FillRectangle(track, bounds);
        }

        var arrow = enabled ? Theme.ScrollArrow : Theme.DisabledText;
        var size = Theme.ScrollBarSize;
        if (vertical)
        {
            DrawArrow(g, new Rectangle(bounds.X, bounds.Y, bounds.Width, size), ArrowDirection.Up, arrow);
            DrawArrow(g, new Rectangle(bounds.X, bounds.Bottom - size, bounds.Width, size), ArrowDirection.Down, arrow);
        }
        else
        {
            DrawArrow(g, new Rectangle(bounds.X, bounds.Y, size, bounds.Height), ArrowDirection.Left, arrow);
            DrawArrow(g, new Rectangle(bounds.Right - size, bounds.Y, size, bounds.Height), ArrowDirection.Right, arrow);
        }

        if (enabled && thumb.Width > 0 && thumb.Height > 0)
        {
            using var brush = new SolidBrush(pressed ? Theme.ScrollThumbPressed : Theme.ScrollThumb);
            var inset = vertical ? new Rectangle(thumb.X + 1, thumb.Y, thumb.Width - 2, thumb.Height) : new Rectangle(thumb.X, thumb.Y + 1, thumb.Width, thumb.Height - 2);
            g.FillRectangle(brush, inset);
        }
    }

    /// <summary>Draws text aligned in a rectangle, as a control's text: access-key ampersands removed, clipped.</summary>
    /// <param name="g">The surface.</param>
    /// <param name="text">The text.</param>
    /// <param name="font">The font.</param>
    /// <param name="color">The color.</param>
    /// <param name="bounds">The rectangle.</param>
    /// <param name="alignment">Where the text sits in the rectangle.</param>
    /// <param name="wrap">Whether lines wrap at the rectangle's width.</param>
    /// <param name="ellipsis">Whether text that does not fit ends with an ellipsis.</param>
    /// <param name="mnemonics">Whether ampersands mark access keys.</param>
    public static void DrawText(Graphics g, string text, Font font, Color color, Rectangle bounds, ContentAlignment alignment, bool wrap = false, bool ellipsis = false, bool mnemonics = true)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var shown = mnemonics ? Graphics.Prefixed(text, HotkeyPrefix.Hide) : text;
        var options = new TextOptions(Horizontal(alignment), Vertical(alignment), wrap, ellipsis, Clip: true);
        g.DrawText(shown, font, color, bounds, options);
    }

    /// <summary>The horizontal part of a content alignment.</summary>
    /// <param name="alignment">The alignment.</param>
    public static StringAlignment Horizontal(ContentAlignment alignment) => alignment switch
    {
        ContentAlignment.TopCenter or ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter => StringAlignment.Center,
        ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight => StringAlignment.Far,
        _ => StringAlignment.Near,
    };

    /// <summary>The vertical part of a content alignment.</summary>
    /// <param name="alignment">The alignment.</param>
    public static StringAlignment Vertical(ContentAlignment alignment) => alignment switch
    {
        ContentAlignment.MiddleLeft or ContentAlignment.MiddleCenter or ContentAlignment.MiddleRight => StringAlignment.Center,
        ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight => StringAlignment.Far,
        _ => StringAlignment.Near,
    };

    /// <summary>Where a box of a size sits in a rectangle at an alignment.</summary>
    /// <param name="bounds">The rectangle.</param>
    /// <param name="size">The box's size.</param>
    /// <param name="alignment">The alignment.</param>
    public static Rectangle Align(Rectangle bounds, Size size, ContentAlignment alignment)
    {
        var x = Horizontal(alignment) switch
        {
            StringAlignment.Center => bounds.X + ((bounds.Width - size.Width) / 2),
            StringAlignment.Far => bounds.Right - size.Width,
            _ => bounds.X,
        };
        var y = Vertical(alignment) switch
        {
            StringAlignment.Center => bounds.Y + ((bounds.Height - size.Height) / 2),
            StringAlignment.Far => bounds.Bottom - size.Height,
            _ => bounds.Y,
        };
        return new Rectangle(x, y, size.Width, size.Height);
    }

    /// <summary>Mixes two colors.</summary>
    /// <param name="from">The first color.</param>
    /// <param name="to">The second color.</param>
    /// <param name="amount">How much of the second, from 0 to 1.</param>
    public static Color Blend(Color from, Color to, float amount) => Color.FromArgb(
        from.A,
        (int)(from.R + ((to.R - from.R) * amount)),
        (int)(from.G + ((to.G - from.G) * amount)),
        (int)(from.B + ((to.B - from.B) * amount)));
}
