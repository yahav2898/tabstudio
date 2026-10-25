using System.Text.Json;
using TabStudio.LanguageServers.Documents;
using TabStudio.LanguageServers.Features;
using TabStudio.LanguageServers.JsonRpc;
using TabStudio.LanguageServers.Protocol;

namespace TabStudio.LanguageServers.Hosting;

/// <summary>Searches one server's workspace symbols with <c>workspace/symbol</c>.</summary>
internal static class WorkspaceSymbols
{
    /// <summary>How many symbols one search keeps from one server.</summary>
    private const int Limit = 200;

    /// <summary>The symbols matching a query, in the server's order.</summary>
    /// <param name="server">The server.</param>
    /// <param name="query">What the user typed.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    public static async Task<List<LspWorkspaceSymbol>> FindAsync(LanguageServerInstance server, string query, CancellationToken cancellationToken)
    {
        var symbols = new List<LspWorkspaceSymbol>();
        if (await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false) is not { } connection)
        {
            return symbols;
        }

        JsonElement result;
        try
        {
            result = await connection.SendRequestAsync(
                "workspace/symbol",
                new WorkspaceSymbolParamsDto(query),
                LspJsonContext.Default.WorkspaceSymbolParamsDto,
                LspJsonContext.Default.JsonElement,
                cancellationToken).ConfigureAwait(false);
        }
        catch (JsonRpcException ex)
        {
            server.LogRequestFailure("workspace/symbol", ex);
            return symbols;
        }

        if (result.ValueKind != JsonValueKind.Array)
        {
            return symbols;
        }

        // Many symbols share a file: read each file once.
        var files = new CachingSnapshotSource(server.Files);
        foreach (var item in result.EnumerateArray())
        {
            if (symbols.Count >= Limit)
            {
                break;
            }

            try
            {
                if (ToSymbol(item, files) is { } symbol)
                {
                    symbols.Add(symbol);
                }
            }
            catch (JsonException)
            {
                // A symbol the server described wrongly is left out.
            }
        }

        return symbols;
    }

    /// <summary>A <c>SymbolInformation | WorkspaceSymbol</c>; the latter's location may have only a URI.</summary>
    private static LspWorkspaceSymbol? ToSymbol(JsonElement item, ISnapshotSource files)
    {
        if (item.Deserialize(LspJsonContext.Default.WorkspaceSymbolDto) is not { Name: { } name } symbol
            || symbol.Location is not { ValueKind: JsonValueKind.Object } location
            || !location.TryGetProperty("uri", out var uriElement)
            || uriElement.GetString() is not { } uri
            || DocumentUri.ToPath(uri) is not { } path)
        {
            return null;
        }

        var where = location.TryGetProperty("range", out var rangeElement)
            && rangeElement.Deserialize(LspJsonContext.Default.RangeDto) is { Start: not null, End: not null } range
            && LspConvert.ToLocation(uri, range, files) is { } found
                ? found
                : new LspLocation(path, new TextSpan(0, 0), 1, 1);
        return new LspWorkspaceSymbol(name, (LspSymbolKind)symbol.Kind, symbol.ContainerName, where);
    }

    /// <summary>Reads each file's text once.</summary>
    private sealed class CachingSnapshotSource(ISnapshotSource inner) : ISnapshotSource
    {
        private readonly Dictionary<string, TextSnapshot?> _snapshots = new(DocumentUri.PathComparer);

        public TextSnapshot? GetSnapshot(string filePath)
        {
            if (!_snapshots.TryGetValue(filePath, out var snapshot))
            {
                snapshot = inner.GetSnapshot(filePath);
                _snapshots[filePath] = snapshot;
            }

            return snapshot;
        }
    }
}
