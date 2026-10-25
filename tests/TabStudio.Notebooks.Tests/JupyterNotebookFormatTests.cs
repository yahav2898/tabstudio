using System.Text.Json.Nodes;
using TabStudio.Notebooks.Formats;
using TabStudio.Notebooks.Outputs;

namespace TabStudio.Notebooks.Tests;

public sealed class JupyterNotebookFormatTests
{
    [Fact]
    public void Reads_cells_languages_and_outputs_from_a_VS_Code_notebook()
    {
        var document = JupyterNotebookFormat.Parse(Fixtures.VsCodeNotebook);

        Assert.Equal(NotebookFormat.Jupyter, document.Format);
        Assert.Equal(NotebookLanguages.CSharp, document.DefaultLanguage);
        Assert.Equal(6, document.Cells.Count);

        var markdown = document.Cells[0];
        Assert.Equal(NotebookCellKind.Markdown, markdown.Kind);
        Assert.Equal("# Sales report\n\nNumbers for **October** — שלום 🙂", markdown.Source);

        var write = document.Cells[1];
        Assert.Equal("77476c72", write.Id);
        Assert.Equal(NotebookCellKind.Code, write.Kind);
        Assert.Equal(NotebookLanguages.CSharp, write.Language);
        Assert.Equal(1, write.ExecutionCount);
        var stream = Assert.IsType<StreamOutput>(Assert.Single(write.Outputs));
        Assert.Equal(StreamOutput.StandardOutput, stream.Name);
        Assert.Equal("output\r\n", stream.Text);

        var error = Assert.IsType<ErrorOutput>(Assert.Single(document.Cells[2].Outputs));
        Assert.Equal("Error", error.Name);
        Assert.Equal(2, error.Traceback.Count);

        var display = Assert.IsType<DisplayOutput>(Assert.Single(document.Cells[3].Outputs));
        Assert.False(display.IsResult);
        Assert.Contains("<td>Ada &amp; Co</td>", display.Data.Html, StringComparison.Ordinal);
        Assert.StartsWith("Name ", display.Data.PlainText, StringComparison.Ordinal);
        Assert.Equal("var sales = new[] { new { Name = \"Ada & Co\", Total = 42 } };\ndisplay(sales);", document.Cells[3].Source);

        Assert.Equal(NotebookLanguages.FSharp, document.Cells[4].Language);

        var empty = document.Cells[5];
        Assert.Equal(string.Empty, empty.Source);
        Assert.Null(empty.ExecutionCount);
        Assert.Empty(empty.Outputs);
    }

    [Fact]
    public void A_VS_Code_notebook_saves_back_byte_for_byte()
    {
        var document = JupyterNotebookFormat.Parse(Fixtures.VsCodeNotebook);

        Assert.Equal(Fixtures.VsCodeNotebook, JupyterNotebookFormat.Serialize(document));
    }

    [Fact]
    public void A_Jupyter_notebook_with_attachments_raw_cells_and_JSON_output_saves_back_byte_for_byte()
    {
        var document = JupyterNotebookFormat.Parse(Fixtures.JupyterNotebook);

        Assert.Equal(Fixtures.JupyterNotebook, JupyterNotebookFormat.Serialize(document));
    }

    [Fact]
    public void Reads_attachments_raw_cells_JSON_results_and_escaped_text_from_a_Jupyter_notebook()
    {
        var document = JupyterNotebookFormat.Parse(Fixtures.JupyterNotebook);

        var markdown = document.Cells[0];
        Assert.True(markdown.AdditionalProperties.ContainsKey("attachments"));
        Assert.Equal("""["intro"]""", markdown.Metadata.TryGetValue("tags", out var tags) ? tags?.ToJsonString() : null);

        var code = document.Cells[1];
        Assert.Equal(NotebookLanguages.CSharp, code.Language);
        var result = Assert.IsType<DisplayOutput>(code.Outputs[0]);
        Assert.True(result.IsResult);
        Assert.Equal(4, result.ExecutionCount);
        Assert.Equal("""{"answer":42,"items":[1,2.25]}""", result.Data[MimeTypes.Json]);
        Assert.Equal("A return value", result.Data.PlainText);

        var stderr = Assert.IsType<StreamOutput>(code.Outputs[1]);
        Assert.True(stderr.IsError);
        Assert.Equal("warning: \"quoted\" \\ backslash\ntab\there \u001b[0m", stderr.Text);

        var raw = document.Cells[2];
        Assert.Equal(NotebookCellKind.Raw, raw.Kind);
        Assert.Equal(".. note:: raw text", raw.Source);
    }

    [Fact]
    public void Editing_one_cell_keeps_everything_else_in_the_file()
    {
        var document = JupyterNotebookFormat.Parse(Fixtures.JupyterNotebook);
        var code = document.Cells[1];

        var edited = document.ReplaceCell(code with { Source = "\"An edited value\"" });
        var saved = JupyterNotebookFormat.Serialize(edited);

        // In the file, the source line is the JSON string "\"A return value\"".
        var expected = Fixtures.JupyterNotebook.Replace("\\\"A return value\\\"", "\\\"An edited value\\\"", StringComparison.Ordinal);
        Assert.NotEqual(Fixtures.JupyterNotebook, expected);
        Assert.Equal(expected, saved);
    }

    [Fact]
    public void Source_stored_as_a_single_string_is_read_and_saved_as_lines()
    {
        const string notebook = """
            {"cells": [{"cell_type": "code", "execution_count": null, "metadata": {}, "outputs": [], "source": "var a = 1;\nvar b = 2;"}],
             "metadata": {}, "nbformat": 4, "nbformat_minor": 4}
            """;

        var document = JupyterNotebookFormat.Parse(notebook);
        var saved = JsonNode.Parse(JupyterNotebookFormat.Serialize(document))!;

        Assert.Equal("var a = 1;\nvar b = 2;", document.Cells[0].Source);
        Assert.Equal("""["var a = 1;\n","var b = 2;"]""", saved["cells"]![0]!["source"]!.ToJsonString());
    }

    [Fact]
    public void Notebooks_from_before_format_4_5_do_not_gain_cell_ids()
    {
        var document = JupyterNotebookFormat.Parse(Fixtures.OldJupyterNotebook);
        var withNewCell = document.InsertCell(1, NotebookCell.Code("var y = 2;"));

        Assert.Equal(Fixtures.OldJupyterNotebook, JupyterNotebookFormat.Serialize(document));
        Assert.DoesNotContain("\"id\"", JupyterNotebookFormat.Serialize(withNewCell), StringComparison.Ordinal);
    }

    [Fact]
    public void A_new_notebook_is_written_with_the_dotnet_kernel_and_cell_ids()
    {
        var cell = NotebookCell.Code("Console.WriteLine(1);");
        var document = NotebookDocument.Create(NotebookCell.Markdown("# Title"), cell);

        var saved = JsonNode.Parse(JupyterNotebookFormat.Serialize(document))!;

        Assert.Equal(4, (int)saved["nbformat"]!);
        Assert.Equal(5, (int)saved["nbformat_minor"]!);
        Assert.Equal(".net-csharp", (string?)saved["metadata"]!["kernelspec"]!["name"]);
        Assert.Equal("C#", (string?)saved["metadata"]!["language_info"]!["name"]);
        Assert.Equal("csharp", (string?)saved["metadata"]!["polyglot_notebook"]!["kernelInfo"]!["defaultKernelName"]);
        Assert.Equal(cell.Id, (string?)saved["cells"]![1]!["id"]);
        Assert.Null(saved["cells"]![1]!["execution_count"]);
        Assert.Empty(saved["cells"]![1]!["outputs"]!.AsArray());
        Assert.Null(saved["cells"]![0]!["outputs"]);
    }

    [Fact]
    public void A_cell_whose_language_changed_records_it_in_its_metadata()
    {
        var document = JupyterNotebookFormat.Parse(Fixtures.JupyterNotebook);
        var changed = document.ReplaceCell(document.Cells[1] with { Language = NotebookLanguages.FSharp });

        var reread = JupyterNotebookFormat.Parse(JupyterNotebookFormat.Serialize(changed));

        Assert.Equal(NotebookLanguages.FSharp, reread.Cells[1].Language);
        Assert.Equal("fsharp", reread.Cells[1].Metadata.GetText("polyglot_notebook", "kernelName"));
        Assert.True(reread.Cells[1].Metadata.ContainsKey("custom_tool"));
    }

    [Fact]
    public void A_kernel_name_on_the_first_line_sets_the_language()
    {
        const string notebook = """
            {"cells": [{"cell_type": "code", "execution_count": null, "metadata": {}, "outputs": [], "source": ["#!fsharp\n", "let x = 1"]}],
             "metadata": {}, "nbformat": 4, "nbformat_minor": 5}
            """;

        var document = JupyterNotebookFormat.Parse(notebook);

        Assert.Equal(NotebookLanguages.FSharp, document.Cells[0].Language);
    }

    [Fact]
    public void Kernel_outputs_are_saved_as_Jupyter_outputs()
    {
        var cell = NotebookCell.Code("1 + 1") with
        {
            ExecutionCount = 7,
            Outputs =
            [
                new StreamOutput(StreamOutput.StandardOutput, "line 1\nline 2\n"),
                new DisplayOutput(MimeBundle.FromText("2")) { IsResult = true, ExecutionCount = 7 },
                new ErrorOutput("System.Exception", "boom", ["System.Exception: boom", "   at Submission#0"]),
            ],
        };

        var saved = JsonNode.Parse(JupyterNotebookFormat.Serialize(NotebookDocument.Create(cell)))!;
        var outputs = saved["cells"]![0]!["outputs"]!.AsArray();

        Assert.Equal(7, (int)saved["cells"]![0]!["execution_count"]!);
        Assert.Equal("""{"name":"stdout","output_type":"stream","text":["line 1\n","line 2\n"]}""", outputs[0]!.ToJsonString());
        Assert.Equal("""{"data":{"text/plain":["2"]},"execution_count":7,"metadata":{},"output_type":"execute_result"}""", outputs[1]!.ToJsonString());
        Assert.Equal("error", (string?)outputs[2]!["output_type"]);
        Assert.Equal("boom", (string?)outputs[2]!["evalue"]);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"cells": [], "metadata": {}, "nbformat": 3, "nbformat_minor": 0}""")]
    [InlineData("""{"metadata": {}, "nbformat": 4, "nbformat_minor": 5}""")]
    [InlineData("""{"cells": [{"cell_type": "heading", "source": []}], "metadata": {}, "nbformat": 4, "nbformat_minor": 5}""")]
    public void Files_that_are_not_nbformat_4_notebooks_are_rejected(string json)
    {
        Assert.Throws<NotebookFormatException>(() => JupyterNotebookFormat.Parse(json));
    }
}
