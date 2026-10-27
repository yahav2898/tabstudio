using System.Security.Cryptography;

namespace TabStudio.Toolchains.Embedded.Flashing;

/// <summary>A contiguous difference between two firmware dumps.</summary>
/// <param name="Offset">Byte offset into the dumps.</param>
/// <param name="Length">Number of differing bytes, including bytes absent from one dump.</param>
public sealed record FirmwareDifference(long Offset, long Length);

/// <summary>A bounded diff with whole-file hashes and exact total mismatch count.</summary>
/// <param name="Identical">Both length and every byte match.</param>
/// <param name="LeftSha256">Left file digest.</param>
/// <param name="RightSha256">Right file digest.</param>
/// <param name="DifferentBytes">Exact differing-byte count.</param>
/// <param name="Ranges">First bounded set of contiguous mismatch ranges.</param>
/// <param name="Truncated">There are more ranges than returned.</param>
public sealed record FirmwareComparison(bool Identical, string LeftSha256, string RightSha256, long DifferentBytes,
    IReadOnlyList<FirmwareDifference> Ranges, bool Truncated);

/// <summary>Verified flash backups and bounded binary comparisons. Protected-read errors propagate; protection is never disabled.</summary>
public static class FirmwareBackup
{
    /// <summary>Compares whole dumps with constant-sized buffers, retaining at most the requested number of difference ranges.</summary>
    public static async Task<FirmwareComparison> CompareAsync(string leftPath, string rightPath, int maximumRanges = 256, CancellationToken cancellationToken = default)
    {
        if (maximumRanges is < 1 or > 65536)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumRanges));
        }
        await using var left = File.OpenRead(leftPath);
        await using var right = File.OpenRead(rightPath);
        using var leftHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var rightHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var leftBuffer = new byte[64 * 1024];
        var rightBuffer = new byte[64 * 1024];
        var differences = new List<FirmwareDifference>();
        long offset = 0;
        long count = 0;
        long? differenceStart = null;
        var truncated = false;
        while (true)
        {
            var leftCount = await ReadBlockAsync(left, leftBuffer, cancellationToken).ConfigureAwait(false);
            var rightCount = await ReadBlockAsync(right, rightBuffer, cancellationToken).ConfigureAwait(false);
            if (leftCount == 0 && rightCount == 0)
            {
                break;
            }
            leftHash.AppendData(leftBuffer.AsSpan(0, leftCount));
            rightHash.AppendData(rightBuffer.AsSpan(0, rightCount));
            var length = Math.Max(leftCount, rightCount);
            for (var index = 0; index < length; index++)
            {
                if (index >= leftCount || index >= rightCount || leftBuffer[index] != rightBuffer[index])
                {
                    count++;
                    differenceStart ??= offset + index;
                }
                else if (differenceStart is { } start)
                {
                    AddDifference(start, offset + index - start);
                    differenceStart = null;
                }
            }
            offset += length;
        }
        if (differenceStart is { } last)
        {
            AddDifference(last, offset - last);
        }
        return new(count == 0, Convert.ToHexStringLower(leftHash.GetHashAndReset()), Convert.ToHexStringLower(rightHash.GetHashAndReset()), count, differences, truncated);

        void AddDifference(long start, long length)
        {
            if (differences.Count < maximumRanges)
            {
                differences.Add(new(start, length));
            }
            else
            {
                truncated = true;
            }
        }
    }

    /// <summary>Reads flash twice through a caller-supplied non-destructive transport, requires identical dumps, then atomically saves the backup.</summary>
    /// <param name="destination">New backup file; existing backups are never overwritten.</param>
    /// <param name="readFlash">Reads the same explicit flash interval into the supplied stream. Must fail on protection.</param>
    /// <param name="cancellationToken">Cancels both reads and removes unfinished files.</param>
    /// <returns>The verified SHA-256 digest.</returns>
    public static async Task<string> CreateAsync(string destination, Func<Stream, CancellationToken, Task> readFlash, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(readFlash);
        var path = Path.GetFullPath(destination);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path))
        {
            throw new IOException("A backup already exists at the requested path.");
        }
        var first = path + "." + Guid.NewGuid().ToString("N") + ".partial";
        var second = path + "." + Guid.NewGuid().ToString("N") + ".verify";
        try
        {
            foreach (var temporary in new[] { first, second })
            {
                await using var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await readFlash(output, cancellationToken).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            if (new FileInfo(first).Length == 0)
            {
                throw new InvalidDataException("The device returned an empty flash dump.");
            }
            var comparison = await CompareAsync(first, second, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!comparison.Identical)
            {
                throw new InvalidDataException("Flash backup verification failed: the two device reads differ.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(first, path, false);
            return comparison.LeftSha256;
        }
        finally
        {
            File.Delete(first);
            File.Delete(second);
        }
    }

    private static async Task<int> ReadBlockAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var count = 0;
        while (count < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[count..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }
            count += read;
        }
        return count;
    }
}
