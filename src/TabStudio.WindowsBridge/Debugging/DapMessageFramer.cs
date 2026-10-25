using System.Buffers;
using System.Globalization;
using System.Text;

namespace TabStudio.WindowsBridge.Debugging;

/// <summary>
/// Cuts a debug adapter's byte stream into messages, as the Debug Adapter Protocol frames them: a
/// <c>Content-Length: n</c> header, an empty line, and <c>n</c> bytes of JSON. Bytes may arrive in pieces of any size.
/// </summary>
/// <remarks>Not thread-safe: one framer reads one direction of one stream.</remarks>
public sealed class DapMessageFramer
{
    /// <summary>The largest message accepted; a debug adapter's messages are far smaller.</summary>
    public const int MaxMessageLength = 64 * 1024 * 1024;

    private static readonly byte[] HeaderEnd = "\r\n\r\n"u8.ToArray();

    private readonly ArrayBufferWriter<byte> _pending = new();

    /// <summary>Frames one message's JSON for the stream.</summary>
    /// <param name="body">The JSON.</param>
    public static byte[] Frame(ReadOnlySpan<byte> body)
    {
        var header = Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture, $"Content-Length: {body.Length}\r\n\r\n"));
        var framed = new byte[header.Length + body.Length];
        header.CopyTo(framed, 0);
        body.CopyTo(framed.AsSpan(header.Length));
        return framed;
    }

    /// <summary>Adds bytes from the stream and returns the messages they complete, in order.</summary>
    /// <param name="data">The bytes.</param>
    /// <returns>The JSON of each complete message.</returns>
    /// <exception cref="InvalidDataException">The stream is not framed as the protocol says.</exception>
    public IReadOnlyList<byte[]> Append(ReadOnlySpan<byte> data)
    {
        _pending.Write(data);
        var messages = new List<byte[]>();
        var consumed = 0;
        while (true)
        {
            var rest = _pending.WrittenSpan[consumed..];
            var end = rest.IndexOf(HeaderEnd);
            if (end < 0)
            {
                if (rest.Length > 8192)
                {
                    throw new InvalidDataException("The debug adapter sent a header without an end.");
                }

                break;
            }

            var length = ContentLength(rest[..end]);
            var start = end + HeaderEnd.Length;
            if (rest.Length - start < length)
            {
                break;
            }

            messages.Add(rest.Slice(start, length).ToArray());
            consumed += start + length;
        }

        if (consumed > 0)
        {
            var left = _pending.WrittenSpan[consumed..].ToArray();
            _pending.Clear();
            _pending.Write(left);
        }

        return messages;
    }

    private static int ContentLength(ReadOnlySpan<byte> header)
    {
        foreach (var line in Encoding.ASCII.GetString(header).Split("\r\n"))
        {
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon > 0 && line[..colon].Trim().Equals("Content-Length", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(line[(colon + 1)..].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var length)
                && length is >= 0 and <= MaxMessageLength)
            {
                return length;
            }
        }

        throw new InvalidDataException("The debug adapter sent a message without its length.");
    }
}
