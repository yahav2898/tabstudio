using TabStudio.Core.Diagnostics;

namespace TabStudio.App.ViewModels;

/// <summary>Errors that ended runs of toolchain programs, such as a Python traceback.</summary>
public sealed partial class ErrorListViewModel
{
    /// <summary>Shows the error a run of a project ended with, replacing that project's previous one.</summary>
    /// <param name="projectDirectory">The project's folder.</param>
    /// <param name="error">The error, or <see langword="null"/> after a run that ended well.</param>
    public void SetRunError(string projectDirectory, BuildDiagnostic? error)
        => SetRunErrors(projectDirectory, error is null ? [] : [error]);

    /// <summary>Replaces all errors from the previous build or run of a toolchain project.</summary>
    /// <param name="projectDirectory">The project's folder.</param>
    /// <param name="errors">The errors, or an empty list after success.</param>
    public void SetRunErrors(string projectDirectory, IReadOnlyList<BuildDiagnostic> errors)
    {
        ArgumentNullException.ThrowIfNull(projectDirectory);

        // Kept apart from the project's analysis and build results, which replace theirs by file.
        SetRestoreDiagnostics("run:" + projectDirectory, errors);
    }
}
