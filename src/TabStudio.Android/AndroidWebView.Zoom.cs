using TabStudio.App.Platform;
using AndroidWebKitView = Android.Webkit.WebView;

namespace TabStudio.Android;

/// <summary>
/// Page zoom for the WPF preview: the WebView honors the page's viewport meta tag (its width and scale, as Chrome on a
/// phone does) and pinches to zoom, without the on-screen zoom buttons.
/// </summary>
internal sealed partial class AndroidWebView : IZoomableWebView
{
    private bool _allowsPageZoom;

    /// <inheritdoc />
    public bool AllowsPageZoom
    {
        get => _allowsPageZoom;
        set
        {
            _allowsPageZoom = value;
            if (_view is { } view)
            {
                ApplyPageZoom(view);
            }
        }
    }

    /// <summary>Applies <see cref="AllowsPageZoom"/> to the view's settings.</summary>
    private void ApplyPageZoom(AndroidWebKitView view)
    {
        // Off, these are the WebView's defaults: every page is laid out at the view's width and fingers do not zoom.
        var settings = view.Settings;
        settings.UseWideViewPort = _allowsPageZoom;
        settings.LoadWithOverviewMode = _allowsPageZoom;
        settings.SetSupportZoom(true);
        settings.BuiltInZoomControls = _allowsPageZoom;
        settings.DisplayZoomControls = false;
    }
}
