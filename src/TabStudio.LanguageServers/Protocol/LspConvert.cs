using System.Text.Json;
using TabStudio.LanguageServers.Documents;
using TabStudio.LanguageServers.Features;

namespace TabStudio.LanguageServers.Protocol;

/// <summary>
/// Converts between the protocol's shapes and TabStudio's results: positions to offsets in the text the server
/// answered about, unions to one form, and file URIs to paths.
/// </summary>
internal static class LspConvert
{
    /// <summary>The protocol position of an offset.</summary>
    public static PositionDto ToPosition(TextSnapshot snapshot, int offset)
    {
        var position = snapshot.GetPosition(offset);
        return new PositionDto(position.Line, position.Character);
    }

    /// <summary>The protocol range of a span.</summary>
    public static RangeDto ToRange(TextSnapshot snapshot, TextSpan span) =>
        new(ToPosition(snapshot, span.Start), ToPosition(snapshot, span.End));

    /// <summary>The span of a protocol range in a text.</summary>
    public static TextSpan ToSpan(TextSnapshot snapshot, RangeDto range)
    {
        var start = snapshot.GetOffset(new TextPosition(range.Start.Line, range.Start.Character));
        var end = snapshot.GetOffset(new TextPosition(range.End.Line, range.End.Character));
        return TextSpan.FromBounds(start, Math.Max(start, end));
    }

    /// <summary>A location in a file, or <see langword="null"/> when the URI is not a file.</summary>
    public static LspLocation? ToLocation(string uri, RangeDto range, ISnapshotSource files)
    {
        if (DocumentUri.ToPath(uri) is not { } path)
        {
            return null;
        }

        var snapshot = files.GetSnapshot(path);
        var span = snapshot is null ? new TextSpan(0, 0) : ToSpan(snapshot, range);
        return new LspLocation(path, span, range.Start.Line + 1, range.Start.Character + 1);
    }

    /// <summary>The locations of a <c>Location | Location[] | LocationLink[] | null</c> result.</summary>
    public static List<LspLocation> ToLocations(JsonElement result, ISnapshotSource files)
    {
        var locations = new List<LspLocation>();
        switch (result.ValueKind)
        {
            case JsonValueKind.Object:
                AddLocation(result, files, locations);
                break;
            case JsonValueKind.Array:
                foreach (var item in result.EnumerateArray())
                {
                    AddLocation(item, files, locations);
                }

                break;
        }

        return locations;
    }

    /// <summary>
    /// Text from a <c>string | MarkupContent | MarkedString | MarkedString[]</c>. A plain string is Markdown in a
    /// hover and plain text elsewhere, as the protocol says.
    /// </summary>
    public static MarkupText? ToMarkup(JsonElement? value, bool stringIsMarkdown)
    {
        if (value is not { } element)
        {
            return null;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return new MarkupText(element.GetString() ?? string.Empty, stringIsMarkdown);
            case JsonValueKind.Object when element.TryGetProperty("kind", out var kind):
                return new MarkupText(
                    element.TryGetProperty("value", out var text) ? text.GetString() ?? string.Empty : string.Empty,
                    string.Equals(kind.GetString(), "markdown", StringComparison.Ordinal));
            case JsonValueKind.Object:
                return new MarkupText(MarkedStringToMarkdown(element), true);
            case JsonValueKind.Array:
                var parts = element.EnumerateArray()
                    .Select(part => part.ValueKind == JsonValueKind.String ? part.GetString() ?? string.Empty : MarkedStringToMarkdown(part))
                    .Where(part => part.Length > 0);
                return new MarkupText(string.Join("\n\n", parts), true);
            default:
                return null;
        }
    }

    /// <summary>A diagnostic, or <see langword="null"/> when its JSON is not one.</summary>
    public static LspDiagnostic? ToDiagnostic(JsonElement raw, string filePath, TextSnapshot? snapshot)
    {
        DiagnosticDto? dto;
        try
        {
            dto = raw.Deserialize(LspJsonContext.Default.DiagnosticDto);
        }
        catch (JsonException)
        {
            return null;
        }

        if (dto?.Range is not { Start: not null, End: not null } range || dto.Message is null)
        {
            return null;
        }

        var tags = dto.Tags ?? [];
        return new LspDiagnostic(raw)
        {
            FilePath = filePath,
            Span = snapshot is null ? new TextSpan(0, 0) : ToSpan(snapshot, range),
            Line = range.Start.Line + 1,
            Column = range.Start.Character + 1,
            Severity = dto.Severity switch
            {
                LspValues.DiagnosticSeverity.Warning => LspDiagnosticSeverity.Warning,
                LspValues.DiagnosticSeverity.Information => LspDiagnosticSeverity.Information,
                LspValues.DiagnosticSeverity.Hint => LspDiagnosticSeverity.Hint,
                _ => LspDiagnosticSeverity.Error,
            },
            Code = CodeText(dto.Code),
            CodeLink = dto.CodeDescription?.Href is { } href && Uri.TryCreate(href, UriKind.Absolute, out var link) ? link : null,
            Source = dto.Source,
            Message = dto.Message,
            IsUnnecessary = tags.Contains(LspValues.DiagnosticTag.Unnecessary),
            IsDeprecated = tags.Contains(LspValues.DiagnosticTag.Deprecated),
        };
    }

    /// <summary>A diagnostic code, which is a string or a number.</summary>
    public static string? CodeText(JsonElement? code) => code switch
    {
        { ValueKind: JsonValueKind.String } text => text.GetString(),
        { ValueKind: JsonValueKind.Number } number => number.GetRawText(),
        _ => null,
    };

    /// <summary>The changes of a workspace edit, with offsets into each file's current text.</summary>
    public static LspWorkspaceEdit ToWorkspaceEdit(WorkspaceEditDto? edit, ISnapshotSource files)
    {
        if (edit is null)
        {
            return LspWorkspaceEdit.Empty;
        }

        var changes = new List<LspFileChange>();

        // Files created or renamed earlier in the same edit are edited as they will be by then.
        var pending = new Dictionary<string, TextSnapshot>(DocumentUri.PathComparer);
        TextSnapshot? SnapshotOf(string path) => pending.TryGetValue(path, out var snapshot) ? snapshot : files.GetSnapshot(path);

        if (edit.DocumentChanges is { ValueKind: JsonValueKind.Array } documentChanges)
        {
            foreach (var change in documentChanges.EnumerateArray())
            {
                if (ToFileChange(change, SnapshotOf, pending) is { } converted)
                {
                    changes.Add(converted);
                }
            }
        }
        else if (edit.Changes is { } byUri)
        {
            foreach (var (uri, edits) in byUri)
            {
                if (DocumentUri.ToPath(uri) is { } path && edits is not null)
                {
                    changes.Add(new LspFileChange(LspFileChangeKind.Edit, path, null, ToTextEdits(edits, SnapshotOf(path))));
                }
            }
        }

        return new LspWorkspaceEdit(changes);
    }

    /// <summary>Text edits as offsets into a text, in document order; edits that start together keep their order.</summary>
    public static List<LspTextEdit> ToTextEdits(IReadOnlyList<TextEditDto> edits, TextSnapshot? snapshot)
    {
        var text = snapshot ?? new TextSnapshot(string.Empty, 0);
        return edits
            .Where(e => e?.Range is { Start: not null, End: not null })
            .Select(e => new LspTextEdit(ToSpan(text, e.Range), e.NewText ?? string.Empty))
            .OrderBy(e => e.Span.Start)
            .ToList();
    }

    /// <summary>The symbols of a <c>DocumentSymbol[] | SymbolInformation[]</c> result.</summary>
    public static List<LspDocumentSymbol> ToDocumentSymbols(JsonElement result, TextSnapshot snapshot)
    {
        var symbols = new List<LspDocumentSymbol>();
        if (result.ValueKind != JsonValueKind.Array)
        {
            return symbols;
        }

        foreach (var item in result.EnumerateArray().Where(i => i.ValueKind == JsonValueKind.Object))
        {
            try
            {
                if (item.TryGetProperty("location", out _))
                {
                    if (item.Deserialize(LspJsonContext.Default.SymbolInformationDto) is { Location.Range: { } range } flat)
                    {
                        var span = ToSpan(snapshot, range);
                        symbols.Add(new LspDocumentSymbol(flat.Name, flat.ContainerName, (LspSymbolKind)flat.Kind, span, span, []));
                    }
                }
                else if (item.Deserialize(LspJsonContext.Default.DocumentSymbolDto) is { } symbol)
                {
                    symbols.Add(ToDocumentSymbol(symbol, snapshot));
                }
            }
            catch (JsonException)
            {
                // A symbol the server described wrongly is left out.
            }
        }

        return symbols;
    }

    /// <summary>Signature help.</summary>
    public static LspSignatureHelp? ToSignatureHelp(SignatureHelpDto? help)
    {
        if (help?.Signatures is not { Count: > 0 } signatures)
        {
            return null;
        }

        var converted = signatures.Select(s => ToSignature(s, help.ActiveParameter)).ToList();
        var active = Math.Clamp(help.ActiveSignature ?? 0, 0, converted.Count - 1);
        return new LspSignatureHelp(converted, active, converted[active].ActiveParameter ?? help.ActiveParameter ?? 0);
    }

    /// <summary>An inlay hint, or <see langword="null"/> when it has no text.</summary>
    public static LspInlayHint? ToInlayHint(InlayHintDto hint, TextSnapshot snapshot)
    {
        var label = hint.Label.ValueKind switch
        {
            JsonValueKind.String => hint.Label.GetString() ?? string.Empty,
            JsonValueKind.Array => string.Concat(hint.Label.EnumerateArray()
                .Select(part => part.ValueKind == JsonValueKind.Object && part.TryGetProperty("value", out var value) ? value.GetString() : null)),
            _ => string.Empty,
        };
        if (label.Length == 0 || hint.Position is null)
        {
            return null;
        }

        var kind = hint.Kind switch
        {
            LspValues.InlayHintKind.Type => LspInlayHintKind.Type,
            LspValues.InlayHintKind.Parameter => LspInlayHintKind.Parameter,
            _ => LspInlayHintKind.Other,
        };
        var offset = snapshot.GetOffset(new TextPosition(hint.Position.Line, hint.Position.Character));
        return new LspInlayHint(offset, label, kind, hint.PaddingLeft == true, hint.PaddingRight == true, ToMarkup(hint.Tooltip, stringIsMarkdown: false)?.Value);
    }

    /// <summary>Decodes semantic tokens: five integers per token, positions relative to the token before.</summary>
    public static List<LspSemanticToken> ToSemanticTokens(IReadOnlyList<int> data, TextSnapshot snapshot, IReadOnlyList<string> types, IReadOnlyList<string> modifiers)
    {
        var tokens = new List<LspSemanticToken>(data.Count / 5);
        var line = 0;
        var character = 0;
        for (var i = 0; i + 4 < data.Count; i += 5)
        {
            var deltaLine = data[i];
            line += deltaLine;
            character = deltaLine == 0 ? character + data[i + 1] : data[i + 1];
            var start = snapshot.GetOffset(new TextPosition(line, character));
            var end = snapshot.GetOffset(new TextPosition(line, character + data[i + 2]));
            var type = data[i + 3] >= 0 && data[i + 3] < types.Count ? types[data[i + 3]] : "unknown";
            tokens.Add(new LspSemanticToken(TextSpan.FromBounds(start, end), type, ModifierNames(data[i + 4], modifiers)));
        }

        return tokens;
    }

    private static List<string> ModifierNames(int bits, IReadOnlyList<string> modifiers)
    {
        if (bits == 0)
        {
            return [];
        }

        var names = new List<string>();
        for (var bit = 0; bit < modifiers.Count && bit < 31; bit++)
        {
            if ((bits & (1 << bit)) != 0)
            {
                names.Add(modifiers[bit]);
            }
        }

        return names;
    }

    private static LspSignature ToSignature(SignatureInformationDto signature, int? helpActiveParameter)
    {
        var label = signature.Label ?? string.Empty;
        var parameters = new List<LspParameter>();
        var searchFrom = 0;
        foreach (var parameter in signature.Parameters ?? [])
        {
            int start;
            int length;
            if (parameter.Label.ValueKind == JsonValueKind.Array && parameter.Label.GetArrayLength() == 2
                && parameter.Label[0].ValueKind == JsonValueKind.Number && parameter.Label[1].ValueKind == JsonValueKind.Number
                && parameter.Label[0].TryGetInt32(out var from) && parameter.Label[1].TryGetInt32(out var to))
            {
                start = Math.Clamp(from, 0, label.Length);
                length = Math.Clamp(to, start, label.Length) - start;
            }
            else
            {
                var text = parameter.Label.ValueKind == JsonValueKind.String ? parameter.Label.GetString() ?? string.Empty : string.Empty;
                start = text.Length == 0 ? -1 : label.IndexOf(text, searchFrom, StringComparison.Ordinal);
                if (start < 0)
                {
                    start = label.Length;
                    length = 0;
                }
                else
                {
                    length = text.Length;
                }
            }

            searchFrom = Math.Min(label.Length, start + length);
            parameters.Add(new LspParameter(start, length, ToMarkup(parameter.Documentation, stringIsMarkdown: false)));
        }

        return new LspSignature(label, ToMarkup(signature.Documentation, stringIsMarkdown: false), parameters, signature.ActiveParameter ?? helpActiveParameter);
    }

    private static LspDocumentSymbol ToDocumentSymbol(DocumentSymbolDto symbol, TextSnapshot snapshot)
    {
        var span = symbol.Range is null ? new TextSpan(0, 0) : ToSpan(snapshot, symbol.Range);
        var nameSpan = symbol.SelectionRange is null ? span : ToSpan(snapshot, symbol.SelectionRange);
        var children = (symbol.Children ?? []).Select(c => ToDocumentSymbol(c, snapshot)).ToList();
        return new LspDocumentSymbol(symbol.Name ?? string.Empty, symbol.Detail, (LspSymbolKind)symbol.Kind, span, nameSpan, children);
    }

    private static void AddLocation(JsonElement item, ISnapshotSource files, List<LspLocation> locations)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        try
        {
            if (item.TryGetProperty("targetUri", out _))
            {
                if (item.Deserialize(LspJsonContext.Default.LocationLinkDto) is { } link
                    && (link.TargetSelectionRange ?? link.TargetRange) is { } range
                    && ToLocation(link.TargetUri, range, files) is { } location)
                {
                    locations.Add(location);
                }
            }
            else if (item.Deserialize(LspJsonContext.Default.LocationDto) is { Range: { } range } plain
                && ToLocation(plain.Uri, range, files) is { } location)
            {
                locations.Add(location);
            }
        }
        catch (JsonException)
        {
            // A location the server described wrongly is left out.
        }
    }

    private static LspFileChange? ToFileChange(JsonElement change, Func<string, TextSnapshot?> snapshotOf, Dictionary<string, TextSnapshot> pending)
    {
        if (change.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        try
        {
            var kind = change.TryGetProperty("kind", out var value) ? value.GetString() : null;
            switch (kind)
            {
                case "create" when change.Deserialize(LspJsonContext.Default.CreateFileDto) is { } create && DocumentUri.ToPath(create.Uri) is { } path:
                    pending[path] = new TextSnapshot(string.Empty, 0);
                    return new LspFileChange(LspFileChangeKind.Create, path, null, [], create.Options?.Overwrite == true, create.Options?.IgnoreIfExists == true);
                case "rename" when change.Deserialize(LspJsonContext.Default.RenameFileDto) is { } rename
                    && DocumentUri.ToPath(rename.OldUri) is { } from && DocumentUri.ToPath(rename.NewUri) is { } to:
                    if (snapshotOf(from) is { } moved)
                    {
                        pending[to] = moved;
                    }

                    return new LspFileChange(LspFileChangeKind.Rename, from, to, [], rename.Options?.Overwrite == true, rename.Options?.IgnoreIfExists == true);
                case "delete" when change.Deserialize(LspJsonContext.Default.DeleteFileDto) is { } delete && DocumentUri.ToPath(delete.Uri) is { } deleted:
                    return new LspFileChange(LspFileChangeKind.Delete, deleted, null, []);
                case null when change.Deserialize(LspJsonContext.Default.TextDocumentEditDto) is { TextDocument.Uri: { } uri } edit && DocumentUri.ToPath(uri) is { } edited:
                    return new LspFileChange(LspFileChangeKind.Edit, edited, null, ToTextEdits(edit.Edits ?? [], snapshotOf(edited)));
                default:
                    return null;
            }
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string MarkedStringToMarkdown(JsonElement marked)
    {
        var language = marked.TryGetProperty("language", out var name) ? name.GetString() : null;
        var value = marked.TryGetProperty("value", out var text) ? text.GetString() ?? string.Empty : string.Empty;
        return language is null ? value : $"```{language}\n{value}\n```";
    }
}
