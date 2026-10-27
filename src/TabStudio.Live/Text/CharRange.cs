using System.Text.Json.Serialization;

namespace TabStudio.Live.Text;

/// <summary>
/// A run of characters one participant typed one after the other: identities with the same participant and
/// consecutive clocks, as a deletion names them.
/// </summary>
/// <param name="Replica">The participant that typed them.</param>
/// <param name="Clock">The clock of the first one.</param>
/// <param name="Length">How many there are.</param>
public readonly record struct CharRange(
    [property: JsonPropertyName("r")] int Replica,
    [property: JsonPropertyName("c")] long Clock,
    [property: JsonPropertyName("n")] int Length)
{
    /// <summary>The clock of the last character.</summary>
    [JsonIgnore]
    public long LastClock => Clock + Length - 1;

    /// <summary>The identity of the first character.</summary>
    [JsonIgnore]
    public CharId First => new(Replica, Clock);
}
