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

    /// <summary>Open the light bulb menu of quick fixes and refactorings (Ctrl+.).</summary>
    QuickActions,

    /// <summary>Open the completion list (Ctrl+J).</summary>
    ListMembers,

    /// <summary>Complete the word, or open the completion list when there are several choices (Ctrl+Space).</summary>
    CompleteWord,

    /// <summary>Show the signature of the call at the caret (Ctrl+Shift+Space).</summary>
    ParameterInfo,

    /// <summary>Show Quick Info for the symbol at the caret (Ctrl+K, Ctrl+I).</summary>
    QuickInfo,
}
