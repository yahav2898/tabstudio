using System.Text.Json.Nodes;
using TabStudio.Notebooks.Formats;
using TabStudio.Notebooks.Outputs;
using TabStudio.Testing;

namespace TabStudio.Notebooks.Tests;

public sealed class NotebookDocumentTests
{
    [Fact]
    public void Editing_returns_a_new_document_and_leaves_the_old_one_alone()
    {
        var first = NotebookCell.Code("var a = 1;");
        var second = NotebookCell.Markdown("# Notes");
        var document = NotebookDocument.Create(first, second);

        var inserted = document.InsertCell(1, NotebookCell.Raw("raw"));
        var moved = inserted.MoveCell(first.Id, 2);
        var removed = moved.RemoveCell(second.Id);
        var replaced = removed.ReplaceCell(first with { Source = "var a = 2;" });

        Assert.Equal(2, document.Cells.Count);
        Assert.Equal(new[] { NotebookCellKind.Code, NotebookCellKind.Raw, NotebookCellKind.Markdown }, inserted.Cells.Select(c => c.Kind));
        Assert.Equal(2, moved.IndexOf(first.Id));
        Assert.Null(removed.FindCell(second.Id));
        Assert.Equal("var a = 2;", replaced.FindCell(first.Id)?.Source);
        Assert.Throws<ArgumentException>(() => document.ReplaceCell(NotebookCell.Code("unknown")));
    }

    [Fact]
    public void Clearing_outputs_resets_execution_counts()
    {
        var cell = NotebookCell.Code("1") with
        {
            ExecutionCount = 3,
            Outputs = [new DisplayOutput(MimeBundle.FromText("1")) { IsResult = true, ExecutionCount = 3 }],
        };

        var cleared = NotebookDocument.Create(cell).WithoutOutputs().Cells[0];

        Assert.Empty(cleared.Outputs);
        Assert.Null(cleared.ExecutionCount);
    }

    [Fact]
    public void Metadata_is_immutable_and_compares_by_content()
    {
        var original = NotebookMetadata.Parse("""{"tags":["a"],"nested":{"value":1}}""");
        var copy = original.ToJsonObject();
        copy["tags"] = new JsonArray("changed");

        var changed = original.With("extra", JsonValue.Create(true));
        var reordered = NotebookMetadata.Parse("""{"nested":{"value":1},"tags":["a"]}""");

        Assert.Equal("""{"tags":["a"],"nested":{"value":1}}""", original.ToJsonString());
        Assert.Equal(3, changed.Count);
        Assert.Equal(2, original.Count);
        Assert.Equal(original, reordered);
        Assert.NotEqual(original, changed);
        Assert.Equal(original, changed.Without("extra"));
        Assert.Throws<NotebookFormatException>(() => NotebookMetadata.Parse("[1]"));
    }

    [Fact]
    public void Language_names_and_aliases_are_normalized()
    {
        Assert.Equal(NotebookLanguages.CSharp, NotebookLanguages.Normalize("C#"));
        Assert.Equal(NotebookLanguages.CSharp, NotebookLanguages.Normalize("cs"));
        Assert.Equal(NotebookLanguages.PowerShell, NotebookLanguages.Normalize("powershell"));
        Assert.Equal("python", NotebookLanguages.Normalize("python"));
        Assert.True(NotebookLanguages.IsCSharp("csharp"));
        Assert.False(NotebookLanguages.IsCSharp("fsharp"));
        Assert.Equal("F#", NotebookLanguages.GetDisplayName("fsharp"));
    }

    [Fact]
    public async Task Notebooks_load_and_save_by_file_extension()
    {
        using var directory = new TempDirectory();
        var ipynb = directory.Write("Report.ipynb", Fixtures.VsCodeNotebook);
        var dib = directory.Resolve("Report.dib");

        var document = await NotebookSerializer.LoadAsync(ipynb);
        await NotebookSerializer.SaveAsync(document, dib);
        await NotebookSerializer.SaveAsync(document, ipynb);

        Assert.Equal(Fixtures.VsCodeNotebook, await File.ReadAllTextAsync(ipynb));
        var converted = await NotebookSerializer.LoadAsync(dib);

        // .dib files do not keep empty cells, and the notebook ends with one.
        Assert.Equal(document.Cells.Count - 1, converted.Cells.Count);
        Assert.Equal(document.Cells[4].Source, converted.Cells[4].Source);
        Assert.Equal(NotebookLanguages.FSharp, converted.Cells[4].Language);
        Assert.Equal(NotebookFormat.Polyglot, NotebookSerializer.GetFormat("a/b/Notes.DIB"));
        Assert.Null(NotebookSerializer.GetFormat("Program.cs"));
        await Assert.ThrowsAsync<ArgumentException>(() => NotebookSerializer.LoadAsync(directory.Write("Program.cs", "class P {}")));
    }
}
