using System.Text;
using System.Text.Json.Nodes;

namespace TabStudio.Notebooks.Formats;

/// <summary>
/// Reads and writes Polyglot Notebooks files (<c>.dib</c>): plain text in which a line such as <c>#!csharp</c>
/// starts a cell in that language.
/// </summary>
/// <remarks>
/// <para>The format, as dotnet/interactive reads and writes it:</para>
/// <code>
/// #!meta
///
/// {"kernelInfo":{"defaultKernelName":"csharp","items":[{"aliases":[],"name":"csharp"}]}}
///
/// #!markdown
///
/// # A heading
///
/// #!csharp
///
/// Console.WriteLine("Hello");
/// </code>
/// <para>
/// A <c>#!</c> line starts a new cell only when it names a known kernel or one of its aliases; any other
/// <c>#!</c> line, such as <c>#!time</c>, is a magic command and belongs to the cell. Blank lines around a
/// cell are not part of it, empty cells are not written, and the format has no outputs, cell ids or cell
/// metadata. Raw cells are written as Markdown, since the format has no raw cells.
/// </para>
/// </remarks>
public static class PolyglotNotebookFormat
{
    /// <summary>The file extension of Polyglot Notebooks files.</summary>
    public const string FileExtension = ".dib";

    private const string MagicPrefix = "#!";
    private const string MetadataMarker = "#!meta";

    private static readonly string[] LineSeparators = ["\r\n", "\n"];

    /// <summary>Reads a notebook from the text of a <c>.dib</c> file.</summary>
    /// <param name="text">The file's text.</param>
    /// <exception cref="NotebookFormatException">The <c>#!meta</c> block is not a JSON object.</exception>
    public static NotebookDocument Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        string[] lines = text.Length == 0 ? [] : text.Split(LineSeparators, StringSplitOptions.None);
        var metadata = NotebookMetadata.Empty;
        var declaredKernels = new Dictionary<string, string>(StringComparer.Ordinal);
        var cells = new List<NotebookCell>();
        var content = new List<string>();
        var language = NotebookLanguages.CSharp;
        var foundMetadata = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (!foundMetadata && line.StartsWith(MetadataMarker, StringComparison.Ordinal))
            {
                foundMetadata = true;
                var json = new StringBuilder();
                while (i + 1 < lines.Length && !lines[i + 1].StartsWith(MagicPrefix, StringComparison.Ordinal))
                {
                    json.Append(lines[++i]).Append('\n');
                }

                metadata = ParseMetadata(json.ToString());
                declaredKernels = KernelInfoMetadata.GetDeclaredKernels(metadata);
                continue;
            }

            if (line.StartsWith(MagicPrefix, StringComparison.Ordinal) &&
                TryGetKernel(line[MagicPrefix.Length..], declaredKernels, out var kernel))
            {
                AddCell(cells, language, content);
                language = kernel;
                content.Clear();
            }
            else
            {
                content.Add(line);
            }
        }

        AddCell(cells, language, content);
        if (cells.Count == 0)
        {
            cells.Add(CreateCell(language, string.Empty));
        }

        return new NotebookDocument { Cells = cells, Metadata = metadata, Format = NotebookFormat.Polyglot };
    }

    /// <summary>Writes a notebook as the text of a <c>.dib</c> file. Outputs are not part of the format.</summary>
    /// <param name="document">The notebook.</param>
    /// <param name="newLine">The line break to write: <c>\n</c> unless given, as dotnet/interactive writes.</param>
    public static string Serialize(NotebookDocument document, string newLine = "\n")
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrEmpty(newLine);

        var lines = new List<string>();
        if (CreateMetadata(document) is { Count: > 0 } metadata)
        {
            lines.Add(MetadataMarker);
            lines.Add(string.Empty);
            lines.Add(NotebookJsonWriter.WriteCompact(metadata));
            lines.Add(string.Empty);
        }

        foreach (var cell in document.Cells)
        {
            var cellLines = TrimBlankLines(cell.Source.Split(LineSeparators, StringSplitOptions.None));
            if (cellLines.Count == 0)
            {
                continue;
            }

            lines.Add(MagicPrefix + (cell.Kind == NotebookCellKind.Code ? cell.Language : NotebookLanguages.Markdown));
            lines.Add(string.Empty);
            lines.AddRange(cellLines);
            lines.Add(string.Empty);
        }

        return string.Join(newLine, lines);
    }

    private static NotebookMetadata ParseMetadata(string json) =>
        string.IsNullOrWhiteSpace(json) ? NotebookMetadata.Empty : NotebookMetadata.Parse(json);

    /// <summary>
    /// The <c>#!meta</c> object to write: the metadata of a notebook read from a <c>.dib</c> file or created in
    /// memory as it is, and only the Polyglot kernel list of one read from an <c>.ipynb</c> file.
    /// </summary>
    private static JsonObject? CreateMetadata(NotebookDocument document)
    {
        if (document.Format != NotebookFormat.Jupyter)
        {
            return document.Metadata.ToJsonObject();
        }

        return KernelInfoMetadata.FindKernelInfo(document.Metadata) is { } kernelInfo
            ? new JsonObject { [KernelInfoMetadata.KernelInfoKey] = kernelInfo.DeepClone() }
            : null;
    }

    private static bool TryGetKernel(string name, Dictionary<string, string> declaredKernels, out string kernel)
    {
        if (declaredKernels.TryGetValue(name, out var declared))
        {
            kernel = NotebookLanguages.Normalize(declared);
            return true;
        }

        if (NotebookLanguages.TryNormalize(name, out var known))
        {
            kernel = known;
            return true;
        }

        kernel = string.Empty;
        return false;
    }

    private static void AddCell(List<NotebookCell> cells, string language, List<string> lines)
    {
        var trimmed = TrimBlankLines(lines);
        if (trimmed.Count > 0)
        {
            cells.Add(CreateCell(language, string.Join('\n', trimmed)));
        }
    }

    private static List<string> TrimBlankLines(IReadOnlyList<string> lines)
    {
        var start = 0;
        var end = lines.Count;
        while (start < end && lines[start].Length == 0)
        {
            start++;
        }

        while (end > start && lines[end - 1].Length == 0)
        {
            end--;
        }

        return lines.Skip(start).Take(end - start).ToList();
    }

    private static NotebookCell CreateCell(string language, string source) => language == NotebookLanguages.Markdown
        ? new NotebookCell { Kind = NotebookCellKind.Markdown, Language = NotebookLanguages.Markdown, Source = source, HasGeneratedId = true }
        : new NotebookCell { Kind = NotebookCellKind.Code, Language = language, Source = source, HasGeneratedId = true };
}
