using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace TabStudio.App.Swarm.Web;

/// <summary>What the page's screen script says about the page: its size, theme and readiness, and what a person did on it.</summary>
/// <param name="Ready">Whether the page has loaded.</param>
/// <param name="Url">The page's address.</param>
/// <param name="Width">The screen's width, in the page's pixels, which are device-independent.</param>
/// <param name="Height">The screen's height, in the page's pixels.</param>
/// <param name="Scale">The browser's pixels per page pixel.</param>
/// <param name="Dark">Whether the page's background is dark.</param>
/// <param name="Rtl">Whether the page lays text out from the right.</param>
/// <param name="Inputs">What a person did since the last read, oldest first.</param>
internal sealed record WebScreenState(
    bool Ready,
    string? Url,
    double Width,
    double Height,
    double Scale,
    bool Dark,
    bool Rtl,
    IReadOnlyList<WebScreenInput>? Inputs);

/// <summary>One thing a person did on the page, by element, as the page's script reports it.</summary>
/// <param name="Kind">Tap, LongPress, Scroll, Text or Key.</param>
/// <param name="Path">The element's path, empty for text and keys.</param>
/// <param name="X">Where inside the element across, from 0 to 1.</param>
/// <param name="Y">Where inside the element down, from 0 to 1.</param>
/// <param name="Text">The text typed.</param>
/// <param name="Key">The key, as Avalonia names it.</param>
/// <param name="Modifiers">The modifiers, as Avalonia names them.</param>
/// <param name="Dx">How far it scrolled across.</param>
/// <param name="Dy">How far it scrolled down.</param>
internal sealed record WebScreenInput(
    string Kind,
    string? Path,
    double X,
    double Y,
    string? Text,
    string? Key,
    string? Modifiers,
    double Dx,
    double Dy);

/// <summary>Where to touch an element, in the browser's pixels.</summary>
/// <param name="X">From the browser's left edge.</param>
/// <param name="Y">From the browser's top edge.</param>
internal sealed record WebScreenPoint(double X, double Y);

/// <summary>An element's box in the page's viewport, in the page's pixels.</summary>
/// <param name="X">The left edge.</param>
/// <param name="Y">The top edge.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
internal sealed record WebScreenBox(double X, double Y, double Width, double Height);

/// <summary>A layout problem the page's script found.</summary>
/// <param name="Kind">The kind, as <see cref="LayoutIssueKind"/> names it.</param>
/// <param name="Severity">The severity, as <see cref="LayoutIssueSeverity"/> names it.</param>
/// <param name="Path">The element's path.</param>
/// <param name="Message">What is wrong, in a sentence.</param>
/// <param name="Bounds">Where the element is.</param>
internal sealed record WebScreenIssue(string Kind, string Severity, string Path, string Message, WebScreenBox? Bounds);

/// <summary>Reads the screen script's answers, which come back from the browser as JSON.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(WebScreenState))]
[JsonSerializable(typeof(WebScreenPoint))]
[JsonSerializable(typeof(List<WebScreenIssue>))]
internal sealed partial class WebScreenJson : JsonSerializerContext
{
    /// <summary>An answer, or <see langword="null"/> when there is none or it is not what was asked for.</summary>
    /// <typeparam name="T">What the answer is.</typeparam>
    /// <param name="json">The answer.</param>
    /// <param name="type">How to read it.</param>
    public static T? Read<T>(string? json, JsonTypeInfo<T> type)
        where T : class
    {
        if (string.IsNullOrEmpty(json) || json == "null")
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(json, type);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
