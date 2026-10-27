using CommunityToolkit.Mvvm.ComponentModel;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.Core.Previews;

namespace TabStudio.App.ViewModels;

/// <summary>
/// How the web preview shows a WPF app's window: Fit (the whole window, scaled down, the default), 100% (its declared
/// size, scrolling) or Fill (the pane's size), remembered for each project. TabStudio's page around the app applies the
/// mode itself with the browser's own page zoom (see <c>wpf.js</c> in TabStudio.Wpf, and
/// <see cref="PreviewLayoutCalculator"/>), so the browser maps every touch to the right control at every zoom and fingers
/// pinch to zoom further. The page reads the mode from its address (<see cref="ViewQueryName"/>); a browser that runs
/// script switches it in place, any other loads the page again.
/// </summary>
public sealed partial class WebPreviewViewModel
{
    /// <summary>The query parameter of the page's address that names its mode, such as <c>?tabstudio-view=fit</c>.</summary>
    public const string ViewQueryName = "tabstudio-view";

    private static readonly PreviewZoomChoice[] Choices =
    [
        new(PreviewZoomMode.Fit, "Fit"),
        new(PreviewZoomMode.ActualSize, "100%"),
        new(PreviewZoomMode.Fill, "Fill"),
    ];

    private readonly PreviewZoomStore? _zoomStore;
    private string? _zoomProject;
    private bool _loadingZoom;

    /// <summary>The choices of the zoom selector: Fit, 100% and Fill.</summary>
    public IReadOnlyList<PreviewZoomChoice> ZoomChoices => Choices;

    /// <summary>The zoom chosen for the WPF app shown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZoomMode))]
    public partial PreviewZoomChoice SelectedZoom { get; set; } = Choices[0];

    /// <summary>How the WPF app's window is shown.</summary>
    public PreviewZoomMode ZoomMode
    {
        get => SelectedZoom.Mode;
        set => SelectedZoom = Array.Find(Choices, c => c.Mode == value) ?? Choices[0];
    }

    /// <summary>The value of <see cref="ViewQueryName"/> for a mode: <c>fit</c>, <c>actual</c> or <c>fill</c>.</summary>
    /// <param name="mode">The mode.</param>
    public static string ViewToken(PreviewZoomMode mode) => mode switch
    {
        PreviewZoomMode.ActualSize => "actual",
        PreviewZoomMode.Fill => "fill",
        _ => "fit",
    };

    /// <summary>A page's address with its mode set, keeping the rest of its query.</summary>
    /// <param name="page">The page.</param>
    /// <param name="mode">The mode.</param>
    public static Uri WithView(Uri page, PreviewZoomMode mode)
    {
        ArgumentNullException.ThrowIfNull(page);
        var builder = new UriBuilder(page);
        var parameters = builder.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !p.StartsWith(ViewQueryName + "=", StringComparison.Ordinal) && p != ViewQueryName)
            .Append($"{ViewQueryName}={ViewToken(mode)}");
        builder.Query = string.Join('&', parameters);
        return builder.Uri;
    }

    /// <summary>
    /// Shows a WPF app that started, in TabStudio's page around its window, with the zoom last chosen for its project.
    /// </summary>
    /// <param name="appName">The project's name.</param>
    /// <param name="address">Where its page is served.</param>
    /// <param name="projectPath">The project file, which the zoom is remembered for.</param>
    public void ShowWpfApp(string appName, Uri address, string projectPath)
    {
        ArgumentNullException.ThrowIfNull(projectPath);
        _zoomProject = projectPath;
        _loadingZoom = true;
        try
        {
            ZoomMode = _zoomStore?.Load(projectPath) ?? PreviewZoomMode.Fit;
        }
        finally
        {
            _loadingZoom = false;
        }

        // A restart keeps the page the user was on; its address carries the mode it is to show.
        if (CurrentPage is { } current && AppName == appName)
        {
            CurrentPage = WithView(current, ZoomMode);
        }

        Show(appName, address, "?" + ViewQueryName + "=" + ViewToken(ZoomMode), wpfApp: true);
    }

    partial void OnSelectedZoomChanged(PreviewZoomChoice value)
    {
        if (_loadingZoom || !IsWpfApp)
        {
            return;
        }

        if (_zoomProject is { } project)
        {
            _zoomStore?.Save(project, value.Mode);
        }

        if (CurrentPage is not { } current)
        {
            return;
        }

        var page = WithView(current, value.Mode);
        if (!IsRunning)
        {
            OnNavigated(page);
        }
        else if (WebView is IScriptableWebView browser)
        {
            // The page switches in place and keeps the app's state; it puts the mode in its own address too, so the
            // browser's reload shows the same.
            OnNavigated(page);
            _ = browser.EvaluateAsync($"window.tabStudioWpf && window.tabStudioWpf.setView('{ViewToken(value.Mode)}')", CancellationToken.None);
        }
        else
        {
            GoTo(page);
        }
    }

    partial void OnIsWpfAppChanged(bool value) => ApplyPageZoom();

    partial void OnWebViewChanged(IWebView? value) => ApplyPageZoom();

    /// <summary>The browser zooms pages, as a phone's does, while it shows a WPF app's page.</summary>
    private void ApplyPageZoom()
    {
        if (WebView is IZoomableWebView zoomable && zoomable.AllowsPageZoom != IsWpfApp)
        {
            zoomable.AllowsPageZoom = IsWpfApp;
        }
    }
}
