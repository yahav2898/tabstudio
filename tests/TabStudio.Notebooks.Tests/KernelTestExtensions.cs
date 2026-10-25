using TabStudio.Notebooks.Execution;
using TabStudio.Notebooks.Outputs;

namespace TabStudio.Notebooks.Tests;

/// <summary>Shortcuts for asserting on kernel results.</summary>
internal static class KernelTestExtensions
{
    /// <summary>The plain text of the cell's result, or <see langword="null"/> when it has none.</summary>
    public static string? ResultText(this CellExecutionResult result) =>
        result.Outputs.OfType<DisplayOutput>().SingleOrDefault(o => o.IsResult)?.Data.PlainText;

    /// <summary>Everything the cell wrote to a console stream.</summary>
    public static string StreamText(this CellExecutionResult result, string stream = StreamOutput.StandardOutput) =>
        string.Concat(result.Outputs.OfType<StreamOutput>().Where(o => o.Name == stream).Select(o => o.Text)).ReplaceLineEndings("\n");

    /// <summary>A readable account of a result, for assertion messages.</summary>
    public static string Describe(this CellExecutionResult result) =>
        $"{result.Status}: " + string.Join(" | ", result.Diagnostics.Select(d => d.ToString())
            .Concat(result.Outputs.OfType<ErrorOutput>().SelectMany(e => e.Traceback)));

    /// <summary>Runs code and asserts that it succeeded.</summary>
    public static async Task<CellExecutionResult> RunAsync(this CSharpKernel kernel, string code)
    {
        var result = await kernel.ExecuteAsync(code);
        Assert.True(result.Succeeded, result.Describe());
        return result;
    }
}
