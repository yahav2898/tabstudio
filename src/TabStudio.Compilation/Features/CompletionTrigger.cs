namespace TabStudio.Compilation.Features;

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
