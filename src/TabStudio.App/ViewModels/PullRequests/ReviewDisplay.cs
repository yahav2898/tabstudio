using System.Globalization;
using Avalonia.Media;
using TabStudio.App.Theming;

namespace TabStudio.App.ViewModels.PullRequests;

/// <summary>How pull requests and reviews read in the IDE: ages, people's initials and colors, and counts.</summary>
public static class ReviewDisplay
{
    /// <summary>How many avatar colors the palette has (<c>Brush.Avatar.1</c> to <c>Brush.Avatar.8</c>), all dark enough for white letters.</summary>
    private const int AvatarColors = 8;

    /// <summary>How long ago something happened, such as "3 hours ago", or the date when it was over a month ago.</summary>
    /// <param name="when">When it happened.</param>
    /// <param name="now">The current time.</param>
    public static string Age(DateTimeOffset when, DateTimeOffset now)
    {
        var age = now - when;
        return age.TotalMinutes switch
        {
            < 1 => "just now",
            < 2 => "1 minute ago",
            < 60 => string.Create(CultureInfo.CurrentCulture, $"{(int)age.TotalMinutes} minutes ago"),
            < 120 => "1 hour ago",
            < 24 * 60 => string.Create(CultureInfo.CurrentCulture, $"{(int)age.TotalHours} hours ago"),
            < 48 * 60 => "yesterday",
            < 30 * 24 * 60 => string.Create(CultureInfo.CurrentCulture, $"{(int)age.TotalDays} days ago"),
            _ => "on " + when.ToLocalTime().ToString("d MMM yyyy", CultureInfo.CurrentCulture),
        };
    }

    /// <summary>The letter on a person's avatar.</summary>
    /// <param name="login">Their login.</param>
    public static string Initial(string login) =>
        string.IsNullOrEmpty(login) ? "?" : char.ToUpperInvariant(login[0]).ToString(CultureInfo.InvariantCulture);

    /// <summary>A person's avatar color, the same every time for the same login.</summary>
    /// <param name="login">Their login.</param>
    public static IBrush AvatarBrush(string login)
    {
        ArgumentNullException.ThrowIfNull(login);
        var hash = 0;
        foreach (var c in login)
        {
            hash = unchecked((hash * 31) + c);
        }

        return ThemeBrushes.Get(string.Create(CultureInfo.InvariantCulture, $"Brush.Avatar.{((hash & int.MaxValue) % AvatarColors) + 1}"));
    }

    /// <summary>"1 comment", "3 comments".</summary>
    /// <param name="count">How many.</param>
    /// <param name="singular">The word for one.</param>
    /// <param name="plural">The word for several; the singular with an s by default.</param>
    public static string Count(int count, string singular, string? plural = null) =>
        count == 1 ? $"1 {singular}" : string.Create(CultureInfo.CurrentCulture, $"{count} {plural ?? singular + "s"}");

    /// <summary>"line 12" or "lines 10–12".</summary>
    /// <param name="line">The line, or the last line of a range.</param>
    /// <param name="startLine">The first line of a range, if it is one.</param>
    public static string Lines(int line, int? startLine) =>
        startLine is { } start && start != line
            ? string.Create(CultureInfo.CurrentCulture, $"lines {start}–{line}")
            : string.Create(CultureInfo.CurrentCulture, $"line {line}");
}
