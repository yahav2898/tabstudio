using System.ComponentModel;
using TabStudio.App.Platform;
using TabStudio.App.Swarm;
using TabStudio.App.Swarm.Web;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Web apps on Swarm's screens: while a web app runs in the web preview, and the platform's browser runs script,
/// the preview is a screen, first among <see cref="Screens"/>, with its layout checked like an Avalonia app's.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    private WebAppScreen? _webScreen;
    private ProjectInfo? _webProject;

    /// <summary>The web preview as a screen, in <see cref="Screens"/> while a web app runs where the browser can run script.</summary>
    public WebAppScreen WebScreen => _webScreen ??= new WebAppScreen();

    /// <summary>A web app started or restarted in the web preview.</summary>
    private void OnWebAppStarted(ProjectInfo project)
    {
        _webProject = project;
        WebPreview.PropertyChanged -= OnWebPreviewChangedForSwarm;
        WebPreview.PropertyChanged += OnWebPreviewChangedForSwarm;
        UpdateWebScreen();
    }

    /// <summary>The web app stopped: its page is no longer a screen.</summary>
    private void OnWebAppStopped()
    {
        _webProject = null;
        WebPreview.PropertyChanged -= OnWebPreviewChangedForSwarm;
        UpdateWebScreen();
    }

    /// <summary>The web preview's view creates its browser when it first shows, which can be after the app started.</summary>
    private void OnWebPreviewChangedForSwarm(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WebPreviewViewModel.WebView))
        {
            UpdateWebScreen();
        }
    }

    private void UpdateWebScreen()
    {
        if (_webProject is not null && WebPreview.WebView is IScriptableWebView browser)
        {
            WebScreen.Show(browser);
            if (!_screens.Screens.Contains(WebScreen))
            {
                _screens.Screens.Insert(0, WebScreen);
            }
        }
        else if (_webScreen is { } screen)
        {
            screen.Hide();
            _screens.Remove(screen);
        }
    }

    private void DisposeWebScreen()
    {
        WebPreview.PropertyChanged -= OnWebPreviewChangedForSwarm;
        if (_webScreen is { } screen)
        {
            _screens.Remove(screen);
            screen.Dispose();
        }
    }

    /// <summary>Where an element of the web app's page is written, for a layout finding on the web preview.</summary>
    private WebSourceLocation? WebSourceOf(LayoutFinding finding) =>
        _webProject is { } project && finding.Screens.Any(s => s.ScreenId == WebAppScreen.WebPreviewId)
            ? WebSourceMap.Find(project.DirectoryPath, WebScreen.Page, finding.ElementPath)
            : null;
}
