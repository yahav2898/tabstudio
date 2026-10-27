using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.Core.Previews;
using TabStudio.Testing;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>The web preview's zoom for WPF apps: the mode in the page's address, switched in place or by a reload, and remembered per project.</summary>
public sealed class WebPreviewZoomTests : IDisposable
{
    private static readonly Uri Address = new("http://127.0.0.1:5000/");

    private readonly TempDirectory _temp = new();
    private readonly PreviewZoomStore _store;
    private readonly string _project;

    public WebPreviewZoomTests()
    {
        _store = new PreviewZoomStore(new AppPaths(_temp.Path));
        _project = Path.Combine(_temp.Path, "Calculator", "Calculator.csproj");
    }

    [Fact]
    public void A_wpf_app_opens_in_Fit_with_the_mode_in_its_address_and_page_zoom_on()
    {
        var browser = new RecordingBrowser();
        var preview = Create(browser);

        preview.ShowWpfApp("Calculator", Address, _project);

        Assert.True(preview.IsWpfApp);
        Assert.Equal(PreviewZoomMode.Fit, preview.ZoomMode);
        Assert.Equal("Fit", preview.SelectedZoom.ToString());
        Assert.Equal(new[] { "Fit", "100%", "Fill" }, preview.ZoomChoices.Select(c => c.Label));
        Assert.Equal(new Uri("http://127.0.0.1:5000/?tabstudio-view=fit"), Assert.Single(browser.Navigations));
        Assert.True(browser.AllowsPageZoom);
    }

    [Fact]
    public void Choosing_a_mode_saves_it_for_the_project_and_reloads_a_browser_that_runs_no_script()
    {
        var browser = new RecordingBrowser();
        var preview = Create(browser);
        preview.ShowWpfApp("Calculator", Address, _project);

        preview.ZoomMode = PreviewZoomMode.ActualSize;

        Assert.Equal(PreviewZoomMode.ActualSize, _store.Load(_project));
        Assert.Equal(new Uri("http://127.0.0.1:5000/?tabstudio-view=actual"), browser.Navigations[^1]);
        Assert.Equal("http://127.0.0.1:5000/?tabstudio-view=actual", preview.AddressText);

        // The next run of the project, even in a new session, opens in the mode chosen.
        var next = new RecordingBrowser();
        var reopened = Create(next);
        reopened.ShowWpfApp("Calculator", Address, _project);
        Assert.Equal(PreviewZoomMode.ActualSize, reopened.ZoomMode);
        Assert.Equal(new Uri("http://127.0.0.1:5000/?tabstudio-view=actual"), Assert.Single(next.Navigations));
    }

    [Fact]
    public void A_browser_that_runs_script_switches_the_mode_in_place_without_reloading_the_app()
    {
        var browser = new ScriptedBrowser();
        var preview = Create(browser);
        preview.ShowWpfApp("Calculator", Address, _project);

        preview.ZoomMode = PreviewZoomMode.Fill;

        Assert.Single(browser.Navigations);
        Assert.Equal("window.tabStudioWpf && window.tabStudioWpf.setView('fill')", Assert.Single(browser.Scripts));
        Assert.Equal(new Uri("http://127.0.0.1:5000/?tabstudio-view=fill"), preview.CurrentPage);
    }

    [Fact]
    public void A_restart_keeps_the_page_and_its_mode()
    {
        var browser = new RecordingBrowser();
        var preview = Create(browser);
        preview.ShowWpfApp("Calculator", Address, _project);
        preview.ZoomMode = PreviewZoomMode.Fill;
        preview.ShowStopped();

        preview.ShowWpfApp("Calculator", new Uri("http://127.0.0.1:5001/"), _project);

        Assert.Equal(new Uri("http://127.0.0.1:5001/?tabstudio-view=fill"), browser.Navigations[^1]);
    }

    [Fact]
    public void Other_web_apps_show_no_zoom_and_keep_the_browser_at_its_own_width()
    {
        var browser = new RecordingBrowser();
        var preview = Create(browser);
        preview.ShowWpfApp("Calculator", Address, _project);

        preview.Show("HelloWeb", new Uri("http://127.0.0.1:5002/"), "weatherforecast");
        preview.ZoomMode = PreviewZoomMode.Fill;

        Assert.False(preview.IsWpfApp);
        Assert.False(browser.AllowsPageZoom);
        Assert.Equal(new Uri("http://127.0.0.1:5002/weatherforecast"), browser.Navigations[^1]);
        Assert.Equal(PreviewZoomMode.Fit, _store.Load(_project));
    }

    [Theory]
    [InlineData("http://127.0.0.1:5000/", PreviewZoomMode.Fit, "http://127.0.0.1:5000/?tabstudio-view=fit")]
    [InlineData("http://127.0.0.1:5000/?tabstudio-view=fit", PreviewZoomMode.ActualSize, "http://127.0.0.1:5000/?tabstudio-view=actual")]
    [InlineData("http://127.0.0.1:5000/index.html?a=1&tabstudio-view=fill&b=2", PreviewZoomMode.Fit, "http://127.0.0.1:5000/index.html?a=1&b=2&tabstudio-view=fit")]
    public void The_mode_replaces_its_own_parameter_and_keeps_the_others(string page, PreviewZoomMode mode, string expected)
    {
        Assert.Equal(new Uri(expected), WebPreviewViewModel.WithView(new Uri(page), mode));
    }

    public void Dispose() => _temp.Dispose();

    private WebPreviewViewModel Create(IWebView browser)
    {
        var preview = new WebPreviewViewModel(new SingleBrowser(browser), new FakeBrowser(), _store);
        preview.NavigationRequested += (_, page) => browser.Navigate(page);
        preview.WebView = browser;
        return preview;
    }

    private sealed class SingleBrowser(IWebView browser) : IWebViewFactory
    {
        public IWebView? Create() => browser;
    }

    /// <summary>A browser that zooms pages and remembers what it was asked to open.</summary>
    private class RecordingBrowser : IZoomableWebView
    {
        public event EventHandler<Uri>? Navigated
        {
            add { }
            remove { }
        }

        public List<Uri> Navigations { get; } = [];

        public bool AllowsPageZoom { get; set; }

        public Control Control => throw new NotSupportedException("The tests do not lay the browser out.");

        public void Navigate(Uri uri) => Navigations.Add(uri);

        public void Reload()
        {
        }

        public void GoBack()
        {
        }

        public void GoForward()
        {
        }
    }

    /// <summary>A browser that also runs script, as Android's WebView does.</summary>
    private sealed class ScriptedBrowser : RecordingBrowser, IScriptableWebView
    {
        public List<string> Scripts { get; } = [];

        public Task<string?> EvaluateAsync(string script, CancellationToken cancellationToken)
        {
            Scripts.Add(script);
            return Task.FromResult<string?>("\"fill\"");
        }

        public Task<byte[]?> CaptureAsync(int maxPixels, CancellationToken cancellationToken) => Task.FromResult<byte[]?>(null);

        public Task<bool> TouchAsync(Point pixel, TimeSpan hold, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<bool> PressKeyAsync(Key key, KeyModifiers modifiers, CancellationToken cancellationToken) => Task.FromResult(false);
    }
}
