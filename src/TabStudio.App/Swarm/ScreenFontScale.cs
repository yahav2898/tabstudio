using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;

namespace TabStudio.App.Swarm;

/// <summary>
/// The system font scale of a virtual device, as a phone's accessibility settings set it: every font size the app
/// sets is multiplied by it, inside the device's frame only, while sizes that are inherited pass on unchanged, so
/// nothing is scaled twice. Padding, icons and fixed widths stay as they are, which is what shows text that no
/// longer fits.
/// </summary>
/// <remarks>
/// Avalonia has no font scale of its own, so the scale is applied where font sizes are set: a coercion of the font
/// size of text blocks, templated controls and content presenters, which multiplies a size the element sets itself,
/// locally or by a style, by the scale of the frame it is in. Outside a scaled frame the scale is 1 and sizes are
/// what they were. The frame sets its own default size, which everything without a size inherits.
/// </remarks>
public static class ScreenFontScale
{
    /// <summary>The font scale of the frame an element is in, 1 outside one.</summary>
    public static readonly AttachedProperty<double> ScaleProperty =
        AvaloniaProperty.RegisterAttached<Visual, double>("Scale", typeof(ScreenFontScale), defaultValue: 1, inherits: true);

    private static readonly Lock Gate = new();
    private static bool _installed;

    /// <summary>The font scale of the frame an element is in.</summary>
    /// <param name="element">The element.</param>
    public static double GetScale(Visual element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.GetValue(ScaleProperty);
    }

    /// <summary>Sets the font scale of a frame and everything in it.</summary>
    /// <param name="element">The frame.</param>
    /// <param name="value">The scale, such as 1.3.</param>
    public static void SetScale(Visual element, double value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(ScaleProperty, value);
    }

    /// <summary>
    /// Makes font sizes follow the scale. Once per process, before the app's views are created: values Avalonia
    /// has already stored keep the rules they were stored with.
    /// </summary>
    public static void Install()
    {
        lock (Gate)
        {
            if (_installed)
            {
                return;
            }

            _installed = true;
        }

        Coerce<TextBlock>();
        Coerce<TemplatedControl>();
        Coerce<ContentPresenter>();
        ScaleProperty.Changed.AddClassHandler<StyledElement>((element, _) => element.CoerceValue(TextElement.FontSizeProperty));
    }

    private static void Coerce<T>()
        where T : AvaloniaObject
    {
        var inherited = TextElement.FontSizeProperty.GetMetadata(typeof(T)).CoerceValue;
        TextElement.FontSizeProperty.OverrideMetadata<T>(new StyledPropertyMetadata<double>(coerce: (element, size) =>
        {
            size = inherited?.Invoke(element, size) ?? size;

            // Only a size the element sets itself: an inherited one was scaled where it was set.
            return element.IsSet(TextElement.FontSizeProperty) && element is Visual visual && GetScale(visual) is var scale && scale != 1
                ? size * scale
                : size;
        }));
    }
}
