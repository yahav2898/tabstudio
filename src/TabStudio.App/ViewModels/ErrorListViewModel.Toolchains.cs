using TabStudio.Core.Diagnostics;

namespace TabStudio.App.ViewModels;

/// <summary>Errors that ended runs of toolchain programs, such as a Python traceback.</summary>
public sealed partial class ErrorListViewModel
{
    /// <summary>Shows the error a run of a project ended with, replacing that project's previous one.</summary>
    /// <param name="projectDirectory">The project's folder.</param>
    /// <param name="error">The error, or <see langword="null"/> after a run that ended well.</param>
    public void SetRunError(string projectDirectory, BuildDiagnostic? error)
    {
        ArgumentNullException.ThrowIfNull(projectDirectory);

        // Kept apart from the project's analysis and build results, which replace theirs by file.
        SetRestoreDiagnostics("run:" + projectDirectory, error is null ? [] : [error]);
    }
}
