using TabStudio.Compilation.Features;
using TabStudio.Compilation.Wpf.Editing;

namespace TabStudio.App.Services;

/// <summary>Adapts WPF XAML editing to the editor's language features, keeping analysis off the UI thread.</summary>
/// <param name="features">The XAML engine for the document's workspace.</param>
/// <param name="synchronize">Sends all open documents' unsaved text before a query.</param>
internal sealed class WpfXamlLanguageFeatures(Func<WpfXamlFeatures?> features, Action synchronize) : ILanguageFeatures
{
    /// <inheritdoc />
    public Task<CompletionResult> GetCompletionsAsync(string filePath, int position, CompletionTrigger trigger, CancellationToken cancellationToken) =>
        RunAsync(engine => engine.GetCompletionsAsync(filePath, position, cancellationToken), CompletionResult.Empty, cancellationToken);

    /// <inheritdoc />
    public bool ShouldTriggerCompletion(string filePath, int position, char typedCharacter) => features()?.ShouldTriggerCompletion(filePath, position, typedCharacter) == true;

    /// <inheritdoc />
    public Task<CompletionCommit> GetCompletionChangeAsync(string filePath, CompletionEntry entry, char? commitCharacter, CancellationToken cancellationToken) =>
        RunAsync(engine => engine.GetCompletionChangeAsync(filePath, entry, commitCharacter, cancellationToken), new CompletionCommit([], null, false), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<TaggedRun>> GetCompletionDescriptionAsync(CompletionEntry entry, CancellationToken cancellationToken) =>
        RunAsync(engine => engine.GetCompletionDescriptionAsync(entry, cancellationToken), [], cancellationToken);

    /// <inheritdoc />
    public Task<QuickInfoResult?> GetQuickInfoAsync(string filePath, int position, CancellationToken cancellationToken) =>
        RunAsync(engine => engine.GetQuickInfoAsync(filePath, position, cancellationToken), null, cancellationToken);

    /// <inheritdoc />
    public Task<DefinitionResult?> FindDefinitionAsync(string filePath, int position, CancellationToken cancellationToken) =>
        RunAsync(engine => engine.FindDefinitionAsync(filePath, position, cancellationToken), null, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<CodeActionItem>> GetCodeActionsAsync(string filePath, int start, int length, CancellationToken cancellationToken) =>
        RunAsync(engine => engine.GetCodeActionsAsync(filePath, start, length, cancellationToken), [], cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<DocumentEdit>> GetCodeActionEditsAsync(string id, CancellationToken cancellationToken) =>
        RunAsync(engine => engine.GetCodeActionEditsAsync(id, cancellationToken), [], cancellationToken);

    /// <inheritdoc />
    public Task<SignatureHelpResult?> GetSignatureHelpAsync(string filePath, int position, CancellationToken cancellationToken) => Task.FromResult<SignatureHelpResult?>(null);

    /// <inheritdoc />
    public Task<IReadOnlyList<DefinitionResult>> FindImplementationsAsync(string filePath, int position, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DefinitionResult>>([]);

    /// <inheritdoc />
    public Task<IReadOnlyList<ReferenceItem>> FindReferencesAsync(string filePath, int position, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ReferenceItem>>([]);

    /// <inheritdoc />
    public Task<IReadOnlyList<DocumentHighlight>> GetDocumentHighlightsAsync(string filePath, int position, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DocumentHighlight>>([]);

    /// <inheritdoc />
    public Task<RenameTarget> CanRenameAsync(string filePath, int position, CancellationToken cancellationToken) => Task.FromResult(RenameTarget.Unavailable("Rename is not available in WPF XAML."));

    /// <inheritdoc />
    public Task<RenameResult> GetRenameEditsAsync(string filePath, int position, string newName, RenameOptions? options, CancellationToken cancellationToken) => Task.FromResult(RenameResult.Failed("Rename is not available in WPF XAML."));

    /// <inheritdoc />
    public Task<IReadOnlyList<DocumentSymbol>> GetDocumentSymbolsAsync(string filePath, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DocumentSymbol>>([]);

    /// <inheritdoc />
    public Task<IReadOnlyList<DocumentSymbol>> GetSymbolPathAsync(string filePath, int position, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DocumentSymbol>>([]);

    private Task<T> RunAsync<T>(Func<WpfXamlFeatures, Task<T>> query, T empty, CancellationToken cancellationToken)
    {
        synchronize();
        return features() is { } engine ? RunQueryAsync(engine, query, empty, cancellationToken) : Task.FromResult(empty);
    }

    private static Task<T> RunQueryAsync<T>(WpfXamlFeatures engine, Func<WpfXamlFeatures, Task<T>> query, T empty, CancellationToken cancellationToken) =>
        Task.Run(async () =>
        {
            try
            {
                return await query(engine).ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                // Switching to a linked host or closing the workspace disposed the local host during the request.
                return empty;
            }
        }, cancellationToken);

    /// <inheritdoc />
    public Task WarmUpAsync(string filePath, CancellationToken cancellationToken) => Task.CompletedTask;
}
