using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;

namespace TabStudio.App.Accessibility;

/// <summary>
/// The IDE's type scale (docs/design/README.md, "Type"): every font size is one of these tokens, a dynamic resource
/// such as <c>{DynamicResource Type.Body}</c>, never a number. <see cref="Apply"/> sets them all for a text size, so the
/// whole IDE and its editors follow the text size setting at once.
/// </summary>
public static class TypeScale
{
    /// <summary>The tokens and their sizes at the default text size, smallest first.</summary>
    public static IReadOnlyList<(string Key, double Size)> Tokens { get; } =
    [
        ("Type.Badge", 9.5),
        ("Type.Micro", 10.5),
        ("Type.Caption", 11),
        ("Type.Fine", 11.5),
        ("Type.Small", 12),
        ("Type.Body", 12.5),
        ("Type.Section", 13),
        ("Type.Lead", 13.5),
        ("Type.Large", 14),
        ("Type.Code", 14),
        ("Type.Subtitle", 15),
        ("Type.Heading", 16),
        ("Type.Title", 18),
        ("Type.Hero", 20),
        ("Type.Display", 26),

        // Line heights of wrapped text that sets one: they grow with the text, or its lines would overlap.
        ("Type.Line.Body", 18),
        ("Type.Line.Relaxed", 20),
    ];

    /// <summary>
    /// Fluent's own font sizes, scaled with the IDE's so that text no view sizes (a window's default, a tooltip, a
    /// built-in control) follows too.
    /// </summary>
    private static readonly (string Key, double Size)[] FluentTokens =
    [
        ("ControlContentThemeFontSize", 14),
        ("ToolTipContentThemeFontSize", 12),
    ];

    /// <summary>
    /// The size at the default text size of text that code creates (a Markdown heading, a table cell, the editor of a
    /// notebook cell): its font size follows the text size, as a token's does. For text with a size of the scale,
    /// prefer the token's dynamic resource.
    /// </summary>
    public static readonly AttachedProperty<double> BaseFontSizeProperty =
        AvaloniaProperty.RegisterAttached<StyledElement, double>("BaseFontSize", typeof(TypeScale), double.NaN);

    private static readonly ConditionalWeakTable<StyledElement, object> Sized = new();

    static TypeScale()
    {
        BaseFontSizeProperty.Changed.AddClassHandler<StyledElement>((element, _) => Resize(element));
    }

    /// <summary>The factor the sizes are multiplied by now: 1 at the default text size.</summary>
    public static double Factor { get; private set; } = 1;

    /// <summary>Raised after the sizes changed, for text drawn in code (the terminal, the line numbers).</summary>
    public static event EventHandler? Changed;

    /// <summary>Gets the size at the default text size that an element's font follows.</summary>
    public static double GetBaseFontSize(StyledElement element) => element.GetValue(BaseFontSizeProperty);

    /// <summary>Makes an element's font follow the text size, from its size at the default text size.</summary>
    /// <param name="element">A text block, a run, or a control such as an editor.</param>
    /// <param name="size">The size at the default text size, in dips.</param>
    public static void SetBaseFontSize(StyledElement element, double size) => element.SetValue(BaseFontSizeProperty, size);

    /// <summary>A token's size at the default text size.</summary>
    /// <param name="key">The token, such as <c>Type.Body</c>.</param>
    public static double BaseSize(string key)
    {
        foreach (var (token, size) in Tokens)
        {
            if (token == key)
            {
                return size;
            }
        }

        throw new ArgumentException($"{key} is not a type token.", nameof(key));
    }

    /// <summary>A token's size now.</summary>
    /// <param name="key">The token, such as <c>Type.Body</c>.</param>
    public static double Size(string key) => Scaled(BaseSize(key), Factor);

    /// <summary>A size scaled by a factor, rounded to a quarter of a dip so text stays crisp.</summary>
    public static double Scaled(double size, double factor) => Math.Round(size * factor * 4, MidpointRounding.AwayFromZero) / 4;

    /// <summary>The token for a font size, as a view once wrote it: the nearest size of the scale.</summary>
    /// <param name="size">The size in dips.</param>
    public static string TokenFor(double size) => size switch
    {
        < 10 => "Type.Badge",
        < 10.75 => "Type.Micro",
        < 11.25 => "Type.Caption",
        < 11.75 => "Type.Fine",
        < 12.25 => "Type.Small",
        < 12.75 => "Type.Body",
        < 13.25 => "Type.Section",
        < 13.75 => "Type.Lead",
        < 14.5 => "Type.Large",
        < 15.5 => "Type.Subtitle",
        < 17.5 => "Type.Heading",
        < 19.5 => "Type.Title",
        < 23 => "Type.Hero",
        _ => "Type.Display",
    };

    /// <summary>Sets every token for a factor in the app's resources, which every view reads through its dynamic resources.</summary>
    /// <param name="factor">1 for the default text size; see <see cref="TextSizes.Factor"/>.</param>
    public static void Apply(double factor)
    {
        if (Application.Current is { } application)
        {
            Apply(application.Resources, factor);
        }
        else
        {
            Factor = factor;
        }
    }

    /// <summary>Sets every token for a factor in a resource dictionary.</summary>
    /// <param name="resources">The dictionary, usually the app's.</param>
    /// <param name="factor">1 for the default text size.</param>
    public static void Apply(IResourceDictionary resources, double factor)
    {
        ArgumentNullException.ThrowIfNull(resources);
        if (factor is <= 0 or double.NaN)
        {
            throw new ArgumentOutOfRangeException(nameof(factor), factor.ToString(CultureInfo.InvariantCulture));
        }

        var changed = factor != Factor || !resources.ContainsKey("Type.Body");
        Factor = factor;
        foreach (var (key, size) in Tokens)
        {
            resources[key] = Scaled(size, factor);
        }

        foreach (var (key, size) in FluentTokens)
        {
            resources[key] = Scaled(size, factor);
        }

        if (changed)
        {
            foreach (var (element, _) in Sized.ToArray())
            {
                Resize(element);
            }

            Changed?.Invoke(null, EventArgs.Empty);
        }
    }

    private static void Resize(StyledElement element)
    {
        var size = element.GetValue(BaseFontSizeProperty);
        if (double.IsNaN(size))
        {
            Sized.Remove(element);
            return;
        }

        Sized.AddOrUpdate(element, Sized);
        element.SetValue(TextElement.FontSizeProperty, Scaled(size, Factor));
    }
}
