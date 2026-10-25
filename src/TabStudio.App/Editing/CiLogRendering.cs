using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using TabStudio.App.Services.Ci;

namespace TabStudio.App.Editing;

/// <summary>The brushes of a job log, from the app's theme (Theme/Actions.axaml), with fallbacks for tests without it.</summary>
internal sealed class CiLogBrushes
{
    public IBrush Error { get; } = Find("Brush.Log.Error", "#F47067");

    public IBrush ErrorBackground { get; } = Find("Brush.Log.ErrorBackground", "#33F47067");

    public IBrush Warning { get; } = Find("Brush.Log.Warning", "#E3B341");

    public IBrush WarningBackground { get; } = Find("Brush.Log.WarningBackground", "#26E3B341");

    public IBrush Notice { get; } = Find("Brush.Log.Notice", "#6CB6FF");

    public IBrush Debug { get; } = Find("Brush.Log.Debug", "#8B949E");

    public IBrush Command { get; } = Find("Brush.Log.Command", "#8FB8FF");

    public IBrush Step { get; } = Find("Brush.Log.Step", "#E8ECF2");

    public IBrush StepBackground { get; } = Find("Brush.Log.StepBackground", "#262A33");

    public IBrush Group { get; } = Find("Brush.Log.Group", "#C5CAD3");

    public IBrush Omitted { get; } = Find("Brush.Log.Omitted", "#9AA1AE");

    public IBrush Timestamp { get; } = Find("Brush.LineNumber", "#858C99");

    public IBrush? Ansi(CiLogColor color) => color switch
    {
        CiLogColor.Gray => AnsiGray,
        CiLogColor.Red => AnsiRed,
        CiLogColor.Green => AnsiGreen,
        CiLogColor.Yellow => AnsiYellow,
        CiLogColor.Blue => AnsiBlue,
        CiLogColor.Magenta => AnsiMagenta,
        CiLogColor.Cyan => AnsiCyan,
        CiLogColor.White => AnsiWhite,
        _ => null,
    };

    private IBrush AnsiGray { get; } = Find("Brush.Ansi.Gray", "#8B949E");

    private IBrush AnsiRed { get; } = Find("Brush.Ansi.Red", "#FF7B72");

    private IBrush AnsiGreen { get; } = Find("Brush.Ansi.Green", "#7EE787");

    private IBrush AnsiYellow { get; } = Find("Brush.Ansi.Yellow", "#E3B341");

    private IBrush AnsiBlue { get; } = Find("Brush.Ansi.Blue", "#79C0FF");

    private IBrush AnsiMagenta { get; } = Find("Brush.Ansi.Magenta", "#D2A8FF");

    private IBrush AnsiCyan { get; } = Find("Brush.Ansi.Cyan", "#56D4DD");

    private IBrush AnsiWhite { get; } = Find("Brush.Ansi.White", "#F0F6FC");

    private static IBrush Find(string key, string fallback) =>
        Application.Current?.TryGetResource(key, Application.Current.ActualThemeVariant, out var value) == true && value is IBrush brush
            ? brush
            : new ImmutableSolidColorBrush(Color.Parse(fallback));
}

/// <summary>
/// Colors a job log's lines: errors red, warnings amber, notices blue, commands and debug output in their colors,
/// step headings bright and bold, the timestamp column muted, and the program's own terminal colors.
/// </summary>
/// <param name="log">The log the editor shows, or <see langword="null"/> while it loads.</param>
/// <param name="brushes">The log's brushes.</param>
internal sealed class CiLogColorizer(Func<CiLog?> log, CiLogBrushes brushes) : DocumentColorizingTransformer
{
    /// <inheritdoc />
    protected override void ColorizeLine(DocumentLine line)
    {
        if (log() is not { } current || line.Length == 0)
        {
            return;
        }

        var end = line.EndOffset;
        var prefix = Math.Min(current.PrefixLength, line.Length);
        var start = line.Offset + prefix;
        if (prefix > 0)
        {
            ChangeLinePart(line.Offset, start, e => e.TextRunProperties.SetForegroundBrush(brushes.Timestamp));
        }

        if (start >= end)
        {
            return;
        }

        var kind = current.KindOf(line.LineNumber);
        var (foreground, bold) = kind switch
        {
            CiLogLineKind.Error => (brushes.Error, false),
            CiLogLineKind.Warning => (brushes.Warning, false),
            CiLogLineKind.Notice => (brushes.Notice, false),
            CiLogLineKind.Debug => (brushes.Debug, false),
            CiLogLineKind.Command => (brushes.Command, false),
            CiLogLineKind.Step => (brushes.Step, true),
            CiLogLineKind.Group => (brushes.Group, true),
            CiLogLineKind.Omitted => (brushes.Omitted, false),
            _ => ((IBrush?)null, false),
        };
        if (foreground is not null || bold)
        {
            ChangeLinePart(start, end, element =>
            {
                if (foreground is not null)
                {
                    element.TextRunProperties.SetForegroundBrush(foreground);
                }

                if (bold)
                {
                    element.TextRunProperties.SetTypeface(new Typeface(element.TextRunProperties.Typeface.FontFamily, FontStyle.Normal, FontWeight.SemiBold));
                }
            });
        }

        // The program's own colors, on ordinary lines; errors and warnings keep theirs.
        if (kind is CiLogLineKind.Error or CiLogLineKind.Warning || !current.Colors.TryGetValue(line.LineNumber, out var spans))
        {
            return;
        }

        foreach (var span in spans)
        {
            var from = Math.Min(start + span.Start, end);
            var to = Math.Min(from + span.Length, end);
            if (to <= from)
            {
                continue;
            }

            var color = brushes.Ansi(span.Color);
            ChangeLinePart(from, to, element =>
            {
                if (color is not null)
                {
                    element.TextRunProperties.SetForegroundBrush(color);
                }

                if (span.IsBold)
                {
                    element.TextRunProperties.SetTypeface(new Typeface(element.TextRunProperties.Typeface.FontFamily, FontStyle.Normal, FontWeight.SemiBold));
                }
            });
        }
    }
}

/// <summary>Draws the bands behind a job log's step headings, and behind error and warning lines.</summary>
/// <param name="log">The log the editor shows, or <see langword="null"/> while it loads.</param>
/// <param name="brushes">The log's brushes.</param>
internal sealed class CiLogBackgroundRenderer(Func<CiLog?> log, CiLogBrushes brushes) : IBackgroundRenderer
{
    /// <inheritdoc />
    public KnownLayer Layer => KnownLayer.Background;

    /// <inheritdoc />
    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (log() is not { } current || !textView.VisualLinesValid)
        {
            return;
        }

        var width = textView.Bounds.Width;
        foreach (var visual in textView.VisualLines)
        {
            var brush = current.KindOf(visual.FirstDocumentLine.LineNumber) switch
            {
                CiLogLineKind.Step => brushes.StepBackground,
                CiLogLineKind.Error => brushes.ErrorBackground,
                CiLogLineKind.Warning => brushes.WarningBackground,
                _ => null,
            };
            if (brush is not null)
            {
                var top = visual.VisualTop - textView.VerticalOffset;
                drawingContext.FillRectangle(brush, new Rect(0, top, width, visual.Height));
            }
        }
    }
}
