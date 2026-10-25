using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using TabStudio.LanguageServers.JsonRpc;

namespace TabStudio.LanguageServers.Loopback;

/// <summary>
/// A language server for <see cref="LoopbackScript"/> that runs inside TabStudio, over in-memory streams, with
/// diagnostics, completion, hover, definition, highlights, symbols and rename. It is what the self test proves the
/// editor's language server support with on a device without real servers, and what the IDE's tests use.
/// </summary>
public sealed class LoopbackLanguageServer : IAsyncDisposable
{
    private readonly InMemoryPipe _toServer = new();
    private readonly InMemoryPipe _toClient = new();
    private readonly JsonRpcConnection _connection;
    private readonly ConcurrentDictionary<string, string> _documents = new(StringComparer.Ordinal);
    private readonly TaskCompletionSource<int> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Creates the server; it answers once <see cref="Start"/> was called.</summary>
    public LoopbackLanguageServer()
    {
        _connection = new JsonRpcConnection(_toServer.Reader, _toClient.Writer);
    }

    /// <summary>What the client writes: the server's standard input.</summary>
    public Stream StandardInput => _toServer.Writer;

    /// <summary>What the client reads: the server's standard output.</summary>
    public Stream StandardOutput => _toClient.Reader;

    /// <summary>Completes with the exit code once the client sent <c>exit</c> or the server was stopped.</summary>
    public Task<int> Exited => _exited.Task;

    /// <summary>Starts answering.</summary>
    public void Start()
    {
        _connection.AddRequestHandler("initialize", (_, _) => Task.FromResult<JsonNode?>(new JsonObject
        {
            ["capabilities"] = new JsonObject
            {
                ["textDocumentSync"] = new JsonObject { ["openClose"] = true, ["change"] = 1 },
                ["completionProvider"] = new JsonObject { ["triggerCharacters"] = new JsonArray(" ") },
                ["hoverProvider"] = true,
                ["definitionProvider"] = true,
                ["referencesProvider"] = true,
                ["documentHighlightProvider"] = true,
                ["documentSymbolProvider"] = true,
                ["workspaceSymbolProvider"] = true,
                ["semanticTokensProvider"] = new JsonObject
                {
                    ["legend"] = new JsonObject
                    {
                        ["tokenTypes"] = new JsonArray("keyword", "variable", "string", "number", "comment"),
                        ["tokenModifiers"] = new JsonArray("declaration"),
                    },
                    ["full"] = true,
                },
                ["renameProvider"] = new JsonObject { ["prepareProvider"] = false },
            },
            ["serverInfo"] = new JsonObject { ["name"] = "TabStudio loopback", ["version"] = "1.0" },
        }));
        _connection.AddRequestHandler("shutdown", (_, _) => Task.FromResult<JsonNode?>(null));
        _connection.AddNotificationHandler("exit", _ => Stop(0));
        _connection.AddNotificationHandler("textDocument/didOpen", p =>
        {
            var document = p.GetProperty("textDocument");
            Update(document.GetProperty("uri").GetString()!, document.GetProperty("text").GetString()!);
        });
        _connection.AddNotificationHandler("textDocument/didChange", p =>
        {
            var uri = p.GetProperty("textDocument").GetProperty("uri").GetString()!;
            foreach (var change in p.GetProperty("contentChanges").EnumerateArray())
            {
                if (!change.TryGetProperty("range", out _))
                {
                    Update(uri, change.GetProperty("text").GetString()!);
                }
            }
        });
        _connection.AddNotificationHandler("textDocument/didClose", p =>
        {
            var uri = p.GetProperty("textDocument").GetProperty("uri").GetString()!;
            _documents.TryRemove(uri, out _);
            _ = Publish(uri, new JsonArray());
        });
        Answer("textDocument/completion", Complete);
        Answer("textDocument/hover", Hover);
        Answer("textDocument/definition", Definition);
        Answer("textDocument/references", References);
        Answer("textDocument/documentHighlight", Highlights);
        Answer("textDocument/documentSymbol", Symbols);
        Answer("textDocument/rename", Rename);
        Answer("workspace/symbol", WorkspaceSymbols);
        Answer("textDocument/semanticTokens/full", SemanticTokens);
        _connection.Start();
    }

    /// <summary>Ends the server, as if its process exited.</summary>
    /// <param name="exitCode">The exit code to report.</param>
    public void Stop(int exitCode)
    {
        _toClient.Complete();
        _toServer.Complete();
        _exited.TrySetResult(exitCode);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Stop(0);
        await _connection.DisposeAsync().ConfigureAwait(false);
        _toServer.Dispose();
        _toClient.Dispose();
    }

    private static JsonObject Range(int line, int start, int end) => new()
    {
        ["start"] = new JsonObject { ["line"] = line, ["character"] = start },
        ["end"] = new JsonObject { ["line"] = line, ["character"] = end },
    };

    private static (string Uri, int Line, int Character) Position(JsonElement parameters) =>
        (parameters.GetProperty("textDocument").GetProperty("uri").GetString()!,
         parameters.GetProperty("position").GetProperty("line").GetInt32(),
         parameters.GetProperty("position").GetProperty("character").GetInt32());

    /// <summary>The name at a position, and where it is on its line.</summary>
    private static (string Name, int Start)? NameAt(List<LoopbackStatement> statements, int line, int character)
    {
        if (line < 0 || line >= statements.Count)
        {
            return null;
        }

        var text = statements[line].Text;
        var start = Math.Clamp(character, 0, text.Length);
        var end = start;
        while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_'))
        {
            start--;
        }

        while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_'))
        {
            end++;
        }

        var name = text[start..end];
        return LoopbackScript.IsName(name) && name is not ("let" or "print") ? (name, start) : null;
    }

    /// <summary>Every place a name is written: where it is set and where it is used.</summary>
    private static IEnumerable<(int Line, int Start)> Occurrences(List<LoopbackStatement> statements, string name)
    {
        foreach (var statement in statements)
        {
            if (statement.Kind == LoopbackStatementKind.Let && statement.Name == name)
            {
                yield return (statement.Line, statement.NameColumn);
            }

            var value = statement.Kind == LoopbackStatementKind.Let ? statement.Value : statement.Kind == LoopbackStatementKind.Print ? statement.Name : string.Empty;
            var column = statement.Kind == LoopbackStatementKind.Let ? statement.ValueColumn : statement.NameColumn;
            if (value == name)
            {
                yield return (statement.Line, column);
            }
        }
    }

    private static LoopbackStatement? DefinitionOf(List<LoopbackStatement> statements, string name, int beforeLine) =>
        statements.LastOrDefault(s => s.Kind == LoopbackStatementKind.Let && s.Name == name && s.Line <= beforeLine)
        ?? statements.FirstOrDefault(s => s.Kind == LoopbackStatementKind.Let && s.Name == name);

    private void Answer(string method, Func<JsonElement, JsonNode?> answer) =>
        _connection.AddRequestHandler(method, (parameters, _) => Task.FromResult(answer(parameters)));

    private void Update(string uri, string text)
    {
        _documents[uri] = text;
        _ = Publish(uri, Diagnose(LoopbackScript.Parse(text)));
    }

    private Task Publish(string uri, JsonArray diagnostics) =>
        _connection.SendNotificationAsync("textDocument/publishDiagnostics", new JsonObject { ["uri"] = uri, ["diagnostics"] = diagnostics });

    private static JsonArray Diagnose(List<LoopbackStatement> statements)
    {
        var diagnostics = new JsonArray();
        var defined = new HashSet<string>(StringComparer.Ordinal);
        foreach (var statement in statements)
        {
            switch (statement.Kind)
            {
                case LoopbackStatementKind.Let when statement.Name.Length == 0 || statement.Value.Length == 0:
                    diagnostics.Add(Diagnostic(statement.Line, statement.Column, statement.Text.TrimEnd().Length, 1, "LB0002", "A let gives a name a value: let name = value."));
                    break;
                case LoopbackStatementKind.Let:
                    if (LoopbackScript.IsName(statement.Value) && !defined.Contains(statement.Value))
                    {
                        diagnostics.Add(Undefined(statement.Line, statement.ValueColumn, statement.Value));
                    }

                    defined.Add(statement.Name);
                    break;
                case LoopbackStatementKind.Print when LoopbackScript.IsName(statement.Name) && !defined.Contains(statement.Name):
                    diagnostics.Add(Undefined(statement.Line, statement.NameColumn, statement.Name));
                    break;
                case LoopbackStatementKind.Unknown:
                    diagnostics.Add(Diagnostic(statement.Line, statement.Column, statement.Text.TrimEnd().Length, 2, "LB0003", $"'{statement.Name.Split(' ')[0]}' is not a statement; use let or print."));
                    break;
            }
        }

        return diagnostics;
    }

    private static JsonObject Undefined(int line, int column, string name) =>
        Diagnostic(line, column, column + name.Length, 1, "LB0001", $"'{name}' is not defined.");

    private static JsonObject Diagnostic(int line, int start, int end, int severity, string code, string message) => new()
    {
        ["range"] = Range(line, start, Math.Max(start + 1, end)),
        ["severity"] = severity,
        ["code"] = code,
        ["codeDescription"] = new JsonObject { ["href"] = "https://github.com/yahav2898/tabstudio/blob/main/docs/ARCHITECTURE.md" },
        ["source"] = "loopback",
        ["message"] = message,
    };

    private List<LoopbackStatement> Statements(string uri) =>
        LoopbackScript.Parse(_documents.TryGetValue(uri, out var text) ? text : string.Empty);

    private JsonNode? Complete(JsonElement parameters)
    {
        var (uri, line, _) = Position(parameters);
        var statements = Statements(uri);
        var items = new JsonArray
        {
            new JsonObject { ["label"] = "let", ["kind"] = 14, ["detail"] = "let name = value", ["insertTextFormat"] = 2, ["insertText"] = "let ${1:name} = ${2:value}" },
            new JsonObject { ["label"] = "print", ["kind"] = 14, ["detail"] = "print value" },
        };
        foreach (var name in statements.Where(s => s.Kind == LoopbackStatementKind.Let && s.Line < line && s.Name.Length > 0).Select(s => s.Name).Distinct(StringComparer.Ordinal))
        {
            var definition = DefinitionOf(statements, name, line)!;
            items.Add(new JsonObject
            {
                ["label"] = name,
                ["kind"] = 6,
                ["detail"] = definition.Text.Trim(),
                ["documentation"] = new JsonObject { ["kind"] = "markdown", ["value"] = $"Set on line {(definition.Line + 1).ToString(CultureInfo.InvariantCulture)}." },
            });
        }

        return new JsonObject { ["isIncomplete"] = false, ["items"] = items };
    }

    private JsonNode? Hover(JsonElement parameters)
    {
        var (uri, line, character) = Position(parameters);
        var statements = Statements(uri);
        if (NameAt(statements, line, character) is not { } name || DefinitionOf(statements, name.Name, line) is not { } definition)
        {
            return null;
        }

        var type = LoopbackScript.TypeOf(definition.Value);
        return new JsonObject
        {
            ["contents"] = new JsonObject
            {
                ["kind"] = "markdown",
                ["value"] = $"```loopback\n(variable) {name.Name}: {type}\n```\n---\nSet to `{definition.Value}` on line {(definition.Line + 1).ToString(CultureInfo.InvariantCulture)}.",
            },
            ["range"] = Range(line, name.Start, name.Start + name.Name.Length),
        };
    }

    private JsonNode? Definition(JsonElement parameters)
    {
        var (uri, line, character) = Position(parameters);
        var statements = Statements(uri);
        return NameAt(statements, line, character) is { } name && DefinitionOf(statements, name.Name, line) is { } definition
            ? new JsonObject { ["uri"] = uri, ["range"] = Range(definition.Line, definition.NameColumn, definition.NameColumn + name.Name.Length) }
            : null;
    }

    private JsonNode? References(JsonElement parameters)
    {
        var (uri, line, character) = Position(parameters);
        var statements = Statements(uri);
        var result = new JsonArray();
        if (NameAt(statements, line, character) is { } name)
        {
            foreach (var (at, start) in Occurrences(statements, name.Name))
            {
                result.Add(new JsonObject { ["uri"] = uri, ["range"] = Range(at, start, start + name.Name.Length) });
            }
        }

        return result;
    }

    private JsonNode? Highlights(JsonElement parameters)
    {
        var (uri, line, character) = Position(parameters);
        var statements = Statements(uri);
        var result = new JsonArray();
        if (NameAt(statements, line, character) is { } name)
        {
            foreach (var (at, start) in Occurrences(statements, name.Name))
            {
                var isSet = statements[at].Kind == LoopbackStatementKind.Let && statements[at].NameColumn == start;
                result.Add(new JsonObject { ["range"] = Range(at, start, start + name.Name.Length), ["kind"] = isSet ? 3 : 2 });
            }
        }

        return result;
    }

    private JsonNode? Symbols(JsonElement parameters)
    {
        var uri = parameters.GetProperty("textDocument").GetProperty("uri").GetString()!;
        var result = new JsonArray();
        foreach (var statement in Statements(uri).Where(s => s.Kind == LoopbackStatementKind.Let && s.Name.Length > 0))
        {
            result.Add(new JsonObject
            {
                ["name"] = statement.Name,
                ["kind"] = 13,
                ["range"] = Range(statement.Line, statement.Column, statement.Text.TrimEnd().Length),
                ["selectionRange"] = Range(statement.Line, statement.NameColumn, statement.NameColumn + statement.Name.Length),
            });
        }

        return result;
    }

    /// <summary>The script's colors: keywords, names, strings, numbers and comments, in the protocol's relative encoding.</summary>
    private JsonNode? SemanticTokens(JsonElement parameters)
    {
        var uri = parameters.GetProperty("textDocument").GetProperty("uri").GetString()!;
        var tokens = new List<(int Line, int Start, int Length, int Type, int Modifiers)>();
        foreach (var statement in Statements(uri))
        {
            var trimmed = statement.Text.Trim();
            switch (statement.Kind)
            {
                case LoopbackStatementKind.None when trimmed.StartsWith('#'):
                    tokens.Add((statement.Line, statement.Column, trimmed.Length, 4, 0));
                    break;
                case LoopbackStatementKind.Let:
                    tokens.Add((statement.Line, statement.Column, 3, 0, 0));
                    if (statement.Name.Length > 0)
                    {
                        tokens.Add((statement.Line, statement.NameColumn, statement.Name.Length, 1, 1));
                    }

                    AddValue(tokens, statement.Line, statement.ValueColumn, statement.Value);
                    break;
                case LoopbackStatementKind.Print:
                    tokens.Add((statement.Line, statement.Column, 5, 0, 0));
                    AddValue(tokens, statement.Line, statement.NameColumn, statement.Name);
                    break;
            }
        }

        var data = new JsonArray();
        int line = 0, start = 0;
        foreach (var token in tokens)
        {
            var deltaLine = token.Line - line;
            data.Add(deltaLine);
            data.Add(deltaLine == 0 ? token.Start - start : token.Start);
            data.Add(token.Length);
            data.Add(token.Type);
            data.Add(token.Modifiers);
            line = token.Line;
            start = token.Start;
        }

        return new JsonObject { ["data"] = data };
    }

    private static void AddValue(List<(int Line, int Start, int Length, int Type, int Modifiers)> tokens, int line, int column, string value)
    {
        if (value.Length == 0)
        {
            return;
        }

        var type = value.StartsWith('"') ? 2 : double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _) ? 3 : LoopbackScript.IsName(value) ? 1 : -1;
        if (type >= 0)
        {
            tokens.Add((line, column, value.Length, type, 0));
        }
    }

    private JsonNode? WorkspaceSymbols(JsonElement parameters)
    {
        var query = parameters.TryGetProperty("query", out var value) ? value.GetString() ?? string.Empty : string.Empty;
        var result = new JsonArray();
        foreach (var (uri, text) in _documents)
        {
            foreach (var statement in LoopbackScript.Parse(text).Where(s => s.Kind == LoopbackStatementKind.Let && s.Name.Contains(query, StringComparison.OrdinalIgnoreCase)))
            {
                result.Add(new JsonObject
                {
                    ["name"] = statement.Name,
                    ["kind"] = 13,
                    ["location"] = new JsonObject { ["uri"] = uri, ["range"] = Range(statement.Line, statement.NameColumn, statement.NameColumn + statement.Name.Length) },
                });
            }
        }

        return result;
    }

    private JsonNode? Rename(JsonElement parameters)
    {
        var (uri, line, character) = Position(parameters);
        var newName = parameters.GetProperty("newName").GetString()!;
        var statements = Statements(uri);
        if (NameAt(statements, line, character) is not { } name)
        {
            return null;
        }

        var edits = new JsonArray();
        foreach (var (at, start) in Occurrences(statements, name.Name))
        {
            edits.Add(new JsonObject { ["range"] = Range(at, start, start + name.Name.Length), ["newText"] = newName });
        }

        return new JsonObject { ["changes"] = new JsonObject { [uri] = edits } };
    }
}
