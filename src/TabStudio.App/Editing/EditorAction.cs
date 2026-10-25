namespace TabStudio.App.Editing;

/// <summary>
/// An action a view model asks the editor view to perform, for things that need the editor's selection,
/// caret or scroll position.
/// </summary>
public enum EditorAction
{
    /// <summary>Open the find bar.</summary>
    Find,

    /// <summary>Open the find bar in replace mode.</summary>
    Replace,

    /// <summary>Toggle line comments on the selected lines.</summary>
    ToggleComment,

    /// <summary>Undo the last edit.</summary>
    Undo,

    /// <summary>Redo the last undone edit.</summary>
    Redo,

    /// <summary>Select the whole document.</summary>
    SelectAll,

    /// <summary>Move keyboard focus into the editor.</summary>
    Focus,
}
