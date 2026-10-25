using TabStudio.Notebooks;
using TabStudio.Notebooks.Formats;

namespace TabStudio.App.Notebooks;

/// <summary>Creating notebook files: their starting content and a free name for a new one.</summary>
public static class NotebookFiles
{
    /// <summary>The name of a new notebook, as in Visual Studio Code.</summary>
    public const string DefaultName = "Untitled";

    /// <summary>The extension of new notebooks: Jupyter's, which keeps outputs and opens everywhere.</summary>
    public const string DefaultExtension = ".ipynb";

    /// <summary>A new C# notebook with an empty code cell, ready to type into.</summary>
    /// <param name="title">A title for a Markdown cell at the top, or <see langword="null"/> for none.</param>
    public static NotebookDocument CreateNew(string? title = null) => title is null
        ? NotebookDocument.Create(NotebookCell.Code(string.Empty))
        : NotebookDocument.Create(
            NotebookCell.Markdown($"# {title}\n\nWrite C# in the cell below and run it with Ctrl+Enter or its \u25B6 button. Variables, functions and types carry over from one cell to the next."),
            NotebookCell.Code(string.Empty));

    /// <summary>The text of a new notebook file, in the format its extension names.</summary>
    /// <param name="path">The file path, ending in <c>.ipynb</c> or <c>.dib</c>.</param>
    /// <param name="title">A title for a Markdown cell at the top, or <see langword="null"/> for none.</param>
    public static string ContentFor(string path, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        return NotebookSerializer.Serialize(CreateNew(title), NotebookSerializer.GetFormat(path) ?? NotebookFormat.Jupyter);
    }

    /// <summary>Finds a file name in a folder that is not taken: <c>Untitled.ipynb</c>, then <c>Untitled-2.ipynb</c> and so on.</summary>
    /// <param name="folder">The folder.</param>
    /// <param name="name">The preferred name, without extension.</param>
    /// <param name="extension">The extension, with its dot.</param>
    public static string GetAvailablePath(string folder, string name = DefaultName, string extension = DefaultExtension)
    {
        ArgumentNullException.ThrowIfNull(folder);
        var candidate = Path.Combine(folder, name + extension);
        for (var i = 2; File.Exists(candidate) || Directory.Exists(candidate); i++)
        {
            candidate = Path.Combine(folder, $"{name}-{i}{extension}");
        }

        return candidate;
    }

    /// <summary>Creates a new notebook file in a folder and returns its path.</summary>
    /// <param name="folder">The folder, which is created if needed.</param>
    /// <param name="name">The preferred name, without extension.</param>
    /// <param name="cancellationToken">Cancels writing.</param>
    public static async Task<string> CreateAsync(string folder, string name = DefaultName, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(folder);
        var path = GetAvailablePath(folder, name);
        await File.WriteAllTextAsync(path, ContentFor(path), cancellationToken);
        return path;
    }
}
