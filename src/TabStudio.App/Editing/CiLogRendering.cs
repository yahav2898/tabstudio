using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using TabStudio.App.Services.Ci;
using TabStudio.App.Theming;

namespace TabStudio.App.Editing;

/// <summary>The brushes of a job log, from the app's theme (the <c>Brush.Log.*</c> and <c>Brush.Ansi.*</c> tokens); they follow the theme.</summary>
internal sealed class CiLogBrushes
{
    public IBrush Error { get; } = ThemeBrushes.Get("Brush.Log.Error");

    public IBrush ErrorBackground { get; } = ThemeBrushes.Get("Brush.Log.ErrorBackground");

    public IBrush Warning { get; } = ThemeBrushes.Get("Brush.Log.Warning");

    public IBrush WarningBackground { get; } = ThemeBrushes.Get("Brush.Log.WarningBackground");

    public IBrush Notice { get; } = ThemeBrushes.Get("Brush.Log.Notice");

    public IBrush Debug { get; } = ThemeBrushes.Get("Brush.Log.Debug");

    public IBrush Command { get; } = ThemeBrushes.Get("Brush.Log.Command");

    public IBrush Step { get; } = ThemeBrushes.Get("Brush.Log.Step");

    public IBrush StepBackground { get; } = ThemeBrushes.Get("Brush.Log.StepBackground");

    public IBrush Group { get; } = ThemeBrushes.Get("Brush.Log.Group");

    public IBrush Omitted { get; } = ThemeBrushes.Get("Brush.Log.Omitted");

    public IBrush Timestamp { get; } = ThemeBrushes.Get("Brush.LineNumber");

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

    private IBrush AnsiGray { get; } = ThemeBrushes.Get("Brush.Ansi.Gray");

    private IBrush AnsiRed { get; } = ThemeBrushes.Get("Brush.Ansi.Red");

    private IBrush AnsiGreen { get; } = ThemeBrushes.Get("Brush.Ansi.Green");

    private IBrush AnsiYellow { get; } = ThemeBrushes.Get("Brush.Ansi.Yellow");

    private IBrush AnsiBlue { get; } = ThemeBrushes.Get("Brush.Ansi.Blue");

    private IBrush AnsiMagenta { get; } = ThemeBrushes.Get("Brush.Ansi.Magenta");

    private IBrush AnsiCyan { get; } = ThemeBrushes.Get("Brush.Ansi.Cyan");

    private IBrush AnsiWhite { get; } = ThemeBrushes.Get("Brush.Ansi.White");

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
