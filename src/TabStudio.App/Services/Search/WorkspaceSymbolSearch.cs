using TabStudio.App.Services.LanguageServers;
using TabStudio.Compilation.Features;
using TabStudio.Core.Workspaces;
using TabStudio.LanguageServers.Hosting;

namespace TabStudio.App.Services.Search;

/// <summary>
/// The types and members of the open solution, for the search (Ctrl+Q, then <c>#</c> or the Symbols chip): C#
/// declarations from Roslyn through the language service, the same declarations the breadcrumb shows, so the search
/// works over TabStudio Link too; and the workspace symbols of every running language server, for Python, TypeScript
/// and the other languages.
/// </summary>
/// <remarks>
/// The C# declarations are read once per search session: <see cref="Invalidate"/> when the search opens, and the first
/// query that needs them reads every C# file of the solution, which later keystrokes reuse. Call on the UI thread.
/// </remarks>
/// <param name="workspace">The open workspace, or <see langword="null"/> before one is loaded.</param>
/// <param name="features">The language service's IntelliSense, or <see langword="null"/> before it is loaded.</param>
/// <param name="servers">The workspace's language servers, or <see langword="null"/> when none started.</param>
/// <param name="readText">A file's text as the editor has it, or <see langword="null"/> when it cannot be read.</param>
internal sealed class WorkspaceSymbolSearch(
    Func<Workspace?> workspace,
    Func<ILanguageFeatures?> features,
    Func<LanguageServerHost?> servers,
    Func<string, string?> readText) : ISymbolSearch
{
    /// <summary>The most symbols one search takes from the language servers.</summary>
    public const int ServerLimit = 20;

    /// <summary>How long a query waits for more typing before it asks the language servers.</summary>
    private static readonly TimeSpan ServerDelay = TimeSpan.FromMilliseconds(150);

    private Task<List<Declaration>>? _declarations;

    /// <summary>Forgets the C# declarations read before, so the next query reads them again, as the code may have changed.</summary>
    public void Invalidate() => _declarations = null;

    /// <inheritdoc />
    public async Task<IReadOnlyList<SymbolHit>> FindAsync(string query, int limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var hits = new List<SymbolHit>();
        var declarations = await DeclarationsAsync();
        cancellationToken.ThrowIfCancellationRequested();
        var texts = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (declaration, _) in declarations
                     .Select(declaration => (Declaration: declaration, Match: SearchMatcher.Match(query, declaration.Name)))
                     .Where(found => found.Match is not null)
                     .OrderByDescending(found => found.Match!.Score)
                     .ThenBy(found => found.Declaration.Name.Length)
                     .Take(limit))
        {
            if (!texts.TryGetValue(declaration.FilePath, out var text))
            {
                text = readText(declaration.FilePath);
                texts[declaration.FilePath] = text;
            }

            var (line, column) = Position(text, declaration.Offset);
            hits.Add(new SymbolHit(declaration.Name, Describe(declaration.Glyph), declaration.Container, declaration.Glyph, declaration.FilePath, line, column));
        }

        hits.AddRange(await FindInServersAsync(query, cancellationToken));
        return hits;
    }

    /// <summary>What a declaration is, in words, such as "Method".</summary>
    /// <param name="glyph">Its IntelliSense glyph.</param>
    public static string Describe(SymbolGlyph glyph) => glyph switch
    {
        SymbolGlyph.Class => "Class",
        SymbolGlyph.Struct => "Struct",
        SymbolGlyph.Interface => "Interface",
        SymbolGlyph.Enum => "Enum",
        SymbolGlyph.EnumMember => "Enum member",
        SymbolGlyph.Delegate => "Delegate",
        SymbolGlyph.Method => "Method",
        SymbolGlyph.ExtensionMethod => "Extension method",
        SymbolGlyph.Property => "Property",
        SymbolGlyph.Field => "Field",
        SymbolGlyph.Event => "Event",
        SymbolGlyph.Constant => "Constant",
        SymbolGlyph.Namespace => "Namespace",
        _ => "Symbol",
    };

    /// <summary>The one-based line and column of an offset in a text; the first line when the text could not be read.</summary>
    private static (int Line, int Column) Position(string? text, int offset)
    {
        if (text is null)
        {
            return (1, 1);
        }

        offset = Math.Clamp(offset, 0, text.Length);
        var line = 1;
        var lineStart = 0;
        for (var i = 0; i < offset; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                lineStart = i + 1;
            }
        }

        return (line, offset - lineStart + 1);
    }

    /// <summary>The solution's C# declarations, read once while the language service is loaded.</summary>
    private Task<List<Declaration>> DeclarationsAsync()
    {
        if (workspace() is not { } open || features() is not { } language)
        {
            return Task.FromResult(new List<Declaration>());
        }

        return _declarations ??= ReadDeclarationsAsync(open, language);
    }

    /// <summary>Every type and member declared in the solution's C# files, as the language service reads them.</summary>
    private static async Task<List<Declaration>> ReadDeclarationsAsync(Workspace open, ILanguageFeatures language)
    {
        var declarations = new List<Declaration>();

        var files = open.Tree.DescendantsAndSelf()
            .Where(node => node.IsOpenableFile && node.FullPath is { } path && path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Select(node => node.FullPath!)
            .ToList();
        foreach (var file in files)
        {
            IReadOnlyList<DocumentSymbol> symbols;
            try
            {
                symbols = await language.GetDocumentSymbolsAsync(file, CancellationToken.None);
            }
#pragma warning disable CA1031 // One file the language service cannot read, or a service that stopped, must not end the search.
            catch (Exception)
#pragma warning restore CA1031
            {
                continue;
            }

            Collect(symbols, file, container: null, declarations);
        }

        return declarations;
    }

    private static void Collect(IReadOnlyList<DocumentSymbol> symbols, string file, string? container, List<Declaration> declarations)
    {
        foreach (var symbol in symbols)
        {
            if (symbol.Kind != SymbolGlyph.Namespace)
            {
                declarations.Add(new Declaration(symbol.Name, symbol.Kind, container, file, symbol.NameStart));
            }

            Collect(symbol.Children, file, symbol.Name, declarations);
        }
    }

    /// <summary>The symbols of the running language servers that match a query, after a short pause for more typing.</summary>
    private async Task<IReadOnlyList<SymbolHit>> FindInServersAsync(string query, CancellationToken cancellationToken)
    {
        if (servers() is not { } host || !host.Servers.Any(server => server.State == LanguageServerState.Running))
        {
            return [];
        }

        await Task.Delay(ServerDelay, cancellationToken);
        var symbols = await host.FindWorkspaceSymbolsAsync(query, cancellationToken);
        return [.. symbols.Take(ServerLimit).Select(symbol => new SymbolHit(
            symbol.Name,
            LspMapping.Describe(symbol.Kind),
            symbol.ContainerName is { Length: > 0 } name ? name : null,
            LspMapping.Glyph(symbol.Kind),
            symbol.Location.FilePath,
            symbol.Location.Line,
            symbol.Location.Column))];
    }

    /// <summary>A C# type or member, where its name is.</summary>
    private sealed record Declaration(string Name, SymbolGlyph Glyph, string? Container, string FilePath, int Offset);
}
