namespace TabStudio.App.Accessibility;

/// <summary>The IDE's text size, under Tools &gt; Text Size.</summary>
public enum TextSize
{
    /// <summary>Android's font size (Settings &gt; Display &gt; Font size), changing when it does. The default.</summary>
    System,

    /// <summary>Smaller than the default: 85%.</summary>
    Small,

    /// <summary>The IDE's own sizes.</summary>
    Default,

    /// <summary>115%.</summary>
    Large,

    /// <summary>130%.</summary>
    Larger,

    /// <summary>150%, the largest; every view stays usable and unclipped at it.</summary>
    VeryLarge,
}

/// <summary>What each text size means.</summary>
public static class TextSizes
{
    /// <summary>The smallest factor, the Small size's.</summary>
    public const double Smallest = 0.85;

    /// <summary>The largest factor, the Very large size's. Android's larger font sizes (up to 200%) use it.</summary>
    public const double Largest = 1.5;

    /// <summary>The sizes in the order the menu lists them.</summary>
    public static IReadOnlyList<TextSize> All { get; } =
        [TextSize.System, TextSize.Small, TextSize.Default, TextSize.Large, TextSize.Larger, TextSize.VeryLarge];

    /// <summary>The sizes that have a factor of their own, smallest first, which Make Text Larger and Smaller step through.</summary>
    public static IReadOnlyList<TextSize> Steps { get; } =
        [TextSize.Small, TextSize.Default, TextSize.Large, TextSize.Larger, TextSize.VeryLarge];

    /// <summary>The factor of a size, the system's font scale for <see cref="TextSize.System"/>.</summary>
    /// <param name="size">The size.</param>
    /// <param name="systemFontScale">Android's font scale, 1 where there is none.</param>
    public static double Factor(TextSize size, double systemFontScale) => size switch
    {
        TextSize.Small => Smallest,
        TextSize.Default => 1,
        TextSize.Large => 1.15,
        TextSize.Larger => 1.3,
        TextSize.VeryLarge => Largest,
        _ => double.IsFinite(systemFontScale) && systemFontScale > 0 ? Math.Clamp(systemFontScale, Smallest, Largest) : 1,
    };

    /// <summary>The size's name, as in the menu.</summary>
    /// <param name="size">The size.</param>
    public static string DisplayName(TextSize size) => size switch
    {
        TextSize.Small => "Small",
        TextSize.Default => "Default",
        TextSize.Large => "Large",
        TextSize.Larger => "Larger",
        TextSize.VeryLarge => "Very large",
        _ => "Follow the system",
    };

    /// <summary>The next size up or down from the factor in use, as Make Text Larger and Smaller choose it.</summary>
    /// <param name="factor">The factor in use.</param>
    /// <param name="larger">Whether to go up.</param>
    public static TextSize Step(double factor, bool larger)
    {
        if (larger)
        {
            return Steps.FirstOrDefault(s => Factor(s, 1) > factor + 0.001, TextSize.VeryLarge);
        }

        return Steps.LastOrDefault(s => Factor(s, 1) < factor - 0.001, TextSize.Small);
    }
}
