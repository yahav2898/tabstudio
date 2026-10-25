using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TabStudio.App.ViewModels.PullRequests;

/// <summary>How pull requests and reviews read in the IDE: ages, people's initials and colors, and counts.</summary>
public static class ReviewDisplay
{
    private static readonly IBrush[] AvatarBrushes =
    [
        new ImmutableSolidColorBrush(Color.Parse("#2E6F9E")),
        new ImmutableSolidColorBrush(Color.Parse("#7A4FA0")),
        new ImmutableSolidColorBrush(Color.Parse("#2F7D5B")),
        new ImmutableSolidColorBrush(Color.Parse("#A0563A")),
        new ImmutableSolidColorBrush(Color.Parse("#8A6D1F")),
        new ImmutableSolidColorBrush(Color.Parse("#9C3F5E")),
        new ImmutableSolidColorBrush(Color.Parse("#3F6A8A")),
        new ImmutableSolidColorBrush(Color.Parse("#566B2E")),
    ];

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

        return AvatarBrushes[(hash & int.MaxValue) % AvatarBrushes.Length];
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
