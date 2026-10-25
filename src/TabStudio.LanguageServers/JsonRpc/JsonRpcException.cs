namespace TabStudio.LanguageServers.JsonRpc;

/// <summary>A JSON-RPC request failed: the other side answered with an error, or the connection closed first.</summary>
public sealed class JsonRpcException : Exception
{
    /// <summary>Creates the exception.</summary>
    public JsonRpcException()
        : this("The request failed.")
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What went wrong.</param>
    public JsonRpcException(string message)
        : this(JsonRpcErrorCodes.InternalError, message)
    {
    }

    /// <summary>Creates the exception with a message and its cause.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">The cause.</param>
    public JsonRpcException(string message, Exception innerException)
        : base(message, innerException) => Code = JsonRpcErrorCodes.InternalError;

    /// <summary>Creates the exception for an error the other side answered with.</summary>
    /// <param name="code">The error code, such as <see cref="JsonRpcErrorCodes.MethodNotFound"/>.</param>
    /// <param name="message">The error message.</param>
    public JsonRpcException(int code, string message)
        : base(message) => Code = code;

    /// <summary>The JSON-RPC error code.</summary>
    public int Code { get; }

    /// <summary>Whether the connection closed before the answer arrived, such as when the server crashed.</summary>
    public bool IsConnectionClosed => Code == JsonRpcErrorCodes.ConnectionClosed;
}
