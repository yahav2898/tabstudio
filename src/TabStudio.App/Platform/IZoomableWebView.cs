namespace TabStudio.App.Platform;

/// <summary>
/// An embedded browser that can let a page set its own zoom, as a phone's browser does: the page's viewport meta tag
/// sets the width it is laid out at and the scale it is drawn at, and fingers pinch to zoom and pan. The web preview
/// turns it on for WPF apps, whose page scales a large window down this way, so the browser itself maps every touch
/// to the page and the app's own layout and input never see a scale. Android's system WebView is one.
/// </summary>
public interface IZoomableWebView : IWebView
{
    /// <summary>
    /// Whether pages may set their zoom with the viewport meta tag and be pinch-zoomed. Off by default, which lays every
    /// page out at the browser's width, as a page that declares <c>width=device-width</c> asks.
    /// </summary>
    bool AllowsPageZoom { get; set; }
}
