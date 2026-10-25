namespace TabStudio.App.ViewModels;

/// <summary>The Locals panel of a program a debug adapter paused: the variables of the frame chosen in the Call Stack.</summary>
public sealed partial class LocalsViewModel
{
    /// <summary>Shows variables a debug adapter read, such as the locals of a frame and its other scopes.</summary>
    /// <param name="variables">The rows, in order.</param>
    /// <param name="note">A note above them, or <see langword="null"/> for none.</param>
    public Task ShowVariablesAsync(IReadOnlyList<VariableSource> variables, string? note)
    {
        ArgumentNullException.ThrowIfNull(variables);
        Note = variables.Count == 0 ? note ?? "No variables are in scope at this statement." : note;
        return Tree.ShowAsync(variables);
    }
}
