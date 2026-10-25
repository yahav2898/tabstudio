using TabStudio.Compilation;
using TabStudio.Compilation.Features;

namespace TabStudio.App.Services;

/// <summary>
/// The <see cref="ILanguageFeatures"/> that runs Roslyn in TabStudio's own process, over the host of a
/// <see cref="LocalLanguageService"/>.
/// </summary>
/// <remarks>
/// Every query runs on the thread pool: Roslyn does real work before its first await, and the editor calls these
/// methods from the UI thread while the user types.
/// </remarks>
public sealed class LocalLanguageFeatures : ILanguageFeatures
{
    private readonly LanguageFeatures _features;

    /// <summary>Creates the features for a host.</summary>
    /// <param name="host">The host that holds the loaded projects.</param>
    public LocalLanguageFeatures(RoslynWorkspaceHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _features = new LanguageFeatures(host);
    }

    /// <inheritdoc />
    public Task<CompletionResult> GetCompletionsAsync(string filePath, int position, CompletionTrigger trigger, CancellationToken cancellationToken) =>
        Task.Run(() => _features.Completion.GetCompletionsAsync(filePath, position, trigger, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public bool ShouldTriggerCompletion(string filePath, int position, char typedCharacter) =>
        _features.Completion.ShouldTriggerCompletion(filePath, position, typedCharacter);

    /// <inheritdoc />
    public Task<CompletionCommit> GetCompletionChangeAsync(string filePath, CompletionEntry entry, char? commitCharacter, CancellationToken cancellationToken) =>
        Task.Run(() => _features.Completion.GetCompletionChangeAsync(filePath, entry, commitCharacter, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<TaggedRun>> GetCompletionDescriptionAsync(CompletionEntry entry, CancellationToken cancellationToken) =>
        Task.Run(() => _features.Completion.GetCompletionDescriptionAsync(entry, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<QuickInfoResult?> GetQuickInfoAsync(string filePath, int position, CancellationToken cancellationToken) =>
        Task.Run(() => _features.QuickInfo.GetQuickInfoAsync(filePath, position, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<SignatureHelpResult?> GetSignatureHelpAsync(string filePath, int position, CancellationToken cancellationToken) =>
        Task.Run(() => _features.SignatureHelp.GetSignatureHelpAsync(filePath, position, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<DefinitionResult?> FindDefinitionAsync(string filePath, int position, CancellationToken cancellationToken) =>
        Task.Run(() => _features.Navigation.FindDefinitionAsync(filePath, position, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<DefinitionResult>> FindImplementationsAsync(string filePath, int position, CancellationToken cancellationToken) =>
        Task.Run(() => _features.Navigation.FindImplementationsAsync(filePath, position, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<ReferenceItem>> FindReferencesAsync(string filePath, int position, CancellationToken cancellationToken) =>
        Task.Run(() => _features.Navigation.FindReferencesAsync(filePath, position, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<DocumentHighlight>> GetDocumentHighlightsAsync(string filePath, int position, CancellationToken cancellationToken) =>
        Task.Run(() => _features.Highlights.GetDocumentHighlightsAsync(filePath, position, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<RenameTarget> CanRenameAsync(string filePath, int position, CancellationToken cancellationToken) =>
        Task.Run(() => _features.Rename.CanRenameAsync(filePath, position, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<RenameResult> GetRenameEditsAsync(string filePath, int position, string newName, RenameOptions? options, CancellationToken cancellationToken) =>
        Task.Run(() => _features.Rename.GetRenameEditsAsync(filePath, position, newName, options, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<CodeActionItem>> GetCodeActionsAsync(string filePath, int start, int length, CancellationToken cancellationToken) =>
        Task.Run(() => _features.CodeActions.GetCodeActionsAsync(filePath, start, length, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<DocumentEdit>> GetCodeActionEditsAsync(string id, CancellationToken cancellationToken) =>
        Task.Run(() => _features.CodeActions.GetCodeActionEditsAsync(id, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<DocumentSymbol>> GetDocumentSymbolsAsync(string filePath, CancellationToken cancellationToken) =>
        Task.Run(() => _features.Symbols.GetDocumentSymbolsAsync(filePath, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<DocumentSymbol>> GetSymbolPathAsync(string filePath, int position, CancellationToken cancellationToken) =>
        Task.Run(() => _features.Symbols.GetSymbolPathAsync(filePath, position, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// Lists code actions once, which discovers Roslyn's fix and refactoring providers (about a second), then asks
    /// for completion inside a member body, which builds the index of types from namespaces that are not imported
    /// (a few seconds). Both are shared, so later requests in any file are fast.
    /// </remarks>
    public async Task WarmUpAsync(string filePath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        await GetCodeActionsAsync(filePath, 0, 0, cancellationToken).ConfigureAwait(false);

        var symbols = await GetDocumentSymbolsAsync(filePath, cancellationToken).ConfigureAwait(false);
        if (FindMemberBody(symbols) is { } position)
        {
            await GetCompletionsAsync(filePath, position, CompletionTrigger.Invoke, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// A position inside the body of the first method or property, where completion offers types: just before the
    /// closing brace or semicolon that ends the member.
    /// </summary>
    private static int? FindMemberBody(IReadOnlyList<DocumentSymbol> symbols)
    {
        foreach (var symbol in symbols)
        {
            if (symbol.Kind is SymbolGlyph.Method or SymbolGlyph.Property && symbol.Length > 1)
            {
                return symbol.Start + symbol.Length - 1;
            }

            if (FindMemberBody(symbol.Children) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }
}
