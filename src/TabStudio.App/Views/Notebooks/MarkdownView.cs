using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TabStudio.App.Editing;
using TabStudio.App.Notebooks.Markdown;
using TabStudio.Compilation;
using TabStudio.Notebooks;
using TabStudio.App.Accessibility;

namespace TabStudio.App.Views.Notebooks;

/// <summary>
/// Shows Markdown as native text: headings, paragraphs with bold, italic, code and links, lists, code blocks
/// (C# ones colored like the editor), quotes, rules and tables. Tapping a link opens it in the browser.
/// </summary>
public sealed class MarkdownView : StackPanel
{
    /// <summary>The Markdown to show.</summary>
    public static readonly StyledProperty<IReadOnlyList<MarkdownBlock>?> BlocksProperty =
        AvaloniaProperty.Register<MarkdownView, IReadOnlyList<MarkdownBlock>?>(nameof(Blocks));

    private const double BodySize = 13.5;
    private const double SmallSize = 12.5;

    static MarkdownView()
    {
        BlocksProperty.Changed.AddClassHandler<MarkdownView>((view, _) => view.Render());
    }

    /// <summary>Creates the view.</summary>
    public MarkdownView()
    {
        Spacing = 8;
    }

    /// <inheritdoc cref="BlocksProperty"/>
    public IReadOnlyList<MarkdownBlock>? Blocks
    {
        get => GetValue(BlocksProperty);
        set => SetValue(BlocksProperty, value);
    }

    private static IBrush TextBrush => ThemeResources.Brush("Brush.Text");

    private static IBrush BrightBrush => ThemeResources.Brush("Brush.TextBright");

    private static IBrush MutedBrush => ThemeResources.Brush("Brush.TextMuted");

    private static IBrush LinkBrush => ThemeResources.Brush("Brush.Link");

    private static IBrush DividerBrush => ThemeResources.Brush("Brush.Divider");

    private static IBrush CodeBackground => ThemeResources.Brush("Brush.Notebook.CodeBlock");

    private void Render()
    {
        Children.Clear();
        foreach (var block in Blocks ?? [])
        {
            Children.Add(CreateBlock(block));
        }
    }

    private static Control CreateBlock(MarkdownBlock block) => block switch
    {
        MarkdownHeading heading => CreateHeading(heading),
        MarkdownParagraph paragraph => CreateText(paragraph.Spans, BodySize, FontWeight.Normal, TextBrush),
        MarkdownList list => CreateList(list),
        MarkdownCodeBlock code => CreateCode(code),
        MarkdownQuote quote => CreateQuote(quote),
        MarkdownTable table => CreateTable(table),
        _ => new Border { Height = 1, Background = DividerBrush, Margin = new Thickness(0, 4) },
    };

    private static Control CreateHeading(MarkdownHeading heading)
    {
        var size = heading.Level switch
        {
            1 => 22,
            2 => 18.5,
            3 => 16,
            4 => 14.5,
            _ => BodySize,
        };
        var text = CreateText(heading.Spans, size, heading.Level <= 2 ? FontWeight.Bold : FontWeight.SemiBold, BrightBrush);
        if (heading.Level > 2)
        {
            text.Margin = new Thickness(0, 4, 0, 0);
            return text;
        }

        // Top-level headings are underlined, as in Visual Studio Code's Markdown preview.
        return new Border
        {
            BorderBrush = DividerBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 0, 0, 6),
            Margin = new Thickness(0, heading.Level == 1 ? 0 : 6, 0, 2),
            Child = text,
        };
    }

    private static StackPanel CreateList(MarkdownList list)
    {
        var panel = new StackPanel { Spacing = 4 };
        foreach (var item in list.Items)
        {
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                Margin = new Thickness(4 + (item.Depth * 20), 0, 0, 0),
            };
            var marker = new TextBlock
            {
                Text = item.Marker,
                MinWidth = 20,
                Foreground = MutedBrush,
            };
            TypeScale.SetBaseFontSize(marker, BodySize);
            var content = CreateText(item.Spans, BodySize, FontWeight.Normal, TextBrush);
            Grid.SetColumn(content, 1);
            row.Children.Add(marker);
            row.Children.Add(content);
            panel.Children.Add(row);
        }

        return panel;
    }

    private static Border CreateCode(MarkdownCodeBlock code)
    {
        var text = new TextBlock
        {
            FontFamily = ThemeResources.CodeFont,
            FontSize = SmallSize,
            Foreground = TextBrush,
        };
        TypeScale.SetBaseFontSize(text, SmallSize);

        if (code.Language is { } language && NotebookLanguages.IsCSharp(language))
        {
            AddColoredRuns(text, code.Code);
        }
        else
        {
            text.Text = code.Code;
        }

        return new Border
        {
            Background = CodeBackground,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 9),
            Child = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = text,
            },
        };
    }

    private static void AddColoredRuns(TextBlock text, string code)
    {
        var inlines = text.Inlines!;
        var position = 0;
        foreach (var range in SyntaxClassifier.Classify(code))
        {
            if (range.Start < position || range.Length <= 0 || range.Start + range.Length > code.Length)
            {
                continue;
            }

            if (range.Start > position)
            {
                inlines.Add(new Run(code[position..range.Start]));
            }

            var run = new Run(code.Substring(range.Start, range.Length));
            if (SyntaxTheme.For(range.Classification) is { } brush)
            {
                run.Foreground = brush;
            }

            inlines.Add(run);
            position = range.Start + range.Length;
        }

        if (position < code.Length)
        {
            inlines.Add(new Run(code[position..]));
        }
    }

    private static Border CreateQuote(MarkdownQuote quote) => new Border
    {
        BorderBrush = ThemeResources.Brush("Brush.BorderStrong"),
        BorderThickness = new Thickness(3, 0, 0, 0),
        Padding = new Thickness(12, 2, 0, 2),
        Child = new MarkdownView { Blocks = quote.Blocks, Opacity = 0.85 },
    };

    private static Border CreateTable(MarkdownTable table)
    {
        var header = table.Header.Select(cell => (Control)CreateText(cell, SmallSize, FontWeight.SemiBold, BrightBrush)).ToList();
        var rows = table.Rows.Select(row => (IReadOnlyList<Control>)row.Select(cell => (Control)CreateText(cell, SmallSize, FontWeight.Normal, TextBrush)).ToList());
        return new Border
        {
            BorderBrush = ThemeResources.Brush("Brush.Border"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = TableLayout.Create(header, rows),
            },
        };
    }

    private static TextBlock CreateText(IReadOnlyList<MarkdownSpan> spans, double size, FontWeight weight, IBrush foreground)
    {
        var text = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontWeight = weight,
            Foreground = foreground,
        };
        TypeScale.SetBaseFontSize(text, size);

        var inlines = text.Inlines!;
        var links = new List<(int Start, int End, string Destination)>();
        var offset = 0;
        foreach (var span in spans)
        {
            var run = new Run(span.Text);
            if ((span.Style & MarkdownStyles.Bold) != 0)
            {
                run.FontWeight = FontWeight.Bold;
            }

            if ((span.Style & MarkdownStyles.Italic) != 0)
            {
                run.FontStyle = FontStyle.Italic;
            }

            if ((span.Style & MarkdownStyles.Code) != 0)
            {
                run.FontFamily = ThemeResources.CodeFont;
                TypeScale.SetBaseFontSize(run, size - 1);
                run.Background = CodeBackground;
            }

            var decorations = new TextDecorationCollection();
            if ((span.Style & MarkdownStyles.Strikethrough) != 0)
            {
                decorations.AddRange(TextDecorations.Strikethrough);
            }

            if (span.Destination is { } destination)
            {
                run.Foreground = LinkBrush;
                decorations.AddRange(TextDecorations.Underline);
                links.Add((offset, offset + span.Text.Length, destination));
            }

            if (decorations.Count > 0)
            {
                run.TextDecorations = decorations;
            }

            inlines.Add(run);
            offset += span.Text.Length;
        }

        if (links.Count > 0)
        {
            text.Cursor = new Cursor(StandardCursorType.Hand);
            text.Tapped += (_, e) => OpenLink(text, e, links);
        }

        return text;
    }

    private static void OpenLink(TextBlock text, TappedEventArgs e, List<(int Start, int End, string Destination)> links)
    {
        var position = text.TextLayout.HitTestPoint(e.GetPosition(text)).TextPosition;
        foreach (var (start, end, destination) in links)
        {
            if (position >= start && position < end
                && Uri.TryCreate(destination, UriKind.Absolute, out var uri)
                && uri.Scheme is "http" or "https" or "mailto")
            {
                _ = TopLevel.GetTopLevel(text)?.Launcher.LaunchUriAsync(uri);
                e.Handled = true;
                return;
            }
        }
    }
}
