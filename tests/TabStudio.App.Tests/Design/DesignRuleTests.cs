using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace TabStudio.App.Tests.Design;

/// <summary>
/// The XAML of the panels 0.15 and 0.16 added, read as text, against the rules of docs/design/README.md that a
/// screenshot cannot check: colors are tokens, sizes are on the type scale, spacing is on the 4-pixel grid (2 between a
/// toolbar's items), radii are the radius tokens, buttons use the IDE's button themes, icons come from the icon set and
/// headers are in sentence case. <see cref="PanelAuditTests"/> checks how the same panels look.
/// </summary>
public sealed partial class DesignRuleTests
{
    /// <summary>The type scale, in dips (docs/design/README.md, "Type").</summary>
    private static readonly double[] TypeScale = [26, 18, 15, 14, 13, 12.5, 12, 11, 9.5];

    /// <summary>The radius tokens, in dips (docs/design/README.md, "Corner radii").</summary>
    private static readonly double[] Radii = [4, 7, 8, 10, 12];

    private static readonly string[] BrushProperties = ["Foreground", "Background", "BorderBrush", "Fill", "Stroke", "Color", "BoxShadow"];

    private static readonly string[] SpacingProperties = ["Margin", "Padding", "Spacing", "RowSpacing", "ColumnSpacing"];

    /// <summary>
    /// Each new panel's XAML, under src/TabStudio.App: a whole file, or after '#' the element that holds the new part of an
    /// older file.
    /// </summary>
    public static TheoryData<string> NewParts { get; } =
    [
        "Views/Database/DatabaseExplorerView.axaml",
        "Views/Database/DatabaseQueryView.axaml",
        "Views/ServicesView.axaml",
        "Views/RemoteExplorerView.axaml",
        "Views/PortSummaryView.axaml",
        "Views/Toolchains/SerialMonitorView.axaml",
        "Views/Toolchains/ToolchainOverlaysView.axaml#ServerNotice",
        "Views/AgentPanelView.axaml#Settings",
        "Views/WorkspaceView.axaml#RecordingTimeline",
        "Views/WorkspaceView.axaml#PromptDialog",
    ];

    [Theory]
    [MemberData(nameof(NewParts))]
    public void A_new_panel_follows_the_design_rules(string part)
    {
        var pieces = part.Split('#');
        var file = pieces[0];
        var name = pieces.Length > 1 ? pieces[1] : null;
        var full = Path.Combine(RepositoryRoot(), "src", "TabStudio.App", file);
        var document = XDocument.Load(full, LoadOptions.SetLineInfo);
        var root = name is null
            ? document.Root!
            : document.Descendants().SingleOrDefault(e => e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == name))
              ?? throw new InvalidOperationException($"{file} has no element named {name}.");

        var problems = root.DescendantsAndSelf().SelectMany(Check).Select(p => $"{file}:{p.Line}: {p.Text}").ToList();
        if (name is null && File.Exists(full + ".cs"))
        {
            var code = File.ReadAllLines(full + ".cs");
            for (var i = 0; i < code.Length; i++)
            {
                if (LiteralBrushInCode().IsMatch(code[i]))
                {
                    problems.Add($"{file}.cs:{i + 1}: a color in code; use ThemeBrushes.Get(\"Brush.…\")");
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    private static IEnumerable<(int Line, string Text)> Check(XElement element)
    {
        var line = ((System.Xml.IXmlLineInfo)element).LineNumber;
        var kind = element.Name.LocalName;
        var isSetter = kind == "Setter";
        var values = new List<(string Property, string Value)>();
        if (isSetter)
        {
            values.Add(((string?)element.Attribute("Property") ?? string.Empty, (string?)element.Attribute("Value") ?? string.Empty));
        }
        else
        {
            values.AddRange(element.Attributes().Where(a => !a.IsNamespaceDeclaration).Select(a => (a.Name.LocalName, a.Value)));
        }

        var selector = isSetter ? (string?)element.Parent?.Attribute("Selector") ?? string.Empty : string.Empty;
        var height = isSetter
            ? (string?)element.Parent?.Elements().FirstOrDefault(s => (string?)s.Attribute("Property") == "Height")?.Attribute("Value")
            : (string?)element.Attribute("Height");

        foreach (var (property, value) in values)
        {
            if (value.StartsWith('{'))
            {
                continue;
            }

            if (BrushProperties.Contains(property) && (value.StartsWith('#') || NamedColor().IsMatch(value)))
            {
                yield return (line, $"{kind}.{property} is the color {value}; use a Brush.… token");
            }
            else if (property == "FontSize" && !TypeScale.Contains(Number(value)))
            {
                yield return (line, $"{kind}.FontSize {value} is not on the type scale ({string.Join(", ", TypeScale)})");
            }
            else if (property == "Opacity" && (kind is "TextBlock" or "SelectableTextBlock" || selector.Contains("TextBlock", StringComparison.Ordinal)))
            {
                yield return (line, $"{kind} mutes its text with Opacity {value}; use Brush.TextMuted or Brush.TextLabel");
            }
            else if (property == "FontFamily" && value is "monospace" or "Monospace")
            {
                yield return (line, $"{kind}.FontFamily {value}; use {{DynamicResource CodeFont}} (the mono class)");
            }
            else if (property == "CornerRadius" && !IsRadius(value, height, selector))
            {
                yield return (line, $"{kind}.CornerRadius {value} is not a radius token ({string.Join(", ", Radii)}) or half its height");
            }
            else if (SpacingProperties.Contains(property) && value.Split(',', ' ').Any(v => v.Length > 0 && !double.IsNaN(Number(v)) && Number(v) % 2 != 0))
            {
                yield return (line, $"{kind}.{property} {value} is off the 4-pixel grid");
            }
        }

        if (kind == "Button" && element.Attribute("Theme") is null)
        {
            yield return (line, "a Button without a theme; use ToolButton, SecondaryButton or AccentButton");
        }

        if (kind == "Button" && (string?)element.Attribute("Content") is { Length: > 0 and <= 2 } glyph && !glyph.Any(char.IsLetterOrDigit))
        {
            yield return (line, $"a Button whose content is the glyph {glyph}; use a StrokeIcon from the icon set");
        }

        if (kind == "StrokeIcon" && (string?)element.Attribute("Data") is { } data && !data.StartsWith("{StaticResource Icon.", StringComparison.Ordinal) && !data.StartsWith("{Binding", StringComparison.Ordinal))
        {
            yield return (line, $"an icon drawn from {data}; use an Icon.… geometry from the icon set");
        }

        if (kind is "TextBlock" or "Expander" or "TabItem" or "CheckBox" && ((string?)element.Attribute("Text") ?? (string?)element.Attribute("Header")) is { Length: > 3 } text
            && text.Any(char.IsLetter) && !text.Any(char.IsLower))
        {
            yield return (line, $"\"{text}\" is in capitals; headers are in sentence case");
        }
    }

    private static bool IsRadius(string value, string? height, string selector)
    {
        if (selector.Contains("ProgressBar", StringComparison.Ordinal))
        {
            return true;
        }

        var radius = value.Split(',', ' ').Where(v => v.Length > 0).Select(Number).Distinct().ToList();
        if (radius.All(r => r == 0 || Radii.Contains(r)))
        {
            return true;
        }

        // A pill: its height's half.
        return height is not null && radius.Count == 1 && Math.Abs((Number(height) / 2) - radius[0]) < 0.01;
    }

    private static double Number(string value) =>
        double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : double.NaN;

    private static string RepositoryRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "TabStudio.slnx")))
            {
                return folder.FullName;
            }
        }

        throw new InvalidOperationException("The repository root, with TabStudio.slnx, was not found above the test.");
    }

    [GeneratedRegex(@"^(Black|White|Red|Green|Blue|Gray|Grey|Yellow|Orange|DarkGray|LightGray|Teal)$")]
    private static partial Regex NamedColor();

    [GeneratedRegex(@"Brushes\.(?!Transparent)\w+|Color\.Parse|Color\.FromRgb|Color\.FromArgb|new SolidColorBrush\(")]
    private static partial Regex LiteralBrushInCode();
}
