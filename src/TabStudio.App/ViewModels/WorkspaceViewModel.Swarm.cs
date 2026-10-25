using System.Collections.Specialized;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Previews;
using TabStudio.App.Swarm;
using TabStudio.App.Swarm.LayoutChecks;
using TabStudio.App.Swarm.Web;
using TabStudio.App.ViewModels.Agent;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Swarm's screens in the workspace: the app preview as a screen while an Avalonia app runs, the Device Matrix with
/// its virtual devices, the layout checks of every screen in the Error List, and "Fix with Agent" for them.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    /// <summary>The Device Matrix tab's name; it names no file.</summary>
    private const string DeviceMatrixTabName = "Device Matrix";

    private AppScreenRegistry _screens = new();
    private SwarmSettingsStore? _swarmSettings;
    private LayoutInspector _layoutInspector = new();
    private ScreenLayoutChecks? _layoutChecks;
    private PreviewAppScreen? _previewScreen;
    private AvaloniaAppHost? _avaloniaApp;
    private ProjectInfo? _avaloniaProject;
    private readonly Dictionary<string, XamlSourceLocation?> _xamlOfPaths = new(StringComparer.Ordinal);

    /// <summary>Every screen the app being built runs on: the preview, virtual devices and the swarm's phones.</summary>
    public AppScreenRegistry Screens => _screens;

    /// <summary>The app preview as a screen, in <see cref="Screens"/> while an Avalonia app runs.</summary>
    public PreviewAppScreen PreviewScreen => _previewScreen!;

    /// <summary>The layout checks of every screen.</summary>
    public ScreenLayoutChecks LayoutChecks => _layoutChecks!;

    /// <summary>The Device Matrix, while its tab is open.</summary>
    [ObservableProperty]
    public partial DeviceMatrixViewModel? DeviceMatrix { get; private set; }

    /// <summary>Shows the Device Matrix, opening its tab if it is not open, as View > Device Matrix does.</summary>
    [RelayCommand]
    private void ShowDeviceMatrix() => OpenDeviceMatrix(activate: true);

    private void InitializeSwarm(AppScreenRegistry? screens)
    {
        _screens = screens ?? new AppScreenRegistry();
        _swarmSettings = new SwarmSettingsStore(_paths);
        _layoutInspector = new LayoutInspector(_swarmSettings.Load().Rules);
        _layoutChecks = new ScreenLayoutChecks(_screens, () => _layoutInspector.Settings);
        _layoutChecks.FindingsChanged += OnLayoutFindingsChanged;
        _previewScreen = new PreviewAppScreen(AppPreview, _layoutInspector);
        AppPreview.OpenDeviceMatrixCommand = ShowDeviceMatrixCommand;
        ErrorList.LayoutFindingOpened += OnLayoutFindingOpened;
        Documents.CollectionChanged += OnDocumentsChangedForSwarm;
    }

    private void DisposeSwarm()
    {
        Documents.CollectionChanged -= OnDocumentsChangedForSwarm;
        ErrorList.LayoutFindingOpened -= OnLayoutFindingOpened;
        DeviceMatrix?.Dispose();
        DeviceMatrix = null;
        if (_layoutChecks is { } checks)
        {
            checks.FindingsChanged -= OnLayoutFindingsChanged;
            checks.Dispose();
        }

        if (_previewScreen is { } preview)
        {
            _screens.Remove(preview);
            preview.Dispose();
        }

        DisposeWebScreen();
        ErrorList.SetLayoutFindings([]);
    }

    /// <summary>An Avalonia app started or restarted in the preview: it shows on every screen TabStudio draws.</summary>
    private void OnAvaloniaAppStarted(ProjectInfo project, AvaloniaAppHost app)
    {
        _avaloniaApp = app;
        _avaloniaProject = project;
        _xamlOfPaths.Clear();
        if (app.MainView is { } mainView && _previewScreen is { } preview)
        {
            preview.Show(app.View, mainView);
            if (!_screens.Screens.Contains(preview))
            {
                // The tablet's own preview comes first in every list of screens.
                _screens.Screens.Insert(0, preview);
            }
        }

        DeviceMatrix?.OnAppStarted(app);
    }

    /// <summary>The app is stopping, or restarting with saved changes: the screens let go of it.</summary>
    private void OnAvaloniaAppStopping()
    {
        DeviceMatrix?.OnAppStopping(restarting: _restartLiveApp);
        _previewScreen?.Hide();
        _avaloniaApp = null;
    }

    /// <summary>A live app's run ended: the preview is no longer a screen the app runs on.</summary>
    private void OnLiveRunEnded()
    {
        if (_previewScreen is { } preview)
        {
            _screens.Remove(preview);
        }

        DeviceMatrix?.OnAppStopping(restarting: false);
    }

    /// <summary>Opens the Device Matrix's tab, or goes to it.</summary>
    /// <param name="activate">Whether the tab comes to the front, or opens beside the current one.</param>
    private void OpenDeviceMatrix(bool activate)
    {
        if (Documents.FirstOrDefault(d => d.IsDeviceMatrix) is { } open)
        {
            if (activate)
            {
                ActiveDocument = open;
            }

            return;
        }

        var matrix = new DeviceMatrixViewModel(_screens, _swarmSettings!, _layoutInspector, _layoutChecks!, () => _avaloniaApp, ApplyLayoutRules);
        if (_avaloniaApp is { } app)
        {
            matrix.OnAppStarted(app);
        }

        DeviceMatrix = matrix;
        var document = DocumentViewModel.ForDeviceMatrix(matrix, Path.Combine(Workspace?.RootDirectory ?? _paths.Projects, DeviceMatrixTabName), _profile);
        Documents.Insert(ActiveDocument is null ? Documents.Count : Documents.IndexOf(ActiveDocument) + 1, document);
        if (activate || ActiveDocument is null)
        {
            ActiveDocument = document;
        }
    }

    /// <summary>Closing the Device Matrix's tab stops its virtual devices.</summary>
    private void OnDocumentsChangedForSwarm(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (DeviceMatrix is { } matrix && !Documents.Any(d => ReferenceEquals(d.DeviceMatrix, matrix)))
        {
            DeviceMatrix = null;
            matrix.Dispose();
        }
    }

    /// <summary>The user turned layout checks on or off: every screen is checked again with them.</summary>
    private void ApplyLayoutRules(LayoutCheckSettings rules)
    {
        _layoutInspector.Settings = rules;
        _layoutChecks?.CheckAll();
    }

    /// <summary>The layout findings, as Error List rows with their XAML elements.</summary>
    private void OnLayoutFindingsChanged(object? sender, EventArgs e)
    {
        var rows = new List<LayoutFindingRow>();
        foreach (var finding in _layoutChecks!.Findings)
        {
            var xaml = XamlOf(finding.ElementPath);
            var file = xaml is null ? null : FileOf(xaml);
            var location = xaml is not null && file is not null ? new DiagnosticLocation(file, xaml.Start, xaml.Length, xaml.Line, xaml.Column) : null;
            var web = xaml is null ? WebSourceOf(finding) : null;
            if (web is not null)
            {
                location = new DiagnosticLocation(web.FilePath, web.Start, web.Length, web.Line, web.Column);
            }

            var severity = finding.Severity == LayoutIssueSeverity.Error ? DiagnosticSeverityLevel.Error : DiagnosticSeverityLevel.Warning;
            var project = (xaml is null ? null : ProjectOfAssembly(xaml.AssemblyName))?.Name ?? (IsOnWebPreview(finding) ? _webProject?.Name : null) ?? _avaloniaProject?.Name;
            var diagnostic = new BuildDiagnostic(finding.Code, severity, finding.Message, location, project) { HelpLink = LayoutRules.HelpLink(finding.Kind) };
            rows.Add(new LayoutFindingRow(finding, diagnostic, xaml) { WebSource = web });
        }

        ErrorList.SetLayoutFindings(rows);
    }

    /// <summary>
    /// Opening a layout row outlines its element in the Device Matrix's tiles. The editor opens the XAML; when the
    /// element has no XAML, the matrix comes to the front instead.
    /// </summary>
    private void OnLayoutFindingOpened(object? sender, LayoutFindingRow row)
    {
        OpenDeviceMatrix(activate: row.Diagnostic.Location is null);
        DeviceMatrix?.ShowFinding(row.Finding);
    }

    /// <summary>Where an element, named by its path, is written in the app's XAML, from any screen TabStudio draws.</summary>
    private XamlSourceLocation? XamlOf(string elementPath)
    {
        if (_xamlOfPaths.TryGetValue(elementPath, out var known))
        {
            return known;
        }

        XamlSourceLocation? found = null;
        foreach (var screen in _screens.Screens.OfType<LocalAppScreen>())
        {
            if (screen.AppRoot is { } root && ElementPaths.Resolve(root, elementPath, bringIntoView: false) is { } element
                && XamlSourceMap.Find(element) is { } location)
            {
                found = location;
                break;
            }
        }

        // An element no screen here has, such as one only a phone shows, is looked up again next time.
        if (found is not null)
        {
            _xamlOfPaths[elementPath] = found;
        }

        return found;
    }

    /// <summary>The XAML file on disk.</summary>
    private string? FileOf(XamlSourceLocation xaml) => ProjectOfAssembly(xaml.AssemblyName) is { } project
        ? Path.GetFullPath(Path.Combine(project.DirectoryPath, xaml.AppPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)))
        : null;

    private ProjectInfo? ProjectOfAssembly(string assemblyName) =>
        Workspace?.Projects.FirstOrDefault(p => string.Equals(p.Properties.GetValueOrDefault("AssemblyName") ?? p.Name, assemblyName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// "Fix with Agent" on a layout row: Claude gets the problem on every screen it shows on, the element's XAML,
    /// and a picture of the screen with the element outlined.
    /// </summary>
    private async Task FixLayoutWithAgentAsync(LayoutFindingRow row)
    {
        SelectSideBarPanel(SideBarPanel.Agent);
        var finding = row.Finding;
        var first = finding.Screens[0];
        var screen = _screens.Screens.FirstOrDefault(s => s.Id == first.ScreenId);
        byte[]? picture = null;
        if (screen is LocalAppScreen local && local.IsRunning)
        {
            picture = await local.CaptureAsync(first.Issue.Bounds, CancellationToken.None);
        }
        else if (first.Screenshot is { } taken)
        {
            // A picture another screen took: the element is outlined on it here.
            picture = ScreenCapture.Outlined(taken, first.Profile.Width, first.Issue.Bounds);
        }

        picture ??= first.Screenshot;
        var prompt = DescribeLayoutForAgent(row);
        if (picture is not null)
        {
            var caption = $"{first.Profile.Name}, {ScreenDetails.Describe(first.Profile)}";
            await Agent.AskAsync(prompt, new AgentImageViewModel(caption, picture));
        }
        else
        {
            await Agent.AskAsync(prompt);
        }
    }

    /// <summary>The request "Fix with Agent" sends for a layout finding.</summary>
    private string DescribeLayoutForAgent(LayoutFindingRow row)
    {
        var finding = row.Finding;
        var text = new StringBuilder();
        var web = IsOnWebPreview(finding);
        var kind = web ? "web app" : "Avalonia app";
        var app = (row.Diagnostic.ProjectName ?? (web ? _webProject?.Name : _avaloniaProject?.Name)) is { } name ? $"the {kind} {name}" : $"the {kind}";
        text.Append(CultureInfo.InvariantCulture, $"Fix this layout problem in {app}, so the view works on every screen below, then check that the project builds.\n");
        text.Append(CultureInfo.InvariantCulture, $"{finding.Code} {LayoutRules.Title(finding.Kind)}: {finding.Message}\n");
        text.Append("It shows on:\n");
        foreach (var screen in finding.Screens)
        {
            text.Append(CultureInfo.InvariantCulture, $"- {screen.Profile.Name} ({ScreenDetails.Describe(screen.Profile)}): {screen.Issue.Message}\n");
        }

        text.Append(CultureInfo.InvariantCulture, $"The element's path in the view is {finding.ElementPath}.\n");
        if (row.Xaml is { } xaml && row.Diagnostic.Location is { } location)
        {
            var root = Workspace?.RootDirectory;
            var file = root is not null && location.FilePath.StartsWith(root, StringComparison.Ordinal)
                ? Path.GetRelativePath(root, location.FilePath).Replace('\\', '/')
                : location.FilePath;
            text.Append(CultureInfo.InvariantCulture, $"It is written in {file}, line {location.Line}, column {location.Column}:\n```xml\n{xaml.Snippet}\n```\n");
        }
        else if (row.WebSource is { } page)
        {
            var root = Workspace?.RootDirectory;
            var file = root is not null && page.FilePath.StartsWith(root, StringComparison.Ordinal)
                ? Path.GetRelativePath(root, page.FilePath).Replace('\\', '/')
                : page.FilePath;
            text.Append(CultureInfo.InvariantCulture, $"It is written in {file}, line {page.Line}, column {page.Column}:\n```html\n{page.Snippet}\n```\n");
        }

        text.Append(CultureInfo.InvariantCulture, $"The picture shows {finding.Screens[0].Profile.Name} with the element outlined in red.\n");
        text.Append(web ? WebLayoutHint(finding.Kind) : LayoutHint(finding.Kind));
        return text.ToString().TrimEnd();
    }

    /// <summary>Whether a finding is on the web preview, so its element is in a page rather than in XAML.</summary>
    private static bool IsOnWebPreview(LayoutFinding finding) => finding.Screens.Any(s => s.ScreenId == WebAppScreen.WebPreviewId);

    /// <summary>What a good fix looks like in a web page, for each kind of problem.</summary>
    private static string WebLayoutHint(LayoutIssueKind kind) => kind switch
    {
        LayoutIssueKind.ClippedText => "Keep the design: let the text wrap, give it the room it needs with flexible widths, or shorten it, rather than shrinking its font.",
        LayoutIssueKind.OffScreen => "Keep the design: make the layout adapt to narrow screens with flexible widths, max-width: 100%, flex-wrap or a media query, rather than fixed widths and offsets.",
        LayoutIssueKind.Overlap => "Keep the design: give each control its own space with flex or grid layout, rather than negative margins or positioning that push one over the other.",
        LayoutIssueKind.SmallTouchTarget => "Keep the look: make the control at least 44 × 44 CSS pixels with min-width and min-height or padding, so a finger hits it reliably.",
        LayoutIssueKind.LowContrast => "Keep the design: use text and background colors with at least 4.5:1 contrast (3:1 for large text).",
        _ => string.Empty,
    };

    /// <summary>What a good fix looks like, for each kind of problem.</summary>
    private static string LayoutHint(LayoutIssueKind kind) => kind switch
    {
        LayoutIssueKind.ClippedText => "Keep the design: let the text wrap, give it the room it needs with star sizes or MinWidth, or shorten it, rather than shrinking its font.",
        LayoutIssueKind.OffScreen => "Keep the design: make the layout adapt to narrow screens with star sizes, wrapping panels or a ScrollViewer, rather than fixed widths and offsets.",
        LayoutIssueKind.Overlap => "Keep the design: give each control its own space with the layout panels, rather than margins that push one over the other.",
        LayoutIssueKind.SmallTouchTarget => "Keep the look: make the control at least 44 × 44 with MinWidth and MinHeight or padding, so a finger hits it reliably.",
        LayoutIssueKind.LowContrast => "Keep the design: use foreground and background colors with at least 4.5:1 contrast (3:1 for large text), preferably the theme's own resources.",
        _ => string.Empty,
    };
}
