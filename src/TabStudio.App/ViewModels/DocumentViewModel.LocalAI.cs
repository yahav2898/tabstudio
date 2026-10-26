using TabStudio.Agent.Local;
using TabStudio.App.Services.LocalAI;

namespace TabStudio.App.ViewModels;

public sealed partial class DocumentViewModel
{
    /// <summary>The app's local inference engine; null for a standalone document outside a workspace.</summary>
    public LocalAIService? LocalAI { get; internal set; }

    /// <summary>Unsaved text from the other open tabs, captured on the UI thread.</summary>
    public Func<IReadOnlyList<OpenFileContext>>? LocalAINeighbors { get; internal set; }
}
