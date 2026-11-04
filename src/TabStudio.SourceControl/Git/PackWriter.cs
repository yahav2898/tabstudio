using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;

namespace TabStudio.SourceControl.Git;

/// <summary>Writes a pack of whole (undeltified) objects, as a push sends them.</summary>
internal static class PackWriter
{
    /// <summary>Writes a pack holding the given objects.</summary>
    /// <param name="destination">Where the pack goes.</param>
    /// <param name="store">The database holding the objects.</param>
    /// <param name="ids">The objects.</param>
    public static void Write(Stream destination, ObjectStore store, IReadOnlyCollection<string> ids)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        using var hashed = new HashTee(destination, sha);
        Span<byte> header = stackalloc byte[PackFormat.HeaderSize];
        "PACK"u8.CopyTo(header);
        BinaryPrimitives.WriteUInt32BigEndian(header[4..], 2);
        BinaryPrimitives.WriteUInt32BigEndian(header[8..], (uint)ids.Count);
        hashed.Write(header);
        foreach (var id in ids)
        {
            var value = store.Read(id);
            PackFormat.WriteObjectHeader(hashed, value.Kind, value.Content.Length);
            using (var deflate = new ZLibStream(hashed, CompressionLevel.Fastest, leaveOpen: true))
            {
                deflate.Write(value.Content);
            }
        }

        destination.Write(sha.GetHashAndReset());
    }

    /// <summary>Passes writes on and hashes them.</summary>
    private sealed class HashTee(Stream next, IncrementalHash hash) : Stream
    {
        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            hash.AppendData(buffer);
            next.Write(buffer);
        }

        public override void Flush() => next.Flush();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
