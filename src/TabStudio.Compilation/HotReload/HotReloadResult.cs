using TabStudio.Core.Diagnostics;

namespace TabStudio.Compilation.HotReload;

/// <summary>The result of applying the current edits to a loaded program.</summary>
public enum HotReloadStatus
{
    /// <summary>The runtime accepted the deltas.</summary>
    Applied,
    /// <summary>There were no executable changes.</summary>
    NoChanges,
    /// <summary>The edits require a new run.</summary>
    RestartRequired,
    /// <summary>The edited source does not compile.</summary>
    CompilationErrors,
    /// <summary>This runtime was not started with metadata updates enabled.</summary>
    Unsupported,
}

/// <summary>An outcome and diagnostics suitable for the IDE's Output panel and Error List.</summary>
/// <param name="Status">What happened.</param>
/// <param name="Message">A user-facing explanation.</param>
/// <param name="Diagnostics">Compiler errors with source locations.</param>
public sealed record HotReloadResult(HotReloadStatus Status, string Message, IReadOnlyList<BuildDiagnostic> Diagnostics)
{
    /// <summary>Whether it is safe to continue the program.</summary>
    public bool Success => Status is HotReloadStatus.Applied or HotReloadStatus.NoChanges;
}
