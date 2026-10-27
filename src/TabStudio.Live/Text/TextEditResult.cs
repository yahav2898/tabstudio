namespace TabStudio.Live.Text;

/// <summary>What an undo or a redo did: the operations for the other devices and the changes for this device's editor.</summary>
/// <param name="Operations">The operations, in the order they were made.</param>
/// <param name="Changes">The changes of the visible text, in the order to apply them.</param>
public sealed record TextEditResult(IReadOnlyList<TextOperation> Operations, IReadOnlyList<TextChange> Changes);
