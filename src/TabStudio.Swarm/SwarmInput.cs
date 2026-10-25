namespace TabStudio.Swarm;

/// <summary>What a person did on a device's screen.</summary>
public enum SwarmInputKind
{
    /// <summary>A tap or click on an element.</summary>
    Tap,

    /// <summary>A long press, or a right click.</summary>
    LongPress,

    /// <summary>A scroll of the element under the pointer.</summary>
    Scroll,

    /// <summary>Text typed into the focused element.</summary>
    Text,

    /// <summary>A key with modifiers on the focused element.</summary>
    Key,
}

/// <summary>
/// One input on a device's screen as it travels between devices: aimed at an element by its path, so it lands on the
/// same control on a screen of another size. The app turns it into its own input type and back.
/// </summary>
/// <param name="Kind">What happened.</param>
/// <param name="ElementPath">The element, from the app's root, such as <c>MainView/StackPanel[0]/Button#CountButton</c>; empty for keys and text.</param>
/// <param name="X">Where inside the element, as a fraction of its width.</param>
/// <param name="Y">Where inside the element, as a fraction of its height.</param>
/// <param name="Text">The text typed, for <see cref="SwarmInputKind.Text"/>.</param>
/// <param name="Key">The key, as Avalonia names it, for <see cref="SwarmInputKind.Key"/>.</param>
/// <param name="Modifiers">The modifiers held, as Avalonia names them.</param>
/// <param name="ScrollX">How far it scrolled sideways, in device-independent pixels.</param>
/// <param name="ScrollY">How far it scrolled down, in device-independent pixels.</param>
public sealed record SwarmInput(
    SwarmInputKind Kind,
    string ElementPath,
    double X = 0,
    double Y = 0,
    string? Text = null,
    string? Key = null,
    string? Modifiers = null,
    double ScrollX = 0,
    double ScrollY = 0);
