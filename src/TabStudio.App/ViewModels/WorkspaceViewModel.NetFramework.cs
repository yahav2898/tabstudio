using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Editing;
using TabStudio.Compilation.Building;
using TabStudio.Core.Compatibility;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Legacy .NET Framework projects in the IDE. Those that use only what TabStudio's WPF, Windows Forms and console
/// paths provide build and run like SDK-style projects. A program that needs Windows is not compiled against .NET:
/// the Error List has one diagnostic that says why, and the editor shows a banner over its files with Run on Windows
/// Bridge and Upgrade project. Upgrade project converts any legacy project to an SDK-style one and shows the change to
/// its project file as a diff.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    /// <summary>The Error List keeps a .NET Framework program's needs-Windows diagnostic under its project file and this suffix.</summary>
    private const string NetFrameworkDiagnosticsSuffix = "#netfx";

    private readonly HashSet<string> _netFrameworkDiagnostics = new(StringComparer.Ordinal);

    /// <summary>The Error List key of the problems found while the workspace loaded.</summary>
    private const string LoadErrorsKey = "|workspace-load";

    /// <summary>The Error List code of a solution or project file TabStudio could not read.</summary>
    internal const string LoadErrorId = "TS1101";

    /// <summary>The .NET Framework program that needs Windows whose file is in the editor, for the editor's banner.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNetFrameworkBanner), nameof(NetFrameworkBannerText))]
    [NotifyCanExecuteChangedFor(nameof(RunNetFrameworkProjectOnWindowsBridgeCommand))]
    public partial ProjectInfo? NetFrameworkBannerProject { get; private set; }

    /// <summary>Whether the editor shows the banner of a .NET Framework program that needs Windows.</summary>
    public bool HasNetFrameworkBanner => NetFrameworkBannerProject is not null;

    /// <summary>The banner's text: which project, and why it needs Windows.</summary>
    public string? NetFrameworkBannerText => NetFrameworkBannerProject is { } project ? NetFrameworkCompatibility.NeedsWindowsMessage(project) : null;

    /// <summary>
    /// Shows one diagnostic for each .NET Framework program that needs Windows, in place of the errors of compiling it
    /// against the wrong framework, and takes away those of projects that no longer do.
    /// </summary>
    private void ShowNetFrameworkDiagnostics(Workspace workspace)
    {
        ShowLoadErrors(workspace);
        foreach (var key in _netFrameworkDiagnostics)
        {
            ErrorList.SetRestoreDiagnostics(key, []);
        }

        _netFrameworkDiagnostics.Clear();
        foreach (var project in workspace.Projects.Where(NetFrameworkCompatibility.NeedsWindows))
        {
            var key = project.FilePath + NetFrameworkDiagnosticsSuffix;
            _netFrameworkDiagnostics.Add(key);
            ErrorList.SetRestoreDiagnostics(key,
            [
                new BuildDiagnostic(
                    ProjectBuilder.NeedsWindowsId,
                    DiagnosticSeverityLevel.Warning,
                    NetFrameworkCompatibility.NeedsWindowsMessage(project) + " It is not compiled here: it builds and runs on your PC through the Windows Bridge, or Upgrade project makes it an SDK-style .NET project.",
                    null,
                    project.Name),
            ]);
        }

        UpdateNetFrameworkBanner();
    }

    /// <summary>Shows the banner when the active document belongs to a .NET Framework program that needs Windows.</summary>
    private void UpdateNetFrameworkBanner()
    {
        var project = ActiveDocument is { IsComparison: false } document ? Workspace?.FindProjectForFile(document.FilePath) : null;
        NetFrameworkBannerProject = project is not null && NetFrameworkCompatibility.NeedsWindows(project) ? project : null;
    }

    private bool CanRunNetFrameworkProjectOnWindowsBridge() => NetFrameworkBannerProject is not null;

    /// <summary>
    /// The banner's Run on Windows Bridge: builds and runs the program on the paired PC, or, without one, opens the
    /// Windows Bridge's pairing.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRunNetFrameworkProjectOnWindowsBridge))]
    private async Task RunNetFrameworkProjectOnWindowsBridgeAsync()
    {
        if (NetFrameworkBannerProject is not { } project)
        {
            return;
        }

        SetStartupProject(project);
        if (_windowsBridge is { HasPairedPc: true })
        {
            await RunOnWindowsBridgeAsync(project, showOutput: true, skipReferences: false, replacing: null);
            return;
        }

        SelectedPanel = BottomPanel.Output;
        IsBottomPanelVisible = true;
        Output.WriteLine(
            $"{NetFrameworkCompatibility.NeedsWindowsMessage(project)} Pair a Windows PC in File > TabStudio Link > Windows PC, and it builds and runs there.",
            OutputKind.Info);
        if (HasWindowsBridge)
        {
            OpenWindowsBridge();
        }
    }

    /// <summary>
    /// Upgrade project: converts a legacy .NET Framework project to an SDK-style .NET project, as .NET Upgrade Assistant
    /// does, reloads the workspace and shows the project file's change as a diff. Takes a Compatibility Doctor row, a
    /// project, or nothing for the banner's project.
    /// </summary>
    /// <param name="target">A <see cref="CompatibilityRowViewModel"/>, a <see cref="ProjectInfo"/>, or <see langword="null"/>.</param>
    [RelayCommand]
    private async Task UpgradeProjectAsync(object? target)
    {
        var project = target switch
        {
            ProjectInfo info => info,
            CompatibilityRowViewModel row => Workspace?.Projects.FirstOrDefault(p => p.Name == row.Project),
            _ => NetFrameworkBannerProject,
        };
        if (project is null || !ProjectParser.IsLegacyFrameworkProject(project) || !await SaveAllCoreAsync())
        {
            return;
        }

        LegacyProjectUpgrade upgrade;
        try
        {
            upgrade = await Task.Run(() =>
            {
                var planned = LegacyProjectUpgrader.Plan(project.FilePath);
                planned.Apply();
                return planned;
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ProjectLoadException or InvalidOperationException)
        {
            Output.WriteLine($"{project.Name} could not be upgraded: {ex.Message}", OutputKind.Failure);
            SelectedPanel = BottomPanel.Output;
            IsBottomPanelVisible = true;
            return;
        }

        Output.WriteLine($"Upgraded {project.Name} to an SDK-style project for {upgrade.TargetFramework}. Review the change to {Path.GetFileName(project.FilePath)}.", OutputKind.Info);
        foreach (var note in upgrade.Notes)
        {
            Output.WriteLine("  " + note, OutputKind.Info);
        }

        await ReloadWorkspaceAsync();
        foreach (var document in Documents.Where(d => !d.IsComparison && string.Equals(d.FilePath, project.FilePath, StringComparison.Ordinal)).ToList())
        {
            document.ReloadFromDisk(upgrade.UpgradedXml);
        }

        var root = Workspace?.RootDirectory ?? project.DirectoryPath;
        var version = project.GetProperty("TargetFrameworkVersion")?.Trim().TrimStart('v', 'V') ?? "4.x";
        var comparison = new FileComparison(
            project.FilePath,
            Path.GetRelativePath(root, project.FilePath).Replace('\\', '/'),
            $".NET Framework {version}",
            InlineDiff.Create(upgrade.OriginalXml.Replace("\r\n", "\n", StringComparison.Ordinal), upgrade.UpgradedXml))
        {
            Caption = $"{Path.GetFileName(project.FilePath)} upgrade",
            BaseName = "Legacy project",
            HeadName = $"SDK-style project ({upgrade.TargetFramework})",
        };
        ((ISourceControlHost)this).ShowComparison(comparison);
        StatusText = $"Upgraded {project.Name}";
        if (Workspace is { } workspace)
        {
            StartPackageRestore(workspace);
        }
    }

    /// <summary>
    /// Puts what could not be read while the workspace loaded, such as a project file that is not valid XML, in the Error
    /// List and the Output, as Visual Studio does: before 0.21.2 such a project was shown as an unknown project with no
    /// word of why.
    /// </summary>
    private void ShowLoadErrors(Workspace workspace)
    {
        ErrorList.SetRestoreDiagnostics(LoadErrorsKey, [.. workspace.LoadErrors.Select(e => new BuildDiagnostic(LoadErrorId, DiagnosticSeverityLevel.Error, e, null))]);
        foreach (var error in workspace.LoadErrors)
        {
            Output.WriteLine(error);
        }
    }
}
