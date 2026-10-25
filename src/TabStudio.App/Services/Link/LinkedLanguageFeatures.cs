using TabStudio.Compilation.Features;

namespace TabStudio.App.Services.Link;

/// <summary>IntelliSense from wherever <see cref="LinkedLanguageService"/> answers: the linked host, or this device.</summary>
public sealed class LinkedLanguageFeatures : ILanguageFeatures
{
    private readonly LinkedLanguageService _service;

    internal LinkedLanguageFeatures(LinkedLanguageService service)
    {
        _service = service;
    }

    /// <inheritdoc />
    public Task<CompletionResult> GetCompletionsAsync(string filePath, int position, CompletionTrigger trigger, CancellationToken cancellationToken) =>
        _service.CallAsync(s => s.Features.GetCompletionsAsync(filePath, position, trigger, cancellationToken));

    /// <inheritdoc />
    public bool ShouldTriggerCompletion(string filePath, int position, char typedCharacter) =>
        _service.ActiveFeatures()?.ShouldTriggerCompletion(filePath, position, typedCharacter) ?? false;

    /// <inheritdoc />
    public Task<CompletionCommit> GetCompletionChangeAsync(string filePath, CompletionEntry entry, char? commitCharacter, CancellationToken cancellationToken) =>
        _service.CallAsync(s => s.Features.GetCompletionChangeAsync(filePath, entry, commitCharacter, cancellationToken));

    /// <inheritdoc />
    public Task<IReadOnlyList<TaggedRun>> GetCompletionDescriptionAsync(CompletionEntry entry, CancellationToken cancellationToken) =>
        _service.CallAsync(s => s.Features.GetCompletionDescriptionAsync(entry, cancellationToken));

    /// <inheritdoc />
    public Task<QuickInfoResult?> GetQuickInfoAsync(string filePath, int position, CancellationToken cancellationToken) =>
        _service.CallAsync(s => s.Features.GetQuickInfoAsync(filePath, position, cancellationToken));

    /// <inheritdoc />
    public Task<SignatureHelpResult?> GetSignatureHelpAsync(string filePath, int position, CancellationToken cancellationToken) =>
        _service.CallAsync(s => s.Features.GetSignatureHelpAsync(filePath, position, cancellationToken));

    /// <inheritdoc />
    public Task<DefinitionResult?> FindDefinitionAsync(string filePath, int position, CancellationToken cancellationToken) =>
        _service.CallAsync(s => s.Features.FindDefinitionAsync(filePath, position, cancellationToken));

    /// <inheritdoc />
    public Task<IReadOnlyList<DefinitionResult>> FindImplementationsAsync(string filePath, int position, CancellationToken cancellationToken) =>
        _service.CallAsync(s => s.Features.FindImplementationsAsync(filePath, position, cancellationToken));

    /// <inheritdoc />
    public Task<IReadOnlyList<ReferenceItem>> FindReferencesAsync(string filePath, int position, CancellationToken cancellationToken) =>
        _service.CallAsync(s => s.Features.FindReferencesAsync(filePath, position, cancellationToken));

    /// <inheritdoc />
    public Task<IReadOnlyList<DocumentHighlight>> GetDocumentHighlightsAsync(string filePath, int position, CancellationToken cancellationToken) =>
        _service.CallAsync(s => s.Features.GetDocumentHighlightsAsync(filePath, position, cancellationToken));

    /// <inheritdoc />
    public Task<RenameTarget> CanRenameAsync(string filePath, int position, CancellationToken cancellationToken) =>
        _service.CallAsync(s => s.Features.CanRenameAsync(filePath, position, cancellationToken));

    /// <inheritdoc />
    public Task<RenameResult> GetRenameEditsAsync(string filePath, int position, string newName, RenameOptions? options, CancellationToken cancellationToken) =>
        _service.CallAsync(s => s.Features.GetRenameEditsAsync(filePath, position, newName, options, cancellationToken));

    /// <inheritdoc />
    public Task<IReadOnlyList<CodeActionItem>> GetCodeActionsAsync(string filePath, int start, int length, CancellationToken cancellationToken) =>
        _service.CallAsync(s => s.Features.GetCodeActionsAsync(filePath, start, length, cancellationToken));

    /// <inheritdoc />
    public Task<IReadOnlyList<DocumentEdit>> GetCodeActionEditsAsync(string id, CancellationToken cancellationToken) =>
        _service.CallAsync(s => s.Features.GetCodeActionEditsAsync(id, cancellationToken));

    /// <inheritdoc />
    public Task<IReadOnlyList<DocumentSymbol>> GetDocumentSymbolsAsync(string filePath, CancellationToken cancellationToken) =>
        _service.CallAsync(s => s.Features.GetDocumentSymbolsAsync(filePath, cancellationToken));

    /// <inheritdoc />
    public Task<IReadOnlyList<DocumentSymbol>> GetSymbolPathAsync(string filePath, int position, CancellationToken cancellationToken) =>
        _service.CallAsync(s => s.Features.GetSymbolPathAsync(filePath, position, cancellationToken));

    /// <inheritdoc />
    public Task WarmUpAsync(string filePath, CancellationToken cancellationToken) =>
        _service.CallAsync(async s =>
        {
            await s.Features.WarmUpAsync(filePath, cancellationToken).ConfigureAwait(false);
            return true;
        });
}
