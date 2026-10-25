namespace TabStudio.App.ViewModels;

/// <summary>What the code editor asks of the debugger: breakpoints from its margin and DataTips. The workspace implements it.</summary>
public interface IEditorDebugger
{
    /// <summary>Whether the program is paused, so the editor can show DataTips.</summary>
    bool IsPaused { get; }

    /// <summary>Sets a breakpoint on a line, or removes the one that is there.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="line">One-based line.</param>
    void ToggleBreakpoint(string filePath, int line);

    /// <summary>Asks for the condition of the breakpoint on a line, setting a breakpoint there first if needed.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="line">One-based line.</param>
    Task EditBreakpointConditionAsync(string filePath, int line);

    /// <summary>Makes the breakpoint on a line stop every time again.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="line">One-based line.</param>
    void ClearBreakpointCondition(string filePath, int line);

    /// <summary>Finds the value of a variable or member path, such as <c>order.Total</c>, in the paused program.</summary>
    /// <param name="expression">The path under the pointer.</param>
    /// <returns>A tree with the value as its only root, or <see langword="null"/> when there is nothing to show.</returns>
    Task<VariableTreeViewModel?> EvaluateDataTipAsync(string expression);
}
