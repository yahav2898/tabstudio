namespace System.Drawing;

/// <summary>One-pixel pens of the Windows system colors, shared and immutable.</summary>
public static class SystemPens
{
    private static readonly Dictionary<KnownColor, Pen> Cache = [];

    /// <summary>A pen of the active border color.</summary>
    public static Pen ActiveBorder => Get(KnownColor.ActiveBorder);

    /// <summary>A pen of the active caption color.</summary>
    public static Pen ActiveCaption => Get(KnownColor.ActiveCaption);

    /// <summary>A pen of the active caption text color.</summary>
    public static Pen ActiveCaptionText => Get(KnownColor.ActiveCaptionText);

    /// <summary>A pen of the app workspace color.</summary>
    public static Pen AppWorkspace => Get(KnownColor.AppWorkspace);

    /// <summary>A pen of the button face color.</summary>
    public static Pen ButtonFace => Get(KnownColor.ButtonFace);

    /// <summary>A pen of the button highlight color.</summary>
    public static Pen ButtonHighlight => Get(KnownColor.ButtonHighlight);

    /// <summary>A pen of the button shadow color.</summary>
    public static Pen ButtonShadow => Get(KnownColor.ButtonShadow);

    /// <summary>A pen of the control color.</summary>
    public static Pen Control => Get(KnownColor.Control);

    /// <summary>A pen of the control dark color.</summary>
    public static Pen ControlDark => Get(KnownColor.ControlDark);

    /// <summary>A pen of the control dark dark color.</summary>
    public static Pen ControlDarkDark => Get(KnownColor.ControlDarkDark);

    /// <summary>A pen of the control light color.</summary>
    public static Pen ControlLight => Get(KnownColor.ControlLight);

    /// <summary>A pen of the control light light color.</summary>
    public static Pen ControlLightLight => Get(KnownColor.ControlLightLight);

    /// <summary>A pen of the control text color.</summary>
    public static Pen ControlText => Get(KnownColor.ControlText);

    /// <summary>A pen of the desktop color.</summary>
    public static Pen Desktop => Get(KnownColor.Desktop);

    /// <summary>A pen of the gradient active caption color.</summary>
    public static Pen GradientActiveCaption => Get(KnownColor.GradientActiveCaption);

    /// <summary>A pen of the gradient inactive caption color.</summary>
    public static Pen GradientInactiveCaption => Get(KnownColor.GradientInactiveCaption);

    /// <summary>A pen of the gray text color.</summary>
    public static Pen GrayText => Get(KnownColor.GrayText);

    /// <summary>A pen of the highlight color.</summary>
    public static Pen Highlight => Get(KnownColor.Highlight);

    /// <summary>A pen of the highlight text color.</summary>
    public static Pen HighlightText => Get(KnownColor.HighlightText);

    /// <summary>A pen of the hot track color.</summary>
    public static Pen HotTrack => Get(KnownColor.HotTrack);

    /// <summary>A pen of the inactive border color.</summary>
    public static Pen InactiveBorder => Get(KnownColor.InactiveBorder);

    /// <summary>A pen of the inactive caption color.</summary>
    public static Pen InactiveCaption => Get(KnownColor.InactiveCaption);

    /// <summary>A pen of the inactive caption text color.</summary>
    public static Pen InactiveCaptionText => Get(KnownColor.InactiveCaptionText);

    /// <summary>A pen of the info color.</summary>
    public static Pen Info => Get(KnownColor.Info);

    /// <summary>A pen of the info text color.</summary>
    public static Pen InfoText => Get(KnownColor.InfoText);

    /// <summary>A pen of the menu color.</summary>
    public static Pen Menu => Get(KnownColor.Menu);

    /// <summary>A pen of the menu bar color.</summary>
    public static Pen MenuBar => Get(KnownColor.MenuBar);

    /// <summary>A pen of the menu highlight color.</summary>
    public static Pen MenuHighlight => Get(KnownColor.MenuHighlight);

    /// <summary>A pen of the menu text color.</summary>
    public static Pen MenuText => Get(KnownColor.MenuText);

    /// <summary>A pen of the scroll bar color.</summary>
    public static Pen ScrollBar => Get(KnownColor.ScrollBar);

    /// <summary>A pen of the window color.</summary>
    public static Pen Window => Get(KnownColor.Window);

    /// <summary>A pen of the window frame color.</summary>
    public static Pen WindowFrame => Get(KnownColor.WindowFrame);

    /// <summary>A pen of the window text color.</summary>
    public static Pen WindowText => Get(KnownColor.WindowText);

    private static Pen Get(KnownColor color)
    {
        lock (Cache)
        {
            if (!Cache.TryGetValue(color, out var value))
            {
                value = new Pen(Color.FromKnownColor(color), 1f, immutable: true);
                Cache[color] = value;
            }

            return value;
        }
    }

    /// <summary>The pen of a system color.</summary>
    /// <param name="c">A system color, such as <see cref="SystemColors.ControlText"/>.</param>
    public static Pen FromSystemColor(Color c) => Get(c.IsKnownColor ? c.ToKnownColor() : KnownColor.ControlText);
}
