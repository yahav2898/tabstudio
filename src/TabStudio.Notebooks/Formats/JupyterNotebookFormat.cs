using System.Text.Json;
using System.Text.Json.Nodes;
using TabStudio.Notebooks.Outputs;

namespace TabStudio.Notebooks.Formats;

/// <summary>
/// Reads and writes Jupyter notebooks (<c>.ipynb</c>, nbformat 4), including the metadata that VS Code and
/// Polyglot Notebooks add for .NET.
/// </summary>
/// <remarks>
/// <para>
/// Saving writes the canonical form that Jupyter and VS Code write: keys sorted, one space of indentation,
/// multi-line text as a list of lines and a final newline. A notebook already in that form saves back byte for
/// byte, and everything TabStudio does not interpret, such as other tools' metadata and cell attachments, is kept.
/// </para>
/// <para>
/// Cell ids are written for notebooks of format 4.5 and later, as the format requires, except for cells that
/// had none in the file they were read from.
/// </para>
/// </remarks>
public static class JupyterNotebookFormat
{
    /// <summary>The file extension of Jupyter notebooks.</summary>
    public const string FileExtension = ".ipynb";

    /// <summary>The nbformat major version this class reads and writes.</summary>
    public const int FormatVersion = 4;

    /// <summary>The nbformat minor version of new notebooks: 4.5, the first with cell ids.</summary>
    public const int LatestMinorVersion = 5;

    private const string NbFormatKey = "nbformat";
    private const string NbFormatMinorKey = "nbformat_minor";
    private const string CellsKey = "cells";
    private const string MetadataKey = "metadata";
    private const string CodeCellType = "code";
    private const string MarkdownCellType = "markdown";
    private const string RawCellType = "raw";

    private static readonly JsonDocumentOptions ReadOptions = new() { MaxDepth = 256 };

    /// <summary>Reads a notebook from the JSON text of an <c>.ipynb</c> file.</summary>
    /// <param name="json">The file's text.</param>
    /// <exception cref="NotebookFormatException">The text is not an nbformat 4 notebook.</exception>
    public static NotebookDocument Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            return Read(json);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException or FormatException)
        {
            throw new NotebookFormatException($"The notebook could not be read: {ex.Message}", ex);
        }
    }

    /// <summary>Writes a notebook as the JSON text of an <c>.ipynb</c> file.</summary>
    /// <param name="document">The notebook.</param>
    /// <returns>The text, ending with a newline.</returns>
    public static string Serialize(NotebookDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var minor = JsonNodes.IntOf(document.AdditionalProperties.Peek(NbFormatMinorKey)) ?? LatestMinorVersion;
        var writeIds = minor >= 5;
        var defaultLanguage = document.DefaultLanguage;

        var cells = new JsonArray();
        foreach (var cell in document.Cells)
        {
            cells.Add(WriteCell(cell, defaultLanguage, writeIds));
        }

        var root = document.AdditionalProperties.ToJsonObject();
        root[NbFormatKey] ??= FormatVersion;
        root[NbFormatMinorKey] ??= minor;
        root[MetadataKey] = WriteMetadata(document);
        root[CellsKey] = cells;

        return NotebookJsonWriter.WriteIndented(root) + "\n";
    }

    /// <summary>
    /// Works out a code cell's language the way Polyglot Notebooks does: a kernel name on the first line, such as
    /// <c>#!fsharp</c>, wins over the cell's metadata, and the notebook's default applies when neither says.
    /// </summary>
    internal static string InferCodeLanguage(NotebookMetadata metadata, string source, string defaultLanguage)
    {
        if (source.StartsWith("#!", StringComparison.Ordinal))
        {
            var end = source.AsSpan(2).IndexOfAny(' ', '\r', '\n');
            var magic = end < 0 ? source[2..] : source.Substring(2, end);
            if (NotebookLanguages.TryNormalize(magic, out var byMagic))
            {
                return byMagic;
            }
        }

        var name = metadata.GetText(KernelInfoMetadata.PolyglotKey, "kernelName")
            ?? metadata.GetText(KernelInfoMetadata.DotNetInteractiveKey, "language");
        return name is null ? defaultLanguage : NotebookLanguages.Normalize(name);
    }

    private static NotebookDocument Read(string json)
    {
        if (JsonNode.Parse(json, documentOptions: ReadOptions) is not JsonObject root)
        {
            throw new NotebookFormatException("A Jupyter notebook must be a JSON object.");
        }

        var major = JsonNodes.IntOf(root[NbFormatKey]);
        if (major != FormatVersion)
        {
            throw new NotebookFormatException(major is null
                ? "The file does not say which notebook format version it uses."
                : $"Only nbformat {FormatVersion} notebooks can be opened; this one uses version {major}.");
        }

        var metadata = NotebookMetadata.Own(JsonNodes.TakeObject(root, MetadataKey) ?? new JsonObject());
        var defaultLanguage = KernelInfoMetadata.GetDefaultLanguage(metadata);
        if (JsonNodes.Take(root, CellsKey) is not JsonArray cellNodes)
        {
            throw new NotebookFormatException("The notebook has no list of cells.");
        }

        var cells = new List<NotebookCell>(cellNodes.Count);
        for (var i = 0; i < cellNodes.Count; i++)
        {
            if (cellNodes[i] is not JsonObject cell)
            {
                throw new NotebookFormatException($"Cell {i + 1} is not a JSON object.");
            }

            cells.Add(ReadCell(cell, i, defaultLanguage));
        }

        return new NotebookDocument
        {
            Cells = cells,
            Metadata = metadata,
            AdditionalProperties = NotebookMetadata.Own(root),
            Format = NotebookFormat.Jupyter,
        };
    }

    private static NotebookCell ReadCell(JsonObject cell, int index, string defaultLanguage)
    {
        var type = JsonNodes.TextOf(JsonNodes.Take(cell, "cell_type"));
        var kind = type switch
        {
            CodeCellType => NotebookCellKind.Code,
            MarkdownCellType => NotebookCellKind.Markdown,
            RawCellType => NotebookCellKind.Raw,
            null => throw new NotebookFormatException($"Cell {index + 1} has no cell_type."),
            _ => throw new NotebookFormatException($"Cell {index + 1} has an unknown cell_type '{type}'."),
        };

        var id = JsonNodes.TextOf(JsonNodes.Take(cell, "id"));
        var metadata = NotebookMetadata.Own(JsonNodes.TakeObject(cell, MetadataKey) ?? new JsonObject());
        var source = JsonNodes.JoinText(JsonNodes.Take(cell, "source"));

        var outputs = new List<NotebookOutput>();
        int? executionCount = null;
        if (kind == NotebookCellKind.Code)
        {
            outputs = ReadOutputs(JsonNodes.Take(cell, "outputs"), index);
            executionCount = JsonNodes.IntOf(JsonNodes.Take(cell, "execution_count"));
        }

        return new NotebookCell
        {
            Id = id ?? NotebookCell.NewId(),
            HasGeneratedId = id is null,
            Kind = kind,
            Language = kind switch
            {
                NotebookCellKind.Markdown => NotebookLanguages.Markdown,
                NotebookCellKind.Raw => NotebookLanguages.Raw,
                _ => InferCodeLanguage(metadata, source, defaultLanguage),
            },
            Source = source,
            Outputs = outputs,
            ExecutionCount = executionCount,
            Metadata = metadata,
            AdditionalProperties = NotebookMetadata.Own(cell),
        };
    }

    private static List<NotebookOutput> ReadOutputs(JsonNode? node, int cellIndex)
    {
        var outputs = new List<NotebookOutput>();
        if (node is not JsonArray items)
        {
            return outputs;
        }

        foreach (var item in items)
        {
            if (item is not JsonObject output)
            {
                throw new NotebookFormatException($"An output of cell {cellIndex + 1} is not a JSON object.");
            }

            outputs.Add(ReadOutput(output, cellIndex));
        }

        return outputs;
    }

    private static NotebookOutput ReadOutput(JsonObject output, int cellIndex)
    {
        var type = JsonNodes.TextOf(JsonNodes.Take(output, "output_type"));
        NotebookOutput result = type switch
        {
            "stream" => new StreamOutput(
                JsonNodes.TextOf(JsonNodes.Take(output, "name")) ?? StreamOutput.StandardOutput,
                JsonNodes.JoinText(JsonNodes.Take(output, "text"))),
            "display_data" => ReadDisplay(output, isResult: false),
            "execute_result" => ReadDisplay(output, isResult: true),
            "error" => new ErrorOutput(
                JsonNodes.TextOf(JsonNodes.Take(output, "ename")) ?? string.Empty,
                JsonNodes.TextOf(JsonNodes.Take(output, "evalue")) ?? string.Empty,
                JsonNodes.ReadList(JsonNodes.Take(output, "traceback"))),
            _ => throw new NotebookFormatException($"Cell {cellIndex + 1} has an output of unknown type '{type}'."),
        };

        return result with { AdditionalProperties = NotebookMetadata.Own(output) };
    }

    private static DisplayOutput ReadDisplay(JsonObject output, bool isResult)
    {
        var data = ReadMimeBundle(JsonNodes.TakeObject(output, "data"));
        var metadata = NotebookMetadata.Own(JsonNodes.TakeObject(output, MetadataKey) ?? new JsonObject());
        var executionCount = isResult ? JsonNodes.IntOf(JsonNodes.Take(output, "execution_count")) : null;
        return new DisplayOutput(data) { Metadata = metadata, IsResult = isResult, ExecutionCount = executionCount };
    }

    private static MimeBundle ReadMimeBundle(JsonObject? data)
    {
        if (data is null)
        {
            return MimeBundle.Empty;
        }

        var entries = new List<KeyValuePair<string, string>>(data.Count);
        foreach (var (mimeType, value) in data)
        {
            var text = MimeTypes.IsJson(mimeType) && value is not null && JsonNodes.TextOf(value) is null
                ? NotebookJsonWriter.WriteCompact(value)
                : JsonNodes.JoinText(value);
            entries.Add(new(mimeType, text));
        }

        return MimeBundle.Create(entries);
    }

    private static JsonObject WriteCell(NotebookCell cell, string defaultLanguage, bool writeIds)
    {
        var json = cell.AdditionalProperties.ToJsonObject();
        json["cell_type"] = cell.Kind switch
        {
            NotebookCellKind.Code => CodeCellType,
            NotebookCellKind.Markdown => MarkdownCellType,
            _ => RawCellType,
        };

        if (writeIds && !cell.HasGeneratedId)
        {
            json["id"] = cell.Id;
        }

        json[MetadataKey] = WriteCellMetadata(cell, defaultLanguage);
        json["source"] = JsonNodes.SplitLines(cell.Source);

        if (cell.Kind == NotebookCellKind.Code)
        {
            json["execution_count"] = cell.ExecutionCount;
            var outputs = new JsonArray();
            foreach (var output in cell.Outputs)
            {
                outputs.Add(WriteOutput(output));
            }

            json["outputs"] = outputs;
        }

        return json;
    }

    /// <summary>
    /// Writes a cell's metadata, recording its language the way Polyglot Notebooks does only when the language
    /// cannot already be told from the metadata, the first line or the notebook's default.
    /// </summary>
    private static JsonObject WriteCellMetadata(NotebookCell cell, string defaultLanguage)
    {
        var metadata = cell.Metadata.ToJsonObject();
        if (cell.Kind == NotebookCellKind.Code &&
            InferCodeLanguage(cell.Metadata, cell.Source, defaultLanguage) != NotebookLanguages.Normalize(cell.Language))
        {
            JsonNodes.GetOrAddObject(metadata, KernelInfoMetadata.DotNetInteractiveKey)["language"] = cell.Language;
            JsonNodes.GetOrAddObject(metadata, KernelInfoMetadata.PolyglotKey)["kernelName"] = cell.Language;
        }

        return metadata;
    }

    /// <summary>
    /// Writes the notebook's metadata. Metadata read from an <c>.ipynb</c> file is written as it was; a notebook
    /// read from a <c>.dib</c> file or created in memory gets the .NET kernel description Polyglot Notebooks writes.
    /// </summary>
    private static JsonObject WriteMetadata(NotebookDocument document)
    {
        var metadata = document.Metadata.ToJsonObject();
        if (document.Format == NotebookFormat.Jupyter)
        {
            return metadata;
        }

        var language = document.DefaultLanguage;
        var kernelInfo = JsonNodes.TakeObject(metadata, KernelInfoMetadata.KernelInfoKey)
            ?? KernelInfoMetadata.CreateKernelInfo(language);

        var result = new JsonObject
        {
            [KernelInfoMetadata.KernelSpecKey] = KernelInfoMetadata.CreateKernelSpec(language),
            [KernelInfoMetadata.LanguageInfoKey] = KernelInfoMetadata.CreateLanguageInfo(language),
            [KernelInfoMetadata.PolyglotKey] = new JsonObject { [KernelInfoMetadata.KernelInfoKey] = kernelInfo },
        };

        foreach (var key in metadata.Select(p => p.Key).ToList())
        {
            result[key] = JsonNodes.Take(metadata, key);
        }

        return result;
    }

    private static JsonObject WriteOutput(NotebookOutput output)
    {
        var json = output.AdditionalProperties.ToJsonObject();
        switch (output)
        {
            case StreamOutput stream:
                json["name"] = stream.Name;
                json["output_type"] = "stream";
                json["text"] = JsonNodes.SplitLines(stream.Text);
                break;

            case DisplayOutput display:
                json["data"] = WriteMimeBundle(display.Data);
                json[MetadataKey] = display.Metadata.ToJsonObject();
                json["output_type"] = display.IsResult ? "execute_result" : "display_data";
                if (display.IsResult)
                {
                    json["execution_count"] = display.ExecutionCount;
                }

                break;

            case ErrorOutput error:
                var traceback = new JsonArray();
                foreach (var line in error.Traceback)
                {
                    traceback.Add(JsonValue.Create(line));
                }

                json["ename"] = error.Name;
                json["evalue"] = error.Message;
                json["output_type"] = "error";
                json["traceback"] = traceback;
                break;

            default:
                throw new ArgumentException($"Outputs of type {output.GetType().Name} cannot be saved.", nameof(output));
        }

        return json;
    }

    private static JsonObject WriteMimeBundle(MimeBundle data)
    {
        var json = new JsonObject();
        foreach (var (mimeType, value) in data.Entries)
        {
            if (MimeTypes.IsJson(mimeType) && TryParseJson(value, out var parsed))
            {
                json[mimeType] = parsed;
            }
            else if (MimeTypes.IsMultilineText(mimeType))
            {
                json[mimeType] = JsonNodes.SplitLines(value);
            }
            else
            {
                json[mimeType] = value;
            }
        }

        return json;
    }

    private static bool TryParseJson(string text, out JsonNode? node)
    {
        try
        {
            node = JsonNode.Parse(text, documentOptions: ReadOptions);
            return node is not null && JsonNodes.TextOf(node) is null;
        }
        catch (JsonException)
        {
            node = null;
            return false;
        }
    }
}
