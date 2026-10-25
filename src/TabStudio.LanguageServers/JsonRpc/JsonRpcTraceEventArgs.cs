namespace TabStudio.LanguageServers.JsonRpc;

/// <summary>A message sent or received on a connection, described for the message trace.</summary>
/// <param name="message">The description, such as <c>Sending request 'textDocument/hover - (4)'.</c></param>
public sealed class JsonRpcTraceEventArgs(string message) : EventArgs
{
    /// <summary>The description.</summary>
    public string Message { get; } = message;
}
