using System.Drawing;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using SkiaSharp;

namespace TabStudio.Forms.Rendering;

/// <summary>
/// The outlines of text, as <c>GraphicsPath.AddString</c>
/// adds them: laid out by Avalonia, as text is drawn, with each glyph's outline from Skia.
/// </summary>
internal static class TextOutlines
{
    /// <summary>The figures of text's glyph outlines.</summary>
    /// <param name="text">The text.</param>
    /// <param name="font">The font; its size is the em height in world units.</param>
    /// <param name="layout">The layout rectangle; an infinite width or height does not limit the text.</param>
    /// <param name="options">Alignment and wrapping.</param>
    public static List<PathFigureData> Outline(string text, FontSpec font, RectangleF layout, TextOptions options)
    {
        var figures = new List<PathFigureData>();
        if (text.Length == 0 || !TextMetrics.IsAvailable)
        {
            return figures;
        }

        var width = float.IsPositiveInfinity(layout.Width) ? double.PositiveInfinity : layout.Width;
        var height = float.IsPositiveInfinity(layout.Height) ? double.PositiveInfinity : layout.Height;
        var alignment = double.IsPositiveInfinity(width)
            ? TextAlignment.Left
            : options.Horizontal switch
            {
                StringAlignment.Center => TextAlignment.Center,
                StringAlignment.Far => TextAlignment.Right,
                _ => TextAlignment.Left,
            };
        using var textLayout = TextMetrics.CreateLayout(text, font, null, width, double.PositiveInfinity, alignment, options.Wrap ? TextWrapping.Wrap : TextWrapping.NoWrap, trimming: false);
        var top = layout.Y + (double.IsPositiveInfinity(height)
            ? 0
            : options.Vertical switch
            {
                StringAlignment.Center => (height - textLayout.Height) / 2,
                StringAlignment.Far => height - textLayout.Height,
                _ => 0,
            });

        foreach (var line in textLayout.TextLines)
        {
            var x = layout.X + line.Start;
            var baseline = top + line.Baseline;
            foreach (var run in line.TextRuns)
            {
                if (run is not ShapedTextRun shaped)
                {
                    x += run is DrawableTextRun drawable ? drawable.Size.Width : 0;
                    continue;
                }

                var glyphRun = shaped.GlyphRun;
                using var skiaFont = new SKFont(Typeface(glyphRun.GlyphTypeface), (float)glyphRun.FontRenderingEmSize)
                {
                    Embolden = (glyphRun.GlyphTypeface.FontSimulations & FontSimulations.Bold) != 0,
                    SkewX = (glyphRun.GlyphTypeface.FontSimulations & FontSimulations.Oblique) != 0 ? -0.2f : 0,
                };
                foreach (var glyph in glyphRun.GlyphInfos)
                {
                    using var path = skiaFont.GetGlyphPath(glyph.GlyphIndex);
                    if (path is not null)
                    {
                        Append(figures, path, (float)(x + glyph.GlyphOffset.X), (float)(baseline + glyph.GlyphOffset.Y));
                    }

                    x += glyph.GlyphAdvance;
                }
            }

            top += line.Height;
        }

        return figures;
    }

    /// <summary>The Skia typeface Avalonia draws a glyph typeface with, or the closest by name.</summary>
    private static SKTypeface Typeface(IGlyphTypeface typeface)
    {
        // Avalonia's Skia glyph typeface keeps its SKTypeface in a public property of an internal class.
        if (typeface.GetType().GetProperty("SKTypeface")?.GetValue(typeface) is SKTypeface skia)
        {
            return skia;
        }

        var style = new SKFontStyle((SKFontStyleWeight)(int)typeface.Weight, (SKFontStyleWidth)(int)typeface.Stretch, typeface.Style == Avalonia.Media.FontStyle.Normal ? SKFontStyleSlant.Upright : SKFontStyleSlant.Italic);
        return SKTypeface.FromFamilyName(typeface.FamilyName, style) ?? SKTypeface.Default;
    }

    /// <summary>Appends the contours of a Skia path, moved by an offset, as closed figures.</summary>
    private static void Append(List<PathFigureData> figures, SKPath path, float dx, float dy)
    {
        using var iterator = path.CreateIterator(forceClose: true);
        var points = new SKPoint[4];
        PointF? start = null;
        List<PathSegmentData>? segments = null;
        PointF P(SKPoint p) => new(p.X + dx, p.Y + dy);
        void Finish(bool closed)
        {
            if (start is { } first && segments is { Count: > 0 })
            {
                figures.Add(new PathFigureData(first, segments, closed));
            }

            start = null;
            segments = null;
        }

        SKPathVerb verb;
        while ((verb = iterator.Next(points)) != SKPathVerb.Done)
        {
            switch (verb)
            {
                case SKPathVerb.Move:
                    Finish(closed: false);
                    start = P(points[0]);
                    segments = [];
                    break;
                case SKPathVerb.Line:
                    segments?.Add(new LineSegmentData(P(points[1])));
                    break;
                case SKPathVerb.Quad:
                    segments?.Add(Cubic(points[0], points[1], points[2], P));
                    break;
                case SKPathVerb.Conic:
                    var quads = SKPath.ConvertConicToQuads(points[0], points[1], points[2], iterator.ConicWeight(), 2);
                    for (var i = 0; i + 2 < quads.Length; i += 2)
                    {
                        segments?.Add(Cubic(quads[i], quads[i + 1], quads[i + 2], P));
                    }

                    break;
                case SKPathVerb.Cubic:
                    segments?.Add(new BezierSegmentData(P(points[1]), P(points[2]), P(points[3])));
                    break;
                case SKPathVerb.Close:
                    Finish(closed: true);
                    break;
            }
        }

        Finish(closed: true);
    }

    /// <summary>A quadratic curve as the cubic Bézier curve it equals.</summary>
    private static BezierSegmentData Cubic(SKPoint p0, SKPoint p1, SKPoint p2, Func<SKPoint, PointF> map)
    {
        var c1 = new SKPoint(p0.X + (2f / 3 * (p1.X - p0.X)), p0.Y + (2f / 3 * (p1.Y - p0.Y)));
        var c2 = new SKPoint(p2.X + (2f / 3 * (p1.X - p2.X)), p2.Y + (2f / 3 * (p1.Y - p2.Y)));
        return new BezierSegmentData(map(c1), map(c2), map(p2));
    }
}
