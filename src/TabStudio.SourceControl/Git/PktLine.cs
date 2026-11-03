using System.Globalization;
using System.Text;

namespace TabStudio.SourceControl.Git;

/// <summary>What one pkt-line of Git's wire protocol holds.</summary>
internal enum PktKind
{
    /// <summary>A line with data.</summary>
    Data,

    /// <summary><c>0000</c>: the end of a message.</summary>
    Flush,

    /// <summary><c>0001</c>: the end of a section of a protocol v2 message.</summary>
    Delimiter,

    /// <summary><c>0002</c>: the end of a protocol v2 response, in stateless connections.</summary>
    ResponseEnd,

    /// <summary>The stream ended.</summary>
    End,
}

/// <summary>Git's pkt-line framing: four hexadecimal digits of length, then the data.</summary>
internal static class PktLine
{
    /// <summary>The largest data a pkt-line holds.</summary>
    public const int MaximumData = 65516;

    /// <summary>Appends a line of text, adding the newline Git expects after it.</summary>
    public static void WriteLine(Stream stream, string text) => Write(stream, Encoding.UTF8.GetBytes(text + "\n"));

    /// <summary>Appends a line of data.</summary>
    public static void Write(Stream stream, ReadOnlySpan<byte> data)
    {
        if (data.Length > MaximumData)
        {
            throw new ArgumentOutOfRangeException(nameof(data), "A pkt-line holds at most 65516 bytes.");
        }

        stream.Write(Encoding.ASCII.GetBytes((data.Length + 4).ToString("x4", CultureInfo.InvariantCulture)));
        stream.Write(data);
    }

    /// <summary>Appends a flush packet.</summary>
    public static void WriteFlush(Stream stream) => stream.Write("0000"u8);

    /// <summary>Appends a delimiter packet.</summary>
    public static void WriteDelimiter(Stream stream) => stream.Write("0001"u8);
}

/// <summary>Reads pkt-lines from a response.</summary>
internal sealed class PktLineReader(Stream stream)
{
    private readonly byte[] _length = new byte[4];

    /// <summary>The data of the last line read.</summary>
    public byte[] Data { get; private set; } = [];

    /// <summary>The last line as text, without its newline.</summary>
    public string Text => Encoding.UTF8.GetString(Data).TrimEnd('\n');

    /// <summary>Reads the next line.</summary>
    /// <exception cref="InvalidDataException">The stream is not pkt-lines.</exception>
    public async Task<PktKind> ReadAsync(CancellationToken cancellationToken)
    {
        if (!await FillAsync(_length, allowEnd: true, cancellationToken).ConfigureAwait(false))
        {
            Data = [];
            return PktKind.End;
        }

        if (!int.TryParse(Encoding.ASCII.GetString(_length), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var length))
        {
            throw new InvalidDataException($"The server sent '{Encoding.ASCII.GetString(_length)}' where a Git pkt-line length belongs.");
        }

        switch (length)
        {
            case 0:
                Data = [];
                return PktKind.Flush;
            case 1:
                Data = [];
                return PktKind.Delimiter;
            case 2:
                Data = [];
                return PktKind.ResponseEnd;
            case < 4:
                throw new InvalidDataException("The server sent an invalid Git pkt-line.");
        }

        var data = new byte[length - 4];
        await FillAsync(data, allowEnd: false, cancellationToken).ConfigureAwait(false);
        Data = data;
        return PktKind.Data;
    }

    private async Task<bool> FillAsync(byte[] buffer, bool allowEnd, CancellationToken cancellationToken)
    {
        var filled = 0;
        while (filled < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(filled), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                if (allowEnd && filled == 0)
                {
                    return false;
                }

                throw new EndOfStreamException("The connection ended in the middle of the server's answer.");
            }

            filled += read;
        }

        return true;
    }
}
