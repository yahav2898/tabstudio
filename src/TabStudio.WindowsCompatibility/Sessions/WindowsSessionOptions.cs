namespace TabStudio.WindowsCompatibility.Sessions;

/// <summary>How WPF apps draw under Wine.</summary>
public enum WpfRendering
{
    /// <summary>WPF's own software renderer, which needs no 3D: the default, and the fastest without a GPU.</summary>
    Software,

    /// <summary>Direct3D 9 through Wine's WineD3D on Mesa's software OpenGL.</summary>
    Hardware,
}

/// <summary>How a Windows session looks: the desktop's size and scale, and how WPF draws.</summary>
public sealed record WindowsSessionOptions
{
    /// <summary>The desktop's width in pixels.</summary>
    public int Width { get; init; } = 1280;

    /// <summary>The desktop's height in pixels.</summary>
    public int Height { get; init; } = 800;

    /// <summary>
    /// The scale Windows apps draw at, as Windows' display scaling: 1 is 96 dots per inch, 1.5 is 144. WPF and
    /// DPI-aware Windows Forms apps draw sharply at it.
    /// </summary>
    public double Scale { get; init; } = 1;

    /// <summary>How WPF draws.</summary>
    public WpfRendering WpfRendering { get; init; } = WpfRendering.Software;

    /// <summary>The dots per inch Windows reports for <see cref="Scale"/>, in Windows' steps of a quarter.</summary>
    public int Dpi => (int)Math.Round(96 * Math.Clamp(Math.Round(Scale * 4) / 4, 1, 3));
}
