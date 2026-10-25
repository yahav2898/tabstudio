using CommunityToolkit.Mvvm.ComponentModel;
using TabStudio.App.Platform;

namespace TabStudio.App.ViewModels;

/// <summary>The web preview's browser, which Swarm's web preview screen scripts, touches and photographs.</summary>
public sealed partial class WebPreviewViewModel
{
    /// <summary>The embedded browser the view created, or <see langword="null"/> where the platform has none.</summary>
    [ObservableProperty]
    public partial IWebView? WebView { get; set; }
}
