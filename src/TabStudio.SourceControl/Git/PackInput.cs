namespace TabStudio.SourceControl.Git;

/// <summary>
/// A buffered reader over a seekable stream that knows its exact position, so a pack can be walked object by object:
/// a pack does not record where each object's compressed data ends, only the inflater finds out.
/// </summary>
internal sealed class PackInput(Stream stream)
{
    private readonly byte[] _buffer = new byte[64 * 1024];
    private long _bufferStart;
    private int _length;
    private int _index;

    /// <summary>The position of the next byte.</summary>
    public long Position
    {
        get => _bufferStart + _index;
        set
        {
            if (value >= _bufferStart && value <= _bufferStart + _length)
            {
                _index = (int)(value - _bufferStart);
            }
            else
            {
                _bufferStart = value;
                _length = 0;
                _index = 0;
            }
        }
    }

    /// <summary>Reads a byte; <see langword="false"/> at the end of the stream.</summary>
    public bool TryReadByte(out byte value)
    {
        if (_index == _length && !Fill())
        {
            value = 0;
            return false;
        }

        value = _buffer[_index++];
        return true;
    }

    /// <summary>Reads a byte.</summary>
    /// <exception cref="EndOfStreamException">The stream ended.</exception>
    public byte ReadByte() => TryReadByte(out var value) ? value : throw new EndOfStreamException("The pack ended in the middle of an object.");

    /// <summary>Fills a span.</summary>
    /// <exception cref="EndOfStreamException">The stream ended first.</exception>
    public void ReadExactly(Span<byte> destination)
    {
        while (destination.Length > 0)
        {
            if (_index == _length && !Fill())
            {
                throw new EndOfStreamException("The pack ended in the middle of an object.");
            }

            var count = Math.Min(destination.Length, _length - _index);
            _buffer.AsSpan(_index, count).CopyTo(destination);
            _index += count;
            destination = destination[count..];
        }
    }

    private bool Fill()
    {
        _bufferStart += _length;
        _index = 0;
        stream.Position = _bufferStart;
        _length = stream.Read(_buffer, 0, _buffer.Length);
        return _length > 0;
    }
}
