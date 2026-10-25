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
