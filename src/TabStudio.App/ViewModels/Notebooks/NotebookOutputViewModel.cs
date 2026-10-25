using CommunityToolkit.Mvvm.ComponentModel;
using TabStudio.App.Notebooks;
using TabStudio.Notebooks.Outputs;

namespace TabStudio.App.ViewModels.Notebooks;

/// <summary>
/// Something shown under a code cell: console text, an error, a table, text, HTML, Markdown or an image. Each
/// kind has its own template in the notebook view.
/// </summary>
public abstract class NotebookOutputViewModel : ObservableObject
{
    /// <summary>Picks the best way to show an output natively. Avalonia has no HTML renderer, so a table is
    /// preferred to HTML, and HTML falls back to its plain-text version.</summary>
    /// <param name="output">The output, as the kernel produced it or as the file stored it.</param>
    public static NotebookOutputViewModel Create(NotebookOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return output switch
        {
            StreamOutput stream => new StreamOutputViewModel(stream.Name, stream.Text),
            ErrorOutput error => new ErrorOutputViewModel(error),
            DisplayOutput display => CreateDisplay(display),
            _ => new TextOutputViewModel(string.Empty),
        };
    }

    private static NotebookOutputViewModel CreateDisplay(DisplayOutput display)
    {
        var data = display.Data;
        if (display.Table is { } table)
        {
            return new TableOutputViewModel(table);
        }

        if (data[MimeTypes.Png] is { } png && ImageOutputViewModel.TryCreate(png) is { } image)
        {
            return image;
        }

        if (data[MimeTypes.Markdown] is { } markdown)
        {
            return new MarkdownOutputViewModel(markdown);
        }

        if (data.Html is { } html)
        {
            return HtmlTableReader.TryRead(html) is { } parsed
                ? new TableOutputViewModel(parsed)
                : new HtmlOutputViewModel(html, data.PlainText);
        }

        if (data.PlainText is { } text)
        {
            return new TextOutputViewModel(text);
        }

        // Something TabStudio cannot draw, such as a chart's JSON: say what it is.
        var kinds = string.Join(", ", data.Entries.Select(e => e.Key));
        return new TextOutputViewModel(kinds.Length == 0 ? string.Empty : $"({kinds} output, which TabStudio cannot show)");
    }
}
