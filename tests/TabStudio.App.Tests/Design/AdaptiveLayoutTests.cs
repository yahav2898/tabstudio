using TabStudio.App.Controls;

namespace TabStudio.App.Tests.Design;

/// <summary>Fitting a bar into its width: shrink the least important items to icons, then move them into More.</summary>
public sealed class AdaptiveLayoutTests
{
    // Four items 100 wide (40 as icons), priorities 4, 3, 2, 1; no spacing; More is 40.
    private static readonly AdaptiveSlot[] Items =
    [
        new(100, 40, 4),
        new(100, 40, 3),
        new(100, 40, 2),
        new(100, 40, 1),
    ];

    [Fact]
    public void Everything_shows_in_full_when_it_fits()
    {
        var modes = AdaptiveLayout.Fit(Items, 400, 0, 40);

        Assert.All(modes, mode => Assert.Equal(AdaptiveMode.Full, mode));
    }

    [Fact]
    public void The_least_important_items_shrink_to_icons_first()
    {
        var modes = AdaptiveLayout.Fit(Items, 280, 0, 40);

        Assert.Equal(new[] { AdaptiveMode.Full, AdaptiveMode.Full, AdaptiveMode.Compact, AdaptiveMode.Compact }, modes);
        Assert.Equal(280, AdaptiveLayout.Length(Items, modes, 0, 40));
    }

    [Fact]
    public void Then_they_move_into_More_least_important_first()
    {
        var modes = AdaptiveLayout.Fit(Items, 120, 0, 40);

        // As icons the four take 160; two icons and More take 120.
        Assert.Equal(new[] { AdaptiveMode.Compact, AdaptiveMode.Compact, AdaptiveMode.Overflow, AdaptiveMode.Overflow }, modes);
        Assert.Equal(120, AdaptiveLayout.Length(Items, modes, 0, 40));
    }

    [Fact]
    public void Room_freed_by_an_item_that_left_gives_a_more_important_one_its_label_back()
    {
        AdaptiveSlot[] items = [new(100, 40, 3), new(100, 100, 2), new(100, 40, 1)];

        var modes = AdaptiveLayout.Fit(items, 160, 0, 40);

        Assert.Equal(new[] { AdaptiveMode.Full, AdaptiveMode.Overflow, AdaptiveMode.Overflow }, modes);
        Assert.Equal(140, AdaptiveLayout.Length(items, modes, 0, 40));
    }

    [Fact]
    public void Pinned_items_never_leave_the_bar()
    {
        AdaptiveSlot[] items = [new(100, 100, 0, IsPinned: true), new(100, 40, 5), new(100, 40, 4)];

        var modes = AdaptiveLayout.Fit(items, 150, 0, 40);

        Assert.Equal(AdaptiveMode.Full, modes[0]);
        Assert.Equal(AdaptiveMode.Overflow, modes[2]);
    }

    [Fact]
    public void Equal_priorities_leave_from_the_end_of_the_bar()
    {
        AdaptiveSlot[] items = [new(100, 100, 1), new(100, 100, 1), new(100, 100, 1)];

        var modes = AdaptiveLayout.Fit(items, 250, 0, 40);

        Assert.Equal(new[] { AdaptiveMode.Full, AdaptiveMode.Full, AdaptiveMode.Overflow }, modes);
    }

    [Fact]
    public void A_separator_shows_only_between_two_items_that_stay()
    {
        AdaptiveSlot[] items =
        [
            new(100, 100, 3),
            new(10, 10, int.MaxValue, IsSeparator: true),
            new(100, 100, 1),
            new(10, 10, int.MaxValue, IsSeparator: true),
            new(100, 100, 2),
        ];

        var wide = AdaptiveLayout.Fit(items, 1000, 0, 40);
        var narrow = AdaptiveLayout.Fit(items, 250, 0, 40);

        Assert.All(wide, mode => Assert.Equal(AdaptiveMode.Full, mode));
        Assert.Equal(new[] { AdaptiveMode.Full, AdaptiveMode.Overflow, AdaptiveMode.Overflow, AdaptiveMode.Full, AdaptiveMode.Full }, narrow);
        Assert.Equal(250, AdaptiveLayout.Length(items, narrow, 0, 40));
    }

    [Fact]
    public void Spacing_counts_between_shown_items_and_More()
    {
        AdaptiveSlot[] items = [new(50, 50, 2), new(50, 50, 1)];

        Assert.Equal(104, AdaptiveLayout.Length(items, new[] { AdaptiveMode.Full, AdaptiveMode.Full }, 4, 30));
        Assert.Equal(84, AdaptiveLayout.Length(items, new[] { AdaptiveMode.Full, AdaptiveMode.Overflow }, 4, 30));
    }
}
