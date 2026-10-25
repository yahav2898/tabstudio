using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TabStudio.App.Editing.Extensions.Svg;

/// <summary>
/// Draws the SVG images that file icon themes use: paths and basic shapes with fills, strokes, opacity, transforms,
/// <c>use</c> references, linear and radial gradients, and simple CSS rules in a <c>style</c> element. Filters, masks
/// and text are left out, which icons rarely need.
/// </summary>
internal static partial class SvgDrawingReader
{
    private static readonly XNamespace Xlink = "http://www.w3.org/1999/xlink";

    /// <summary>Reads an SVG document into an image whose size is the document's view box.</summary>
    /// <param name="xml">The document.</param>
    /// <returns>The image, or <see langword="null"/> when the document is not an SVG image.</returns>
    public static DrawingImage? Read(string xml)
    {
        ArgumentNullException.ThrowIfNull(xml);
        XDocument document;
        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
            using var text = new StringReader(xml);
            using var reader = XmlReader.Create(text, settings);
            document = XDocument.Load(reader);
        }
        catch (XmlException)
        {
            return null;
        }

        if (document.Root is not { Name.LocalName: "svg" } root)
        {
            return null;
        }

        var box = ViewBox(root);
        if (box.Width <= 0 || box.Height <= 0)
        {
            return null;
        }

        var context = new SvgDocument(document);
        var content = new DrawingGroup();
        context.DrawChildren(root, Style.Initial, content);

        // A transparent rectangle keeps the image as large as its view box, and the clip keeps it inside.
        var image = new DrawingGroup { ClipGeometry = new RectangleGeometry(box) };
        image.Children.Add(new GeometryDrawing { Brush = Brushes.Transparent, Geometry = new RectangleGeometry(box) });
        image.Children.Add(content);
        return new DrawingImage(image);
    }

    private static Rect ViewBox(XElement root)
    {
        if (root.Attribute("viewBox")?.Value is { } viewBox)
        {
            var numbers = Numbers(viewBox);
            if (numbers.Count == 4)
            {
                return new Rect(numbers[0], numbers[1], numbers[2], numbers[3]);
            }
        }

        return new Rect(0, 0, Length(root.Attribute("width")?.Value, 16), Length(root.Attribute("height")?.Value, 16));
    }

    /// <summary>A length such as <c>16</c> or <c>16px</c>; percentages and other units give the fallback.</summary>
    private static double Length(string? text, double fallback)
    {
        if (text is null)
        {
            return fallback;
        }

        var trimmed = text.Trim();
        if (trimmed.EndsWith("px", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^2];
        }

        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }

    private static List<double> Numbers(string text)
    {
        var numbers = new List<double>();
        foreach (Match match in NumberPattern().Matches(text))
        {
            if (double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                numbers.Add(value);
            }
        }

        return numbers;
    }

    private static double Number(XElement element, string name, double fallback = 0) =>
        element.Attribute(name)?.Value is { } text && double.TryParse(text.Trim().TrimEnd('x', 'p'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    [GeneratedRegex(@"[+-]?(?:\d+\.?\d*|\.\d+)(?:[eE][+-]?\d+)?")]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"(?<name>[a-zA-Z]+)\s*\((?<args>[^)]*)\)")]
    private static partial Regex TransformPattern();

    [GeneratedRegex(@"(?<selectors>[^{}]+)\{(?<declarations>[^}]*)\}")]
    private static partial Regex CssRulePattern();

    /// <summary>The paint and line properties an element inherits from its parent.</summary>
    private sealed record Style(
        string Fill,
        double FillOpacity,
        FillRule FillRule,
        string Stroke,
        double StrokeWidth,
        double StrokeOpacity,
        PenLineCap LineCap,
        PenLineJoin LineJoin,
        string Color)
    {
        public static Style Initial { get; } = new("black", 1, FillRule.NonZero, "none", 1, 1, PenLineCap.Flat, PenLineJoin.Miter, "black");
    }

    /// <summary>The document being drawn: its elements by id and its CSS rules.</summary>
    private sealed class SvgDocument
    {
        private const int MaximumDepth = 32;

        private readonly Dictionary<string, XElement> _ids = new(StringComparer.Ordinal);
        private readonly List<(string Selector, Dictionary<string, string> Declarations)> _rules = [];
        private int _depth;

        public SvgDocument(XDocument document)
        {
            foreach (var element in document.Descendants())
            {
                if (element.Attribute("id")?.Value is { Length: > 0 } id)
                {
                    _ids.TryAdd(id, element);
                }

                if (element.Name.LocalName == "style")
                {
                    ReadCss(element.Value);
                }
            }
        }

        public void DrawChildren(XElement parent, Style style, DrawingGroup target)
        {
            foreach (var child in parent.Elements())
            {
                if (Draw(child, style) is { } drawing)
                {
                    target.Children.Add(drawing);
                }
            }
        }

        private Drawing? Draw(XElement element, Style inherited)
        {
            var properties = Properties(element);
            if (properties.GetValueOrDefault("display") == "none" || properties.GetValueOrDefault("visibility") is "hidden" or "collapse")
            {
                return null;
            }

            var style = Apply(inherited, properties);
            Drawing? drawing = element.Name.LocalName switch
            {
                "g" or "a" or "switch" => Group(element, style),
                "svg" when element.Parent is not null => NestedSvg(element, style),
                "use" => Use(element, style),
                "path" => Shape(element, style, element.Attribute("d")?.Value is { } d ? SvgPathReader.Read(d, style.FillRule) : null),
                "rect" => Shape(element, style, Rectangle(element)),
                "circle" => Shape(element, style, Ellipse(element, Number(element, "r"), Number(element, "r"))),
                "ellipse" => Shape(element, style, Ellipse(element, Number(element, "rx"), Number(element, "ry"))),
                "line" => Shape(element, style, new LineGeometry(new Point(Number(element, "x1"), Number(element, "y1")), new Point(Number(element, "x2"), Number(element, "y2")))),
                "polyline" => Shape(element, style, element.Attribute("points")?.Value is { } points ? SvgPathReader.ReadPoints(points, closed: false, style.FillRule) : null),
                "polygon" => Shape(element, style, element.Attribute("points")?.Value is { } corners ? SvgPathReader.ReadPoints(corners, closed: true, style.FillRule) : null),
                _ => null,
            };
            if (drawing is null)
            {
                return null;
            }

            var transform = Transform(element.Attribute("transform")?.Value);
            var opacity = Opacity(properties.GetValueOrDefault("opacity"));
            if (transform is null && opacity >= 1)
            {
                return drawing;
            }

            var group = new DrawingGroup { Opacity = opacity };
            if (transform is { } matrix)
            {
                group.Transform = new MatrixTransform(matrix);
            }

            group.Children.Add(drawing);
            return group;
        }

        private DrawingGroup? Group(XElement element, Style style)
        {
            if (++_depth > MaximumDepth)
            {
                _depth--;
                return null;
            }

            var group = new DrawingGroup();
            DrawChildren(element, style, group);
            _depth--;
            return group.Children.Count > 0 ? group : null;
        }

        private DrawingGroup? NestedSvg(XElement element, Style style)
        {
            var group = Group(element, style);
            if (group is null)
            {
                return null;
            }

            var x = Number(element, "x");
            var y = Number(element, "y");
            if (x != 0 || y != 0)
            {
                var outer = new DrawingGroup { Transform = new MatrixTransform(Matrix.CreateTranslation(x, y)) };
                outer.Children.Add(group);
                return outer;
            }

            return group;
        }

        /// <summary>A <c>use</c> element: the referenced element, drawn again at the given offset.</summary>
        private DrawingGroup? Use(XElement element, Style style)
        {
            var reference = (element.Attribute(Xlink + "href") ?? element.Attribute("href"))?.Value;
            if (reference is not { Length: > 1 } || reference[0] != '#' || !_ids.TryGetValue(reference[1..], out var target)
                || target.AncestorsAndSelf().Contains(element) || ++_depth > MaximumDepth)
            {
                return null;
            }

            var drawing = target.Name.LocalName == "symbol" ? Group(target, style) : Draw(target, style);
            _depth--;
            if (drawing is null)
            {
                return null;
            }

            var group = new DrawingGroup();
            var x = Number(element, "x");
            var y = Number(element, "y");
            if (x != 0 || y != 0)
            {
                group.Transform = new MatrixTransform(Matrix.CreateTranslation(x, y));
            }

            group.Children.Add(drawing);
            return group;
        }

        private GeometryDrawing? Shape(XElement element, Style style, Geometry? geometry)
        {
            if (geometry is null)
            {
                return null;
            }

            var fill = Paint(style.Fill, style.FillOpacity, style.Color);
            var stroke = Paint(style.Stroke, style.StrokeOpacity, style.Color);
            if (fill is null && stroke is null)
            {
                return null;
            }

            return new GeometryDrawing
            {
                Geometry = geometry,
                Brush = element.Name.LocalName == "line" ? null : fill,
                Pen = stroke is null || style.StrokeWidth <= 0 ? null : new Pen(stroke, style.StrokeWidth, lineCap: style.LineCap, lineJoin: style.LineJoin),
            };
        }

        private static Geometry? Rectangle(XElement element)
        {
            var width = Number(element, "width");
            var height = Number(element, "height");
            if (width <= 0 || height <= 0)
            {
                return null;
            }

            var rect = new Rect(Number(element, "x"), Number(element, "y"), width, height);
            var rx = Number(element, "rx", double.NaN);
            var ry = Number(element, "ry", double.NaN);
            rx = double.IsNaN(rx) ? (double.IsNaN(ry) ? 0 : ry) : rx;
            ry = double.IsNaN(ry) ? rx : ry;
            return new RectangleGeometry(rect, Math.Min(rx, width / 2), Math.Min(ry, height / 2));
        }

        private static EllipseGeometry? Ellipse(XElement element, double rx, double ry) =>
            rx > 0 && ry > 0 ? new EllipseGeometry(new Rect(Number(element, "cx") - rx, Number(element, "cy") - ry, 2 * rx, 2 * ry)) : null;

        /// <summary>A fill or stroke: a color, <c>currentColor</c>, a gradient by reference, or none.</summary>
        private IBrush? Paint(string paint, double opacity, string currentColor)
        {
            var value = paint.Trim();
            if (value.Length == 0 || value == "none" || value == "transparent")
            {
                return null;
            }

            if (value.StartsWith("url(", StringComparison.OrdinalIgnoreCase))
            {
                var end = value.IndexOf(')', StringComparison.Ordinal);
                var id = value[4..(end < 0 ? value.Length : end)].Trim().Trim('\'', '"').TrimStart('#');
                var fallback = end >= 0 ? value[(end + 1)..].Trim() : string.Empty;
                return _ids.TryGetValue(id, out var gradient) && Gradient(gradient, opacity) is { } brush
                    ? brush
                    : fallback.Length > 0 ? Paint(fallback, opacity, currentColor) : null;
            }

            if (value == "currentColor")
            {
                value = currentColor;
            }

            return ParseColor(value) is { } color ? new ImmutableSolidColorBrush(WithOpacity(color, opacity)) : null;
        }

        private IBrush? Gradient(XElement gradient, double opacity)
        {
            var stops = Stops(gradient, 0);
            if (stops.Count == 0)
            {
                return null;
            }

            if (stops.Count == 1)
            {
                return new ImmutableSolidColorBrush(WithOpacity(stops[0].Color, opacity));
            }

            var userSpace = Inherited(gradient, "gradientUnits") == "userSpaceOnUse";
            var unit = userSpace ? RelativeUnit.Absolute : RelativeUnit.Relative;
            double Coordinate(string name, double fallback) => Fraction(Inherited(gradient, name), fallback);
            var gradientStops = new GradientStops();
            foreach (var (offset, color) in stops)
            {
                gradientStops.Add(new GradientStop(WithOpacity(color, opacity), offset));
            }

            var spread = Inherited(gradient, "spreadMethod") switch
            {
                "reflect" => GradientSpreadMethod.Reflect,
                "repeat" => GradientSpreadMethod.Repeat,
                _ => GradientSpreadMethod.Pad,
            };
            GradientBrush brush;
            if (gradient.Name.LocalName == "radialGradient")
            {
                var cx = Coordinate("cx", 0.5);
                var cy = Coordinate("cy", 0.5);
                var r = Coordinate("r", 0.5);
                brush = new RadialGradientBrush
                {
                    Center = new RelativePoint(cx, cy, unit),
                    GradientOrigin = new RelativePoint(Coordinate("fx", cx), Coordinate("fy", cy), unit),
                    RadiusX = new RelativeScalar(r, unit),
                    RadiusY = new RelativeScalar(r, unit),
                };
            }
            else
            {
                brush = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(Coordinate("x1", 0), Coordinate("y1", 0), unit),
                    EndPoint = new RelativePoint(Coordinate("x2", 1), Coordinate("y2", 0), unit),
                };
            }

            brush.GradientStops = gradientStops;
            brush.SpreadMethod = spread;
            if (userSpace && Transform(Inherited(gradient, "gradientTransform")) is { } matrix)
            {
                brush.Transform = new MatrixTransform(matrix);
                brush.TransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
            }

            return brush;
        }

        /// <summary>A gradient's stops, from itself or the gradient it refers to.</summary>
        private List<(double Offset, Color Color)> Stops(XElement gradient, int depth)
        {
            var stops = new List<(double Offset, Color Color)>();
            var last = 0d;
            foreach (var stop in gradient.Elements().Where(e => e.Name.LocalName == "stop"))
            {
                var properties = Properties(stop);
                var offset = Math.Clamp(Fraction(stop.Attribute("offset")?.Value, 0), last, 1);
                last = offset;
                var color = ParseColor(properties.GetValueOrDefault("stop-color") ?? "black") ?? Colors.Black;
                stops.Add((offset, WithOpacity(color, Opacity(properties.GetValueOrDefault("stop-opacity")))));
            }

            if (stops.Count == 0 && depth < 8 && Reference(gradient) is { } referenced)
            {
                return Stops(referenced, depth + 1);
            }

            return stops;
        }

        /// <summary>A gradient attribute from the gradient or, when it has none, the gradient it refers to.</summary>
        private string? Inherited(XElement gradient, string name)
        {
            XElement? current = gradient;
            for (var depth = 0; current is not null && depth < 8; depth++)
            {
                if (current.Attribute(name)?.Value is { } value)
                {
                    return value.Trim();
                }

                current = Reference(current);
            }

            return null;
        }

        private XElement? Reference(XElement element) =>
            (element.Attribute(Xlink + "href") ?? element.Attribute("href"))?.Value is { Length: > 1 } href && href[0] == '#' && _ids.TryGetValue(href[1..], out var target)
                ? target
                : null;

        /// <summary>An element's style properties: its attributes, then the CSS rules that match it, then its style attribute.</summary>
        private Dictionary<string, string> Properties(XElement element)
        {
            var properties = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var attribute in element.Attributes().Where(a => a.Name.Namespace == XNamespace.None))
            {
                properties[attribute.Name.LocalName] = attribute.Value.Trim();
            }

            if (_rules.Count > 0)
            {
                var classes = (element.Attribute("class")?.Value ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var id = element.Attribute("id")?.Value;
                foreach (var (selector, declarations) in _rules)
                {
                    var matches = selector == element.Name.LocalName
                        || (selector.StartsWith('.') && classes.Contains(selector[1..], StringComparer.Ordinal))
                        || (selector.StartsWith('#') && selector[1..] == id);
                    if (matches)
                    {
                        foreach (var (name, value) in declarations)
                        {
                            properties[name] = value;
                        }
                    }
                }
            }

            if (element.Attribute("style")?.Value is { } inline)
            {
                foreach (var (name, value) in Declarations(inline))
                {
                    properties[name] = value;
                }
            }

            return properties;
        }

        private void ReadCss(string css)
        {
            foreach (Match rule in CssRulePattern().Matches(css))
            {
                var declarations = Declarations(rule.Groups["declarations"].Value);
                foreach (var selector in rule.Groups["selectors"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    _rules.Add((selector, declarations));
                }
            }
        }

        private static Dictionary<string, string> Declarations(string text)
        {
            var declarations = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var part in text.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var colon = part.IndexOf(':', StringComparison.Ordinal);
                if (colon > 0)
                {
                    declarations[part[..colon].Trim()] = part[(colon + 1)..].Replace("!important", string.Empty, StringComparison.Ordinal).Trim();
                }
            }

            return declarations;
        }

        private static Style Apply(Style style, Dictionary<string, string> properties)
        {
            foreach (var (name, value) in properties)
            {
                if (value == "inherit")
                {
                    continue;
                }

                style = name switch
                {
                    "fill" => style with { Fill = value },
                    "fill-opacity" => style with { FillOpacity = Opacity(value) },
                    "fill-rule" => style with { FillRule = value == "evenodd" ? FillRule.EvenOdd : FillRule.NonZero },
                    "stroke" => style with { Stroke = value },
                    "stroke-width" => style with { StrokeWidth = Length(value, style.StrokeWidth) },
                    "stroke-opacity" => style with { StrokeOpacity = Opacity(value) },
                    "stroke-linecap" => style with { LineCap = value switch { "round" => PenLineCap.Round, "square" => PenLineCap.Square, _ => PenLineCap.Flat } },
                    "stroke-linejoin" => style with { LineJoin = value switch { "round" => PenLineJoin.Round, "bevel" => PenLineJoin.Bevel, _ => PenLineJoin.Miter } },
                    "color" => style with { Color = value },
                    _ => style,
                };
            }

            return style;
        }

        private static double Opacity(string? value)
        {
            if (value is null)
            {
                return 1;
            }

            var percent = value.EndsWith('%');
            return double.TryParse(percent ? value[..^1] : value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                ? Math.Clamp(percent ? number / 100 : number, 0, 1)
                : 1;
        }

        /// <summary>A gradient coordinate or stop offset: a number, or a percentage of the bounding box.</summary>
        private static double Fraction(string? value, double fallback)
        {
            if (value is null)
            {
                return fallback;
            }

            if (value.EndsWith('%'))
            {
                return double.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percent) ? percent / 100 : fallback;
            }

            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : fallback;
        }
    }

    /// <summary>A transform list, such as <c>translate(2 3) rotate(45 12 12)</c>, or <see langword="null"/> for none.</summary>
    private static Matrix? Transform(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var result = Matrix.Identity;
        foreach (Match match in TransformPattern().Matches(text))
        {
            var args = Numbers(match.Groups["args"].Value);
            var matrix = (match.Groups["name"].Value, args.Count) switch
            {
                ("matrix", 6) => new Matrix(args[0], args[1], args[2], args[3], args[4], args[5]),
                ("translate", 1) => Matrix.CreateTranslation(args[0], 0),
                ("translate", 2) => Matrix.CreateTranslation(args[0], args[1]),
                ("scale", 1) => Matrix.CreateScale(args[0], args[0]),
                ("scale", 2) => Matrix.CreateScale(args[0], args[1]),
                ("rotate", 1) => Matrix.CreateRotation(Radians(args[0])),
                ("rotate", 3) => Matrix.CreateTranslation(-args[1], -args[2]) * Matrix.CreateRotation(Radians(args[0])) * Matrix.CreateTranslation(args[1], args[2]),
                ("skewX", 1) => new Matrix(1, 0, Math.Tan(Radians(args[0])), 1, 0, 0),
                ("skewY", 1) => new Matrix(1, Math.Tan(Radians(args[0])), 0, 1, 0, 0),
                _ => Matrix.Identity,
            };

            // SVG applies the last transform of the list first.
            result = matrix * result;
        }

        return result;
    }

    private static double Radians(double degrees) => degrees * Math.PI / 180;

    private static Color WithOpacity(Color color, double opacity) =>
        opacity >= 1 ? color : Color.FromArgb((byte)Math.Round(color.A * Math.Clamp(opacity, 0, 1)), color.R, color.G, color.B);

    /// <summary>A CSS color: <c>#rgb</c>, <c>#rrggbb</c>, <c>#rrggbbaa</c>, <c>rgb()</c>, <c>rgba()</c> or a name.</summary>
    private static Color? ParseColor(string text)
    {
        var value = text.Trim();
        if (value.StartsWith('#'))
        {
            return ThemeColors.TryParse(value, out var hex) ? hex : null;
        }

        if (value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            var open = value.IndexOf('(', StringComparison.Ordinal);
            var close = value.LastIndexOf(')');
            if (open < 0 || close <= open)
            {
                return null;
            }

            var parts = value[(open + 1)..close].Split([',', ' ', '/'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
            {
                return null;
            }

            byte Channel(string part) => part.EndsWith('%')
                ? (byte)Math.Clamp(Math.Round(double.Parse(part[..^1], CultureInfo.InvariantCulture) * 2.55), 0, 255)
                : (byte)Math.Clamp(Math.Round(double.Parse(part, CultureInfo.InvariantCulture)), 0, 255);
            try
            {
                var alpha = parts.Length > 3 ? (byte)Math.Round(255 * Math.Clamp(parts[3].EndsWith('%') ? double.Parse(parts[3][..^1], CultureInfo.InvariantCulture) / 100 : double.Parse(parts[3], CultureInfo.InvariantCulture), 0, 1)) : (byte)255;
                return Color.FromArgb(alpha, Channel(parts[0]), Channel(parts[1]), Channel(parts[2]));
            }
            catch (FormatException)
            {
                return null;
            }
        }

        return Color.TryParse(value, out var named) ? named : null;
    }
}
