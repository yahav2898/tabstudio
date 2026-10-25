using Avalonia.Input;

namespace TabStudio.App.Swarm;

/// <summary>
/// Keys as inputs name them: Avalonia's names, with one name for each key. Some keys have two names in Avalonia,
/// such as Enter and Return, and the familiar one is used.
/// </summary>
public static class KeyNames
{
    /// <summary>A key's name, such as <c>Enter</c>.</summary>
    /// <param name="key">The key.</param>
    public static string Of(Key key) => key switch
    {
        Key.Enter => nameof(Key.Enter),
        Key.PageUp => nameof(Key.PageUp),
        Key.PageDown => nameof(Key.PageDown),
        Key.CapsLock => nameof(Key.CapsLock),
        Key.PrintScreen => nameof(Key.PrintScreen),
        _ => key.ToString(),
    };
}
