namespace TabStudio.Notebooks.Execution;

/// <summary>How running a cell ended.</summary>
public enum CellExecutionStatus
{
    /// <summary>The cell compiled and ran to the end.</summary>
    Succeeded,

    /// <summary>The cell did not compile, or used something the kernel does not support; it did not run.</summary>
    CompilationFailed,

    /// <summary>The cell threw an exception it did not handle.</summary>
    Failed,

    /// <summary>The cell was interrupted while it ran, or before it started.</summary>
    Interrupted,

    /// <summary>The cell is not a code cell, so there was nothing to run.</summary>
    Skipped,

    /// <summary>The cell is in a language the kernel does not run, such as F#.</summary>
    Unsupported,
}
