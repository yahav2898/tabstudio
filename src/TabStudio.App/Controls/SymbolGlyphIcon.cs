using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using TabStudio.App.Theming;
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

    /// <summary>Creates the icon, drawn again in the other theme's colors when the theme changes.</summary>
    public SymbolGlyphIcon()
    {
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
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
            foreach (var part in SymbolGlyphArt.For(Kind, ActualThemeVariant))
            {
                context.DrawGeometry(part.Fill, part.Pen, part.Geometry);
            }
        }
    }
}

/// <summary>
/// The vector art of each <see cref="SymbolGlyph"/>, in Visual Studio's colors for the dark or the light theme (the
/// <c>Symbol.*</c> tokens of Colors.axaml).
/// </summary>
internal static class SymbolGlyphArt
{
    private static readonly Geometry CubeTop = Geometry.Parse("M8,1.5 L14,4.75 L8,8 L2,4.75 Z");
    private static readonly Geometry CubeLeft = Geometry.Parse("M2,4.75 L8,8 L8,14.5 L2,11.25 Z");
    private static readonly Geometry CubeRight = Geometry.Parse("M8,8 L14,4.75 L14,11.25 L8,14.5 Z");

    private static readonly Dictionary<ThemeVariant, (Dictionary<SymbolGlyph, GlyphPart[]> Art, GlyphPart[] Other)> Sets = [];

    /// <summary>The parts to draw for a kind of symbol, back to front, in a theme.</summary>
    /// <param name="kind">The kind of symbol.</param>
    /// <param name="variant">The theme the glyph is drawn on: light, or dark for any other.</param>
    public static IReadOnlyList<GlyphPart> For(SymbolGlyph kind, ThemeVariant variant)
    {
        var key = variant == ThemeVariant.Light ? ThemeVariant.Light : ThemeVariant.Dark;
        if (!Sets.TryGetValue(key, out var set))
        {
            var colors = GlyphColors.Of(key);
            GlyphPart[] other = [Fill("M4.5,8 A3.5,3.5 0 1 0 11.5,8 A3.5,3.5 0 1 0 4.5,8", colors.Gray)];
            set = (Build(colors), other);
            Sets[key] = set;
        }

        return set.Art.GetValueOrDefault(kind) ?? set.Other;
    }

    private static Dictionary<SymbolGlyph, GlyphPart[]> Build(GlyphColors c) => new()
    {
        [SymbolGlyph.Method] = Cube(c.Purple),
        [SymbolGlyph.ExtensionMethod] =
        [
            .. Cube(c.Purple),
            Stroke("M12.5,9.5 L12.5,15 M10.5,13 L12.5,15 L14.5,13", c.White, 1.4),
        ],
        [SymbolGlyph.Field] = Cube(c.LightBlue),
        [SymbolGlyph.Class] =
        [
            Fill("M1.5,1.5 H8.5 V7.5 H1.5 Z", c.Blue),
            Fill("M8.5,9 H14.5 V14.5 H8.5 Z", c.Blue),
            Stroke("M5,7.5 V11.75 H8.5", c.Blue, 1.5),
        ],
        [SymbolGlyph.Struct] =
        [
            Fill("M2,2 H7.25 V7.25 H2 Z M8.75,2 H14 V7.25 H8.75 Z M2,8.75 H7.25 V14 H2 Z M8.75,8.75 H14 V14 H8.75 Z", c.Blue),
        ],
        [SymbolGlyph.Interface] =
        [
            Stroke("M7.3,8 A3.2,3.2 0 1 0 13.7,8 A3.2,3.2 0 1 0 7.3,8", c.LightBlue, 2.2),
            Stroke("M1.5,8 H7.3", c.LightBlue, 2.2),
        ],
        [SymbolGlyph.Enum] =
        [
            Fill("M2,3 H14 V13 H2 Z", c.Gold),
            Stroke("M5,6.5 H11 M5,9.5 H11", c.Ink, 1.4),
        ],
        [SymbolGlyph.EnumMember] =
        [
            Fill("M3,4.5 H13 V11.5 H3 Z", c.LightBlue),
            Stroke("M5.5,8 H10.5", c.Ink, 1.4),
        ],
        [SymbolGlyph.Delegate] =
        [
            Fill("M1.5,4 H9.5 V12 H1.5 Z", c.Purple),
            Stroke("M9.5,8 H14.5 M12.25,5.5 L14.5,8 L12.25,10.5", c.Purple, 1.5),
        ],
        [SymbolGlyph.Property] =
        [
            Stroke("M3,13 L8.5,7.5", c.Silver, 2.6),
            Fill("M7.6,5.4 A3.4,3.4 0 1 0 14.4,5.4 A3.4,3.4 0 1 0 7.6,5.4", c.Silver),
            Stroke("M11,5.4 L14.6,1.8", c.Ink, 2),
        ],
        [SymbolGlyph.Event] =
        [
            Fill("M9.5,1 L3,9 L7.5,9 L6,15 L13,6.5 L8.5,6.5 L10.5,1 Z", c.Gold),
        ],
        [SymbolGlyph.Local] =
        [
            Fill("M2.5,4.5 H13.5 V11.5 H2.5 Z", Color.FromArgb(70, c.LightBlue.R, c.LightBlue.G, c.LightBlue.B)),
            Stroke("M2.5,4.5 H13.5 V11.5 H2.5 Z", c.LightBlue, 1.5),
        ],
        [SymbolGlyph.Parameter] =
        [
            Stroke("M2.5,4.5 H13.5 V11.5 H2.5 Z", c.LightBlue, 1.5),
            Fill("M5.5,6.75 H10.5 V9.25 H5.5 Z", c.LightBlue),
        ],
        [SymbolGlyph.Constant] =
        [
            Fill("M2,4 H14 V12 H2 Z", c.Gray),
            Stroke("M5,7 H11 M5,9.5 H11", c.Ink, 1.3),
        ],
        [SymbolGlyph.Namespace] =
        [
            Stroke("M6,2 C4.5,2 4,2.8 4,4.5 C4,6.8 3.4,8 2,8 C3.4,8 4,9.2 4,11.5 C4,13.2 4.5,14 6,14 M10,2 C11.5,2 12,2.8 12,4.5 C12,6.8 12.6,8 14,8 C12.6,8 12,9.2 12,11.5 C12,13.2 11.5,14 10,14", c.Silver, 1.5),
        ],
        [SymbolGlyph.Keyword] =
        [
            Stroke("M2,3.5 H14 V12.5 H2 Z", c.Gray, 1.4),
            Stroke("M4.75,6.5 H11.25 M4.75,9.5 H9", c.Gray, 1.4),
        ],
        [SymbolGlyph.Snippet] =
        [
            new GlyphPart(Geometry.Parse("M2,3.5 H14 V12.5 H2 Z"), null, new ImmutablePen(new ImmutableSolidColorBrush(c.Gray), 1.3, new ImmutableDashStyle([2, 1.5], 0))),
            Stroke("M5,8 H11 M9,6 L11,8 L9,10", c.Gray, 1.4),
        ],
        [SymbolGlyph.TypeParameter] =
        [
            Stroke("M2,2 H14 V14 H2 Z", c.Blue, 1.4),
            Stroke("M5,5 H11 M8,5 V11.5", c.Blue, 1.6),
        ],
        [SymbolGlyph.Label] =
        [
            Fill("M2,4 H10 L14,8 L10,12 H2 Z", c.Gray),
        ],
    };

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

/// <summary>The colors of the symbol glyphs in one theme.</summary>
/// <param name="Purple">Methods and delegates.</param>
/// <param name="Blue">Classes, structs and type parameters.</param>
/// <param name="LightBlue">Fields, interfaces, enum members, locals and parameters.</param>
/// <param name="Gold">Enums and events.</param>
/// <param name="Silver">Properties and namespaces.</param>
/// <param name="Gray">Constants, keywords, snippets and labels.</param>
/// <param name="Ink">Details drawn over a filled shape.</param>
/// <param name="White">The arrow of an extension method.</param>
internal sealed record GlyphColors(Color Purple, Color Blue, Color LightBlue, Color Gold, Color Silver, Color Gray, Color Ink, Color White)
{
    /// <summary>The glyph colors of a theme, from the palette.</summary>
    /// <param name="variant">Dark or light.</param>
    public static GlyphColors Of(ThemeVariant variant) => new(
        ThemeBrushes.ColorOf("Symbol.Purple", variant),
        ThemeBrushes.ColorOf("Symbol.Blue", variant),
        ThemeBrushes.ColorOf("Symbol.LightBlue", variant),
        ThemeBrushes.ColorOf("Symbol.Gold", variant),
        ThemeBrushes.ColorOf("Symbol.Silver", variant),
        ThemeBrushes.ColorOf("Symbol.Gray", variant),
        ThemeBrushes.ColorOf("Symbol.Ink", variant),
        ThemeBrushes.ColorOf("Brush.OnAccent", variant));
}

/// <summary>One shape of a glyph: a geometry with a fill, an outline, or both.</summary>
/// <param name="Geometry">The shape, on a 16 by 16 grid.</param>
/// <param name="Fill">The fill, or <see langword="null"/>.</param>
/// <param name="Pen">The outline, or <see langword="null"/>.</param>
internal sealed record GlyphPart(Geometry Geometry, IBrush? Fill, IPen? Pen);
