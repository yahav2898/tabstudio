using System.Globalization;
using System.Text;

namespace TabStudio.LanguageServers.JsonRpc;

/// <summary>
/// Reads the messages of the base protocol that LSP and DAP share: a header with <c>Content-Length</c>, an empty
/// line, then that many bytes of UTF-8 JSON.
/// </summary>
/// <remarks>
/// Header lines other than <c>Content-Length</c> are ignored, and so are lines a server prints to its standard output
/// before its first header, which some servers do while they start.
/// </remarks>
internal sealed class MessageReader
{
    private const int MaxHeaderLine = 16 * 1024;
    private const int MaxMessage = 256 * 1024 * 1024;

    private readonly Stream _stream;
    private byte[] _buffer = new byte[16 * 1024];
    private int _start;
    private int _end;

    /// <summary>Creates a reader over a stream, such as a server's standard output.</summary>
    /// <param name="stream">The stream to read.</param>
    public MessageReader(Stream stream) => _stream = stream;

    /// <summary>Reads the next message.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The message's JSON, or <see langword="null"/> when the stream ended.</returns>
    /// <exception cref="InvalidDataException">The header is not valid.</exception>
    public async Task<byte[]?> ReadAsync(CancellationToken cancellationToken)
    {
        var contentLength = -1;
        while (true)
        {
            var line = await ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                return null;
            }

            if (line.Length == 0)
            {
                if (contentLength >= 0)
                {
                    break;
                }

                continue;
            }

            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon > 0 && line.AsSpan(0, colon).Trim().Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(line.AsSpan(colon + 1).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out contentLength)
                    || contentLength > MaxMessage)
                {
                    throw new InvalidDataException($"The message header has an invalid Content-Length: '{line}'.");
                }
            }
        }

        var body = new byte[contentLength];
        var copied = Math.Min(contentLength, _end - _start);
        _buffer.AsSpan(_start, copied).CopyTo(body);
        _start += copied;
        while (copied < contentLength)
        {
            var read = await _stream.ReadAsync(body.AsMemory(copied), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            copied += read;
        }

        return body;
    }

    /// <summary>Reads one header line, without its line break.</summary>
    /// <returns>The line, or <see langword="null"/> when the stream ended.</returns>
    private async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        // How many unread bytes are known to hold no line break; filling may move them to the front of the buffer.
        var scanned = 0;
        while (true)
        {
            var newline = Array.IndexOf(_buffer, (byte)'\n', _start + scanned, _end - _start - scanned);
            if (newline >= 0)
            {
                var length = newline - _start;
                if (length > 0 && _buffer[newline - 1] == (byte)'\r')
                {
                    length--;
                }

                var line = Encoding.ASCII.GetString(_buffer, _start, length);
                _start = newline + 1;
                return line;
            }

            scanned = _end - _start;
            if (scanned > MaxHeaderLine)
            {
                throw new InvalidDataException("A message header line is too long.");
            }

            if (!await FillAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }
        }
    }

    /// <summary>Reads more bytes into the buffer, moving the unread ones to its front first.</summary>
    /// <returns>Whether any bytes were read.</returns>
    private async Task<bool> FillAsync(CancellationToken cancellationToken)
    {
        if (_start > 0)
        {
            var unread = _end - _start;
            Buffer.BlockCopy(_buffer, _start, _buffer, 0, unread);
            _start = 0;
            _end = unread;
        }

        if (_end == _buffer.Length)
        {
            Array.Resize(ref _buffer, _buffer.Length * 2);
        }

        var read = await _stream.ReadAsync(_buffer.AsMemory(_end), cancellationToken).ConfigureAwait(false);
        _end += read;
        return read > 0;
    }
}
