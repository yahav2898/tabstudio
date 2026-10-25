using Avalonia;
using Avalonia.Input;

namespace TabStudio.App.Platform;

/// <summary>
/// An embedded browser that TabStudio can script, touch and photograph, which makes the web preview one of Swarm's
/// screens: its elements named, what a person does on it captured, taps and keys replayed through the browser's own
/// input, and its layout checked. Android's system WebView is one. Every member is called on the UI thread.
/// </summary>
public interface IScriptableWebView : IWebView
{
    /// <summary>Runs script in the page shown.</summary>
    /// <param name="script">The script; its last expression is its result.</param>
    /// <param name="cancellationToken">Stops waiting for the result.</param>
    /// <returns>
    /// The result as JSON, such as <c>{"ready":true}</c>, or <c>null</c> as JSON for no result; <see langword="null"/>
    /// when there is no page to run it in.
    /// </returns>
    Task<string?> EvaluateAsync(string script, CancellationToken cancellationToken);

    /// <summary>Draws what the browser shows into a picture.</summary>
    /// <param name="maxPixels">The longest side the picture may have; a larger browser is scaled down.</param>
    /// <param name="cancellationToken">Stops the capture.</param>
    /// <returns>The picture as PNG, or <see langword="null"/> when the browser is not on screen.</returns>
    Task<byte[]?> CaptureAsync(int maxPixels, CancellationToken cancellationToken);

    /// <summary>Touches the page with a finger, through the platform's touch input, and lifts it.</summary>
    /// <param name="pixel">Where, in the browser's pixels from its top left corner.</param>
    /// <param name="hold">How long the finger stays down: a long hold is a long press.</param>
    /// <param name="cancellationToken">Lifts the finger early.</param>
    /// <returns>Whether the touch reached the browser.</returns>
    Task<bool> TouchAsync(Point pixel, TimeSpan hold, CancellationToken cancellationToken);

    /// <summary>Presses and releases a key on the page's focused element, through the platform's key input.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifiers held with it.</param>
    /// <param name="cancellationToken">Stops the key press.</param>
    /// <returns>Whether the platform has the key and the browser took it.</returns>
    Task<bool> PressKeyAsync(Key key, KeyModifiers modifiers, CancellationToken cancellationToken);
}
