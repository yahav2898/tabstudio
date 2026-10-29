using TabStudio.Compilation.Features;
using TabStudio.Link.Language;

namespace TabStudio.App.Services.Link;

/// <summary>IntelliSense from a TabStudio Link host. Only the per-keystroke completion trigger is decided on this device.</summary>
public sealed class RemoteLanguageFeatures : ILanguageFeatures
{
    private readonly RemoteLanguageService _service;
    private readonly LinkLanguageClient _client;

    internal RemoteLanguageFeatures(RemoteLanguageService service, LinkLanguageClient client)
    {
        _service = service;
        _client = client;
    }

    /// <inheritdoc />
    public Task<CompletionResult> GetCompletionsAsync(string filePath, int position, CompletionTrigger trigger, CancellationToken cancellationToken) =>
        _service.AskAsync(c => c.GetCompletionsAsync(filePath, position, trigger, cancellationToken));

    /// <inheritdoc />
    public bool ShouldTriggerCompletion(string filePath, int position, char typedCharacter) =>
        CompletionTriggers.ShouldTrigger(_service.LatestText(filePath), position, typedCharacter, filePath);

    /// <inheritdoc />
    public Task<CompletionCommit> GetCompletionChangeAsync(string filePath, CompletionEntry entry, char? commitCharacter, CancellationToken cancellationToken) =>
        _service.AskAsync(c => c.GetCompletionChangeAsync(filePath, entry, commitCharacter, cancellationToken));

    /// <inheritdoc />
    public Task<IReadOnlyList<TaggedRun>> GetCompletionDescriptionAsync(CompletionEntry entry, CancellationToken cancellationToken) =>
        _client.GetCompletionDescriptionAsync(entry, cancellationToken);

    /// <inheritdoc />
    public Task<QuickInfoResult?> GetQuickInfoAsync(string filePath, int position, CancellationToken cancellationToken) =>
        _service.AskAsync(c => c.GetQuickInfoAsync(filePath, position, cancellationToken));

    /// <inheritdoc />
    public Task<SignatureHelpResult?> GetSignatureHelpAsync(string filePath, int position, CancellationToken cancellationToken) =>
        _service.AskAsync(c => c.GetSignatureHelpAsync(filePath, position, cancellationToken));

    /// <inheritdoc />
    public Task<DefinitionResult?> FindDefinitionAsync(string filePath, int position, CancellationToken cancellationToken) =>
        _service.AskAsync(c => c.FindDefinitionAsync(filePath, position, cancellationToken));

    /// <inheritdoc />
    public Task<IReadOnlyList<DefinitionResult>> FindImplementationsAsync(string filePath, int position, CancellationToken cancellationToken) =>
        _service.AskAsync(c => c.FindImplementationsAsync(filePath, position, cancellationToken));

    /// <inheritdoc />
    public Task<IReadOnlyList<ReferenceItem>> FindReferencesAsync(string filePath, int position, CancellationToken cancellationToken) =>
        _service.AskAsync(c => c.FindReferencesAsync(filePath, position, cancellationToken));

    /// <inheritdoc />
    public Task<IReadOnlyList<DocumentHighlight>> GetDocumentHighlightsAsync(string filePath, int position, CancellationToken cancellationToken) =>
        _service.AskAsync(c => c.GetDocumentHighlightsAsync(filePath, position, cancellationToken));

    /// <inheritdoc />
    public Task<RenameTarget> CanRenameAsync(string filePath, int position, CancellationToken cancellationToken) =>
        _service.AskAsync(c => c.CanRenameAsync(filePath, position, cancellationToken));

    /// <inheritdoc />
    public Task<RenameResult> GetRenameEditsAsync(string filePath, int position, string newName, RenameOptions? options, CancellationToken cancellationToken) =>
        _service.AskAsync(c => c.GetRenameEditsAsync(filePath, position, newName, options, cancellationToken));

    /// <inheritdoc />
    public Task<IReadOnlyList<CodeActionItem>> GetCodeActionsAsync(string filePath, int start, int length, CancellationToken cancellationToken) =>
        _service.AskAsync(c => c.GetCodeActionsAsync(filePath, start, length, cancellationToken));

    /// <inheritdoc />
    public Task<IReadOnlyList<DocumentEdit>> GetCodeActionEditsAsync(string id, CancellationToken cancellationToken) =>
        _service.AskAsync(c => c.GetCodeActionEditsAsync(id, cancellationToken));

    /// <inheritdoc />
    public Task<IReadOnlyList<DocumentSymbol>> GetDocumentSymbolsAsync(string filePath, CancellationToken cancellationToken) =>
        _service.AskAsync(c => c.GetDocumentSymbolsAsync(filePath, cancellationToken));

    /// <inheritdoc />
    public Task<IReadOnlyList<DocumentSymbol>> GetSymbolPathAsync(string filePath, int position, CancellationToken cancellationToken) =>
        _service.AskAsync(c => c.GetSymbolPathAsync(filePath, position, cancellationToken));

    /// <inheritdoc />
    public Task WarmUpAsync(string filePath, CancellationToken cancellationToken) =>
        _service.AskAsync(async c =>
        {
            await c.WarmUpAsync(filePath, cancellationToken).ConfigureAwait(false);
            return true;
        });
}
