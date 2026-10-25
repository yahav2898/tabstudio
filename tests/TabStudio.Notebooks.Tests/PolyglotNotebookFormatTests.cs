using TabStudio.Notebooks.Formats;

namespace TabStudio.Notebooks.Tests;

public sealed class PolyglotNotebookFormatTests
{
    [Fact]
    public void Reads_cells_in_every_language_and_the_kernel_list()
    {
        var document = PolyglotNotebookFormat.Parse(Fixtures.PolyglotDib);

        Assert.Equal(NotebookFormat.Polyglot, document.Format);
        Assert.Equal(NotebookLanguages.CSharp, document.DefaultLanguage);
        Assert.Equal(
            new[] { "markdown", "csharp", "fsharp", "pwsh", "javascript", "mermaid" },
            document.Cells.Select(c => c.Language));
        Assert.Equal(NotebookCellKind.Markdown, document.Cells[0].Kind);
        Assert.All(document.Cells.Skip(1), cell => Assert.Equal(NotebookCellKind.Code, cell.Kind));
        Assert.Equal("* Markdown code", document.Cells[0].Source);
        Assert.Equal("// C# code", document.Cells[1].Source);
        Assert.True(document.Metadata.ContainsKey("kernelInfo"));
    }

    [Fact]
    public void The_dotnet_interactive_round_trip_file_saves_back_unchanged()
    {
        var document = PolyglotNotebookFormat.Parse(Fixtures.PolyglotDib);

        Assert.Equal(Fixtures.PolyglotDib, PolyglotNotebookFormat.Serialize(document));
    }

    [Fact]
    public void Magic_commands_stay_in_their_cell_and_aliases_start_cells()
    {
        var document = PolyglotNotebookFormat.Parse(Fixtures.CSharpDib);

        Assert.Equal(3, document.Cells.Count);
        Assert.Equal("# Prime numbers\n\nUsing a **sieve**.", document.Cells[0].Source);
        Assert.StartsWith("#!time\nstatic bool IsPrime", document.Cells[1].Source, StringComparison.Ordinal);
        Assert.Equal(NotebookLanguages.CSharp, document.Cells[2].Language);
        Assert.Equal("Enumerable.Range(1, 30).Where(IsPrime)", document.Cells[2].Source);
    }

    [Fact]
    public void Saving_writes_canonical_kernel_names_and_keeps_the_metadata()
    {
        var document = PolyglotNotebookFormat.Parse(Fixtures.CSharpDib);

        var saved = PolyglotNotebookFormat.Serialize(document);

        Assert.Equal(Fixtures.CSharpDib.Replace("#!C#", "#!csharp", StringComparison.Ordinal), saved);
    }

    [Fact]
    public void A_file_without_metadata_starts_in_CSharp_and_saves_without_it()
    {
        const string text = "var x = 1;\n\n\n#!markdown\n\nNotes\n";

        var document = PolyglotNotebookFormat.Parse(text);

        Assert.Equal(2, document.Cells.Count);
        Assert.Equal(NotebookLanguages.CSharp, document.Cells[0].Language);
        Assert.Equal("var x = 1;", document.Cells[0].Source);
        Assert.Equal("#!csharp\n\nvar x = 1;\n\n#!markdown\n\nNotes\n", PolyglotNotebookFormat.Serialize(document));
    }

    [Fact]
    public void Windows_line_endings_are_read_and_can_be_written()
    {
        var text = Fixtures.CSharpDib.Replace("\n", "\r\n", StringComparison.Ordinal);

        var document = PolyglotNotebookFormat.Parse(text);

        Assert.Equal("# Prime numbers\n\nUsing a **sieve**.", document.Cells[0].Source);
        Assert.Equal(
            Fixtures.CSharpDib.Replace("#!C#", "#!csharp", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal),
            PolyglotNotebookFormat.Serialize(document, "\r\n"));
    }

    [Fact]
    public void Empty_cells_are_not_written_and_an_empty_file_has_one_empty_cell()
    {
        var document = NotebookDocument.Create(NotebookCell.Code(string.Empty), NotebookCell.Code("\n  x\n\n"));

        var saved = PolyglotNotebookFormat.Serialize(document);
        var empty = PolyglotNotebookFormat.Parse(string.Empty);

        Assert.Equal(
            "#!meta\n\n{\"kernelInfo\":{\"defaultKernelName\":\"csharp\",\"items\":[{\"aliases\":[],\"name\":\"csharp\"}]}}\n\n#!csharp\n\n  x\n",
            saved);
        Assert.Equal(NotebookLanguages.CSharp, Assert.Single(empty.Cells).Language);
    }

    [Fact]
    public void Converting_to_ipynb_and_back_keeps_cells_languages_and_kernels()
    {
        var dib = PolyglotNotebookFormat.Parse(Fixtures.CSharpDib);

        var ipynb = JupyterNotebookFormat.Parse(JupyterNotebookFormat.Serialize(dib));
        var back = PolyglotNotebookFormat.Parse(PolyglotNotebookFormat.Serialize(ipynb));

        Assert.Equal(".net-csharp", ipynb.Metadata.GetText("kernelspec", "name"));
        Assert.Equal(dib.Cells.Select(c => (c.Kind, c.Language, c.Source)), ipynb.Cells.Select(c => (c.Kind, c.Language, c.Source)));
        Assert.Equal(dib.Cells.Select(c => (c.Kind, c.Language, c.Source)), back.Cells.Select(c => (c.Kind, c.Language, c.Source)));
        Assert.Equal(dib.Metadata, back.Metadata);
    }
}
