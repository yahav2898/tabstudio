using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Platform;
using TabStudio.App.Swarm.LayoutChecks;

namespace TabStudio.App.Swarm.Web;

/// <summary>
/// The script that makes a web page one of Swarm's screens (<c>Assets/Swarm/web-screen.js</c>), and the calls the web
/// preview's screen makes into it. Set up once per page, it names the page's elements by path (<c>tag#id</c> where
/// the id is unique, otherwise <c>tag[index]</c> among siblings of the same tag, from <c>body</c>), queues what a
/// person does on the page by element, tells where to touch an element, types and scrolls, and checks the page's
/// layout with the five rules of the Avalonia checks, worded the same way.
/// </summary>
/// <remarks>
/// A replayed input is announced to the page first, so the page does not capture it again as a person's. Text a
/// person types arrives as <c>input</c> and composition events, since soft keyboards send few key events; keys
/// that are commands, such as Enter or Ctrl+A, arrive as key events.
/// </remarks>
internal static class WebScreenScript
{
    private static readonly Uri InstallAsset = new("avares://TabStudio.App/Assets/Swarm/web-screen.js");
    private static readonly Lazy<string> InstallScript = new(() =>
    {
        using var reader = new StreamReader(AssetLoader.Open(InstallAsset));
        return reader.ReadToEnd();
    });

    /// <summary>The page's size, theme and readiness, with what a person did since the last read; <c>null</c> before <see cref="Install"/>.</summary>
    public const string State = "window.__tabStudioScreen ? window.__tabStudioScreen.state() : null";

    /// <summary>Whether the last touch aimed by <see cref="PointAt"/> reached its element; <c>null</c> before the page saw it.</summary>
    public const string Landed = "window.__tabStudioScreen ? window.__tabStudioScreen.landed() : null";

    /// <summary>Sets the page up as a screen; running it again on the same page does nothing.</summary>
    public static string Install => InstallScript.Value;

    /// <summary>Where to touch an element, in the browser's pixels, scrolling it into view first if it is out of view.</summary>
    /// <param name="path">The element's path.</param>
    /// <param name="position">Where inside it, as fractions of its size; (0, 0) is its center.</param>
    public static string PointAt(string path, Point position) => Call("point", Text(path), Number(position.X), Number(position.Y));

    /// <summary>Types text into the focused element, as a keyboard does.</summary>
    /// <param name="text">The text.</param>
    public static string Type(string text) => Call("type", Text(text));

    /// <summary>The text the element at a path shows: a field's value, or its text in one line; <c>null</c> when there is none.</summary>
    /// <param name="path">The element's path.</param>
    public static string TextOf(string path) => Call("text", Text(path));

    /// <summary>Scrolls the element at a path, or the scrollable element around it; <c>body</c> scrolls the page.</summary>
    /// <param name="path">The element's path.</param>
    /// <param name="delta">How far, in the page's pixels.</param>
    public static string Scroll(string path, Vector delta) => Call("scroll", Text(path), Number(delta.X), Number(delta.Y));

    /// <summary>Tells the page that an input is being replayed, so it is not captured as a person's.</summary>
    /// <param name="kind">The input's kind.</param>
    /// <param name="value">The key, for a key; <see langword="null"/> for a touch.</param>
    public static string Expect(AppInputKind kind, string? value) => Call("expect", Text(kind.ToString()), value is null ? "null" : Text(value));

    /// <summary>Checks the page's layout, as the Avalonia checks check a view.</summary>
    /// <param name="profile">The screen the page is on.</param>
    public static string Inspect(ScreenProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var minimum = profile.FormFactor == FormFactor.Desktop ? TouchTargetRule.DesktopMinimum : TouchTargetRule.TouchMinimum;
        var options = $"{{ screen: {Text(LayoutText.Screen(profile))}, touch: {Number(minimum)}, formFactor: {Text(profile.FormFactor.ToString())} }}";
        return Call("inspect", options);
    }

    private static string Call(string function, params string[] arguments) =>
        $"window.__tabStudioScreen ? window.__tabStudioScreen.{function}({string.Join(", ", arguments)}) : null";

    /// <summary>A string as a script literal.</summary>
    private static string Text(string value) => $"\"{JsonEncodedText.Encode(value).Value}\"";

    private static string Number(double value) => double.IsFinite(value) ? value.ToString("R", CultureInfo.InvariantCulture) : "0";
}
