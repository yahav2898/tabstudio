using CommunityToolkit.Mvvm.ComponentModel;
using TabStudio.App.Notebooks;
using TabStudio.Notebooks.Outputs;

namespace TabStudio.App.ViewModels.Notebooks;

/// <summary>Text a cell wrote to the console. Standard error is shown in red.</summary>
public sealed partial class StreamOutputViewModel : NotebookOutputViewModel
{
    /// <summary>The most text one output keeps; older text is dropped first, as in a terminal.</summary>
    public const int MaxLength = 200_000;

    private const string TrimmedNotice = "… (earlier output trimmed)\n";

    /// <summary>Creates the output.</summary>
    /// <param name="stream">The stream name: <c>stdout</c> or <c>stderr</c>.</param>
    /// <param name="text">The text so far.</param>
    public StreamOutputViewModel(string stream, string text)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(text);
        Stream = stream;
        Text = Limit(AnsiText.Strip(text));
    }

    /// <summary>The stream name: <c>stdout</c> or <c>stderr</c>.</summary>
    public string Stream { get; }

    /// <summary>Whether the text was written to standard error.</summary>
    public bool IsError => Stream == StreamOutput.StandardError;

    /// <summary>The text, without a trailing line break.</summary>
    [ObservableProperty]
    public partial string Text { get; private set; }

    /// <summary>Adds text that arrived while the cell runs.</summary>
    /// <param name="text">More text from the same stream.</param>
    public void Append(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = Limit(Text + AnsiText.Strip(text));
    }

    partial void OnTextChanged(string value) => OnPropertyChanged(nameof(DisplayText));

    /// <summary>The text as shown: without the line break that ends the last line.</summary>
    public string DisplayText => Text.EndsWith('\n') ? Text.TrimEnd('\n', '\r') : Text;

    private static string Limit(string text) =>
        text.Length <= MaxLength ? text : string.Concat(TrimmedNotice, text.AsSpan(text.Length - MaxLength));
}
