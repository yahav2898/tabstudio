namespace TabStudio.Core.Previews;

/// <summary>
/// How an app preview shows a desktop window that declares its own size, such as a WPF window with
/// <c>Width="1200" Height="800"</c>, in a pane that may be smaller or larger than it.
/// </summary>
public enum PreviewZoomMode
{
    /// <summary>The whole window, scaled down when it is larger than the pane and never scaled up. The default.</summary>
    Fit,

    /// <summary>The window at its declared size, one pixel for one pixel, with scroll bars when it is larger than the pane.</summary>
    ActualSize,

    /// <summary>The window sized to the pane, whatever size it declares, as a maximized window is.</summary>
    Fill,
}
