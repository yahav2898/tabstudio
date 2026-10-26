using CommunityToolkit.Mvvm.Input;
using TabStudio.Core.Compatibility;
using TabStudio.Core.Detection;
using TabStudio.Core.Workspaces;
using TabStudio.Web.Serving;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Static web sites: a folder whose startup item is an <c>index.html</c>, with no .NET project and no Node.js
/// package that runs or serves it. F5 serves the folder from the tablet and shows it in the web preview beside the
/// code; saving a file reloads the page, and a saved style sheet is swapped in without a reload.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    private StaticSiteServer? _siteServer;

    /// <summary>The workspace's static web site, or <see langword="null"/> when it has none.</summary>
    public ProjectInfo? StaticSite { get; private set; }

    /// <summary>The address the static web site is served at while it runs, or <see langword="null"/>.</summary>
    public Uri? StaticSiteAddress => _siteServer?.Address;

    private bool IsStaticSite(ProjectInfo project) =>
        project.Kind == ProjectKind.StaticWebSite && StaticSite is { } site && string.Equals(project.FilePath, site.FilePath, StringComparison.Ordinal);

    /// <summary>
    /// Adds the workspace's static web site to the startup projects and the Compatibility Doctor, when its startup
    /// item is an <c>index.html</c>. It is the startup project unless another one was chosen or runs something.
    /// </summary>
    /// <param name="workspace">The workspace.</param>
    /// <param name="preferred">The startup project remembered for it.</param>
    private void LoadStaticSite(Workspace workspace, string? preferred)
    {
        StaticSite = null;
        var servedByNode = _toolchainProjects.Any(t => t.Toolchain.Id == "node" && t.Project.MarkerPath is { } marker && StaticSiteDetector.IsRunByNode(marker));
        if (workspace.Projects.Count > 0 || servedByNode || StaticSiteDetector.FindIndex(workspace.RootDirectory) is not { } index)
        {
            return;
        }

        var site = new ProjectInfo
        {
            Name = Path.GetFileName(workspace.RootDirectory.TrimEnd(Path.DirectorySeparatorChar)),
            FilePath = index,
            Kind = ProjectKind.StaticWebSite,
            OutputType = "Exe",
        };
        StaticSite = site;
        StartupProjects.Add(new StartupProjectOption(site, new RelayCommand(() => SetStartupProject(site))));
        Compatibility.Add(new CompatibilityRowViewModel(CompatibilityDoctor.Diagnose(site)));

        // A Node.js package that runs nothing, such as one for a formatter, leaves the site the startup item.
        var chosen = preferred is not null && StartupProject?.FilePath == preferred;
        if (string.Equals(preferred, site.FilePath, StringComparison.Ordinal) || StartupProject is null || (!chosen && StartupProject.Kind == ProjectKind.Node))
        {
            StartupProject = site;
        }
    }

    /// <summary>
    /// F5 or Ctrl+F5 on a static web site: serves its folder on a local port until Stop, and shows it in the web
    /// preview, with the requests in the Output panel.
    /// </summary>
    /// <param name="site">The site.</param>
    private async Task ServeStaticSiteAsync(ProjectInfo site)
    {
        await SaveAllCoreAsync();
        var root = Path.GetDirectoryName(site.FilePath)!;
        Output.Clear();
        StaticSiteServer server;
        try
        {
            server = StaticSiteServer.Start(root, _webPort);
        }
        catch (IOException ex)
        {
            Output.WriteLine(ex.Message, OutputKind.Failure);
            State = IdeState.Failed;
            StatusText = $"{site.Name} could not start";
            return;
        }

        _siteServer = server;
        _webPort = server.Address.Port;
        server.RequestServed += OnSiteRequestServed;
        var run = new CancellationTokenSource();
        _run = run;
        State = IdeState.Running;
        Output.WriteLine($"Serving {site.Name} from {root} at {server.Address}. Saving a file reloads the page.", OutputKind.Info);
        WebPreview.Show(site.Name, server.Address, launchUrl: null);
        SelectSideBarPanel(SideBarPanel.WebPreview);
        StatusText = $"Running {site.Name} at {server.Address.GetLeftPart(UriPartial.Authority)}";
        try
        {
            await Task.Delay(Timeout.Infinite, run.Token);
        }
        catch (OperationCanceledException)
        {
            // Stop, Shift+F5, or the workspace closing.
        }
        finally
        {
            server.RequestServed -= OnSiteRequestServed;
            _siteServer = null;
            await server.DisposeAsync();
            WebPreview.ShowStopped();
            run.Dispose();
            _run = null;
        }

        Output.WriteLine($"{site.Name} stopped.", OutputKind.Info);
        State = IdeState.Ready;
        StatusText = "Ready";
    }

    private void OnSiteRequestServed(object? sender, ServedRequest request) => _ui.Post(() =>
    {
        if (!_disposed)
        {
            Output.WriteLine($"{request.Method} /{request.Path} {request.StatusCode}", request.StatusCode >= 400 ? OutputKind.Error : OutputKind.Program);
        }
    });

    /// <summary>A saved file of the running site reloads its pages, or swaps a saved style sheet in place.</summary>
    /// <param name="document">The saved document.</param>
    private void OnDocumentSavedForStaticSite(DocumentViewModel document)
    {
        if (_siteServer is { } server && server.NotifyChanged(document.FilePath))
        {
            var how = Path.GetExtension(document.FilePath).Equals(".css", StringComparison.OrdinalIgnoreCase) ? "applying the style sheet" : "reloading the page";
            Output.WriteLine($"Saved {server.RouteOf(document.FilePath)}: {how}", OutputKind.Info);
        }
    }

    private void DisposeStaticSite()
    {
        if (_siteServer is { } server)
        {
            _siteServer = null;
            _ = server.DisposeAsync().AsTask();
        }
    }
}
