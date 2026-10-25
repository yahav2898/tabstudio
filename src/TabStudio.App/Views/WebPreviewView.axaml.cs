using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using TabStudio.App.Platform;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Views;

/// <summary>
/// The web preview. It creates the platform's embedded browser once, follows the view model's navigation, and
/// reports the pages the user opens back to the address bar. Enter in the address bar opens the address.
/// </summary>
public sealed partial class WebPreviewView : UserControl
{
    private WebPreviewViewModel? _preview;
    private IWebView? _webView;

    /// <summary>Creates the view.</summary>
    public WebPreviewView()
    {
        InitializeComponent();
        Address.KeyDown += OnAddressKeyDown;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_preview is not null)
        {
            _preview.NavigationRequested -= OnNavigationRequested;
            _preview.ActionRequested -= OnActionRequested;
            _preview.PropertyChanged -= OnPreviewPropertyChanged;
        }

        _preview = DataContext as WebPreviewViewModel;
        if (_preview is null)
        {
            return;
        }

        _preview.NavigationRequested += OnNavigationRequested;
        _preview.ActionRequested += OnActionRequested;
        _preview.PropertyChanged += OnPreviewPropertyChanged;
        if (_webView is null && _preview.WebViews.Create() is { } webView)
        {
            _webView = webView;
            _webView.Navigated += (_, page) => _preview?.OnNavigated(page);
            BrowserHost.Children.Add(webView.Control);
        }

        _preview.WebView = _webView;

        UpdatePlaceholder();
        if (_preview.IsRunning && _preview.CurrentPage is { } page)
        {
            _webView?.Navigate(page);
        }
    }

    private void OnNavigationRequested(object? sender, Uri page) => _webView?.Navigate(page);

    private void OnActionRequested(object? sender, WebViewAction action)
    {
        switch (action)
        {
            case WebViewAction.Reload:
                _webView?.Reload();
                break;
            case WebViewAction.Back:
                _webView?.GoBack();
                break;
            case WebViewAction.Forward:
                _webView?.GoForward();
                break;
        }
    }

    private void OnPreviewPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WebPreviewViewModel.IsRunning))
        {
            UpdatePlaceholder();
        }
    }

    /// <summary>The placeholder shows while there is no page to show: no embedded browser, or the app is not running.</summary>
    private void UpdatePlaceholder() => Placeholder.IsVisible = _webView is null || _preview?.IsRunning != true;

    private void OnAddressKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _preview?.NavigateCommand.CanExecute(null) == true)
        {
            _preview.NavigateCommand.Execute(null);
            e.Handled = true;
        }
    }
}
