using System.Text.Json;
using System.Text.Json.Nodes;
using TabStudio.LanguageServers.Documents;
using TabStudio.LanguageServers.Features;
using TabStudio.LanguageServers.JsonRpc;
using TabStudio.LanguageServers.Tests.Fakes;

namespace TabStudio.LanguageServers.Tests.Hosting;

public sealed class LanguageFeatureTests
{
    private const string Code = "import os\n\ndef greet(name, times):\n    return name * times\n\ngreet('a', 2)\npr\n";

    [Fact]
    public async Task Completion_applies_item_defaults_expands_snippets_and_reads_commit_characters()
    {
        await using var workspace = new FakeWorkspace(server =>
        {
            server.Capabilities["completionProvider"] = new JsonObject { ["triggerCharacters"] = new JsonArray("."), ["allCommitCharacters"] = new JsonArray(";") };
            server.On("textDocument/completion", _ => new JsonObject
            {
                ["isIncomplete"] = false,
                ["itemDefaults"] = new JsonObject { ["editRange"] = FakeWorkspace.Range(6, 0, 6, 2), ["commitCharacters"] = new JsonArray(".") },
                ["items"] = new JsonArray(
                    new JsonObject { ["label"] = "print", ["kind"] = 3, ["insertTextFormat"] = 2, ["insertText"] = "print(${1:value})$0", ["sortText"] = "0" },
                    new JsonObject
                    {
                        ["label"] = "property",
                        ["kind"] = 7,
                        ["labelDetails"] = new JsonObject { ["description"] = "builtins" },
                        ["textEdit"] = new JsonObject { ["range"] = FakeWorkspace.Range(6, 0, 6, 2), ["newText"] = "property" },
                        ["commitCharacters"] = new JsonArray("("),
                        ["documentation"] = new JsonObject { ["kind"] = "markdown", ["value"] = "**Decorator**" },
                    }),
            });
        });
        var (document, server) = await workspace.OpenAsync("main.py", Code);

        var list = await document.GetCompletionsAsync(Code.Length - 1, LspCompletionTriggerKind.Invoked, null, CancellationToken.None);

        Assert.NotNull(list);
        Assert.Equal(new[] { "print", "property" }, list.Items.Select(i => i.Label));
        var print = list.Items[0];
        Assert.Equal(LspCompletionItemKind.Function, print.Kind);
        Assert.Equal(new LspCompletionEdit(new TextSpan(Code.Length - 3, 2), "print(value)", 6, 5, IsSnippet: true), print.Edit);
        Assert.Equal(new[] { '.' }, print.CommitCharacters);
        var property = list.Items[1];
        Assert.Equal("builtins", property.LabelDescription);
        Assert.Equal(new[] { '(' }, property.CommitCharacters);
        Assert.Equal(new MarkupText("**Decorator**", IsMarkdown: true), property.Documentation);
        Assert.Equal(new[] { '.' }, document.Capabilities.CompletionTriggerCharacters);
        var request = await server.WaitForRequestAsync("textDocument/completion");
        Assert.Equal(1, request.GetProperty("context").GetProperty("triggerKind").GetInt32());
    }

    [Fact]
    public async Task Resolving_an_item_adds_documentation_and_the_import_it_needs()
    {
        await using var workspace = new FakeWorkspace(server =>
        {
            server.Capabilities["completionProvider"] = new JsonObject { ["resolveProvider"] = true };
            server.On("textDocument/completion", _ => new JsonObject
            {
                ["itemDefaults"] = new JsonObject { ["data"] = new JsonObject { ["id"] = 7 } },
                ["items"] = new JsonArray(new JsonObject { ["label"] = "path" }),
            });
            server.On("completionItem/resolve", item => new JsonObject
            {
                ["label"] = "path",
                ["documentation"] = "Operations on pathnames.",
                ["additionalTextEdits"] = new JsonArray(new JsonObject { ["range"] = FakeWorkspace.Range(0, 0, 0, 0), ["newText"] = "import os.path\n" }),
                ["data"] = JsonNode.Parse(item.GetProperty("data").GetRawText()),
            });
        });
        var (document, server) = await workspace.OpenAsync("main.py", Code);
        var list = await document.GetCompletionsAsync(Code.Length - 1, LspCompletionTriggerKind.Invoked, null, CancellationToken.None);

        var resolved = await document.ResolveCompletionAsync(list!.Items[0], CancellationToken.None);

        Assert.Equal(new MarkupText("Operations on pathnames.", IsMarkdown: false), resolved.Documentation);
        Assert.Equal(new LspTextEdit(new TextSpan(0, 0), "import os.path\n"), Assert.Single(resolved.AdditionalEdits));
        var request = await server.WaitForRequestAsync("completionItem/resolve");
        Assert.Equal(7, request.GetProperty("data").GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Hover_reads_markdown_and_its_range()
    {
        await using var workspace = new FakeWorkspace(server =>
        {
            server.Capabilities["hoverProvider"] = true;
            server.On("textDocument/hover", _ => new JsonObject
            {
                ["contents"] = new JsonObject { ["kind"] = "markdown", ["value"] = "```python\n(function) def greet(name, times)\n```" },
                ["range"] = FakeWorkspace.Range(2, 4, 2, 9),
            });
        });
        var (document, _) = await workspace.OpenAsync("main.py", Code);

        var hover = await document.GetHoverAsync(Code.IndexOf("greet", StringComparison.Ordinal) + 1, CancellationToken.None);

        Assert.NotNull(hover);
        Assert.True(hover.Contents.IsMarkdown);
        Assert.StartsWith("```python", hover.Contents.Value, StringComparison.Ordinal);
        Assert.Equal(new TextSpan(Code.IndexOf("greet", StringComparison.Ordinal), 5), hover.Span);
    }

    [Fact]
    public async Task Marked_strings_of_older_servers_become_markdown()
    {
        await using var workspace = new FakeWorkspace(server =>
        {
            server.Capabilities["hoverProvider"] = true;
            server.On("textDocument/hover", _ => new JsonObject
            {
                ["contents"] = new JsonArray(new JsonObject { ["language"] = "python", ["value"] = "x: int" }, "The answer."),
            });
        });
        var (document, _) = await workspace.OpenAsync("main.py", Code);

        var hover = await document.GetHoverAsync(1, CancellationToken.None);

        Assert.Equal("```python\nx: int\n```\n\nThe answer.", hover?.Contents.Value);
    }

    [Fact]
    public async Task Signature_help_finds_parameters_by_offsets_and_by_text()
    {
        await using var workspace = new FakeWorkspace(server =>
        {
            server.Capabilities["signatureHelpProvider"] = new JsonObject { ["triggerCharacters"] = new JsonArray("(", ","), ["retriggerCharacters"] = new JsonArray(")") };
            server.On("textDocument/signatureHelp", _ => new JsonObject
            {
                ["signatures"] = new JsonArray(new JsonObject
                {
                    ["label"] = "greet(name: str, times: int) -> str",
                    ["documentation"] = "Greets someone.",
                    ["parameters"] = new JsonArray(
                        new JsonObject { ["label"] = new JsonArray(6, 15) },
                        new JsonObject { ["label"] = "times: int", ["documentation"] = "How often." }),
                }),
                ["activeSignature"] = 0,
                ["activeParameter"] = 1,
            });
        });
        var (document, server) = await workspace.OpenAsync("main.py", Code);

        var help = await document.GetSignatureHelpAsync(Code.IndexOf("'a', ", StringComparison.Ordinal) + 5, ',', isRetrigger: false, CancellationToken.None);

        Assert.NotNull(help);
        var signature = Assert.Single(help.Signatures);
        Assert.Equal(new LspParameter(6, 9, null), signature.Parameters[0]);
        Assert.Equal(17, signature.Parameters[1].LabelStart);
        Assert.Equal(10, signature.Parameters[1].LabelLength);
        Assert.Equal("How often.", signature.Parameters[1].Documentation?.Value);
        Assert.Equal(1, help.ActiveParameter);
        Assert.Equal(new[] { '(', ',' }, document.Capabilities.SignatureHelpTriggerCharacters);
        var request = await server.WaitForRequestAsync("textDocument/signatureHelp");
        Assert.Equal(",", request.GetProperty("context").GetProperty("triggerCharacter").GetString());
    }

    [Fact]
    public async Task Definitions_follow_links_into_files_that_are_not_open()
    {
        FakeWorkspace? created = null;
        await using var workspace = created = new FakeWorkspace(server =>
        {
            server.Capabilities["definitionProvider"] = true;
            server.Capabilities["typeDefinitionProvider"] = true;
            server.On("textDocument/definition", _ => new JsonArray(new JsonObject
            {
                ["targetUri"] = DocumentUri.FromPath(Path.Combine(created!.Root, "lib.py")),
                ["targetRange"] = FakeWorkspace.Range(0, 0, 1, 8),
                ["targetSelectionRange"] = FakeWorkspace.Range(1, 4, 1, 8),
            }));
            server.On("textDocument/typeDefinition", _ => new JsonObject
            {
                ["uri"] = DocumentUri.FromPath(Path.Combine(created!.Root, "lib.py")),
                ["range"] = FakeWorkspace.Range(0, 0, 0, 3),
            });
        });
        var library = workspace.Write("lib.py", "# lib\ndef help():\n    pass\n");
        var (document, _) = await workspace.OpenAsync("main.py", Code);

        var definition = Assert.Single(await document.GetDefinitionsAsync(5, CancellationToken.None));
        var type = Assert.Single(await document.GetTypeDefinitionsAsync(5, CancellationToken.None));

        Assert.Equal(new LspLocation(library, new TextSpan(10, 4), 2, 5), definition);
        Assert.Equal(new TextSpan(0, 3), type.Span);
        Assert.Empty(await document.GetImplementationsAsync(5, CancellationToken.None));
    }

    [Fact]
    public async Task References_and_highlights_point_at_their_ranges()
    {
        await using var workspace = new FakeWorkspace(server =>
        {
            server.Capabilities["referencesProvider"] = true;
            server.Capabilities["documentHighlightProvider"] = true;
            server.OnAsync("textDocument/references", parameters =>
            {
                var uri = parameters.GetProperty("textDocument").GetProperty("uri").GetString();
                Assert.True(parameters.GetProperty("context").GetProperty("includeDeclaration").GetBoolean());
                return Task.FromResult<JsonNode?>(new JsonArray(
                    new JsonObject { ["uri"] = uri, ["range"] = FakeWorkspace.Range(2, 4, 2, 9) },
                    new JsonObject { ["uri"] = uri, ["range"] = FakeWorkspace.Range(5, 0, 5, 5) }));
            });
            server.On("textDocument/documentHighlight", _ => new JsonArray(
                new JsonObject { ["range"] = FakeWorkspace.Range(2, 4, 2, 9), ["kind"] = 3 },
                new JsonObject { ["range"] = FakeWorkspace.Range(5, 0, 5, 5), ["kind"] = 2 }));
        });
        var (document, _) = await workspace.OpenAsync("main.py", Code);

        var references = await document.FindReferencesAsync(15, includeDeclaration: true, CancellationToken.None);
        var highlights = await document.GetHighlightsAsync(15, CancellationToken.None);

        Assert.Equal(new[] { 3, 6 }, references.Select(r => r.Line));
        Assert.Equal(new[] { LspHighlightKind.Write, LspHighlightKind.Read }, highlights.Select(h => h.Kind));
        Assert.Equal(new TextSpan(Code.IndexOf("greet('a'", StringComparison.Ordinal), 5), highlights[1].Span);
    }

    [Fact]
    public async Task Document_symbols_are_read_as_a_tree_or_as_a_flat_list()
    {
        await using var workspace = new FakeWorkspace(server =>
        {
            server.Capabilities["documentSymbolProvider"] = true;
            server.On("textDocument/documentSymbol", _ => new JsonArray(new JsonObject
            {
                ["name"] = "greet",
                ["kind"] = 12,
                ["range"] = FakeWorkspace.Range(2, 0, 3, 23),
                ["selectionRange"] = FakeWorkspace.Range(2, 4, 2, 9),
                ["children"] = new JsonArray(new JsonObject
                {
                    ["name"] = "name",
                    ["kind"] = 13,
                    ["range"] = FakeWorkspace.Range(2, 10, 2, 14),
                    ["selectionRange"] = FakeWorkspace.Range(2, 10, 2, 14),
                }),
            }));
        });
        var (document, _) = await workspace.OpenAsync("main.py", Code);

        var symbol = Assert.Single(await document.GetSymbolsAsync(CancellationToken.None));

        Assert.Equal(LspSymbolKind.Function, symbol.Kind);
        Assert.Equal(new TextSpan(Code.IndexOf("greet", StringComparison.Ordinal), 5), symbol.NameSpan);
        Assert.Equal("name", Assert.Single(symbol.Children).Name);
    }

    [Fact]
    public async Task Rename_prepares_then_reads_document_changes_and_file_operations()
    {
        FakeWorkspace? created = null;
        await using var workspace = created = new FakeWorkspace(server =>
        {
            server.Capabilities["renameProvider"] = new JsonObject { ["prepareProvider"] = true };
            server.On("textDocument/prepareRename", _ => new JsonObject { ["range"] = FakeWorkspace.Range(2, 4, 2, 9), ["placeholder"] = "greet" });
            server.On("textDocument/rename", parameters =>
            {
                var uri = parameters.GetProperty("textDocument").GetProperty("uri").GetString();
                var newName = parameters.GetProperty("newName").GetString();
                return new JsonObject
                {
                    ["documentChanges"] = new JsonArray(
                        new JsonObject
                        {
                            ["textDocument"] = new JsonObject { ["uri"] = uri, ["version"] = 1 },
                            ["edits"] = new JsonArray(
                                new JsonObject { ["range"] = FakeWorkspace.Range(5, 0, 5, 5), ["newText"] = newName },
                                new JsonObject { ["range"] = FakeWorkspace.Range(2, 4, 2, 9), ["newText"] = newName }),
                        },
                        new JsonObject { ["kind"] = "create", ["uri"] = DocumentUri.FromPath(Path.Combine(created!.Root, "new.py")) },
                        new JsonObject
                        {
                            ["textDocument"] = new JsonObject { ["uri"] = DocumentUri.FromPath(Path.Combine(created!.Root, "new.py")), ["version"] = null },
                            ["edits"] = new JsonArray(new JsonObject { ["range"] = FakeWorkspace.Range(0, 0, 0, 0), ["newText"] = "# moved\n" }),
                        }),
                };
            });
        });
        var (document, _) = await workspace.OpenAsync("main.py", Code);

        var target = await document.PrepareRenameAsync(17, CancellationToken.None);
        var edit = await document.RenameAsync(17, "welcome", CancellationToken.None);

        Assert.Equal(new LspRenameTarget(new TextSpan(Code.IndexOf("greet", StringComparison.Ordinal), 5), "greet"), target);
        Assert.NotNull(edit);
        Assert.Equal(new[] { LspFileChangeKind.Edit, LspFileChangeKind.Create, LspFileChangeKind.Edit }, edit.Changes.Select(c => c.Kind));
        Assert.Equal(new[] { Code.IndexOf("greet", StringComparison.Ordinal), Code.IndexOf("greet('a'", StringComparison.Ordinal) }, edit.Changes[0].Edits.Select(e => e.Span.Start));
        Assert.Equal(new TextSpan(0, 0), edit.Changes[2].Edits[0].Span);
        Assert.Equal(3, edit.EditCount);
    }

    [Fact]
    public async Task A_rename_the_server_refuses_says_why()
    {
        await using var workspace = new FakeWorkspace(server =>
        {
            server.Capabilities["renameProvider"] = true;
            server.On("textDocument/rename", _ => throw new JsonRpcException(JsonRpcErrorCodes.RequestFailed, "Built-ins cannot be renamed."));
        });
        var (document, _) = await workspace.OpenAsync("main.py", Code);

        var target = await document.PrepareRenameAsync(1, CancellationToken.None);
        var error = await Assert.ThrowsAsync<JsonRpcException>(() => document.RenameAsync(1, "x", CancellationToken.None));

        Assert.Equal("import", target?.Placeholder);
        Assert.Equal("Built-ins cannot be renamed.", error.Message);
    }

    [Fact]
    public async Task Formatting_edits_come_back_in_document_order()
    {
        await using var workspace = new FakeWorkspace(server =>
        {
            server.Capabilities["documentFormattingProvider"] = true;
            server.Capabilities["documentOnTypeFormattingProvider"] = new JsonObject { ["firstTriggerCharacter"] = "}", ["moreTriggerCharacter"] = new JsonArray(";") };
            server.On("textDocument/formatting", parameters =>
            {
                Assert.Equal(4, parameters.GetProperty("options").GetProperty("tabSize").GetInt32());
                return new JsonArray(
                    new JsonObject { ["range"] = FakeWorkspace.Range(5, 0, 5, 0), ["newText"] = "\n" },
                    new JsonObject { ["range"] = FakeWorkspace.Range(0, 0, 0, 0), ["newText"] = "# header\n" });
            });
        });
        var (document, _) = await workspace.OpenAsync("main.py", Code);

        var edits = await document.FormatAsync(4, insertSpaces: true, CancellationToken.None);

        Assert.Equal(new[] { 0, Code.IndexOf("greet('a'", StringComparison.Ordinal) }, edits.Select(e => e.Span.Start));
        Assert.Equal(new[] { '}', ';' }, document.Capabilities.OnTypeFormattingTriggerCharacters);
    }

    [Fact]
    public async Task Code_actions_send_the_diagnostics_in_range_and_apply_edits_commands_send_back()
    {
        FakeWorkspace? created = null;
        await using var workspace = created = new FakeWorkspace(server =>
        {
            server.Capabilities["codeActionProvider"] = true;
            server.Capabilities["executeCommandProvider"] = new JsonObject { ["commands"] = new JsonArray("organize") };
            server.On("textDocument/codeAction", parameters =>
            {
                Assert.Single(parameters.GetProperty("context").GetProperty("diagnostics").EnumerateArray());
                var uri = parameters.GetProperty("textDocument").GetProperty("uri").GetString()!;
                return new JsonArray(
                    new JsonObject
                    {
                        ["title"] = "Remove unused import",
                        ["kind"] = "quickfix",
                        ["isPreferred"] = true,
                        ["edit"] = new JsonObject { ["changes"] = new JsonObject { [uri] = new JsonArray(new JsonObject { ["range"] = FakeWorkspace.Range(0, 0, 1, 0), ["newText"] = string.Empty }) } },
                    },
                    new JsonObject { ["title"] = "Organize imports", ["command"] = "organize", ["arguments"] = new JsonArray(uri) });
            });
        });
        var (document, server) = await workspace.OpenAsync("main.py", Code);
        await server.NotifyClientAsync("textDocument/publishDiagnostics", new JsonObject
        {
            ["uri"] = DocumentUri.FromPath(document.FilePath),
            ["diagnostics"] = new JsonArray(new JsonObject { ["range"] = FakeWorkspace.Range(0, 7, 0, 9), ["message"] = "\"os\" is not accessed", ["code"] = "unused" }),
        });
        await WaitUntilAsync(() => document.Diagnostics.Count == 1);

        var actions = await document.GetCodeActionsAsync(new TextSpan(8, 0), CancellationToken.None);

        Assert.Equal(new[] { "Remove unused import", "Organize imports" }, actions.Select(a => a.Title));
        Assert.True(actions[0].IsQuickFix && actions[0].IsPreferred);
        Assert.False(actions[1].IsQuickFix);
        var fix = await document.GetCodeActionEditAsync(actions[0], CancellationToken.None);
        Assert.Equal(new TextSpan(0, 10), Assert.Single(Assert.Single(fix.Changes).Edits).Span);
        Assert.False(actions[0].HasCommandAfterEdit);
    }

    [Fact]
    public async Task A_command_action_returns_the_edits_the_server_sends_while_it_runs()
    {
        FakeLanguageServer? running = null;
        await using var workspace = new FakeWorkspace(server =>
        {
            running = server;
            server.Capabilities["codeActionProvider"] = new JsonObject { ["resolveProvider"] = false };
            server.Capabilities["executeCommandProvider"] = new JsonObject { ["commands"] = new JsonArray("sort") };
            server.On("textDocument/codeAction", parameters => new JsonArray(new JsonObject
            {
                ["title"] = "Sort imports",
                ["kind"] = "source.organizeImports",
                ["command"] = new JsonObject { ["title"] = "Sort", ["command"] = "sort", ["arguments"] = new JsonArray(parameters.GetProperty("textDocument").GetProperty("uri").GetString()) },
            }));
            server.OnAsync("workspace/executeCommand", async parameters =>
            {
                var uri = parameters.GetProperty("arguments")[0].GetString()!;
                var applied = await running!.RequestClientAsync("workspace/applyEdit", new JsonObject
                {
                    ["label"] = "Sort imports",
                    ["edit"] = new JsonObject { ["changes"] = new JsonObject { [uri] = new JsonArray(new JsonObject { ["range"] = FakeWorkspace.Range(0, 0, 0, 0), ["newText"] = "import sys\n" }) } },
                });
                Assert.True(applied.GetProperty("applied").GetBoolean());
                return null;
            });
        });
        var applyHandlerCalls = 0;
        workspace.Host.ApplyEditHandler = (_, _, _) =>
        {
            applyHandlerCalls++;
            return Task.FromResult(true);
        };
        var (document, _) = await workspace.OpenAsync("main.py", Code);

        var action = Assert.Single(await document.GetCodeActionsAsync(new TextSpan(0, 0), CancellationToken.None));
        var edit = await document.GetCodeActionEditAsync(action, CancellationToken.None);

        Assert.Equal("import sys\n", Assert.Single(Assert.Single(edit.Changes).Edits).NewText);
        Assert.Equal(0, applyHandlerCalls);
    }

    [Fact]
    public async Task An_edit_the_server_asks_for_on_its_own_goes_to_the_apply_handler()
    {
        await using var workspace = new FakeWorkspace();
        LspWorkspaceEdit? received = null;
        workspace.Host.ApplyEditHandler = (edit, label, _) =>
        {
            received = edit;
            return Task.FromResult(label == "Fix all");
        };
        var (document, server) = await workspace.OpenAsync("main.py", Code);

        var answer = await server.RequestClientAsync("workspace/applyEdit", new JsonObject
        {
            ["label"] = "Fix all",
            ["edit"] = new JsonObject { ["changes"] = new JsonObject { [DocumentUri.FromPath(document.FilePath)] = new JsonArray(new JsonObject { ["range"] = FakeWorkspace.Range(1, 0, 1, 0), ["newText"] = "\n" }) } },
        });

        Assert.True(answer.GetProperty("applied").GetBoolean());
        Assert.Equal(new TextSpan(10, 0), received?.Changes[0].Edits[0].Span);
    }

    [Fact]
    public async Task Semantic_tokens_decode_relative_positions_with_the_server_legend()
    {
        await using var workspace = new FakeWorkspace(server =>
        {
            server.Capabilities["semanticTokensProvider"] = new JsonObject
            {
                ["legend"] = new JsonObject { ["tokenTypes"] = new JsonArray("namespace", "function", "parameter"), ["tokenModifiers"] = new JsonArray("declaration", "readonly") },
                ["full"] = true,
            };
            server.On("textDocument/semanticTokens/full", _ => new JsonObject { ["data"] = new JsonArray(0, 7, 2, 0, 0, 2, 4, 5, 1, 1, 0, 6, 4, 2, 3) });
        });
        var (document, _) = await workspace.OpenAsync("main.py", Code);

        var tokens = await document.GetSemanticTokensAsync(CancellationToken.None);

        Assert.NotNull(tokens);
        Assert.Equal(new[] { "namespace", "function", "parameter" }, tokens.Select(t => t.Type));
        Assert.Equal(new TextSpan(7, 2), tokens[0].Span);
        Assert.Equal(new TextSpan(15, 5), tokens[1].Span);
        Assert.Equal(new[] { "declaration" }, tokens[1].Modifiers);
        Assert.Equal(new TextSpan(21, 4), tokens[2].Span);
        Assert.Equal(new[] { "declaration", "readonly" }, tokens[2].Modifiers);
    }

    [Fact]
    public async Task Inlay_hints_and_folding_ranges_are_read()
    {
        await using var workspace = new FakeWorkspace(server =>
        {
            server.Capabilities["inlayHintProvider"] = true;
            server.Capabilities["foldingRangeProvider"] = true;
            server.On("textDocument/inlayHint", _ => new JsonArray(
                new JsonObject { ["position"] = new JsonObject { ["line"] = 5, ["character"] = 6 }, ["label"] = new JsonArray(new JsonObject { ["value"] = "name" }, new JsonObject { ["value"] = "=" }), ["kind"] = 2, ["paddingRight"] = true },
                new JsonObject { ["position"] = new JsonObject { ["line"] = 2, ["character"] = 22 }, ["label"] = " -> str", ["kind"] = 1 }));
            server.On("textDocument/foldingRange", _ => new JsonArray(
                new JsonObject { ["startLine"] = 2, ["endLine"] = 3 },
                new JsonObject { ["startLine"] = 6, ["endLine"] = 6 }));
        });
        var (document, _) = await workspace.OpenAsync("main.py", Code);

        var hints = await document.GetInlayHintsAsync(new TextSpan(0, Code.Length), CancellationToken.None);
        var folds = await document.GetFoldingRangesAsync(CancellationToken.None);

        Assert.Equal(new LspInlayHint(Code.IndexOf("'a'", StringComparison.Ordinal), "name=", LspInlayHintKind.Parameter, false, true, null), hints[0]);
        Assert.Equal(LspInlayHintKind.Type, hints[1].Kind);
        Assert.Equal(new LspFoldingRange(2, 3, null), Assert.Single(folds));
    }

    [Fact]
    public async Task Workspace_symbols_come_from_every_running_server()
    {
        FakeWorkspace? created = null;
        await using var workspace = created = new FakeWorkspace(server =>
        {
            server.Capabilities["workspaceSymbolProvider"] = true;
            server.On("workspace/symbol", parameters => new JsonArray(
                new JsonObject
                {
                    ["name"] = parameters.GetProperty("query").GetString() + "_found",
                    ["kind"] = 12,
                    ["containerName"] = "main",
                    ["location"] = new JsonObject { ["uri"] = DocumentUri.FromPath(Path.Combine(created!.Root, "main.py")), ["range"] = FakeWorkspace.Range(2, 4, 2, 9) },
                },
                new JsonObject
                {
                    ["name"] = "Elsewhere",
                    ["kind"] = 5,
                    ["location"] = new JsonObject { ["uri"] = DocumentUri.FromPath(Path.Combine(created!.Root, "other.py")) },
                }));
        });
        await workspace.OpenAsync("main.py", Code);

        var symbols = await workspace.Host.FindWorkspaceSymbolsAsync("gre", CancellationToken.None);

        Assert.Equal(new[] { "gre_found", "Elsewhere" }, symbols.Select(s => s.Name));
        Assert.Equal(new TextSpan(Code.IndexOf("greet", StringComparison.Ordinal), 5), symbols[0].Location.Span);
        Assert.Equal(1, symbols[1].Location.Line);
    }

    [Fact]
    public async Task A_feature_the_server_lacks_answers_nothing_without_asking_it()
    {
        await using var workspace = new FakeWorkspace();
        var (document, server) = await workspace.OpenAsync("main.py", Code);

        Assert.Null(await document.GetHoverAsync(1, CancellationToken.None));
        Assert.Null(await document.GetCompletionsAsync(1, LspCompletionTriggerKind.Invoked, null, CancellationToken.None));
        Assert.Empty(await document.GetDefinitionsAsync(1, CancellationToken.None));
        Assert.DoesNotContain(server.Requests, r => r.Method.StartsWith("textDocument/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Cancelling_a_request_tells_the_server()
    {
        var gate = new TaskCompletionSource<JsonNode?>();
        await using var workspace = new FakeWorkspace(server =>
        {
            server.Capabilities["hoverProvider"] = true;
            server.OnAsync("textDocument/hover", _ => gate.Task);
        });
        var (document, server) = await workspace.OpenAsync("main.py", Code);
        using var cancellation = new CancellationTokenSource();

        var hover = document.GetHoverAsync(1, cancellation.Token);
        await server.WaitForRequestAsync("textDocument/hover");
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => hover);
        await server.WaitForNotificationAsync("$/cancelRequest");
        gate.SetResult(null);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TestTimeouts.Default;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out.");
            await Task.Delay(20);
        }
    }
}
