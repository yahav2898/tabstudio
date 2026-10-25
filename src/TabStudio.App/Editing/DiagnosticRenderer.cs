using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using TabStudio.Core.Diagnostics;

namespace TabStudio.App.Editing;

/// <summary>
/// Draws squiggles under errors and warnings, and dots under messages, like Visual Studio. The spans follow
/// edits until the next analysis replaces them.
/// </summary>
public sealed class DiagnosticRenderer : IBackgroundRenderer
{
    private static readonly IPen ErrorPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#F47067")), 1.2);
    private static readonly IPen WarningPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#E3B341")), 1.2);
    private static readonly IPen MessagePen = new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#6CB6FF")), 1.6, new ImmutableDashStyle([1, 2], 0));

    // Squiggles by their number of waves, made once and moved under each span, rather than built again on every frame.
    private readonly Dictionary<int, StreamGeometry> _squiggles = [];
    private TrackedRange<BuildDiagnostic>[] _diagnostics = [];

    /// <inheritdoc />
    public KnownLayer Layer => KnownLayer.Selection;

    /// <summary>Replaces the diagnostics shown in the document.</summary>
    /// <param name="diagnostics">Diagnostics located in this document.</param>
    /// <param name="documentLength">The current length of the document, to clamp stale spans.</param>
    public void SetDiagnostics(IEnumerable<BuildDiagnostic> diagnostics, int documentLength)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        _diagnostics = diagnostics
            .Where(d => d.Location is not null && d.Severity != DiagnosticSeverityLevel.Hidden)
            .Select(d => Clamp(d, documentLength))
            .OrderBy(d => d.Value.Severity)
            .ToArray();
    }

    /// <summary>Moves the squiggles through an edit. Call from the document's <c>Changed</c> event.</summary>
    /// <param name="change">The edit.</param>
    public void OnDocumentChanged(DocumentChangeEventArgs change) => _diagnostics = TrackedRanges.Apply(_diagnostics, change);

    /// <summary>The most severe diagnostic at an offset, for tooltips and the status bar.</summary>
    /// <param name="offset">A document offset.</param>
    public BuildDiagnostic? DiagnosticAt(int offset) => _diagnostics
        .Where(d => d.Start <= offset && offset <= d.End)
        .OrderByDescending(d => d.Value.Severity)
        .Select(d => d.Value)
        .FirstOrDefault();

    /// <inheritdoc />
    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(textView);
        ArgumentNullException.ThrowIfNull(drawingContext);

        if (_diagnostics.Length == 0 || !textView.VisualLinesValid || textView.VisualLines.Count == 0)
        {
            return;
        }

        var visibleStart = textView.VisualLines[0].FirstDocumentLine.Offset;
        var visibleEnd = textView.VisualLines[^1].LastDocumentLine.EndOffset;
        var documentLength = textView.Document?.TextLength ?? 0;

        foreach (var diagnostic in _diagnostics)
        {
            if (diagnostic.End < visibleStart || diagnostic.Start > visibleEnd || diagnostic.Start >= documentLength)
            {
                continue;
            }

            var segment = new TextSegment
            {
                StartOffset = diagnostic.Start,
                Length = Math.Max(1, Math.Min(diagnostic.Length, documentLength - diagnostic.Start)),
            };

            var pen = diagnostic.Value.Severity switch
            {
                DiagnosticSeverityLevel.Error => ErrorPen,
                DiagnosticSeverityLevel.Warning => WarningPen,
                _ => MessagePen,
            };

            foreach (var segmentRect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
            {
                // A line with review comments under it is taller; the squiggle stays under its text.
                var rect = segmentRect.WithHeight(Math.Min(segmentRect.Height, textView.DefaultLineHeight));
                if (diagnostic.Value.Severity == DiagnosticSeverityLevel.Info)
                {
                    var y = rect.Bottom - 1;
                    drawingContext.DrawLine(pen, new Point(rect.Left, y), new Point(Math.Min(rect.Right, rect.Left + 12), y));
                }
                else
                {
                    using (drawingContext.PushTransform(Matrix.CreateTranslation(rect.Left, rect.Bottom - 1.5)))
                    {
                        drawingContext.DrawGeometry(null, pen, Squiggle(rect.Width));
                    }
                }
            }
        }
    }

    /// <summary>A squiggle as wide as a span, starting at the origin: up and down in 2.5 dip steps, at least two of them.</summary>
    private StreamGeometry Squiggle(double width)
    {
        const double Step = 2.5;
        var steps = (int)Math.Floor((Math.Max(width, Step * 2) / Step) + 1e-9);
        if (_squiggles.TryGetValue(steps, out var cached))
        {
            return cached;
        }

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(default, isFilled: false);
            for (var i = 1; i <= steps; i++)
            {
                context.LineTo(new Point(i * Step, i % 2 == 1 ? -Step : 0));
            }

            context.EndFigure(isClosed: false);
        }

        if (_squiggles.Count > 256)
        {
            _squiggles.Clear();
        }

        _squiggles[steps] = geometry;
        return geometry;
    }

    private static TrackedRange<BuildDiagnostic> Clamp(BuildDiagnostic diagnostic, int documentLength)
    {
        var start = Math.Clamp(diagnostic.Location!.Start, 0, Math.Max(0, documentLength));
        var length = Math.Clamp(diagnostic.Location.Length, 0, Math.Max(0, documentLength - start));
        return new TrackedRange<BuildDiagnostic>(start, length, diagnostic);
    }
}
