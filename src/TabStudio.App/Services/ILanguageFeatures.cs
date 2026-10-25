using TabStudio.Compilation.Features;

namespace TabStudio.App.Services;

/// <summary>
/// IntelliSense for the editor: completion, Quick Info, parameter info, navigation, rename, code actions and the
/// declarations of a file.
/// </summary>
/// <remarks>
/// <para>
/// Positions are zero-based offsets into the text the language service last received through
/// <see cref="ILanguageService.UpdateText"/>, so editors send their text before they ask. Results are plain records
/// from <c>TabStudio.Compilation.Features</c>, never Roslyn types.
/// </para>
/// <para>
/// <see cref="LocalLanguageFeatures"/> answers in TabStudio's own process; TabStudio Link can answer from another
/// device. Every method may be called from any thread, and every query runs off the caller's thread.
/// </para>
/// </remarks>
public interface ILanguageFeatures
{
    /// <summary>The completion list at a position.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="position">The caret, after the typed character when there is one.</param>
    /// <param name="trigger">What opened the list.</param>
    /// <param name="cancellationToken">Cancels the request, typically because the user typed again.</param>
    Task<CompletionResult> GetCompletionsAsync(string filePath, int position, CompletionTrigger trigger, CancellationToken cancellationToken);

    /// <summary>Whether typing a character should open the completion list. Fast enough for every keystroke.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="position">The caret, after the typed character.</param>
    /// <param name="typedCharacter">The character that was typed. It is already in the text.</param>
    bool ShouldTriggerCompletion(string filePath, int position, char typedCharacter);

    /// <summary>The edits that commit a completion item, against the current text.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="entry">The item.</param>
    /// <param name="commitCharacter">The character that committed it, or <see langword="null"/> for Tab and Enter.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<CompletionCommit> GetCompletionChangeAsync(string filePath, CompletionEntry entry, char? commitCharacter, CancellationToken cancellationToken);

    /// <summary>The description shown next to the selected completion item.</summary>
    /// <param name="entry">The item.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<IReadOnlyList<TaggedRun>> GetCompletionDescriptionAsync(CompletionEntry entry, CancellationToken cancellationToken);

    /// <summary>The Quick Info tooltip for the symbol at a position, or <see langword="null"/>.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="position">A position on or just after the symbol.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<QuickInfoResult?> GetQuickInfoAsync(string filePath, int position, CancellationToken cancellationToken);

    /// <summary>The overloads of the call the caret is in, or <see langword="null"/> outside a call.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="position">The caret.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<SignatureHelpResult?> GetSignatureHelpAsync(string filePath, int position, CancellationToken cancellationToken);

    /// <summary>Where the symbol at a position is defined, or <see langword="null"/> when there is no symbol.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="position">A position on the symbol.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<DefinitionResult?> FindDefinitionAsync(string filePath, int position, CancellationToken cancellationToken);

    /// <summary>The implementations of the interface, abstract or virtual member at a position.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="position">A position on the symbol.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<IReadOnlyList<DefinitionResult>> FindImplementationsAsync(string filePath, int position, CancellationToken cancellationToken);

    /// <summary>Every reference to the symbol at a position, in every loaded project, sorted by file and position.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="position">A position on the symbol.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    Task<IReadOnlyList<ReferenceItem>> FindReferencesAsync(string filePath, int position, CancellationToken cancellationToken);

    /// <summary>The occurrences in the same file of the symbol at a position.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="position">The caret.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<IReadOnlyList<DocumentHighlight>> GetDocumentHighlightsAsync(string filePath, int position, CancellationToken cancellationToken);

    /// <summary>Whether the symbol at a position can be renamed, and its current name.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="position">The caret.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<RenameTarget> CanRenameAsync(string filePath, int position, CancellationToken cancellationToken);

    /// <summary>The edits that rename the symbol at a position, in every affected file.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="position">The caret.</param>
    /// <param name="newName">The new name.</param>
    /// <param name="options">What else to rename, or <see langword="null"/> for the symbol and its references only.</param>
    /// <param name="cancellationToken">Cancels the rename.</param>
    Task<RenameResult> GetRenameEditsAsync(string filePath, int position, string newName, RenameOptions? options, CancellationToken cancellationToken);

    /// <summary>The quick fixes and refactorings for a selection or the caret. Only the last list's ids stay valid.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="start">Start of the selection, or the caret.</param>
    /// <param name="length">Length of the selection, or 0.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<IReadOnlyList<CodeActionItem>> GetCodeActionsAsync(string filePath, int start, int length, CancellationToken cancellationToken);

    /// <summary>What a code action from the last list changes, relative to the text it was listed for.</summary>
    /// <param name="id">The action's <see cref="CodeActionItem.Id"/>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<IReadOnlyList<DocumentEdit>> GetCodeActionEditsAsync(string id, CancellationToken cancellationToken);

    /// <summary>The namespaces, types and members declared in a file, as a tree.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<IReadOnlyList<DocumentSymbol>> GetDocumentSymbolsAsync(string filePath, CancellationToken cancellationToken);

    /// <summary>The declarations that contain a position, outermost first, for the breadcrumb.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="position">The caret.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<IReadOnlyList<DocumentSymbol>> GetSymbolPathAsync(string filePath, int position, CancellationToken cancellationToken);

    /// <summary>
    /// Prepares what the first request would otherwise wait for, such as the list of code actions and the index of
    /// types from namespaces that are not imported yet. Call in the background once a workspace is loaded.
    /// </summary>
    /// <param name="filePath">A C# file of the workspace to warm up with.</param>
    /// <param name="cancellationToken">Cancels the warm-up.</param>
    Task WarmUpAsync(string filePath, CancellationToken cancellationToken);
}
