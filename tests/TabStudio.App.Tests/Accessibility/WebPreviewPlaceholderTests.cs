using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TabStudio.App.Accessibility;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Accessibility;

/// <summary>
/// The web preview's placeholder names the app, and a long name (a repository's folder, such as
/// react-redux-realworld-example-app) wraps instead of running past its box at the largest text size.
/// </summary>
public sealed class WebPreviewPlaceholderTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [AvaloniaFact]
    public async Task A_long_app_name_wraps_in_the_placeholder()
    {
        var preview = new WebPreviewViewModel(new NoBrowser(), new FakeBrowser(), new PreviewZoomStore(new AppPaths(_temp.Path)));
        preview.Show("react-redux-realworld-example-app", new Uri("http://127.0.0.1:4100/"), null);
        var window = new Window { Width = 700, Height = 500, Content = new WebPreviewView { DataContext = preview } };
        window.Show();
        try
        {
            TypeScale.Apply(TextSizes.Largest);
            await UiTestHelpers.WaitUntilAsync(() => true);
            var found = AccessibilityAudit.In(window, dark: true, clipping: true).Where(f => f.Kind == "clipped").ToList();
            AccessibilityReport.AssertNone(found, "The web preview's placeholder");
        }
        finally
        {
            TypeScale.Apply(1);
            window.Close();
        }
    }

    public void Dispose() => _temp.Dispose();

    private sealed class NoBrowser : IWebViewFactory
    {
        public IWebView? Create() => null;
    }
}
