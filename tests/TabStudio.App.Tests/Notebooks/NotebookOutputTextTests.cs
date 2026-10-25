using TabStudio.App.Notebooks;
using TabStudio.App.Services;
using TabStudio.Notebooks;
using TabStudio.Notebooks.Formats;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Notebooks;

/// <summary>How notebook outputs saved as HTML or terminal text are turned into something TabStudio can show.</summary>
public sealed class NotebookOutputTextTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public void Reads_a_kernel_table_back_from_its_html()
    {
        const string Html = """
            <table>
            <thead>
            <tr><th>Name</th><th>Total</th></tr>
            </thead>
            <tbody>
            <tr><td>Ada &amp; Co</td><td>42</td></tr>
            <tr><td>&lt;null&gt;</td><td>7</td></tr>
            </tbody>
            <tfoot>
            <tr><td colspan="2">(98 more rows)</td></tr>
            </tfoot>
            </table>
            """;

        var table = HtmlTableReader.TryRead(Html);

        Assert.NotNull(table);
        Assert.Equal(new[] { "Name", "Total" }, table.Columns);
        Assert.Equal(new[] { "Ada & Co", "42" }, table.Rows[0]);
        Assert.Equal("<null>", table.Rows[1][0]);
        Assert.True(table.IsTruncated);
        Assert.Equal(100, table.TotalRowCount);
    }

    [Fact]
    public void Reads_polyglot_tables_with_markup_in_cells()
    {
        const string Html = "<table><thead><tr><th><i>index</i></th><th>value</th></tr></thead><tbody>"
            + "<tr><td>0</td><td><div class=\"dni-plaintext\"><pre>1</pre></div></td></tr></tbody></table>";

        var table = HtmlTableReader.TryRead(Html);

        Assert.NotNull(table);
        Assert.Equal(new[] { "index", "value" }, table.Columns);
        Assert.Equal(new[] { "0", "1" }, table.Rows[0]);
        Assert.False(table.IsTruncated);
    }

    [Theory]
    [InlineData("<b>bold</b>")]
    [InlineData("<table><tr><td><table><tr><td>nested</td></tr></table></td></tr></table>")]
    public void Leaves_other_html_to_the_text_view(string html) => Assert.Null(HtmlTableReader.TryRead(html));

    [Fact]
    public void Turns_html_into_readable_text()
    {
        var text = HtmlText.ToPlainText("<style>p { color: red }</style><h1>Title</h1><p>One &amp; two<br/>three</p><ul><li>a</li><li>b</li></ul>");

        Assert.Equal("Title\nOne & two\nthree\na\nb", text);
    }

    [Fact]
    public void Removes_terminal_colors() =>
        Assert.Equal("ZeroDivisionError: division by zero", AnsiText.Strip("\u001b[0;31mZeroDivisionError\u001b[0m: division by zero"));

    [Fact]
    public void New_notebook_files_get_a_free_name_and_a_code_cell()
    {
        var first = NotebookFiles.GetAvailablePath(_temp.Path);
        File.WriteAllText(first, NotebookFiles.ContentFor(first));
        var second = NotebookFiles.GetAvailablePath(_temp.Path);

        Assert.Equal("Untitled.ipynb", Path.GetFileName(first));
        Assert.Equal("Untitled-2.ipynb", Path.GetFileName(second));
        var cell = Assert.Single(NotebookSerializer.Parse(File.ReadAllText(first), NotebookFormat.Jupyter).Cells);
        Assert.True(cell.IsCode);
        var dib = NotebookSerializer.Parse(FileTemplates.ContentFor(_temp.Resolve("Script.dib"), project: null), NotebookFormat.Polyglot);
        Assert.True(Assert.Single(dib.Cells).IsCode);
    }

    [Fact]
    public void The_notebook_template_creates_a_titled_notebook_in_a_folder_of_its_own()
    {
        var path = new ProjectTemplates(new AppPaths(_temp.Path)).Create(ProjectTemplates.NotebookId, "Scratch");

        Assert.Equal("Scratch.ipynb", Path.GetFileName(path));
        Assert.Equal("Scratch", Path.GetFileName(Path.GetDirectoryName(path)));
        var notebook = NotebookSerializer.Parse(File.ReadAllText(path), NotebookFormat.Jupyter);
        Assert.Equal(new[] { NotebookCellKind.Markdown, NotebookCellKind.Code }, notebook.Cells.Select(c => c.Kind));
        Assert.StartsWith("# Scratch", notebook.Cells[0].Source, StringComparison.Ordinal);
    }

    public void Dispose() => _temp.Dispose();
}
