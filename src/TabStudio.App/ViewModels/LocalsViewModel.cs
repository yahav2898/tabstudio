using CommunityToolkit.Mvvm.ComponentModel;
using TabStudio.Debugging;

namespace TabStudio.App.ViewModels;

/// <summary>The Locals panel: <c>this</c>, the parameters and the locals of the function where the program is paused.</summary>
public sealed partial class LocalsViewModel : ObservableObject
{
    /// <summary>The variables.</summary>
    public VariableTreeViewModel Tree { get; } = new();

    /// <summary>A note above the variables, such as which frame they belong to; <see langword="null"/> for none.</summary>
    [ObservableProperty]
    public partial string? Note { get; private set; }

    /// <summary>Shows the locals of a pause.</summary>
    /// <param name="locals">The locals of the paused frame.</param>
    public Task ShowAsync(IReadOnlyList<DebugValue> locals)
    {
        ArgumentNullException.ThrowIfNull(locals);
        Note = locals.Count == 0 ? "No variables are in scope at this statement." : null;
        return Tree.ShowAsync(locals.Select(l => new VariableSource(l.Name, l.Name, l)).ToList());
    }

    /// <summary>
    /// Says which frame the locals belong to when the Call Stack shows a caller, because TabStudio's debugger reads
    /// the variables of the paused frame only.
    /// </summary>
    /// <param name="pausedFrame">The method the program is paused in.</param>
    /// <param name="shownFrame">The method chosen in the Call Stack, or <see langword="null"/> when it is the paused one.</param>
    public void ShowFrameNote(string pausedFrame, string? shownFrame)
    {
        ArgumentNullException.ThrowIfNull(pausedFrame);
        Note = shownFrame is null
            ? null
            : $"These are the locals of {pausedFrame}, where the program is paused. The locals of callers such as {shownFrame} are not available.";
    }

    /// <summary>Removes everything, when the debug session ends.</summary>
    public void Clear()
    {
        Note = null;
        Tree.Clear();
    }
}
