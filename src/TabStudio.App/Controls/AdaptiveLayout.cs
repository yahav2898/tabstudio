using Avalonia;
using Avalonia.Input;
using Avalonia.Layout;

namespace TabStudio.App.Controls;

/// <summary>How an item of an <see cref="AdaptiveBar"/> is shown.</summary>
public enum AdaptiveMode
{
    /// <summary>Its icon and its label.</summary>
    Full,

    /// <summary>Its icon alone, with the label as a tooltip.</summary>
    Compact,

    /// <summary>In the More menu, out of the bar.</summary>
    Overflow,
}

/// <summary>One item of a bar as the layout sees it.</summary>
/// <param name="Full">Its length with its label.</param>
/// <param name="Compact">Its length as an icon alone; the same as <paramref name="Full"/> when it cannot shrink.</param>
/// <param name="Priority">How much it matters: items with a lower priority shrink and then leave the bar first.</param>
/// <param name="IsPinned">Whether it always stays in the bar, such as Run or the selected tool window.</param>
/// <param name="IsSeparator">Whether it is a separator, shown only between two items that stay in the bar.</param>
public readonly record struct AdaptiveSlot(double Full, double Compact, int Priority, bool IsPinned = false, bool IsSeparator = false)
{
    /// <summary>Whether it can shrink to its icon.</summary>
    public bool CanCompact => Compact < Full;
}

/// <summary>
/// Fits the items of a toolbar, status bar or activity bar into the space it has, as Visual Studio's toolbars do on a
/// narrow window, but without losing anything: first the least important items shrink to their icons, then they move,
/// least important first, into a More menu at the end of the bar. Pinned items never leave.
/// </summary>
public static class AdaptiveLayout
{
    /// <summary>
    /// Where a bar arranges what it does not show: far outside itself, so it can clip it. Arranging it in an empty
    /// rectangle is not enough, because a button's minimum size would still draw it in the bar's corner.
    /// </summary>
    public static Rect Parked { get; } = new(-100_000, -100_000, 0, 0);

    /// <summary>
    /// Arranges a bar's item where it shows, or parks it when the rectangle is <see cref="Parked"/>. A parked item is
    /// no tab stop, so the keyboard never moves the focus off screen; it is reached through More.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="rect">Where it shows, or <see cref="Parked"/>.</param>
    public static void Place(Layoutable item, Rect rect)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.Arrange(rect);
        if (item is InputElement input)
        {
            KeyboardNavigation.SetIsTabStop(input, rect != Parked);
        }
    }

    /// <summary>How each item is shown.</summary>
    /// <param name="slots">The items, in the bar's order.</param>
    /// <param name="available">The bar's length.</param>
    /// <param name="spacing">The space between two items.</param>
    /// <param name="moreLength">The length of the More button, which appears when an item overflows.</param>
    /// <returns>Each item's mode; separators are <see cref="AdaptiveMode.Full"/> or <see cref="AdaptiveMode.Overflow"/>.</returns>
    public static AdaptiveMode[] Fit(IReadOnlyList<AdaptiveSlot> slots, double available, double spacing, double moreLength)
    {
        ArgumentNullException.ThrowIfNull(slots);
        var modes = new AdaptiveMode[slots.Count];
        Shrink(slots, modes, available, spacing, moreLength);
        HideLoneSeparators(slots, modes);
        return modes;
    }

    /// <summary>The bar's length with these modes, the More button included when something overflows.</summary>
    /// <param name="slots">The items.</param>
    /// <param name="modes">Their modes.</param>
    /// <param name="spacing">The space between two items.</param>
    /// <param name="moreLength">The More button's length.</param>
    public static double Length(IReadOnlyList<AdaptiveSlot> slots, IReadOnlyList<AdaptiveMode> modes, double spacing, double moreLength)
    {
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(modes);
        var shown = Shown(slots, modes);
        var length = 0.0;
        var count = 0;
        var overflows = false;
        for (var i = 0; i < slots.Count; i++)
        {
            if (!shown[i])
            {
                overflows |= !slots[i].IsSeparator && modes[i] == AdaptiveMode.Overflow;
                continue;
            }

            length += modes[i] == AdaptiveMode.Compact ? slots[i].Compact : slots[i].Full;
            count++;
        }

        if (overflows)
        {
            length += moreLength;
            count++;
        }

        return length + (Math.Max(0, count - 1) * spacing);
    }

    private static void Shrink(IReadOnlyList<AdaptiveSlot> slots, AdaptiveMode[] modes, double available, double spacing, double moreLength)
    {
        if (Length(slots, modes, spacing, moreLength) <= available)
        {
            return;
        }

        // Least important first; among equals, the one further along the bar.
        var byImportance = Enumerable.Range(0, slots.Count)
            .Where(i => !slots[i].IsSeparator)
            .OrderBy(i => slots[i].Priority)
            .ThenByDescending(i => i)
            .ToList();

        foreach (var index in byImportance.Where(i => slots[i].CanCompact))
        {
            modes[index] = AdaptiveMode.Compact;
            if (Length(slots, modes, spacing, moreLength) <= available)
            {
                return;
            }
        }

        foreach (var index in byImportance.Where(i => !slots[i].IsPinned))
        {
            modes[index] = AdaptiveMode.Overflow;
            if (Length(slots, modes, spacing, moreLength) <= available)
            {
                break;
            }
        }

        // Room freed by an item that left may let a more important one show its label again.
        for (var k = byImportance.Count - 1; k >= 0; k--)
        {
            var index = byImportance[k];
            if (modes[index] != AdaptiveMode.Compact)
            {
                continue;
            }

            modes[index] = AdaptiveMode.Full;
            if (Length(slots, modes, spacing, moreLength) > available)
            {
                modes[index] = AdaptiveMode.Compact;
            }
        }
    }

    private static void HideLoneSeparators(IReadOnlyList<AdaptiveSlot> slots, AdaptiveMode[] modes)
    {
        var shown = Shown(slots, modes);
        for (var i = 0; i < slots.Count; i++)
        {
            if (slots[i].IsSeparator && !shown[i])
            {
                modes[i] = AdaptiveMode.Overflow;
            }
        }
    }

    /// <summary>Which items show: those in the bar, and separators with a shown item on each side and no separator before them.</summary>
    private static bool[] Shown(IReadOnlyList<AdaptiveSlot> slots, IReadOnlyList<AdaptiveMode> modes)
    {
        var shown = new bool[slots.Count];
        var itemBefore = false;
        for (var i = 0; i < slots.Count; i++)
        {
            if (!slots[i].IsSeparator)
            {
                shown[i] = modes[i] != AdaptiveMode.Overflow;
                itemBefore |= shown[i];
                continue;
            }

            var itemAfter = false;
            for (var j = i + 1; j < slots.Count && !slots[j].IsSeparator; j++)
            {
                itemAfter |= modes[j] != AdaptiveMode.Overflow;
            }

            shown[i] = itemBefore && itemAfter;
            if (shown[i])
            {
                itemBefore = false;
            }
        }

        return shown;
    }
}
