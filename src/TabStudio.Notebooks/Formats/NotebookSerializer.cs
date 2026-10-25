using System.Text;

namespace TabStudio.Notebooks.Formats;

/// <summary>
/// Opens and saves notebooks in any supported format, chosen from the file extension.
/// </summary>
/// <remarks>
/// A notebook can be saved in a different format from the one it was read from, for example to turn a
/// <c>.dib</c> file into an <c>.ipynb</c> file. <c>.dib</c> files do not store outputs, so those are dropped
/// when saving to one.
/// </remarks>
public static class NotebookSerializer
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Works out a notebook's format from its file extension.</summary>
    /// <param name="path">The file path or name.</param>
    /// <returns>The format, or <see langword="null"/> when the file is not a notebook.</returns>
    public static NotebookFormat? GetFormat(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var extension = Path.GetExtension(path);
        if (string.Equals(extension, JupyterNotebookFormat.FileExtension, StringComparison.OrdinalIgnoreCase))
        {
            return NotebookFormat.Jupyter;
        }

        return string.Equals(extension, PolyglotNotebookFormat.FileExtension, StringComparison.OrdinalIgnoreCase)
            ? NotebookFormat.Polyglot
            : null;
    }

    /// <summary>Whether a file is a notebook TabStudio can open.</summary>
    /// <param name="path">The file path or name.</param>
    public static bool IsNotebook(string path) => GetFormat(path) is not null;

    /// <summary>Reads a notebook from text.</summary>
    /// <param name="text">The file's text.</param>
    /// <param name="format">The file's format.</param>
    /// <exception cref="NotebookFormatException">The text is not a valid notebook.</exception>
    public static NotebookDocument Parse(string text, NotebookFormat format) => format switch
    {
        NotebookFormat.Jupyter => JupyterNotebookFormat.Parse(text),
        NotebookFormat.Polyglot => PolyglotNotebookFormat.Parse(text),
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown notebook format."),
    };

    /// <summary>Writes a notebook as text.</summary>
    /// <param name="document">The notebook.</param>
    /// <param name="format">The format to write.</param>
    public static string Serialize(NotebookDocument document, NotebookFormat format) => format switch
    {
        NotebookFormat.Jupyter => JupyterNotebookFormat.Serialize(document),
        NotebookFormat.Polyglot => PolyglotNotebookFormat.Serialize(document),
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown notebook format."),
    };

    /// <summary>Opens a notebook file.</summary>
    /// <param name="path">The file, ending in <c>.ipynb</c> or <c>.dib</c>.</param>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <exception cref="NotebookFormatException">The file is not a valid notebook.</exception>
    public static async Task<NotebookDocument> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        var format = RequireFormat(path);
        var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return Parse(text, format);
    }

    /// <summary>Saves a notebook file as UTF-8 without a byte order mark, in the format its extension names.</summary>
    /// <param name="document">The notebook.</param>
    /// <param name="path">The file, ending in <c>.ipynb</c> or <c>.dib</c>.</param>
    /// <param name="cancellationToken">Cancels writing.</param>
    public static async Task SaveAsync(NotebookDocument document, string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        var text = Serialize(document, RequireFormat(path));
        await File.WriteAllTextAsync(path, text, Utf8WithoutBom, cancellationToken).ConfigureAwait(false);
    }

    private static NotebookFormat RequireFormat(string path) =>
        GetFormat(path) ?? throw new ArgumentException(
            $"'{Path.GetFileName(path)}' is not a notebook. Notebooks end in {JupyterNotebookFormat.FileExtension} or {PolyglotNotebookFormat.FileExtension}.",
            nameof(path));
}
