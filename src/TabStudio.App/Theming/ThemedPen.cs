using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TabStudio.App.Theming;

/// <summary>
/// A pen in a palette token's color, for code that draws lines itself (squiggles, outlines, markers): made once, and
/// again only when the theme changes the token's color.
/// </summary>
public sealed class ThemedPen
{
    private readonly Func<IBrush> _brush;
    private readonly double _thickness;
    private readonly double _opacity;
    private readonly ImmutableDashStyle? _dashStyle;
    private readonly PenLineCap _lineCap;
    private readonly PenLineJoin _lineJoin;
    private ImmutablePen? _pen;
    private Color _color;

    private ThemedPen(Func<IBrush> brush, double thickness, double opacity, ImmutableDashStyle? dashStyle, PenLineCap lineCap, PenLineJoin lineJoin)
    {
        _brush = brush;
        _thickness = thickness;
        _opacity = opacity;
        _dashStyle = dashStyle;
        _lineCap = lineCap;
        _lineJoin = lineJoin;
    }

    /// <summary>The pen in the token's current color.</summary>
    public IPen Pen
    {
        get
        {
            var color = (_brush() as ISolidColorBrush)?.Color ?? Colors.Transparent;
            if (_pen is null || color != _color)
            {
                _color = color;
                _pen = new ImmutablePen(new ImmutableSolidColorBrush(color, _opacity), _thickness, _dashStyle, _lineCap, _lineJoin);
            }

            return _pen;
        }
    }

    /// <summary>A pen in a token of the editors' theme, for what is drawn on code.</summary>
    /// <param name="key">The token, such as <c>Editor.ErrorSquiggle</c>.</param>
    /// <param name="thickness">The line's thickness.</param>
    /// <param name="opacity">The line's opacity.</param>
    /// <param name="dashStyle">Its dashes, or <see langword="null"/> for a solid line.</param>
    /// <param name="lineCap">How the line ends.</param>
    /// <param name="lineJoin">How segments join.</param>
    public static ThemedPen Editor(string key, double thickness, double opacity = 1, ImmutableDashStyle? dashStyle = null, PenLineCap lineCap = PenLineCap.Flat, PenLineJoin lineJoin = PenLineJoin.Miter) =>
        new(() => ThemeBrushes.Editor(key), thickness, opacity, dashStyle, lineCap, lineJoin);

    /// <summary>A pen in a token of the app's theme, for the IDE's chrome.</summary>
    /// <param name="key">The token, such as <c>Brush.Border</c>.</param>
    /// <param name="thickness">The line's thickness.</param>
    /// <param name="opacity">The line's opacity.</param>
    /// <param name="lineCap">How the line ends.</param>
    /// <param name="lineJoin">How segments join.</param>
    public static ThemedPen App(string key, double thickness, double opacity = 1, PenLineCap lineCap = PenLineCap.Flat, PenLineJoin lineJoin = PenLineJoin.Miter) =>
        new(() => ThemeBrushes.Get(key), thickness, opacity, null, lineCap, lineJoin);
}
