using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using TabStudio.Agent;
using TabStudio.App.ViewModels.PullRequests;

namespace TabStudio.App.Converters;

/// <summary>Colors and icons of pull requests and reviews, from the app's theme.</summary>
public static class ReviewConverters
{
    /// <summary>A person's avatar color, from their login.</summary>
    public static IValueConverter AvatarBrush { get; } = new FuncValueConverter<string?, IBrush?>(login => ReviewDisplay.AvatarBrush(login ?? string.Empty));

    /// <summary>The letter on a person's avatar, from their login.</summary>
    public static IValueConverter Initial { get; } = new FuncValueConverter<string?, string>(login => ReviewDisplay.Initial(login ?? string.Empty));

    /// <summary>The color of a pull request's state pill.</summary>
    public static IValueConverter BadgeBrush { get; } = new FuncValueConverter<PullRequestBadge, IBrush?>(badge => Find<IBrush>(badge switch
    {
        PullRequestBadge.Draft => "Brush.PullRequest.Draft",
        PullRequestBadge.Merged => "Brush.PullRequest.Merged",
        PullRequestBadge.Closed => "Brush.PullRequest.Closed",
        _ => "Brush.PullRequest.Open",
    }));

    /// <summary>The icon of a pull request's state pill.</summary>
    public static IValueConverter BadgeIcon { get; } = new FuncValueConverter<PullRequestBadge, Geometry?>(badge => Find<Geometry>(badge == PullRequestBadge.Merged ? "Icon.Merge" : "Icon.PullRequest"));

    /// <summary>The color of a pull request's icon in the list.</summary>
    public static IValueConverter ItemBrush { get; } = new FuncValueConverter<PullRequestItemViewModel?, IBrush?>(item => Find<IBrush>(item switch
    {
        { IsMerged: true } => "Brush.PullRequest.MergedText",
        { IsClosed: true } => "Brush.Error",
        { IsDraft: true } => "Brush.TextMuted",
        _ => "Brush.PullRequest.OpenText",
    }));

    /// <summary>The icon of the merge status line.</summary>
    public static IValueConverter MergeabilityIcon { get; } = new FuncValueConverter<MergeabilityKind, Geometry?>(kind => Find<Geometry>(kind switch
    {
        MergeabilityKind.Good => "Icon.Approved",
        MergeabilityKind.Warning => "Icon.Warning",
        MergeabilityKind.Blocked => "Icon.Error",
        _ => "Icon.TestQueued",
    }));

    /// <summary>The color of the merge status line's icon.</summary>
    public static IValueConverter MergeabilityBrush { get; } = new FuncValueConverter<MergeabilityKind, IBrush?>(kind => Find<IBrush>(kind switch
    {
        MergeabilityKind.Good => "Brush.Success",
        MergeabilityKind.Warning => "Brush.Warning",
        MergeabilityKind.Blocked => "Brush.Error",
        _ => "Brush.TextMuted",
    }));

    /// <summary>The icon of a reviewer's verdict.</summary>
    public static IValueConverter ReviewerIcon { get; } = new FuncValueConverter<ReviewerState, Geometry?>(state => Find<Geometry>(state switch
    {
        ReviewerState.Approved => "Icon.Approved",
        ReviewerState.ChangesRequested => "Icon.ChangesRequested",
        ReviewerState.Commented => "Icon.Conversation",
        _ => "Icon.Eye",
    }));

    /// <summary>The color of a reviewer's verdict.</summary>
    public static IValueConverter ReviewerBrush { get; } = new FuncValueConverter<ReviewerState, IBrush?>(state => Find<IBrush>(state switch
    {
        ReviewerState.Approved => "Brush.Success",
        ReviewerState.ChangesRequested => "Brush.Error",
        ReviewerState.Commented => "Brush.Review.Thread",
        _ => "Brush.Review.Pending",
    }));

    /// <summary>The icon of a conversation entry.</summary>
    public static IValueConverter TimelineIcon { get; } = new FuncValueConverter<TimelineKind, Geometry?>(kind => Find<Geometry>(kind switch
    {
        TimelineKind.Approved => "Icon.Approved",
        TimelineKind.ChangesRequested => "Icon.ChangesRequested",
        TimelineKind.Merged => "Icon.Merge",
        TimelineKind.Opened => "Icon.PullRequest",
        TimelineKind.Reviewed => "Icon.Eye",
        _ => "Icon.Conversation",
    }));

    /// <summary>The color of a conversation entry's icon.</summary>
    public static IValueConverter TimelineBrush { get; } = new FuncValueConverter<TimelineKind, IBrush?>(kind => Find<IBrush>(kind switch
    {
        TimelineKind.Approved => "Brush.Success",
        TimelineKind.ChangesRequested => "Brush.Error",
        TimelineKind.Merged => "Brush.PullRequest.MergedText",
        TimelineKind.Opened => "Brush.PullRequest.OpenText",
        _ => "Brush.TextMuted",
    }));

    /// <summary>The icon of how much one of Claude's findings matters.</summary>
    public static IValueConverter SeverityIcon { get; } = new FuncValueConverter<ReviewSeverity, Geometry?>(severity => Find<Geometry>(severity switch
    {
        ReviewSeverity.Bug => "Icon.Error",
        ReviewSeverity.Warning => "Icon.Warning",
        _ => "Icon.Info",
    }));

    /// <summary>The color of how much one of Claude's findings matters.</summary>
    public static IValueConverter SeverityBrush { get; } = new FuncValueConverter<ReviewSeverity, IBrush?>(severity => Find<IBrush>(severity switch
    {
        ReviewSeverity.Bug => "Brush.Error",
        ReviewSeverity.Warning => "Brush.Warning",
        _ => "Brush.Info",
    }));

    private static T? Find<T>(string key)
        where T : class =>
        Application.Current?.TryGetResource(key, Application.Current.ActualThemeVariant, out var value) == true ? value as T : null;
}
