using Android.Content;
using Android.Graphics;
using Android.Webkit;
using Avalonia.Android;
using Avalonia.Controls;
using Avalonia.Platform;
using TabStudio.App.Platform;
using AndroidWebKitView = Android.Webkit.WebView;

namespace TabStudio.Android;

/// <summary>Creates web previews on Android's system WebView.</summary>
/// <param name="context">The activity the web views belong to.</param>
internal sealed class AndroidWebViewFactory(Func<Context> context) : IWebViewFactory
{
    /// <inheritdoc />
    public IWebView? Create() => new AndroidWebView(context);
}

/// <summary>
/// Android's system WebView inside the Avalonia layout, through a native control host. Links stay inside the
/// preview, and JavaScript and local storage are on, as in a browser.
/// </summary>
internal sealed class AndroidWebView(Func<Context> context) : NativeControlHost, IWebView, IDisposable
{
    private AndroidWebKitView? _view;
    private Uri? _page;

    /// <inheritdoc />
    public event EventHandler<Uri>? Navigated;

    /// <inheritdoc />
    public Control Control => this;

    /// <inheritdoc />
    public void Navigate(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        _page = uri;
        _view?.LoadUrl(uri.AbsoluteUri);
    }

    /// <inheritdoc />
    public void Reload() => _view?.Reload();

    /// <inheritdoc />
    public void GoBack()
    {
        if (_view?.CanGoBack() == true)
        {
            _view.GoBack();
        }
    }

    /// <inheritdoc />
    public void GoForward()
    {
        if (_view?.CanGoForward() == true)
        {
            _view.GoForward();
        }
    }

    /// <inheritdoc />
    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        _view = new AndroidWebKitView(context());
        _view.Settings.JavaScriptEnabled = true;
        _view.Settings.DomStorageEnabled = true;
        _view.SetWebViewClient(new Client(this));
        if (_page is { } page)
        {
            _view.LoadUrl(page.AbsoluteUri);
        }

        return new AndroidViewControlHandle(_view);
    }

    /// <summary>Destroys the Android web view, if one was created and is still alive.</summary>
    public void Dispose()
    {
        // When the host takes the view out of the layout for good, Avalonia's attachment removes it and releases its
        // managed peer before DestroyNativeControlCore runs; calling into a released peer throws, which on the UI thread
        // ended the app (Close Solution after a web app had been previewed). Android collects the removed view then.
        if (_view is { Handle: var handle } view && handle != IntPtr.Zero)
        {
            view.Destroy();
            view.Dispose();
        }

        _view = null;
    }

    /// <inheritdoc />
    protected override void DestroyNativeControlCore(IPlatformHandle control) => Dispose();

    private void OnPageStarted(string? url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var page))
        {
            _page = page;
            Navigated?.Invoke(this, page);
        }
    }

    /// <summary>Keeps links inside the preview and reports each page.</summary>
    private sealed class Client(AndroidWebView owner) : WebViewClient
    {
        public override void OnPageStarted(AndroidWebKitView? view, string? url, Bitmap? favicon)
        {
            base.OnPageStarted(view, url, favicon);
            owner.OnPageStarted(url);
        }

        public override bool ShouldOverrideUrlLoading(AndroidWebKitView? view, IWebResourceRequest? request) => false;
    }
}
