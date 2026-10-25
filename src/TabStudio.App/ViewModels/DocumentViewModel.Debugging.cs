namespace TabStudio.App.ViewModels;

/// <summary>A statement the debugger marks in the editor.</summary>
/// <param name="Start">Zero-based offset where the statement starts.</param>
/// <param name="Length">Length of the statement in characters.</param>
public sealed record ExecutionPoint(int Start, int Length);

/// <summary>What the debugger shows in a document: breakpoints, where the program is stopped, and the exception helper.</summary>
public sealed partial class DocumentViewModel
{
    /// <summary>The breakpoints in this file, for the margin. Kept by the workspace's <see cref="BreakpointsViewModel"/>.</summary>
    public IReadOnlyList<BreakpointViewModel> Breakpoints { get; private set; } = [];

    /// <summary>The statement the paused program runs next, when it is in this file: the yellow arrow.</summary>
    public ExecutionPoint? CurrentStatement { get; private set; }

    /// <summary>The call a caller chosen in the Call Stack is making, when it is in this file: the green arrow.</summary>
    public ExecutionPoint? CallerStatement { get; private set; }

    /// <summary>The exception helper shown at <see cref="CurrentStatement"/> after an unhandled exception.</summary>
    public ExceptionHelperViewModel? ExceptionHelper { get; private set; }

    /// <summary>What the editor asks of the debugger, such as toggling breakpoints and DataTips. Set by the workspace.</summary>
    public IEditorDebugger? Debugger { get; set; }

    /// <summary>Raised on the UI thread when breakpoints, the execution points or the exception helper change.</summary>
    public event EventHandler? DebugStateChanged;

    /// <summary>Replaces the breakpoints shown in the margin.</summary>
    /// <param name="breakpoints">The breakpoints of this file.</param>
    public void SetBreakpoints(IReadOnlyList<BreakpointViewModel> breakpoints)
    {
        ArgumentNullException.ThrowIfNull(breakpoints);
        Breakpoints = breakpoints;
        DebugStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Marks where the program is stopped, or clears the marks when it runs again.</summary>
    /// <param name="current">The current statement, when it is in this file.</param>
    /// <param name="caller">The statement of the caller chosen in the Call Stack, when it is in this file.</param>
    /// <param name="exceptionHelper">The exception helper to show at the current statement, if any.</param>
    public void ShowExecutionPoints(ExecutionPoint? current, ExecutionPoint? caller, ExceptionHelperViewModel? exceptionHelper)
    {
        if (current == CurrentStatement && caller == CallerStatement && ReferenceEquals(exceptionHelper, ExceptionHelper))
        {
            return;
        }

        CurrentStatement = current;
        CallerStatement = caller;
        ExceptionHelper = current is null ? null : exceptionHelper;
        DebugStateChanged?.Invoke(this, EventArgs.Empty);
    }
}
