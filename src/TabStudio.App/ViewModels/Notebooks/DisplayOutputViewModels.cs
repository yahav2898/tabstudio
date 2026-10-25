using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Notebooks;
using TabStudio.App.Notebooks.Markdown;

namespace TabStudio.App.ViewModels.Notebooks;

/// <summary>A value shown as text: a cell's result or something it displayed.</summary>
public sealed class TextOutputViewModel : NotebookOutputViewModel
{
    /// <summary>Creates the output.</summary>
    /// <param name="text">The text.</param>
    public TextOutputViewModel(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = AnsiText.Strip(text).TrimEnd('\n', '\r');
    }

    /// <summary>The text.</summary>
    public string Text { get; }
}

/// <summary>
/// HTML output. Avalonia cannot render HTML, so its plain-text version is shown, with a toggle to read the HTML
/// source.
/// </summary>
public sealed partial class HtmlOutputViewModel : NotebookOutputViewModel
{
    private readonly string _text;

    /// <summary>Creates the output.</summary>
    /// <param name="html">The HTML.</param>
    /// <param name="plainText">The plain-text version stored with it, if any.</param>
    public HtmlOutputViewModel(string html, string? plainText)
    {
        ArgumentNullException.ThrowIfNull(html);
        Html = html.TrimEnd('\n', '\r');
        _text = string.IsNullOrWhiteSpace(plainText) ? HtmlText.ToPlainText(html) : AnsiText.Strip(plainText).TrimEnd('\n', '\r');
    }

    /// <summary>The HTML source.</summary>
    public string Html { get; }

    /// <summary>Whether the HTML source is shown instead of the text.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Text), nameof(ToggleText))]
    public partial bool ShowsSource { get; set; }

    /// <summary>What is shown: the text, or the HTML source.</summary>
    public string Text => ShowsSource ? Html : _text;

    /// <summary>The label of the toggle.</summary>
    public string ToggleText => ShowsSource ? "View text" : "View source";

    [RelayCommand]
    private void ToggleSource() => ShowsSource = !ShowsSource;
}

/// <summary>Markdown output, rendered like a Markdown cell.</summary>
public sealed class MarkdownOutputViewModel : NotebookOutputViewModel
{
    /// <summary>Creates the output.</summary>
    /// <param name="markdown">The Markdown text.</param>
    public MarkdownOutputViewModel(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        Blocks = MarkdownParser.Parse(markdown);
    }

    /// <summary>The parsed Markdown.</summary>
    public IReadOnlyList<MarkdownBlock> Blocks { get; }
}

/// <summary>An image, such as a chart a cell drew as a PNG.</summary>
public sealed class ImageOutputViewModel : NotebookOutputViewModel
{
    private ImageOutputViewModel(Bitmap image)
    {
        Image = image;
    }

    /// <summary>The decoded image.</summary>
    public Bitmap Image { get; }

    /// <summary>Decodes a base64 PNG, as notebooks store images.</summary>
    /// <param name="base64">The image data.</param>
    /// <returns>The output, or <see langword="null"/> when the data is not an image.</returns>
    public static ImageOutputViewModel? TryCreate(string base64)
    {
        ArgumentNullException.ThrowIfNull(base64);
        try
        {
            using var stream = new MemoryStream(Convert.FromBase64String(base64));
            return new ImageOutputViewModel(new Bitmap(stream));
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException or IOException)
        {
            return null;
        }
    }
}
