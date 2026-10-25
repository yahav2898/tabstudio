namespace TabStudio.App.ViewModels;

/// <summary>Peek Definition in the document's editor.</summary>
public sealed partial class DocumentViewModel
{
    /// <summary>Raised when a definition should be peeked at under the caret line.</summary>
    public event EventHandler<PeekDefinitionViewModel>? PeekRequested;

    /// <summary>Shows a definition in the editor, under the caret line.</summary>
    /// <param name="peek">What to show.</param>
    public void ShowPeek(PeekDefinitionViewModel peek)
    {
        ArgumentNullException.ThrowIfNull(peek);
        PeekRequested?.Invoke(this, peek);
    }
}
