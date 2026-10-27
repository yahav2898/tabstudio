using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using TabStudio.App.Platform;
using TabStudio.Testing;

namespace TabStudio.App.Tests;

/// <summary>Creates <see cref="ChromeWebView"/>s over one headless Chrome.</summary>
internal sealed class ChromeWebViewFactory(HeadlessChrome chrome) : IWebViewFactory
{
    /// <summary>The web view the preview created, once it has.</summary>
    public ChromeWebView? WebView { get; private set; }

    /// <inheritdoc />
    public IWebView? Create() => WebView = new ChromeWebView(chrome);
}

/// <summary>
/// The web preview's browser on the headless desktop, for tests and screenshots: headless Chrome loads each page, and
/// the preview shows a picture of it at the preview's size. The test drives the page through <see cref="Chrome"/>,
/// such as clicking a button, then calls <see cref="RefreshAsync"/>. With page zoom on, as the web preview turns it on
/// for WPF apps, Chrome is a mobile browser with touch input, which honors the page's viewport meta tag and zoom as
/// Android's WebView does.
/// </summary>
internal sealed class ChromeWebView : IZoomableWebView
{
    private readonly Image _image = new()
    {
        Stretch = Stretch.None,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
    };

    private readonly Border _host;
    private Uri? _page;
    private Task _rendering = Task.CompletedTask;

    public ChromeWebView(HeadlessChrome chrome)
    {
        Chrome = chrome;
        _host = new Border { Background = Brushes.White, Child = _image, ClipToBounds = true };
    }

    /// <inheritdoc />
    public event EventHandler<Uri>? Navigated;

    /// <summary>The browser that loads the pages.</summary>
    public HeadlessChrome Chrome { get; }

    /// <summary>The page shown.</summary>
    public Uri? Page => _page;

    /// <summary>Finishes when the last page or refresh is drawn in the preview.</summary>
    public Task Rendering => _rendering;

    /// <inheritdoc />
    public Control Control => _host;

    /// <inheritdoc />
    public bool AllowsPageZoom { get; set; }

    /// <inheritdoc />
    public void Navigate(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        _page = uri;
        _rendering = RenderAsync(_rendering, navigate: true);
    }

    /// <inheritdoc />
    public void Reload() => _rendering = RenderAsync(_rendering, navigate: true);

    /// <inheritdoc />
    public void GoBack() => _rendering = ScriptThenRenderAsync(_rendering, "history.back()");

    /// <inheritdoc />
    public void GoForward() => _rendering = ScriptThenRenderAsync(_rendering, "history.forward()");

    /// <summary>Draws what the page shows now, such as after the test clicked in it.</summary>
    public Task RefreshAsync() => _rendering = RenderAsync(_rendering, navigate: false);

    private async Task ScriptThenRenderAsync(Task previous, string script)
    {
        await previous;
        await Chrome.EvaluateAsync(script);
        await RenderAsync(Task.CompletedTask, navigate: false);
    }

    private async Task RenderAsync(Task previous, bool navigate)
    {
        await previous;
        if (_page is not { } page)
        {
            return;
        }

        var size = _host.Bounds.Size;
        await Chrome.ResizeAsync(size.Width >= 100 ? (int)size.Width : 480, size.Height >= 100 ? (int)size.Height : 640, AllowsPageZoom);
        if (navigate)
        {
            await Chrome.NavigateAsync(page);
        }

        // Let fonts, styles and a Blazor render settle before the picture.
        await Task.Delay(500);
        var png = await Chrome.ScreenshotAsync();
        var current = new Uri((await Chrome.EvaluateAsync("location.href"))?.GetValue<string>() ?? page.ToString());
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            using var stream = new MemoryStream(png);
            _image.Source = new Bitmap(stream);
            Navigated?.Invoke(this, current);
        });
    }
}
