using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace TabStudio.SourceControl.Git;

/// <summary>
/// Writes Git's index (<c>.git/index</c>, version 2) for a commit's files, as <c>git reset</c> leaves it: what
/// <c>git status</c> compares the working folder and <c>HEAD</c> with.
/// </summary>
internal static class IndexWriter
{
    /// <summary>Writes the index.</summary>
    /// <param name="gitDirectory">The Git directory.</param>
    /// <param name="root">The working folder.</param>
    /// <param name="entries">Every file, symbolic link and submodule of the commit, by path with <c>/</c> separators.</param>
    /// <param name="matches">
    /// Whether the working folder's file holds the entry's content; only such entries get the file's size and time, so
    /// Git looks again at every other one and sees it as changed.
    /// </param>
    public static void Write(string gitDirectory, string root, IReadOnlyDictionary<string, (string Mode, string Id)> entries, Func<string, bool> matches)
    {
        var sorted = entries.Keys.Select(k => (Path: k, Bytes: Encoding.UTF8.GetBytes(k))).ToList();
        sorted.Sort((a, b) => a.Bytes.AsSpan().SequenceCompareTo(b.Bytes));

        using var output = new MemoryStream();
        Span<byte> word = stackalloc byte[4];
        output.Write("DIRC"u8);
        BinaryPrimitives.WriteUInt32BigEndian(word, 2);
        output.Write(word);
        BinaryPrimitives.WriteUInt32BigEndian(word, (uint)sorted.Count);
        output.Write(word);

        Span<byte> fixedPart = stackalloc byte[62];
        foreach (var (path, name) in sorted)
        {
            var (mode, id) = entries[path];
            fixedPart.Clear();
            uint seconds = 0;
            uint nanoseconds = 0;
            uint size = 0;
            if (mode != TreeCodec.SubmoduleMode && matches(path))
            {
                var info = new FileInfo(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)));
                if (info.Exists || info.LinkTarget is not null)
                {
                    var ticks = (info.LastWriteTimeUtc - DateTime.UnixEpoch).Ticks;
                    seconds = (uint)(ticks / TimeSpan.TicksPerSecond);
                    nanoseconds = (uint)(ticks % TimeSpan.TicksPerSecond * 100);
                    size = mode == TreeCodec.SymbolicLinkMode ? (uint)Encoding.UTF8.GetByteCount(info.LinkTarget ?? string.Empty) : (uint)info.Length;
                }
            }

            // ctime, mtime: seconds and nanoseconds; then dev, ino, mode, uid, gid, size. Git compares dev, ino, uid
            // and gid only when they are set, and re-reads a file whose times or size differ.
            BinaryPrimitives.WriteUInt32BigEndian(fixedPart[0..], seconds);
            BinaryPrimitives.WriteUInt32BigEndian(fixedPart[4..], nanoseconds);
            BinaryPrimitives.WriteUInt32BigEndian(fixedPart[8..], seconds);
            BinaryPrimitives.WriteUInt32BigEndian(fixedPart[12..], nanoseconds);
            BinaryPrimitives.WriteUInt32BigEndian(fixedPart[24..], uint.Parse(mode, NumberStyles.None, CultureInfo.InvariantCulture) switch
            {
                100755 => 0x81EDu,
                120000 => 0xA000u,
                160000 => 0xE000u,
                _ => 0x81A4u,
            });
            BinaryPrimitives.WriteUInt32BigEndian(fixedPart[36..], size);
            GitHash.FromHex(id).CopyTo(fixedPart[40..]);
            BinaryPrimitives.WriteUInt16BigEndian(fixedPart[60..], (ushort)Math.Min(name.Length, 0xFFF));
            output.Write(fixedPart);
            output.Write(name);

            // NUL-padded so each entry is a multiple of eight bytes, with at least one NUL.
            var padding = 8 - ((62 + name.Length) % 8);
            for (var i = 0; i < padding; i++)
            {
                output.WriteByte(0);
            }
        }

        using (var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA1))
        {
            sha.AppendData(output.GetBuffer(), 0, (int)output.Length);
            output.Write(sha.GetHashAndReset());
        }

        var index = Path.Combine(gitDirectory, "index");
        var temporary = index + ".lock";
        File.WriteAllBytes(temporary, output.ToArray());
        File.Move(temporary, index, overwrite: true);
    }
}
