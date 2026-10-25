using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TabStudio.Compilation.Features;

namespace TabStudio.App.Controls;

/// <summary>
/// The small colored icon Visual Studio shows next to a symbol: a purple cube for a method, a blue cube for a field,
/// a wrench for a property, a lightning bolt for an event, and so on. Used by the completion list, Quick Info and
/// the breadcrumb.
/// </summary>
/// <remarks>Every glyph is vector art on a 16 by 16 grid, scaled to <see cref="Size"/>.</remarks>
public sealed class SymbolGlyphIcon : Control
{
    /// <summary>The kind of symbol to draw.</summary>
    public static readonly StyledProperty<SymbolGlyph> KindProperty =
        AvaloniaProperty.Register<SymbolGlyphIcon, SymbolGlyph>(nameof(Kind));

    /// <summary>The width and height of the icon.</summary>
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<SymbolGlyphIcon, double>(nameof(Size), 16);

    private const double GridSize = 16;

    static SymbolGlyphIcon()
    {
        AffectsRender<SymbolGlyphIcon>(KindProperty);
        AffectsMeasure<SymbolGlyphIcon>(SizeProperty);
    }

    /// <inheritdoc cref="KindProperty"/>
    public SymbolGlyph Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <inheritdoc cref="SizeProperty"/>
    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var scale = Math.Min(Bounds.Width, Bounds.Height) / GridSize;
        var offsetX = (Bounds.Width - (GridSize * scale)) / 2;
        var offsetY = (Bounds.Height - (GridSize * scale)) / 2;
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offsetX, offsetY)))
        {
            foreach (var part in SymbolGlyphArt.For(Kind))
            {
                context.DrawGeometry(part.Fill, part.Pen, part.Geometry);
            }
        }
    }
}

/// <summary>The vector art of each <see cref="SymbolGlyph"/>.</summary>
internal static class SymbolGlyphArt
{
    private static readonly Color Purple = Color.Parse("#B180D7");
    private static readonly Color Blue = Color.Parse("#4DA3F2");
    private static readonly Color LightBlue = Color.Parse("#75BEFF");
    private static readonly Color Gold = Color.Parse("#E3B341");
    private static readonly Color Silver = Color.Parse("#C5CAD3");
    private static readonly Color Gray = Color.Parse("#9AA1AE");
    private static readonly Color Ink = Color.Parse("#1B1D23");

    private static readonly Geometry CubeTop = Geometry.Parse("M8,1.5 L14,4.75 L8,8 L2,4.75 Z");
    private static readonly Geometry CubeLeft = Geometry.Parse("M2,4.75 L8,8 L8,14.5 L2,11.25 Z");
    private static readonly Geometry CubeRight = Geometry.Parse("M8,8 L14,4.75 L14,11.25 L8,14.5 Z");

    private static readonly Dictionary<SymbolGlyph, GlyphPart[]> Art = new()
    {
        [SymbolGlyph.Method] = Cube(Purple),
        [SymbolGlyph.ExtensionMethod] =
        [
            .. Cube(Purple),
            Stroke("M12.5,9.5 L12.5,15 M10.5,13 L12.5,15 L14.5,13", Colors.White, 1.4),
        ],
        [SymbolGlyph.Field] = Cube(LightBlue),
        [SymbolGlyph.Class] =
        [
            Fill("M1.5,1.5 H8.5 V7.5 H1.5 Z", Blue),
            Fill("M8.5,9 H14.5 V14.5 H8.5 Z", Blue),
            Stroke("M5,7.5 V11.75 H8.5", Blue, 1.5),
        ],
        [SymbolGlyph.Struct] =
        [
            Fill("M2,2 H7.25 V7.25 H2 Z M8.75,2 H14 V7.25 H8.75 Z M2,8.75 H7.25 V14 H2 Z M8.75,8.75 H14 V14 H8.75 Z", Blue),
        ],
        [SymbolGlyph.Interface] =
        [
            Stroke("M7.3,8 A3.2,3.2 0 1 0 13.7,8 A3.2,3.2 0 1 0 7.3,8", LightBlue, 2.2),
            Stroke("M1.5,8 H7.3", LightBlue, 2.2),
        ],
        [SymbolGlyph.Enum] =
        [
            Fill("M2,3 H14 V13 H2 Z", Gold),
            Stroke("M5,6.5 H11 M5,9.5 H11", Ink, 1.4),
        ],
        [SymbolGlyph.EnumMember] =
        [
            Fill("M3,4.5 H13 V11.5 H3 Z", LightBlue),
            Stroke("M5.5,8 H10.5", Ink, 1.4),
        ],
        [SymbolGlyph.Delegate] =
        [
            Fill("M1.5,4 H9.5 V12 H1.5 Z", Purple),
            Stroke("M9.5,8 H14.5 M12.25,5.5 L14.5,8 L12.25,10.5", Purple, 1.5),
        ],
        [SymbolGlyph.Property] =
        [
            Stroke("M3,13 L8.5,7.5", Silver, 2.6),
            Fill("M7.6,5.4 A3.4,3.4 0 1 0 14.4,5.4 A3.4,3.4 0 1 0 7.6,5.4", Silver),
            Stroke("M11,5.4 L14.6,1.8", Ink, 2),
        ],
        [SymbolGlyph.Event] =
        [
            Fill("M9.5,1 L3,9 L7.5,9 L6,15 L13,6.5 L8.5,6.5 L10.5,1 Z", Gold),
        ],
        [SymbolGlyph.Local] =
        [
            Fill("M2.5,4.5 H13.5 V11.5 H2.5 Z", Color.FromArgb(70, LightBlue.R, LightBlue.G, LightBlue.B)),
            Stroke("M2.5,4.5 H13.5 V11.5 H2.5 Z", LightBlue, 1.5),
        ],
        [SymbolGlyph.Parameter] =
        [
            Stroke("M2.5,4.5 H13.5 V11.5 H2.5 Z", LightBlue, 1.5),
            Fill("M5.5,6.75 H10.5 V9.25 H5.5 Z", LightBlue),
        ],
        [SymbolGlyph.Constant] =
        [
            Fill("M2,4 H14 V12 H2 Z", Gray),
            Stroke("M5,7 H11 M5,9.5 H11", Ink, 1.3),
        ],
        [SymbolGlyph.Namespace] =
        [
            Stroke("M6,2 C4.5,2 4,2.8 4,4.5 C4,6.8 3.4,8 2,8 C3.4,8 4,9.2 4,11.5 C4,13.2 4.5,14 6,14 M10,2 C11.5,2 12,2.8 12,4.5 C12,6.8 12.6,8 14,8 C12.6,8 12,9.2 12,11.5 C12,13.2 11.5,14 10,14", Silver, 1.5),
        ],
        [SymbolGlyph.Keyword] =
        [
            Stroke("M2,3.5 H14 V12.5 H2 Z", Gray, 1.4),
            Stroke("M4.75,6.5 H11.25 M4.75,9.5 H9", Gray, 1.4),
        ],
        [SymbolGlyph.Snippet] =
        [
            new GlyphPart(Geometry.Parse("M2,3.5 H14 V12.5 H2 Z"), null, new ImmutablePen(new ImmutableSolidColorBrush(Gray), 1.3, new ImmutableDashStyle([2, 1.5], 0))),
            Stroke("M5,8 H11 M9,6 L11,8 L9,10", Gray, 1.4),
        ],
        [SymbolGlyph.TypeParameter] =
        [
            Stroke("M2,2 H14 V14 H2 Z", Blue, 1.4),
            Stroke("M5,5 H11 M8,5 V11.5", Blue, 1.6),
        ],
        [SymbolGlyph.Label] =
        [
            Fill("M2,4 H10 L14,8 L10,12 H2 Z", Gray),
        ],
    };

    private static readonly GlyphPart[] Other = [Fill("M4.5,8 A3.5,3.5 0 1 0 11.5,8 A3.5,3.5 0 1 0 4.5,8", Gray)];

    /// <summary>The parts to draw for a kind of symbol, back to front.</summary>
    /// <param name="kind">The kind of symbol.</param>
    public static IReadOnlyList<GlyphPart> For(SymbolGlyph kind) => Art.GetValueOrDefault(kind) ?? Other;

    private static GlyphPart[] Cube(Color color) =>
    [
        new(CubeLeft, new ImmutableSolidColorBrush(color), null),
        new(CubeRight, new ImmutableSolidColorBrush(Shade(color, 0.72)), null),
        new(CubeTop, new ImmutableSolidColorBrush(Tint(color, 0.35)), null),
    ];

    private static GlyphPart Fill(string data, Color color) => new(Geometry.Parse(data), new ImmutableSolidColorBrush(color), null);

    private static GlyphPart Stroke(string data, Color color, double thickness) =>
        new(Geometry.Parse(data), null, new ImmutablePen(new ImmutableSolidColorBrush(color), thickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round));

    private static Color Shade(Color color, double factor) =>
        Color.FromRgb((byte)(color.R * factor), (byte)(color.G * factor), (byte)(color.B * factor));

    private static Color Tint(Color color, double amount) =>
        Color.FromRgb((byte)(color.R + ((255 - color.R) * amount)), (byte)(color.G + ((255 - color.G) * amount)), (byte)(color.B + ((255 - color.B) * amount)));
}

/// <summary>One shape of a glyph: a geometry with a fill, an outline, or both.</summary>
/// <param name="Geometry">The shape, on a 16 by 16 grid.</param>
/// <param name="Fill">The fill, or <see langword="null"/>.</param>
/// <param name="Pen">The outline, or <see langword="null"/>.</param>
internal sealed record GlyphPart(Geometry Geometry, IBrush? Fill, IPen? Pen);
