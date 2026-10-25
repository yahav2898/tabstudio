namespace System.Drawing;

/// <summary>Brushes of the Windows system colors, shared and immutable.</summary>
public static class SystemBrushes
{
    private static readonly Dictionary<KnownColor, Brush> Cache = [];

    /// <summary>A brush of the active border color.</summary>
    public static Brush ActiveBorder => Get(KnownColor.ActiveBorder);

    /// <summary>A brush of the active caption color.</summary>
    public static Brush ActiveCaption => Get(KnownColor.ActiveCaption);

    /// <summary>A brush of the active caption text color.</summary>
    public static Brush ActiveCaptionText => Get(KnownColor.ActiveCaptionText);

    /// <summary>A brush of the app workspace color.</summary>
    public static Brush AppWorkspace => Get(KnownColor.AppWorkspace);

    /// <summary>A brush of the button face color.</summary>
    public static Brush ButtonFace => Get(KnownColor.ButtonFace);

    /// <summary>A brush of the button highlight color.</summary>
    public static Brush ButtonHighlight => Get(KnownColor.ButtonHighlight);

    /// <summary>A brush of the button shadow color.</summary>
    public static Brush ButtonShadow => Get(KnownColor.ButtonShadow);

    /// <summary>A brush of the control color.</summary>
    public static Brush Control => Get(KnownColor.Control);

    /// <summary>A brush of the control dark color.</summary>
    public static Brush ControlDark => Get(KnownColor.ControlDark);

    /// <summary>A brush of the control dark dark color.</summary>
    public static Brush ControlDarkDark => Get(KnownColor.ControlDarkDark);

    /// <summary>A brush of the control light color.</summary>
    public static Brush ControlLight => Get(KnownColor.ControlLight);

    /// <summary>A brush of the control light light color.</summary>
    public static Brush ControlLightLight => Get(KnownColor.ControlLightLight);

    /// <summary>A brush of the control text color.</summary>
    public static Brush ControlText => Get(KnownColor.ControlText);

    /// <summary>A brush of the desktop color.</summary>
    public static Brush Desktop => Get(KnownColor.Desktop);

    /// <summary>A brush of the gradient active caption color.</summary>
    public static Brush GradientActiveCaption => Get(KnownColor.GradientActiveCaption);

    /// <summary>A brush of the gradient inactive caption color.</summary>
    public static Brush GradientInactiveCaption => Get(KnownColor.GradientInactiveCaption);

    /// <summary>A brush of the gray text color.</summary>
    public static Brush GrayText => Get(KnownColor.GrayText);

    /// <summary>A brush of the highlight color.</summary>
    public static Brush Highlight => Get(KnownColor.Highlight);

    /// <summary>A brush of the highlight text color.</summary>
    public static Brush HighlightText => Get(KnownColor.HighlightText);

    /// <summary>A brush of the hot track color.</summary>
    public static Brush HotTrack => Get(KnownColor.HotTrack);

    /// <summary>A brush of the inactive border color.</summary>
    public static Brush InactiveBorder => Get(KnownColor.InactiveBorder);

    /// <summary>A brush of the inactive caption color.</summary>
    public static Brush InactiveCaption => Get(KnownColor.InactiveCaption);

    /// <summary>A brush of the inactive caption text color.</summary>
    public static Brush InactiveCaptionText => Get(KnownColor.InactiveCaptionText);

    /// <summary>A brush of the info color.</summary>
    public static Brush Info => Get(KnownColor.Info);

    /// <summary>A brush of the info text color.</summary>
    public static Brush InfoText => Get(KnownColor.InfoText);

    /// <summary>A brush of the menu color.</summary>
    public static Brush Menu => Get(KnownColor.Menu);

    /// <summary>A brush of the menu bar color.</summary>
    public static Brush MenuBar => Get(KnownColor.MenuBar);

    /// <summary>A brush of the menu highlight color.</summary>
    public static Brush MenuHighlight => Get(KnownColor.MenuHighlight);

    /// <summary>A brush of the menu text color.</summary>
    public static Brush MenuText => Get(KnownColor.MenuText);

    /// <summary>A brush of the scroll bar color.</summary>
    public static Brush ScrollBar => Get(KnownColor.ScrollBar);

    /// <summary>A brush of the window color.</summary>
    public static Brush Window => Get(KnownColor.Window);

    /// <summary>A brush of the window frame color.</summary>
    public static Brush WindowFrame => Get(KnownColor.WindowFrame);

    /// <summary>A brush of the window text color.</summary>
    public static Brush WindowText => Get(KnownColor.WindowText);

    private static Brush Get(KnownColor color)
    {
        lock (Cache)
        {
            if (!Cache.TryGetValue(color, out var value))
            {
                value = new SolidBrush(Color.FromKnownColor(color), immutable: true);
                Cache[color] = value;
            }

            return value;
        }
    }

    /// <summary>The brush of a system color.</summary>
    /// <param name="c">A system color, such as <see cref="SystemColors.Control"/>.</param>
    public static Brush FromSystemColor(Color c) => Get(c.IsKnownColor ? c.ToKnownColor() : KnownColor.Control);
}
