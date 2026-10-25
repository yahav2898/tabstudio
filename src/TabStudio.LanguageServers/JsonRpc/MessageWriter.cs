using System.Globalization;
using System.Text;

namespace TabStudio.LanguageServers.JsonRpc;

/// <summary>
/// Writes messages of the base protocol that LSP and DAP share: <c>Content-Length</c>, an empty line, then the
/// UTF-8 JSON. Messages from several threads are written whole, one after another.
/// </summary>
internal sealed class MessageWriter : IDisposable
{
    private readonly Stream _stream;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Creates a writer over a stream, such as a server's standard input.</summary>
    /// <param name="stream">The stream to write.</param>
    public MessageWriter(Stream stream) => _stream = stream;

    /// <summary>Writes one message.</summary>
    /// <param name="body">The message's JSON.</param>
    /// <param name="cancellationToken">Cancels waiting for other messages to be written; a started write always completes.</param>
    public async Task WriteAsync(ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        var header = Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture, $"Content-Length: {body.Length}\r\n\r\n"));
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // A message cut in half would corrupt every message after it, so the write itself is not cancelled.
            await _stream.WriteAsync(header, CancellationToken.None).ConfigureAwait(false);
            await _stream.WriteAsync(body, CancellationToken.None).ConfigureAwait(false);
            await _stream.FlushAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();
}
