using System.Globalization;
using TabStudio.Compilation;
using TabStudio.Compilation.Features;
using TabStudio.Core.Diagnostics;
using TabStudio.LanguageServers.Features;

namespace TabStudio.App.Services.LanguageServers;

/// <summary>
/// Turns what language servers send into what the editor shows: icons for completion items and symbols, colors for
/// semantic tokens, Error List rows for diagnostics, and document edits for workspace edits.
/// </summary>
internal static class LspMapping
{
    /// <summary>The icon of a completion item.</summary>
    /// <param name="kind">The item's kind.</param>
    public static SymbolGlyph Glyph(LspCompletionItemKind kind) => kind switch
    {
        LspCompletionItemKind.Method or LspCompletionItemKind.Function or LspCompletionItemKind.Constructor => SymbolGlyph.Method,
        LspCompletionItemKind.Field => SymbolGlyph.Field,
        LspCompletionItemKind.Variable => SymbolGlyph.Local,
        LspCompletionItemKind.Class => SymbolGlyph.Class,
        LspCompletionItemKind.Interface => SymbolGlyph.Interface,
        LspCompletionItemKind.Module or LspCompletionItemKind.File or LspCompletionItemKind.Folder or LspCompletionItemKind.Reference => SymbolGlyph.Namespace,
        LspCompletionItemKind.Property or LspCompletionItemKind.Unit => SymbolGlyph.Property,
        LspCompletionItemKind.Value or LspCompletionItemKind.Constant => SymbolGlyph.Constant,
        LspCompletionItemKind.Enum => SymbolGlyph.Enum,
        LspCompletionItemKind.EnumMember => SymbolGlyph.EnumMember,
        LspCompletionItemKind.Keyword or LspCompletionItemKind.Operator => SymbolGlyph.Keyword,
        LspCompletionItemKind.Snippet => SymbolGlyph.Snippet,
        LspCompletionItemKind.Struct => SymbolGlyph.Struct,
        LspCompletionItemKind.Event => SymbolGlyph.Event,
        LspCompletionItemKind.TypeParameter => SymbolGlyph.TypeParameter,
        _ => SymbolGlyph.Other,
    };

    /// <summary>The icon of a document or workspace symbol.</summary>
    /// <param name="kind">The symbol's kind.</param>
    public static SymbolGlyph Glyph(LspSymbolKind kind) => kind switch
    {
        LspSymbolKind.File or LspSymbolKind.Module or LspSymbolKind.Namespace or LspSymbolKind.Package => SymbolGlyph.Namespace,
        LspSymbolKind.Class => SymbolGlyph.Class,
        LspSymbolKind.Method or LspSymbolKind.Function or LspSymbolKind.Constructor or LspSymbolKind.Operator => SymbolGlyph.Method,
        LspSymbolKind.Property => SymbolGlyph.Property,
        LspSymbolKind.Field or LspSymbolKind.Key => SymbolGlyph.Field,
        LspSymbolKind.Enum => SymbolGlyph.Enum,
        LspSymbolKind.Interface => SymbolGlyph.Interface,
        LspSymbolKind.Variable or LspSymbolKind.ObjectValue or LspSymbolKind.Array => SymbolGlyph.Local,
        LspSymbolKind.Constant or LspSymbolKind.StringValue or LspSymbolKind.Number or LspSymbolKind.Boolean or LspSymbolKind.Null => SymbolGlyph.Constant,
        LspSymbolKind.EnumMember => SymbolGlyph.EnumMember,
        LspSymbolKind.Struct => SymbolGlyph.Struct,
        LspSymbolKind.Event => SymbolGlyph.Event,
        LspSymbolKind.TypeParameter => SymbolGlyph.TypeParameter,
        _ => SymbolGlyph.Other,
    };

    /// <summary>What a symbol kind is called, for lists such as Quick Open.</summary>
    /// <param name="kind">The symbol's kind.</param>
    public static string Describe(LspSymbolKind kind) => kind switch
    {
        LspSymbolKind.StringValue => "String",
        LspSymbolKind.ObjectValue => "Object",
        LspSymbolKind.EnumMember => "Enum member",
        LspSymbolKind.TypeParameter => "Type parameter",
        LspSymbolKind.Unknown => "Symbol",
        _ => kind.ToString(),
    };

    /// <summary>
    /// The Roslyn classification a semantic token is colored as, or <see langword="null"/> to leave its TextMate
    /// color, as VS Code layers semantic colors over TextMate's.
    /// </summary>
    /// <param name="token">The token.</param>
    public static string? Classification(LspSemanticToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return token.Type switch
        {
            "namespace" => "namespace name",
            "class" or "type" or "struct" or "interface" or "enum" or "typeParameter" or "decorator" => "class name",
            "function" or "method" or "macro" => "method name",
            "parameter" => "parameter name",
            "variable" when token.Modifiers.Contains("readonly") || token.Modifiers.Contains("defaultLibrary") => "constant name",
            "variable" => "local name",
            "property" => "property name",
            "enumMember" => "enum member name",
            "event" => "event name",
            "keyword" or "modifier" => "keyword",
            "comment" => "comment",
            "string" or "regexp" => "string",
            "number" => "number",
            _ => null,
        };
    }

    /// <summary>
    /// The diagnostics the Error List and the editor's squiggles show: errors, warnings and information. Hints, such
    /// as Pyright's notes about names that are never used, are left out, as VS Code leaves them out of its Problems.
    /// </summary>
    /// <param name="diagnostics">The server's diagnostics of a file.</param>
    /// <param name="projectName">What the Project column shows: the server's workspace folder.</param>
    public static List<BuildDiagnostic> ToBuildDiagnostics(IEnumerable<LspDiagnostic> diagnostics, string projectName) =>
        diagnostics.Where(d => d.Severity != LspDiagnosticSeverity.Hint).Select(d => ToBuildDiagnostic(d, projectName)).ToList();

    /// <summary>A diagnostic as the Error List and the editor's squiggles show it.</summary>
    /// <param name="diagnostic">The server's diagnostic.</param>
    /// <param name="projectName">What the Project column shows: the server's workspace folder.</param>
    public static BuildDiagnostic ToBuildDiagnostic(LspDiagnostic diagnostic, string projectName)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        var severity = diagnostic.Severity switch
        {
            LspDiagnosticSeverity.Error => DiagnosticSeverityLevel.Error,
            LspDiagnosticSeverity.Warning => DiagnosticSeverityLevel.Warning,
            _ => DiagnosticSeverityLevel.Info,
        };
        var location = new DiagnosticLocation(diagnostic.FilePath, diagnostic.Span.Start, diagnostic.Span.Length, diagnostic.Line, diagnostic.Column);
        return new BuildDiagnostic(Code(diagnostic), severity, diagnostic.Message, location, projectName)
        {
            HelpLink = diagnostic.CodeLink,
        };
    }

    /// <summary>
    /// The Code column of a diagnostic: its code, with the prefix TypeScript's codes have in Visual Studio, such as
    /// <c>TS2304</c>; or its source, such as <c>Pyright</c>, when it has no code.
    /// </summary>
    /// <param name="diagnostic">The diagnostic.</param>
    public static string Code(LspDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        if (diagnostic.Code is not { Length: > 0 } code)
        {
            return diagnostic.Source ?? "LSP";
        }

        var isNumber = code.All(char.IsAsciiDigit);
        return isNumber && diagnostic.Source is "ts" or "typescript" or "js" or "javascript" ? "TS" + code : code;
    }

    /// <summary>Edits of a language server as the editor applies them.</summary>
    /// <param name="edits">Edits in document order.</param>
    public static List<TextEdit> ToTextEdits(IEnumerable<LspTextEdit> edits) =>
        edits.Select(e => new TextEdit(e.Span.Start, e.Span.Length, e.NewText)).ToList();

    /// <summary>A workspace edit as the edits of each file, as renames and code actions apply them.</summary>
    /// <param name="edit">The workspace edit.</param>
    /// <param name="readText">Reads a file, for files the edit renames.</param>
    public static List<DocumentEdit> ToDocumentEdits(LspWorkspaceEdit edit, Func<string, string?> readText)
    {
        ArgumentNullException.ThrowIfNull(edit);
        ArgumentNullException.ThrowIfNull(readText);
        var result = new List<DocumentEdit>();
        foreach (var change in edit.Changes)
        {
            switch (change.Kind)
            {
                case LspFileChangeKind.Edit when change.Edits.Count > 0:
                    result.Add(new DocumentEdit(change.FilePath, DocumentEditKind.Modified, ToTextEdits(change.Edits)));
                    break;
                case LspFileChangeKind.Create when !(change.IgnoreIfExists && File.Exists(change.FilePath)):
                    result.Add(new DocumentEdit(change.FilePath, DocumentEditKind.Added, [new TextEdit(0, 0, string.Empty)]));
                    break;
                case LspFileChangeKind.Rename when change.NewFilePath is { } target:
                    var text = readText(change.FilePath) ?? string.Empty;
                    result.Add(new DocumentEdit(target, DocumentEditKind.Added, [new TextEdit(0, 0, text)]));
                    result.Add(new DocumentEdit(change.FilePath, DocumentEditKind.Removed, []));
                    break;
                case LspFileChangeKind.Delete:
                    result.Add(new DocumentEdit(change.FilePath, DocumentEditKind.Removed, []));
                    break;
            }
        }

        return result;
    }

    /// <summary>"3 changes in 2 files: main.py (2), app.py (1)", for the rename preview.</summary>
    /// <param name="edits">The edits.</param>
    public static string Summarize(IReadOnlyList<DocumentEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(edits);
        var count = edits.Sum(e => Math.Max(1, e.Edits.Count));
        var files = string.Join(", ", edits.Select(e => $"{Path.GetFileName(e.FilePath)} ({Math.Max(1, e.Edits.Count).ToString(CultureInfo.CurrentCulture)})"));
        var changes = count == 1 ? "1 change" : $"{count.ToString(CultureInfo.CurrentCulture)} changes";
        var inFiles = edits.Count == 1 ? "1 file" : $"{edits.Count.ToString(CultureInfo.CurrentCulture)} files";
        return $"{changes} in {inFiles}: {files}";
    }
}
