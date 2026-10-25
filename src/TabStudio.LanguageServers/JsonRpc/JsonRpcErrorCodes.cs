namespace TabStudio.LanguageServers.JsonRpc;

/// <summary>The error codes of JSON-RPC 2.0 and the Language Server Protocol.</summary>
public static class JsonRpcErrorCodes
{
    /// <summary>The message was not valid JSON.</summary>
    public const int ParseError = -32700;

    /// <summary>The message was not a valid request.</summary>
    public const int InvalidRequest = -32600;

    /// <summary>The method does not exist on this side.</summary>
    public const int MethodNotFound = -32601;

    /// <summary>The parameters were not valid for the method.</summary>
    public const int InvalidParams = -32602;

    /// <summary>Something went wrong while handling the request.</summary>
    public const int InternalError = -32603;

    /// <summary>The server received a request before <c>initialize</c>.</summary>
    public const int ServerNotInitialized = -32002;

    /// <summary>The request failed for a reason the server does not name.</summary>
    public const int UnknownErrorCode = -32001;

    /// <summary>The request failed although it was valid, for example because the server crashed handling it.</summary>
    public const int RequestFailed = -32803;

    /// <summary>The server cancelled the request on its own.</summary>
    public const int ServerCancelled = -32802;

    /// <summary>The document changed while the request ran, so its answer would be out of date.</summary>
    public const int ContentModified = -32801;

    /// <summary>The client cancelled the request with <c>$/cancelRequest</c>.</summary>
    public const int RequestCancelled = -32800;

    /// <summary>TabStudio's own code: the connection closed before the answer arrived.</summary>
    public const int ConnectionClosed = -32099;
}
