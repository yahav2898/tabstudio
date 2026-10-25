using System.Text.Json;
using TabStudio.LanguageServers.Documents;
using TabStudio.LanguageServers.Features;
using TabStudio.LanguageServers.JsonRpc;
using TabStudio.LanguageServers.Protocol;

namespace TabStudio.LanguageServers.Hosting;

/// <summary>The requests about the document: IntelliSense, navigation, editing and the editor's decorations.</summary>
public sealed partial class LanguageServerDocument
{
    /// <summary>The completion list at an offset.</summary>
    /// <param name="offset">The caret, after the typed character when there is one.</param>
    /// <param name="trigger">What opened the list.</param>
    /// <param name="character">The typed trigger character, for <see cref="LspCompletionTriggerKind.TriggerCharacter"/>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The list, or <see langword="null"/> when there is none.</returns>
    public Task<LspCompletionList?> GetCompletionsAsync(int offset, LspCompletionTriggerKind trigger, char? character, CancellationToken cancellationToken) =>
        RequestAsync<LspCompletionList?>("textDocument/completion", c => c.Completion, async (connection, snapshot) =>
        {
            var context = trigger == LspCompletionTriggerKind.TriggerCharacter && character is { } typed
                ? new CompletionContextDto(LspValues.CompletionTriggerKind.TriggerCharacter, new string(typed, 1))
                : new CompletionContextDto(LspValues.CompletionTriggerKind.Invoked, null);
            var result = await connection.SendRequestAsync(
                "textDocument/completion",
                new CompletionParamsDto(Identifier(), LspConvert.ToPosition(snapshot, offset), context),
                LspJsonContext.Default.CompletionParamsDto,
                LspJsonContext.Default.JsonElement,
                cancellationToken).ConfigureAwait(false);
            return CompletionConverter.ToList(result, snapshot, offset, Capabilities.CompletionCommitCharacters);
        }, null, cancellationToken);

    /// <summary>Fills in what a server leaves out of completion items until they are resolved, such as documentation.</summary>
    /// <param name="item">An item of a list from this document.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The resolved item, or the item itself when there is nothing to resolve.</returns>
    public Task<LspCompletionItem> ResolveCompletionAsync(LspCompletionItem item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        return RequestAsync("completionItem/resolve", c => c.CompletionResolve, async (connection, snapshot) =>
        {
            var resolved = await connection.SendRequestAsync(
                "completionItem/resolve",
                item.Raw,
                LspJsonContext.Default.JsonElement,
                LspJsonContext.Default.JsonElement,
                cancellationToken).ConfigureAwait(false);
            return CompletionConverter.WithResolved(item, resolved, snapshot);
        }, item, cancellationToken);
    }

    /// <summary>The hover tooltip at an offset.</summary>
    /// <param name="offset">A position on or just after the symbol.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The tooltip, or <see langword="null"/> when there is none.</returns>
    public Task<LspHover?> GetHoverAsync(int offset, CancellationToken cancellationToken) =>
        RequestAsync<LspHover?>("textDocument/hover", c => c.Hover, async (connection, snapshot) =>
        {
            var hover = await connection.SendRequestAsync(
                "textDocument/hover",
                PositionParameters(snapshot, offset),
                LspJsonContext.Default.TextDocumentPositionParamsDto,
                LspJsonContext.Default.HoverDto,
                cancellationToken).ConfigureAwait(false);
            if (hover is null || LspConvert.ToMarkup(hover.Contents, stringIsMarkdown: true) is not { IsEmpty: false } contents)
            {
                return null;
            }

            TextSpan? span = hover.Range is { } range ? LspConvert.ToSpan(snapshot, range) : null;
            return new LspHover(contents, span);
        }, null, cancellationToken);

    /// <summary>Signature help at an offset.</summary>
    /// <param name="offset">The caret.</param>
    /// <param name="triggerCharacter">The typed character that asked for it, if any.</param>
    /// <param name="isRetrigger">Whether signature help is already shown and is being updated.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The signatures, or <see langword="null"/> outside a call.</returns>
    public Task<LspSignatureHelp?> GetSignatureHelpAsync(int offset, char? triggerCharacter, bool isRetrigger, CancellationToken cancellationToken) =>
        RequestAsync<LspSignatureHelp?>("textDocument/signatureHelp", c => c.SignatureHelp, async (connection, snapshot) =>
        {
            var context = triggerCharacter is { } typed
                ? new SignatureHelpContextDto(LspValues.SignatureHelpTriggerKind.TriggerCharacter, new string(typed, 1), isRetrigger)
                : new SignatureHelpContextDto(LspValues.SignatureHelpTriggerKind.Invoked, null, isRetrigger);
            var help = await connection.SendRequestAsync(
                "textDocument/signatureHelp",
                new SignatureHelpParamsDto(Identifier(), LspConvert.ToPosition(snapshot, offset), context),
                LspJsonContext.Default.SignatureHelpParamsDto,
                LspJsonContext.Default.SignatureHelpDto,
                cancellationToken).ConfigureAwait(false);
            return LspConvert.ToSignatureHelp(help);
        }, null, cancellationToken);

    /// <summary>Where the symbol at an offset is defined (Go to Definition).</summary>
    /// <param name="offset">A position on the symbol.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task<IReadOnlyList<LspLocation>> GetDefinitionsAsync(int offset, CancellationToken cancellationToken) =>
        GetLocationsAsync("textDocument/definition", c => c.Definition, offset, cancellationToken);

    /// <summary>Where the symbol at an offset is declared (Go to Declaration).</summary>
    /// <param name="offset">A position on the symbol.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task<IReadOnlyList<LspLocation>> GetDeclarationsAsync(int offset, CancellationToken cancellationToken) =>
        GetLocationsAsync("textDocument/declaration", c => c.Declaration, offset, cancellationToken);

    /// <summary>Where the type of the symbol at an offset is defined (Go to Type Definition).</summary>
    /// <param name="offset">A position on the symbol.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task<IReadOnlyList<LspLocation>> GetTypeDefinitionsAsync(int offset, CancellationToken cancellationToken) =>
        GetLocationsAsync("textDocument/typeDefinition", c => c.TypeDefinition, offset, cancellationToken);

    /// <summary>The implementations of the symbol at an offset (Go to Implementation).</summary>
    /// <param name="offset">A position on the symbol.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task<IReadOnlyList<LspLocation>> GetImplementationsAsync(int offset, CancellationToken cancellationToken) =>
        GetLocationsAsync("textDocument/implementation", c => c.Implementation, offset, cancellationToken);

    /// <summary>Every reference to the symbol at an offset (Find All References).</summary>
    /// <param name="offset">A position on the symbol.</param>
    /// <param name="includeDeclaration">Whether the declaration is included.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    public Task<IReadOnlyList<LspLocation>> FindReferencesAsync(int offset, bool includeDeclaration, CancellationToken cancellationToken) =>
        RequestAsync<IReadOnlyList<LspLocation>>("textDocument/references", c => c.References, async (connection, snapshot) =>
        {
            var result = await connection.SendRequestAsync(
                "textDocument/references",
                new ReferenceParamsDto(Identifier(), LspConvert.ToPosition(snapshot, offset), new ReferenceContextDto(includeDeclaration)),
                LspJsonContext.Default.ReferenceParamsDto,
                LspJsonContext.Default.JsonElement,
                cancellationToken).ConfigureAwait(false);
            return LspConvert.ToLocations(result, _server.Files);
        }, [], cancellationToken);

    /// <summary>The occurrences in the document of the symbol at an offset.</summary>
    /// <param name="offset">The caret.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task<IReadOnlyList<LspHighlight>> GetHighlightsAsync(int offset, CancellationToken cancellationToken) =>
        RequestAsync<IReadOnlyList<LspHighlight>>("textDocument/documentHighlight", c => c.DocumentHighlight, async (connection, snapshot) =>
        {
            var result = await connection.SendRequestAsync(
                "textDocument/documentHighlight",
                PositionParameters(snapshot, offset),
                LspJsonContext.Default.TextDocumentPositionParamsDto,
                LspJsonContext.Default.JsonElement,
                cancellationToken).ConfigureAwait(false);
            var highlights = new List<LspHighlight>();
            foreach (var item in ItemsOf(result))
            {
                if (item.Deserialize(LspJsonContext.Default.DocumentHighlightDto) is { Range: { } range } highlight)
                {
                    var kind = highlight.Kind switch
                    {
                        LspValues.DocumentHighlightKind.Read => LspHighlightKind.Read,
                        LspValues.DocumentHighlightKind.Write => LspHighlightKind.Write,
                        _ => LspHighlightKind.Text,
                    };
                    highlights.Add(new LspHighlight(LspConvert.ToSpan(snapshot, range), kind));
                }
            }

            return highlights;
        }, [], cancellationToken);

    /// <summary>The symbols declared in the document, as a tree.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task<IReadOnlyList<LspDocumentSymbol>> GetSymbolsAsync(CancellationToken cancellationToken) =>
        RequestAsync<IReadOnlyList<LspDocumentSymbol>>("textDocument/documentSymbol", c => c.DocumentSymbol, async (connection, snapshot) =>
        {
            var result = await connection.SendRequestAsync(
                "textDocument/documentSymbol",
                new DocumentParamsDto(Identifier()),
                LspJsonContext.Default.DocumentParamsDto,
                LspJsonContext.Default.JsonElement,
                cancellationToken).ConfigureAwait(false);
            return LspConvert.ToDocumentSymbols(result, snapshot);
        }, [], cancellationToken);

    /// <summary>What Rename would rename at an offset.</summary>
    /// <param name="offset">The caret.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The name and where it is, or <see langword="null"/> when nothing there can be renamed.</returns>
    /// <exception cref="JsonRpcException">The server refused, with its reason as the message.</exception>
    public async Task<LspRenameTarget?> PrepareRenameAsync(int offset, CancellationToken cancellationToken)
    {
        if (await _server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false) is not { } connection || !Capabilities.Rename)
        {
            return null;
        }

        var snapshot = Snapshot;
        if (!Capabilities.PrepareRename)
        {
            return WordTarget(snapshot, offset);
        }

        JsonElement result;
        try
        {
            result = await connection.SendRequestAsync(
                "textDocument/prepareRename",
                PositionParameters(snapshot, offset),
                LspJsonContext.Default.TextDocumentPositionParamsDto,
                LspJsonContext.Default.JsonElement,
                cancellationToken).ConfigureAwait(false);
        }
        catch (JsonRpcException ex) when (IsTooLate(ex))
        {
            return null;
        }

        return ToRenameTarget(result, snapshot, offset);
    }

    /// <summary>The edits that rename the symbol at an offset.</summary>
    /// <param name="offset">The caret.</param>
    /// <param name="newName">The new name.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The edits, or <see langword="null"/> when the server has none.</returns>
    /// <exception cref="JsonRpcException">The server refused, with its reason as the message.</exception>
    public async Task<LspWorkspaceEdit?> RenameAsync(int offset, string newName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(newName);
        if (await _server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false) is not { } connection || !Capabilities.Rename)
        {
            return null;
        }

        var snapshot = Snapshot;
        try
        {
            var edit = await connection.SendRequestAsync(
                "textDocument/rename",
                new RenameParamsDto(Identifier(), LspConvert.ToPosition(snapshot, offset), newName),
                LspJsonContext.Default.RenameParamsDto,
                LspJsonContext.Default.WorkspaceEditDto,
                cancellationToken).ConfigureAwait(false);
            return edit is null ? null : LspConvert.ToWorkspaceEdit(edit, _server.Files);
        }
        catch (JsonRpcException ex) when (IsTooLate(ex))
        {
            return null;
        }
    }

    /// <summary>The edits that format the whole document.</summary>
    /// <param name="tabSize">The width of a tab.</param>
    /// <param name="insertSpaces">Whether to indent with spaces.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task<IReadOnlyList<LspTextEdit>> FormatAsync(int tabSize, bool insertSpaces, CancellationToken cancellationToken) =>
        RequestAsync<IReadOnlyList<LspTextEdit>>("textDocument/formatting", c => c.Formatting, async (connection, snapshot) =>
        {
            var edits = await connection.SendRequestAsync(
                "textDocument/formatting",
                new DocumentFormattingParamsDto(Identifier(), new FormattingOptionsDto(tabSize, insertSpaces, true, null)),
                LspJsonContext.Default.DocumentFormattingParamsDto,
                LspJsonContext.Default.IReadOnlyListTextEditDto,
                cancellationToken).ConfigureAwait(false);
            return LspConvert.ToTextEdits(edits ?? [], snapshot);
        }, [], cancellationToken);

    /// <summary>The edits that format the code around a character just typed, such as <c>}</c>.</summary>
    /// <param name="offset">The caret, just after the character.</param>
    /// <param name="character">The character.</param>
    /// <param name="tabSize">The width of a tab.</param>
    /// <param name="insertSpaces">Whether to indent with spaces.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task<IReadOnlyList<LspTextEdit>> FormatOnTypeAsync(int offset, char character, int tabSize, bool insertSpaces, CancellationToken cancellationToken) =>
        RequestAsync<IReadOnlyList<LspTextEdit>>("textDocument/onTypeFormatting", c => c.OnTypeFormatting, async (connection, snapshot) =>
        {
            var edits = await connection.SendRequestAsync(
                "textDocument/onTypeFormatting",
                new DocumentOnTypeFormattingParamsDto(Identifier(), LspConvert.ToPosition(snapshot, offset), new string(character, 1), new FormattingOptionsDto(tabSize, insertSpaces, true, null)),
                LspJsonContext.Default.DocumentOnTypeFormattingParamsDto,
                LspJsonContext.Default.IReadOnlyListTextEditDto,
                cancellationToken).ConfigureAwait(false);
            return LspConvert.ToTextEdits(edits ?? [], snapshot);
        }, [], cancellationToken);

    /// <summary>The quick fixes and refactorings for a range, with the diagnostics in it.</summary>
    /// <param name="span">The selection, or an empty span at the caret.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task<IReadOnlyList<LspCodeAction>> GetCodeActionsAsync(TextSpan span, CancellationToken cancellationToken) =>
        RequestAsync<IReadOnlyList<LspCodeAction>>("textDocument/codeAction", c => c.CodeAction, async (connection, snapshot) =>
        {
            var diagnostics = Diagnostics
                .Where(d => d.Span.Start <= span.End && span.Start <= d.Span.End)
                .Select(d => d.Raw)
                .ToList();
            var parameters = new CodeActionParamsDto(
                Identifier(),
                LspConvert.ToRange(snapshot, span),
                new CodeActionContextDto(diagnostics, null, LspValues.CodeActionTriggerKind.Invoked));
            var result = await connection.SendRequestAsync(
                "textDocument/codeAction",
                parameters,
                LspJsonContext.Default.CodeActionParamsDto,
                LspJsonContext.Default.JsonElement,
                cancellationToken).ConfigureAwait(false);
            return ToCodeActions(result);
        }, [], cancellationToken);

    /// <summary>
    /// What a code action changes: its edit, resolved from the server when it was left out. An action that is only a
    /// command runs it, and its edit is what the server sends back while it runs.
    /// </summary>
    /// <param name="action">An action from <see cref="GetCodeActionsAsync"/>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <remarks>An action with both an edit and a command runs the command after its edit is applied, with <see cref="RunCodeActionCommandAsync"/>.</remarks>
    public async Task<LspWorkspaceEdit> GetCodeActionEditAsync(LspCodeAction action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (await _server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false) is not { } connection)
        {
            return LspWorkspaceEdit.Empty;
        }

        try
        {
            var edit = action.Edit;
            if (edit is null && !action.IsBareCommand && Capabilities.CodeActionResolve)
            {
                var resolved = await connection.SendRequestAsync(
                    "codeAction/resolve",
                    action.Raw,
                    LspJsonContext.Default.JsonElement,
                    LspJsonContext.Default.CodeActionDto,
                    cancellationToken).ConfigureAwait(false);
                if (resolved?.Edit is { } resolvedEdit)
                {
                    edit = LspConvert.ToWorkspaceEdit(resolvedEdit, _server.Files);
                }

                if (resolved?.Command is { Command: { } name } command)
                {
                    action.ResolvedCommand = new LspCommand(command.Title ?? name, name, command.Arguments);
                }
            }

            action.ResolvedEdit = edit;
            if (edit is not null)
            {
                return edit;
            }

            if (action.EffectiveCommand is not { } run || !Capabilities.ExecuteCommand)
            {
                return LspWorkspaceEdit.Empty;
            }

            var captured = await _server.ExecuteCommandCapturingEditsAsync(connection, run, cancellationToken).ConfigureAwait(false);
            action.CommandRan = true;
            return new LspWorkspaceEdit([.. captured.SelectMany(e => e.Changes)]);
        }
        catch (JsonRpcException ex) when (IsTooLate(ex))
        {
            return LspWorkspaceEdit.Empty;
        }
        catch (JsonRpcException ex)
        {
            _server.LogRequestFailure("codeAction/resolve", ex);
            return LspWorkspaceEdit.Empty;
        }
    }

    /// <summary>
    /// Runs the command of a code action that has both an edit and a command, once its edit was applied. Edits the
    /// command makes reach the host's <see cref="LanguageServerHost.ApplyEditHandler"/>.
    /// </summary>
    /// <param name="action">The action, after <see cref="GetCodeActionEditAsync"/>.</param>
    /// <param name="cancellationToken">Cancels the command.</param>
    public async Task RunCodeActionCommandAsync(LspCodeAction action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (action.CommandRan || action.ResolvedEdit is null || action.EffectiveCommand is not { } command || !Capabilities.ExecuteCommand
            || await _server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false) is not { } connection)
        {
            return;
        }

        try
        {
            action.CommandRan = true;
            await connection.SendRequestAsync(
                "workspace/executeCommand",
                new ExecuteCommandParamsDto(command.Name, command.Arguments),
                LspJsonContext.Default.ExecuteCommandParamsDto,
                LspJsonContext.Default.JsonElement,
                cancellationToken).ConfigureAwait(false);
        }
        catch (JsonRpcException ex) when (!IsTooLate(ex))
        {
            _server.LogRequestFailure("workspace/executeCommand", ex);
        }
        catch (JsonRpcException)
        {
            // The server stopped, or the text changed; the command no longer applies.
        }
    }

    /// <summary>The semantic tokens of the whole document.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The tokens, or <see langword="null"/> when the server has none.</returns>
    public Task<IReadOnlyList<LspSemanticToken>?> GetSemanticTokensAsync(CancellationToken cancellationToken) =>
        RequestAsync<IReadOnlyList<LspSemanticToken>?>("textDocument/semanticTokens/full", c => c.SemanticTokens, async (connection, snapshot) =>
        {
            var tokens = await connection.SendRequestAsync(
                "textDocument/semanticTokens/full",
                new DocumentParamsDto(Identifier()),
                LspJsonContext.Default.DocumentParamsDto,
                LspJsonContext.Default.SemanticTokensDto,
                cancellationToken).ConfigureAwait(false);
            var capabilities = Capabilities;
            return tokens?.Data is { } data
                ? LspConvert.ToSemanticTokens(data, snapshot, capabilities.SemanticTokenTypes, capabilities.SemanticTokenModifiers)
                : null;
        }, null, cancellationToken);

    /// <summary>The inlay hints in a range.</summary>
    /// <param name="span">The range, usually the whole document.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task<IReadOnlyList<LspInlayHint>> GetInlayHintsAsync(TextSpan span, CancellationToken cancellationToken) =>
        RequestAsync<IReadOnlyList<LspInlayHint>>("textDocument/inlayHint", c => c.InlayHint, async (connection, snapshot) =>
        {
            var result = await connection.SendRequestAsync(
                "textDocument/inlayHint",
                new InlayHintParamsDto(Identifier(), LspConvert.ToRange(snapshot, span)),
                LspJsonContext.Default.InlayHintParamsDto,
                LspJsonContext.Default.JsonElement,
                cancellationToken).ConfigureAwait(false);
            var hints = new List<LspInlayHint>();
            foreach (var item in ItemsOf(result))
            {
                if (item.Deserialize(LspJsonContext.Default.InlayHintDto) is { } dto && LspConvert.ToInlayHint(dto, snapshot) is { } hint)
                {
                    hints.Add(hint);
                }
            }

            return hints;
        }, [], cancellationToken);

    /// <summary>The ranges of lines that can be folded.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task<IReadOnlyList<LspFoldingRange>> GetFoldingRangesAsync(CancellationToken cancellationToken) =>
        RequestAsync<IReadOnlyList<LspFoldingRange>>("textDocument/foldingRange", c => c.FoldingRange, async (connection, snapshot) =>
        {
            var result = await connection.SendRequestAsync(
                "textDocument/foldingRange",
                new DocumentParamsDto(Identifier()),
                LspJsonContext.Default.DocumentParamsDto,
                LspJsonContext.Default.JsonElement,
                cancellationToken).ConfigureAwait(false);
            var ranges = new List<LspFoldingRange>();
            foreach (var item in ItemsOf(result))
            {
                if (item.Deserialize(LspJsonContext.Default.FoldingRangeDto) is { } range && range.EndLine > range.StartLine)
                {
                    ranges.Add(new LspFoldingRange(range.StartLine, range.EndLine, range.Kind));
                }
            }

            return ranges;
        }, [], cancellationToken);

    /// <summary>Waits for the server, sends a request it supports, and answers <paramref name="fallback"/> when that fails.</summary>
    private async Task<T> RequestAsync<T>(
        string method,
        Func<LanguageServerCapabilities, bool> isSupported,
        Func<JsonRpcConnection, TextSnapshot, Task<T>> send,
        T fallback,
        CancellationToken cancellationToken)
    {
        if (await _server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false) is not { } connection || !isSupported(Capabilities))
        {
            return fallback;
        }

        try
        {
            return await send(connection, Snapshot).ConfigureAwait(false);
        }
        catch (JsonRpcException ex) when (IsTooLate(ex))
        {
            return fallback;
        }
        catch (Exception ex) when (ex is JsonRpcException or JsonException or InvalidOperationException)
        {
            _server.LogRequestFailure(method, ex);
            return fallback;
        }
    }

    /// <summary>Errors that only mean the answer would be too late: the text changed, the request was cancelled, or the server stopped.</summary>
    private static bool IsTooLate(JsonRpcException error) => error.Code is JsonRpcErrorCodes.ContentModified
        or JsonRpcErrorCodes.RequestCancelled
        or JsonRpcErrorCodes.ServerCancelled
        or JsonRpcErrorCodes.ConnectionClosed;

    private TextDocumentIdentifierDto Identifier() => new(Uri);

    private TextDocumentPositionParamsDto PositionParameters(TextSnapshot snapshot, int offset) =>
        new(Identifier(), LspConvert.ToPosition(snapshot, offset));

    private Task<IReadOnlyList<LspLocation>> GetLocationsAsync(string method, Func<LanguageServerCapabilities, bool> isSupported, int offset, CancellationToken cancellationToken) =>
        RequestAsync<IReadOnlyList<LspLocation>>(method, isSupported, async (connection, snapshot) =>
        {
            var result = await connection.SendRequestAsync(
                method,
                PositionParameters(snapshot, offset),
                LspJsonContext.Default.TextDocumentPositionParamsDto,
                LspJsonContext.Default.JsonElement,
                cancellationToken).ConfigureAwait(false);
            return LspConvert.ToLocations(result, _server.Files);
        }, [], cancellationToken);

    private List<LspCodeAction> ToCodeActions(JsonElement result)
    {
        var actions = new List<LspCodeAction>();
        foreach (var item in ItemsOf(result))
        {
            try
            {
                if (ToCodeAction(item) is { } action)
                {
                    actions.Add(action);
                }
            }
            catch (JsonException)
            {
                // An action the server described wrongly is left out.
            }
        }

        return actions;
    }

    /// <summary>A <c>Command | CodeAction</c>: a bare command has a string <c>command</c>, an action an object one.</summary>
    private LspCodeAction? ToCodeAction(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (item.TryGetProperty("command", out var commandProperty) && commandProperty.ValueKind == JsonValueKind.String)
        {
            if (item.Deserialize(LspJsonContext.Default.CommandDto) is not { Command: { } bareName } bare)
            {
                return null;
            }

            return new LspCodeAction(item)
            {
                Title = bare.Title ?? bareName,
                Command = new LspCommand(bare.Title ?? bareName, bareName, bare.Arguments),
                IsBareCommand = true,
            };
        }

        if (item.Deserialize(LspJsonContext.Default.CodeActionDto) is not { Title: { } title } action)
        {
            return null;
        }

        LspCommand? command = null;
        if (action.Command is { Command: { } commandName } commandDto)
        {
            command = new LspCommand(commandDto.Title ?? commandName, commandName, commandDto.Arguments);
        }

        return new LspCodeAction(item)
        {
            Title = title,
            Kind = action.Kind,
            IsPreferred = action.IsPreferred == true,
            DiagnosticCodes = (action.Diagnostics ?? []).Select(d => LspConvert.CodeText(d.Code)).OfType<string>().ToList(),
            DisabledReason = action.Disabled?.Reason,
            Edit = action.Edit is null ? null : LspConvert.ToWorkspaceEdit(action.Edit, _server.Files),
            Command = command,
        };
    }

    private static List<JsonElement> ItemsOf(JsonElement result) =>
        result.ValueKind == JsonValueKind.Array ? [.. result.EnumerateArray()] : [];

    private static LspRenameTarget? WordTarget(TextSnapshot snapshot, int offset)
    {
        var text = snapshot.Text;
        var start = Math.Clamp(offset, 0, text.Length);
        var end = start;
        while (start > 0 && IsNameCharacter(text[start - 1]))
        {
            start--;
        }

        while (end < text.Length && IsNameCharacter(text[end]))
        {
            end++;
        }

        return end > start ? new LspRenameTarget(TextSpan.FromBounds(start, end), text[start..end]) : null;
    }

    private static bool IsNameCharacter(char character) => char.IsLetterOrDigit(character) || character is '_' or '$';

    private static LspRenameTarget? ToRenameTarget(JsonElement result, TextSnapshot snapshot, int offset)
    {
        if (result.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (result.TryGetProperty("defaultBehavior", out _))
        {
            return WordTarget(snapshot, offset);
        }

        var rangeElement = result.TryGetProperty("range", out var nested) ? nested : result;
        if (rangeElement.Deserialize(LspJsonContext.Default.RangeDto) is not { Start: not null, End: not null } range)
        {
            return null;
        }

        var span = LspConvert.ToSpan(snapshot, range);
        var placeholder = result.TryGetProperty("placeholder", out var text) && text.ValueKind == JsonValueKind.String
            ? text.GetString() ?? string.Empty
            : snapshot.Text.Substring(span.Start, span.Length);
        return new LspRenameTarget(span, placeholder);
    }
}
