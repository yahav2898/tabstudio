namespace TabStudio.Packages;

/// <summary>What a restore is doing.</summary>
public enum RestoreStage
{
    /// <summary>Reading versions and dependencies from the package sources.</summary>
    Resolving,

    /// <summary>Downloading packages into the global packages folder and selecting their assemblies.</summary>
    Downloading,

    /// <summary>The restore has finished.</summary>
    Completed,
}

/// <summary>
/// A progress report from <see cref="PackageRestorer.RestoreAsync"/>, for the status bar and the Output panel.
/// </summary>
/// <param name="Stage">What the restore is doing.</param>
/// <param name="Message">One line that describes the step, such as <c>Downloaded Newtonsoft.Json 13.0.3</c>.</param>
/// <param name="Completed">How many steps of this stage are done.</param>
/// <param name="Total">How many steps this stage has, or 0 while that is not known yet.</param>
public sealed record RestoreProgress(RestoreStage Stage, string Message, int Completed, int Total);
