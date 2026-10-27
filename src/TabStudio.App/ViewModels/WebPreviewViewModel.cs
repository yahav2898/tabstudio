using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Platform;
using TabStudio.App.Services;

namespace TabStudio.App.ViewModels;

/// <summary>
/// The web preview in the side bar: the running web app's page in an embedded browser, with an address bar,
/// back, forward and reload, and a button that opens the page in the system browser.
/// </summary>
public sealed partial class WebPreviewViewModel : ObservableObject
{
    private readonly IStoragePicker _browser;

    /// <summary>Creates the preview.</summary>
    /// <param name="webViews">Creates the platform's embedded browser, if it has one.</param>
    /// <param name="browser">Opens pages in the system browser.</param>
    /// <param name="zoomStore">Remembers each WPF project's zoom mode; without it, every WPF app starts in Fit.</param>
    public WebPreviewViewModel(IWebViewFactory webViews, IStoragePicker browser, PreviewZoomStore? zoomStore = null)
    {
        ArgumentNullException.ThrowIfNull(webViews);
        ArgumentNullException.ThrowIfNull(browser);
        WebViews = webViews;
        _browser = browser;
        _zoomStore = zoomStore;
    }

    /// <summary>Raised when the embedded browser should open a page.</summary>
    public event EventHandler<Uri>? NavigationRequested;

    /// <summary>Raised when the embedded browser should reload, go back or go forward.</summary>
    public event EventHandler<WebViewAction>? ActionRequested;

    /// <summary>Creates the embedded browser; the view asks it once.</summary>
    public IWebViewFactory WebViews { get; }

    /// <summary>The web app being previewed, such as <c>HelloWeb</c>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasApp), nameof(Title))]
    public partial string? AppName { get; private set; }

    /// <summary>Where the app listens, such as <c>http://127.0.0.1:52713/</c>.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenInBrowserCommand), nameof(ReloadCommand))]
    public partial Uri? AppAddress { get; private set; }

    /// <summary>The page shown, as the address bar shows it.</summary>
    [ObservableProperty]
    public partial string AddressText { get; set; } = string.Empty;

    /// <summary>Whether the app is running, so its pages can load.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyCanExecuteChangedFor(nameof(OpenInBrowserCommand), nameof(ReloadCommand))]
    public partial bool IsRunning { get; private set; }

    /// <summary>Whether a web app has run in this workspace, which shows the preview's tab.</summary>
    public bool HasApp => AppName is not null;

    /// <summary>The preview's header, such as "HelloWeb".</summary>
    public string Title => AppName ?? "Web Preview";

    /// <summary>What the preview shows when the page cannot load, or where there is no embedded browser.</summary>
    public string StatusText => IsRunning
        ? $"{AppName} is running at {AppAddress?.GetLeftPart(UriPartial.Authority)}."
        : $"{AppName} is not running. Press F5 to start it.";

    /// <summary>The page currently shown.</summary>
    public Uri? CurrentPage { get; private set; }

    /// <summary>Whether the page is TabStudio's page around a WPF app, which shows the zoom choice.</summary>
    [ObservableProperty]
    public partial bool IsWpfApp { get; private set; }

    /// <summary>Shows a web app that started, at its first page.</summary>
    /// <param name="appName">The project's name.</param>
    /// <param name="address">Where it listens.</param>
    /// <param name="launchUrl">The first page, relative to the address, from the launch profile.</param>
    public void Show(string appName, Uri address, string? launchUrl) => Show(appName, address, launchUrl, wpfApp: false);

    /// <summary>Marks the app as stopped; its last page stays in the address bar.</summary>
    public void ShowStopped() => IsRunning = false;

    private void Show(string appName, Uri address, string? launchUrl, bool wpfApp)
    {
        ArgumentNullException.ThrowIfNull(appName);
        ArgumentNullException.ThrowIfNull(address);

        // A restarted app keeps the page the user was on, at the new address.
        var page = AppName == appName && CurrentPage is { } current && AppAddress is { } previous && previous.IsBaseOf(current)
            ? new Uri(address, previous.MakeRelativeUri(current))
            : new Uri(address, launchUrl ?? string.Empty);
        AppName = appName;
        AppAddress = address;
        IsWpfApp = wpfApp;
        IsRunning = true;
        GoTo(page);
    }

    /// <summary>The embedded browser reports the page it shows.</summary>
    /// <param name="page">The page.</param>
    public void OnNavigated(Uri page)
    {
        ArgumentNullException.ThrowIfNull(page);
        CurrentPage = page;
        AddressText = page.ToString();
    }

    [RelayCommand]
    private void Navigate()
    {
        if (AppAddress is not { } address || string.IsNullOrWhiteSpace(AddressText))
        {
            return;
        }

        // A path typed into the address bar is relative to the app.
        var text = AddressText.Trim();
        var page = Uri.TryCreate(text, UriKind.Absolute, out var absolute) && absolute.Scheme is "http" or "https"
            ? absolute
            : new Uri(address, text.TrimStart('/'));
        GoTo(page);
    }

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Reload() => ActionRequested?.Invoke(this, WebViewAction.Reload);

    [RelayCommand]
    private void GoBack() => ActionRequested?.Invoke(this, WebViewAction.Back);

    [RelayCommand]
    private void GoForward() => ActionRequested?.Invoke(this, WebViewAction.Forward);

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private Task OpenInBrowserAsync() => _browser.OpenUriAsync(CurrentPage ?? AppAddress!);

    private void GoTo(Uri page)
    {
        OnNavigated(page);
        NavigationRequested?.Invoke(this, page);
    }
}
