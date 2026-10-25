using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TabStudio.App.ViewModels.Shell;

/// <summary>What an activity bar item's badge shows.</summary>
public enum ActivityBadgeKind
{
    /// <summary>No badge.</summary>
    None,

    /// <summary>A number, such as failed tests or the agent's proposed changes.</summary>
    Count,

    /// <summary>A dot, such as the CI state of the commit.</summary>
    Dot,

    /// <summary>A turning arc: something is working, such as the agent or a test run.</summary>
    Activity,
}

/// <summary>The color of a badge, by what it says.</summary>
public enum ActivityTone
{
    /// <summary>Information that needs no action.</summary>
    Neutral,

    /// <summary>Something new for the user, such as changes to review.</summary>
    Accent,

    /// <summary>All is well, such as passed checks.</summary>
    Success,

    /// <summary>Attention, such as warnings.</summary>
    Warning,

    /// <summary>Something failed.</summary>
    Error,

    /// <summary>Something is in progress, such as running checks.</summary>
    Running,
}

/// <summary>
/// One tool window in the activity bar: its icon, its name and key gesture (the tooltip, and the label a long press
/// shows), and a badge that says what is going on in it without opening it.
/// </summary>
public sealed partial class ActivityItem : ObservableObject
{
    /// <summary>The most a count badge spells out; more reads "99+".</summary>
    public const int MaximumCount = 99;

    /// <summary>Creates an item.</summary>
    /// <param name="id">Identifies the tool window, such as <c>TestExplorer</c>.</param>
    /// <param name="title">Its name, such as "Test Explorer".</param>
    /// <param name="icon">Its icon, a stroke geometry on the 24×24 grid of Icons.axaml.</param>
    /// <param name="gesture">Its key gesture, such as "Ctrl+E, T", or <see langword="null"/>.</param>
    public ActivityItem(string id, string title, Geometry? icon, string? gesture = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentException.ThrowIfNullOrEmpty(title);
        Id = id;
        Title = title;
        Icon = icon;
        Gesture = gesture;
    }

    /// <summary>Identifies the tool window.</summary>
    public string Id { get; }

    /// <summary>Its key gesture, or <see langword="null"/>.</summary>
    public string? Gesture { get; }

    /// <summary>Its name.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToolTip))]
    public partial string Title { get; set; }

    /// <summary>Its icon.</summary>
    [ObservableProperty]
    public partial Geometry? Icon { get; set; }

    /// <summary>Whether the tool window is available now; the App preview only is while an app runs.</summary>
    [ObservableProperty]
    public partial bool IsAvailable { get; set; } = true;

    /// <summary>Whether it sits at the bottom of the bar, away from the tool windows, as settings do.</summary>
    [ObservableProperty]
    public partial bool IsAtBottom { get; set; }

    /// <summary>Whether its tool window is the one the side bar shows.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>What the badge shows.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBadge), nameof(BadgeText), nameof(ToolTip))]
    public partial ActivityBadgeKind BadgeKind { get; private set; }

    /// <summary>The number on a count badge.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BadgeText))]
    public partial int BadgeCount { get; private set; }

    /// <summary>The badge's color.</summary>
    [ObservableProperty]
    public partial ActivityTone BadgeTone { get; private set; }

    /// <summary>What the badge means, in words, for the tooltip and screen readers, such as "1 test failed".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToolTip))]
    public partial string? BadgeDescription { get; private set; }

    /// <summary>Whether there is a badge.</summary>
    public bool HasBadge => BadgeKind != ActivityBadgeKind.None;

    /// <summary>The number as the badge spells it: up to 99, then "99+".</summary>
    public string BadgeText => BadgeKind != ActivityBadgeKind.Count ? string.Empty
        : BadgeCount > MaximumCount ? MaximumCount.ToString(CultureInfo.CurrentCulture) + "+"
        : BadgeCount.ToString(CultureInfo.CurrentCulture);

    /// <summary>The tooltip: the name, the key gesture, and what the badge says.</summary>
    public string ToolTip
    {
        get
        {
            var name = Gesture is null ? Title : $"{Title} ({Gesture})";
            return BadgeDescription is { Length: > 0 } description ? $"{name}\n{description}" : name;
        }
    }

    /// <summary>Shows a number, or no badge for zero.</summary>
    /// <param name="count">The number.</param>
    /// <param name="tone">Its color.</param>
    /// <param name="description">What it means, such as "1 test failed".</param>
    public void ShowCount(int count, ActivityTone tone, string description)
    {
        if (count <= 0)
        {
            ClearBadge();
            return;
        }

        BadgeCount = count;
        Show(ActivityBadgeKind.Count, tone, description);
    }

    /// <summary>Shows a dot.</summary>
    /// <param name="tone">Its color.</param>
    /// <param name="description">What it means, such as "All checks passed".</param>
    public void ShowDot(ActivityTone tone, string description) => Show(ActivityBadgeKind.Dot, tone, description);

    /// <summary>Shows that something is working.</summary>
    /// <param name="description">What, such as "Claude is working".</param>
    public void ShowActivity(string description) => Show(ActivityBadgeKind.Activity, ActivityTone.Running, description);

    /// <summary>Removes the badge.</summary>
    public void ClearBadge()
    {
        BadgeCount = 0;
        Show(ActivityBadgeKind.None, ActivityTone.Neutral, null);
    }

    private void Show(ActivityBadgeKind kind, ActivityTone tone, string? description)
    {
        BadgeTone = tone;
        BadgeDescription = description;
        BadgeKind = kind;
    }
}
