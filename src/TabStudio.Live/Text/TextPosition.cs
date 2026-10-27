using System.Text.Json.Serialization;

namespace TabStudio.Live.Text;

/// <summary>
/// A place in a <see cref="SharedText"/> that stays put while others edit: just after a character, named by identity,
/// or the start of the text. Carets and selections travel as positions, so a participant's caret on another device
/// stays where they typed while everyone types around it.
/// </summary>
/// <param name="After">The character the place is just after; <see langword="null"/> for the start of the text.</param>
public readonly record struct TextPosition([property: JsonPropertyName("a")] CharId? After)
{
    /// <summary>The start of the text.</summary>
    public static TextPosition Start => default;
}
