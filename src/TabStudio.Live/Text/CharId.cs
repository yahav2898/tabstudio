using System.Text.Json.Serialization;

namespace TabStudio.Live.Text;

/// <summary>
/// The identity of one character of a <see cref="SharedText"/>, the same on every device: the participant that typed
/// it and its Lamport clock when it was typed. No two characters ever share an identity, and the identities of
/// concurrent insertions at one place decide their order the same way everywhere.
/// </summary>
/// <param name="Replica">The participant that typed it, as the session numbers participants; never 0.</param>
/// <param name="Clock">Its Lamport clock: larger than the clock of every character its typist had seen.</param>
public readonly record struct CharId(
    [property: JsonPropertyName("r")] int Replica,
    [property: JsonPropertyName("c")] long Clock)
{
    /// <summary>
    /// Orders identities: by clock, then by participant. A character typed later than another one it saw is always
    /// larger, which is what makes concurrent insertions at one place merge the same way on every device.
    /// </summary>
    /// <param name="left">One identity.</param>
    /// <param name="right">The other.</param>
    /// <returns>Less than zero when <paramref name="left"/> is smaller, zero when they are equal, more otherwise.</returns>
    public static int Compare(CharId left, CharId right)
    {
        var byClock = left.Clock.CompareTo(right.Clock);
        return byClock != 0 ? byClock : left.Replica.CompareTo(right.Replica);
    }

    /// <summary>The identity of the character <paramref name="offset"/> places after this one in the same run.</summary>
    /// <param name="offset">How many characters later.</param>
    public CharId Plus(int offset) => new(Replica, Clock + offset);

    /// <inheritdoc />
    public override string ToString() => $"{Replica}:{Clock}";
}
