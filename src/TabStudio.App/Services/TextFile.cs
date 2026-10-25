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

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tabstudio-save";
        var preamble = encoding.GetPreamble();
        await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        {
            await stream.WriteAsync(preamble, cancellationToken).ConfigureAwait(false);
            var body = encoding.GetBytes(text);
            await stream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
        }

        File.Move(temporary, path, overwrite: true);
    }

    private static (Encoding Encoding, int Preamble) Detect(byte[] bytes) => bytes switch
    {
        [0xEF, 0xBB, 0xBF, ..] => (new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), 3),
        [0xFF, 0xFE, ..] => (new UnicodeEncoding(bigEndian: false, byteOrderMark: true), 2),
        [0xFE, 0xFF, ..] => (new UnicodeEncoding(bigEndian: true, byteOrderMark: true), 2),
        _ => (new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 0),
    };

    /// <summary>A NUL byte in the first 8 KB means binary, as Git decides.</summary>
    private static bool LooksBinary(byte[] bytes) => bytes.AsSpan(0, Math.Min(bytes.Length, 8000)).IndexOf((byte)0) >= 0;
}
