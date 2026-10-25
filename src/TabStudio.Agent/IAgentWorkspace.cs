using TabStudio.Core.Diagnostics;

namespace TabStudio.Agent;

/// <summary>
/// What the agent may see and do in the open workspace. The IDE implements it: reads include unsaved editor
/// text, and nothing changes on disk except through <see cref="ApplyChangeAsync"/>, which runs only for changes
/// the user approved.
/// </summary>
public interface IAgentWorkspace
{
    /// <summary>The folder of the open solution or folder; every path the agent uses is relative to it.</summary>
    string RootDirectory { get; }

    /// <summary>The files of the workspace that source control would include, relative to the root with <c>/</c>.</summary>
    /// <param name="cancellationToken">Cancels the listing.</param>
    Task<IReadOnlyList<string>> ListFilesAsync(CancellationToken cancellationToken);

    /// <summary>A file's current text, including unsaved edits in open editors.</summary>
    /// <param name="fullPath">The file.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The text, or <see langword="null"/> when the file does not exist or is not text.</returns>
    Task<string?> ReadFileAsync(string fullPath, CancellationToken cancellationToken);

    /// <summary>The errors and warnings of the solution as it would be with the agent's pending changes.</summary>
    /// <param name="pendingFiles">Files the agent changed: full path to new text, or <see langword="null"/> for a deleted file.</param>
    /// <param name="cancellationToken">Cancels the analysis.</param>
    Task<IReadOnlyList<BuildDiagnostic>> GetDiagnosticsAsync(IReadOnlyDictionary<string, string?> pendingFiles, CancellationToken cancellationToken);

    /// <summary>Builds the test projects as they would be with the pending changes and runs their tests.</summary>
    /// <param name="filter">A test, class or namespace name to run, or <see langword="null"/> for all tests.</param>
    /// <param name="pendingFiles">Files the agent changed.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>A summary for the agent: counts, then each failure with its message and location.</returns>
    Task<string> RunTestsAsync(string? filter, IReadOnlyDictionary<string, string?> pendingFiles, CancellationToken cancellationToken);

    /// <summary>Writes an approved change: into the open editor when the file is open (so it can be undone), else to disk.</summary>
    /// <param name="fullPath">The file.</param>
    /// <param name="text">The new text, or <see langword="null"/> to delete the file.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task ApplyChangeAsync(string fullPath, string? text, CancellationToken cancellationToken);
}
