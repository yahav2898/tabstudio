using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace TabStudio.App.Controls;

/// <summary>
/// Text with some of its characters drawn stronger, such as the letters of a search result that matched what was typed:
/// bold and in the accent text color, so a list of results reads at a glance.
/// </summary>
public sealed class HighlightedText : TextBlock
{
    /// <summary>The positions of the characters to draw stronger.</summary>
    public static readonly StyledProperty<IReadOnlyList<int>?> HighlightsProperty =
        AvaloniaProperty.Register<HighlightedText, IReadOnlyList<int>?>(nameof(Highlights));

    /// <summary>The highlighted characters' color.</summary>
    public static readonly StyledProperty<IBrush?> HighlightBrushProperty =
        AvaloniaProperty.Register<HighlightedText, IBrush?>(nameof(HighlightBrush));

    /// <summary>Creates the text, highlighting in the theme's accent text color.</summary>
    public HighlightedText()
    {
        this.Bind(HighlightBrushProperty, this.GetResourceObservable("Brush.AccentText"));
    }

    /// <inheritdoc cref="HighlightsProperty"/>
    public IReadOnlyList<int>? Highlights
    {
        get => GetValue(HighlightsProperty);
        set => SetValue(HighlightsProperty, value);
    }

    /// <inheritdoc cref="HighlightBrushProperty"/>
    public IBrush? HighlightBrush
    {
        get => GetValue(HighlightBrushProperty);
        set => SetValue(HighlightBrushProperty, value);
    }

    /// <summary>The text in runs: plain, then highlighted, and so on.</summary>
    /// <param name="text">The text.</param>
    /// <param name="highlights">The positions to highlight.</param>
    public static IReadOnlyList<(string Text, bool IsHighlighted)> Split(string text, IReadOnlyCollection<int> highlights)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(highlights);
        var runs = new List<(string, bool)>();
        var start = 0;
        for (var i = 1; i <= text.Length; i++)
        {
            if (i == text.Length || highlights.Contains(i) != highlights.Contains(start))
            {
                runs.Add((text[start..i], highlights.Contains(start)));
                start = i;
            }
        }

        return runs;
    }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(TextBlock);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HighlightsProperty || change.Property == HighlightBrushProperty || change.Property == TextProperty)
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        var text = Text ?? string.Empty;
        var highlights = Highlights ?? [];
        if (highlights.Count == 0 || text.Length == 0)
        {
            Inlines = null;
            return;
        }

        var inlines = new InlineCollection();
        foreach (var (part, isHighlighted) in Split(text, highlights.ToHashSet()))
        {
            inlines.Add(isHighlighted
                ? new Run(part) { FontWeight = FontWeight.Bold, Foreground = HighlightBrush }
                : new Run(part));
        }

        Inlines = inlines;
    }
}
