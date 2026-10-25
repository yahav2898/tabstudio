using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.Core.Workspaces;
using TabStudio.LiveTesting.Building;

namespace TabStudio.App.ViewModels;

/// <summary>What Live Unit Testing needs from the IDE. Implemented by the workspace.</summary>
public interface ILiveTestingHost
{
    /// <summary>The C# test projects of the workspace.</summary>
    IReadOnlyList<ProjectInfo> TestProjects { get; }

    /// <summary>
    /// Whether a live run may use the process now: nothing is building, no program or debug session runs, the Test
    /// Explorer is not running tests and no notebook cell runs. They share the console with test runs.
    /// </summary>
    bool CanRunLive { get; }

    /// <summary>How warm the device is.</summary>
    ThermalState Thermal { get; }

    /// <summary>The device's profile, which sets how long Live Unit Testing waits after typing.</summary>
    DeviceProfile Profile { get; }

    /// <summary>The open documents, whose markers Live Unit Testing keeps up to date.</summary>
    IEnumerable<DocumentViewModel> OpenDocuments { get; }

    /// <summary>The Test Explorer, which shows live results too.</summary>
    TestExplorerViewModel Tests { get; }

    /// <summary>Sends the editors' unsaved text to the language service and waits for package restores, before a live build.</summary>
    Task PrepareLiveRunAsync();

    /// <summary>Builds a test project for coverage.</summary>
    /// <param name="testProject">The test project.</param>
    /// <param name="uninstrumented">Projects to leave without coverage.</param>
    /// <param name="cancellationToken">Cancels the build.</param>
    Task<CoverageBuild> BuildForCoverageAsync(ProjectInfo testProject, IReadOnlySet<string> uninstrumented, CancellationToken cancellationToken);

    /// <summary>The package assemblies a test project needs to run.</summary>
    /// <param name="projectFilePath">The test project.</param>
    IReadOnlyList<string> GetPackageRuntimeAssemblies(string projectFilePath);

    /// <summary>Opens a file and moves the caret to a line and column.</summary>
    /// <param name="path">Full path of the file.</param>
    /// <param name="line">1-based line.</param>
    /// <param name="column">1-based column.</param>
    Task NavigateToAsync(string path, int line, int column);

    /// <summary>Remembers the settings for the open solution.</summary>
    /// <param name="settings">The settings.</param>
    void SaveLiveTestingSettings(LiveTestingSettings settings);
}
