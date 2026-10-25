using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;

namespace TabStudio.App.Swarm.LayoutChecks;

/// <summary>How the layout checks name elements and sizes in their messages: plainly, as a designer would.</summary>
internal static class LayoutText
{
    private const int QuoteLength = 32;

    /// <summary>
    /// A short name for an element, such as <c>Button "Count"</c>, <c>TextBox Email</c> or <c>"Hello from Avalonia!"</c>
    /// for text.
    /// </summary>
    /// <param name="element">The element, or the text inside it.</param>
    public static string Label(Visual element)
    {
        var type = element.GetType().Name;
        return element switch
        {
            TextBlock { Text: { Length: > 0 } text } => Quote(text),
            TextPresenter { Text: { Length: > 0 } text } => Quote(text),
            ContentControl { Content: string { Length: > 0 } content } => $"{type} {Quote(content)}",
            StyledElement { Name: { Length: > 0 } name } => $"{type} {name}",
            _ => type,
        };
    }

    /// <summary>Text in quotes, shortened with an ellipsis when long, on one line.</summary>
    /// <param name="text">The text.</param>
    public static string Quote(string text)
    {
        var line = text.ReplaceLineEndings(" ").Trim();
        return line.Length <= QuoteLength ? $"\"{line}\"" : $"\"{line[..(QuoteLength - 1)].TrimEnd()}…\"";
    }

    /// <summary>A length in device-independent pixels, rounded, such as "212".</summary>
    /// <param name="value">The length.</param>
    public static string Dips(double value) => Math.Round(value).ToString(CultureInfo.InvariantCulture);

    /// <summary>A size, such as "64 × 32".</summary>
    /// <param name="size">The size.</param>
    public static string Dips(Size size) => $"{Dips(size.Width)} × {Dips(size.Height)}";

    /// <summary>The screen, in a few words, such as "a 360-wide phone".</summary>
    /// <param name="profile">The screen.</param>
    public static string Screen(ScreenProfile profile) => profile.FormFactor switch
    {
        FormFactor.Phone => $"a {Dips(profile.Width)}-wide phone",
        FormFactor.Phablet => $"a {Dips(profile.Width)}-wide large phone",
        FormFactor.Tablet => $"a {Dips(profile.Width)}-wide tablet",
        _ => $"a {Dips(profile.Width)}-wide window",
    };
}
