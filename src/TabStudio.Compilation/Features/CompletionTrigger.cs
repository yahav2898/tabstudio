namespace TabStudio.Compilation.Features;

/// <summary>Why a completion list is requested.</summary>
public enum CompletionTriggerKind
{
    /// <summary>The user asked for it, with Ctrl+Space or a tap on the completion button.</summary>
    Invoke,

    /// <summary>The user typed <see cref="CompletionTrigger.Character"/>, such as a letter or a dot.</summary>
    Insertion,

    /// <summary>The user deleted <see cref="CompletionTrigger.Character"/> with Backspace.</summary>
    Deletion,
}

/// <summary>What made the editor ask for completion.</summary>
/// <param name="Kind">Why completion is requested.</param>
/// <param name="Character">The typed or deleted character, for <see cref="CompletionTriggerKind.Insertion"/> and <see cref="CompletionTriggerKind.Deletion"/>.</param>
public readonly record struct CompletionTrigger(CompletionTriggerKind Kind, char Character = '\0')
{
    /// <summary>Completion the user asked for explicitly.</summary>
    public static CompletionTrigger Invoke => new(CompletionTriggerKind.Invoke);

    /// <summary>Completion after typing a character.</summary>
    /// <param name="character">The character that was typed. It is already in the document.</param>
    public static CompletionTrigger Insertion(char character) => new(CompletionTriggerKind.Insertion, character);

    /// <summary>Completion after deleting a character.</summary>
    /// <param name="character">The character that was deleted.</param>
    public static CompletionTrigger Deletion(char character) => new(CompletionTriggerKind.Deletion, character);
}
