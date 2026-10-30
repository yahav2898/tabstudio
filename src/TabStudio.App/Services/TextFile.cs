using System.Text;

namespace TabStudio.App.Services;

/// <summary>The text of a file and how it was encoded, so saving writes it back the same way.</summary>
/// <param name="Text">The decoded text.</param>
/// <param name="Encoding">The encoding, including whether it had a byte order mark.</param>
public sealed record TextFileContent(string Text, Encoding Encoding)
{
    /// <summary>A short label for the status bar, such as "UTF-8" or "UTF-8 BOM".</summary>
    public string EncodingLabel => Encoding switch
    {
        UTF8Encoding utf8 when utf8.GetPreamble().Length > 0 => "UTF-8 BOM",
        UTF8Encoding => "UTF-8",
        UnicodeEncoding { CodePage: 1201 } => "UTF-16 BE",
        UnicodeEncoding => "UTF-16 LE",
        UTF32Encoding { CodePage: 12001 } => "UTF-32 BE",
        UTF32Encoding => "UTF-32 LE",
        _ => Encoding.WebName.ToUpperInvariant(),
    };

    /// <summary>"CRLF" when the text uses Windows line endings, otherwise "LF".</summary>
    public string NewlineLabel => Text.Contains("\r\n", StringComparison.Ordinal) ? "CRLF" : "LF";
}

/// <summary>Reads and writes text files the way an editor should: keeping their encoding, refusing binaries.</summary>
public static class TextFile
{
    /// <summary>Files larger than this are not opened in the editor.</summary>
    public const long MaximumBytes = 16L * 1024 * 1024;

    /// <summary>Reads a text file.</summary>
    /// <param name="path">The file.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <exception cref="IOException">The file cannot be read, is too large, or is not text.</exception>
    public static async Task<TextFileContent> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        var info = new FileInfo(path);
        if (info.Length > MaximumBytes)
        {
            throw new IOException($"'{info.Name}' is larger than 16 MB, which is more than the editor opens.");
        }

        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        var (encoding, preamble) = Detect(bytes);
        if (preamble == 0 && LooksBinary(bytes))
        {
            throw new IOException($"'{info.Name}' is a binary file.");
        }

        if (preamble == 0)
        {
            // A file that is not UTF-8, such as one saved in Windows-1255 or Latin-1, was decoded with replacement
            // characters and saved back as UTF-8: its non-ASCII text was destroyed by the first save. Read as
            // ISO-8859-1, every byte is one character and is written back as it was.
            try
            {
                return new TextFileContent(StrictUtf8.GetString(bytes), encoding);
            }
            catch (DecoderFallbackException)
            {
                return new TextFileContent(SingleByte.GetString(bytes), SingleByte);
            }
        }

        return new TextFileContent(encoding.GetString(bytes, preamble, bytes.Length - preamble), encoding);
    }

    /// <summary>Writes a text file in the given encoding, atomically.</summary>
    /// <param name="path">The file.</param>
    /// <param name="text">The text.</param>
    /// <param name="encoding">The encoding, including whether to write a byte order mark.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    public static async Task WriteAsync(string path, string text, Encoding encoding, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(encoding);

        byte[] body;
        try
        {
            body = encoding.GetBytes(text);
        }
        catch (EncoderFallbackException ex)
        {
            throw new IOException(
                $"'{Path.GetFileName(path)}' is in {encoding.WebName.ToUpperInvariant()}, which cannot store '{ex.CharUnknown}'. Remove it, or convert the file to UTF-8 to keep it.", ex);
        }

        // A link is saved through to the file it names, rather than replaced by a copy.
        var existing = new FileInfo(path);
        if (existing.Exists && existing.ResolveLinkTarget(returnFinalTarget: true) is FileInfo { Exists: true } target)
        {
            path = target.FullName;
            existing = target;
        }

        var folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(folder);

        // Written beside the file under a name of its own, then moved over it: a failed or cancelled save leaves the
        // file as it was and no stray copy in the repository, and the file keeps its permissions, such as a script's
        // execute bit. Flushed to the storage first, so a power cut after the move cannot leave it empty.
        var temporary = Path.Combine(folder, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tabstudio-save");
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await stream.WriteAsync(encoding.GetPreamble(), cancellationToken).ConfigureAwait(false);
                await stream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            if (existing.Exists && !OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(temporary, File.GetUnixFileMode(path));
            }

            File.Move(temporary, path, overwrite: true);
        }
        catch
        {
            try
            {
                File.Delete(temporary);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The save's own error says what went wrong.
            }

            throw;
        }
    }

    /// <summary>UTF-8 that refuses bytes that are not UTF-8, so another encoding is noticed.</summary>
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>ISO-8859-1, which maps every byte to one character and back, refusing to write a character it lacks.</summary>
    private static readonly Encoding SingleByte = Encoding.GetEncoding(28591, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);

    private static (Encoding Encoding, int Preamble) Detect(byte[] bytes) => bytes switch
    {
        [0xEF, 0xBB, 0xBF, ..] => (new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), 3),
        [0xFF, 0xFE, 0x00, 0x00, ..] => (new UTF32Encoding(bigEndian: false, byteOrderMark: true), 4),
        [0x00, 0x00, 0xFE, 0xFF, ..] => (new UTF32Encoding(bigEndian: true, byteOrderMark: true), 4),
        [0xFF, 0xFE, ..] => (new UnicodeEncoding(bigEndian: false, byteOrderMark: true), 2),
        [0xFE, 0xFF, ..] => (new UnicodeEncoding(bigEndian: true, byteOrderMark: true), 2),
        _ => (new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 0),
    };

    /// <summary>A NUL byte in the first 8 KB means binary, as Git decides.</summary>
    private static bool LooksBinary(byte[] bytes) => bytes.AsSpan(0, Math.Min(bytes.Length, 8000)).IndexOf((byte)0) >= 0;
}
