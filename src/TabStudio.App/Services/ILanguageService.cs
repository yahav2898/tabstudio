using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.Debugging.Building;
using TabStudio.Packages;

namespace TabStudio.App.Services;

/// <summary>
/// Everything the IDE asks of the C# language service and build engine for one open workspace.
/// </summary>
/// <remarks>
/// The editor and panels only talk to this interface. Today it is implemented in-process by
/// <see cref="LocalLanguageService"/>; TabStudio Link implements it on another device over the network.
/// Every method may be called from any thread.
/// </remarks>
public interface ILanguageService : IDisposable
{
    /// <summary>Whether a file is a C# source file of a loaded project.</summary>
    /// <param name="filePath">Full path of the file.</param>
    bool ContainsDocument(string filePath);

    /// <summary>
    /// Replaces the whole model with a workspace, for example after files were added, renamed or deleted.
    /// </summary>
    /// <param name="workspace">The reloaded workspace.</param>
    /// <param name="sources">Supplies file text, including unsaved edits in open documents.</param>
    void Reload(Workspace workspace, ISourceTextProvider sources);

    /// <summary>Replaces a file's text with the editor's current text.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="text">The text.</param>
    void UpdateText(string filePath, string text);

    /// <summary>Adds a new file to the project whose folder contains it.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="text">Its text.</param>
    bool AddDocument(string filePath, string text);

    /// <summary>Errors and warnings in one file.</summary>
    Task<IReadOnlyList<BuildDiagnostic>> GetDocumentDiagnosticsAsync(string filePath, CancellationToken cancellationToken);

    /// <summary>Errors and warnings in a whole project.</summary>
    Task<IReadOnlyList<BuildDiagnostic>> GetProjectDiagnosticsAsync(string projectFilePath, CancellationToken cancellationToken);

    /// <summary>Semantic classification of a whole file.</summary>
    Task<IReadOnlyList<ClassifiedRange>> GetClassificationsAsync(string filePath, CancellationToken cancellationToken);

    /// <summary>The edits that format a file.</summary>
    Task<IReadOnlyList<TextEdit>> GetFormattingEditsAsync(string filePath, CancellationToken cancellationToken);

    /// <summary>Builds a project and the projects it references, in memory.</summary>
    Task<BuildResult> BuildAsync(ProjectInfo project, CancellationToken cancellationToken);

    /// <summary>
    /// Builds a project and the projects it references for the debugger: instrumented so it can be paused, stepped
    /// and inspected, with a map from every statement back to the source.
    /// </summary>
    Task<DebugBuild> BuildForDebuggingAsync(ProjectInfo project, CancellationToken cancellationToken);

    /// <summary>Switches between the Debug and Release configurations.</summary>
    void SetOptimization(bool optimize);

    /// <summary>Allows or prevents compiling on several threads, for thermal throttling.</summary>
    void SetConcurrentBuild(bool enabled);

    /// <summary>Releases cached compilations under memory pressure.</summary>
    void TrimMemory();

    /// <summary>
    /// Restores a project's NuGet packages, then gives IntelliSense and builds their reference assemblies.
    /// Never throws for restore problems: they are in the result's diagnostics.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <param name="progress">Receives progress.</param>
    /// <param name="cancellationToken">Cancels the restore.</param>
    Task<RestoreResult> RestorePackagesAsync(ProjectInfo project, IProgress<RestoreProgress>? progress, CancellationToken cancellationToken);

    /// <summary>The package assemblies a program needs when it runs, from the project's last restore.</summary>
    /// <param name="projectFilePath">The project.</param>
    /// <returns>Full paths, or an empty list when the project has no restored packages.</returns>
    IReadOnlyList<string> GetPackageRuntimeAssemblies(string projectFilePath);
}
