using System.Text.Json.Serialization;

namespace TabStudio.Live.Text;

/// <summary>
/// One edit of a <see cref="SharedText"/> as it travels between devices: an insertion of a run of characters after a
/// known character, or a deletion of known characters. Operations name characters by identity, never by offset, so
/// they mean the same thing on every device whatever else was edited meanwhile.
/// </summary>
/// <remarks>
/// An insertion's characters get the identities <c>(Replica, Clock)</c>, <c>(Replica, Clock + 1)</c> and so on, and
/// each but the first follows the one before it. A deletion takes one clock of its own. A participant also numbers its
/// operations 1, 2, 3 (<see cref="Sequence"/>), so a <see cref="VersionVector"/> says exactly which a device has, and
/// one that arrives twice or early is recognized.
/// </remarks>
/// <param name="Replica">The participant that made the edit.</param>
/// <param name="Sequence">The participant's number for the operation: 1 for its first, then one more each time.</param>
/// <param name="Clock">The clock of the edit: of its first character for an insertion.</param>
/// <param name="Origin">For an insertion, the character it was typed after; <see langword="null"/> at the start of the text.</param>
/// <param name="Text">For an insertion, the characters; <see langword="null"/> for a deletion.</param>
/// <param name="Deleted">For a deletion, the characters it removes; <see langword="null"/> for an insertion.</param>
public sealed record TextOperation(
    [property: JsonPropertyName("r")] int Replica,
    [property: JsonPropertyName("s")] long Sequence,
    [property: JsonPropertyName("c")] long Clock,
    [property: JsonPropertyName("o")] CharId? Origin,
    [property: JsonPropertyName("t")] string? Text,
    [property: JsonPropertyName("d")] IReadOnlyList<CharRange>? Deleted)
{
    /// <summary>Whether this inserts characters rather than deleting them.</summary>
    [JsonIgnore]
    public bool IsInsert => Text is not null;

    /// <summary>The largest clock the operation uses: its last character's for an insertion, its own for a deletion.</summary>
    [JsonIgnore]
    public long LastClock => Text is { Length: > 0 } text ? Clock + text.Length - 1 : Clock;

    /// <summary>An insertion.</summary>
    /// <param name="replica">The participant.</param>
    /// <param name="sequence">The participant's number for the operation.</param>
    /// <param name="clock">The clock of the first character.</param>
    /// <param name="origin">The character it follows, or <see langword="null"/> at the start.</param>
    /// <param name="text">The characters; at least one.</param>
    public static TextOperation Insert(int replica, long sequence, long clock, CharId? origin, string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        return new TextOperation(replica, sequence, clock, origin, text, null);
    }

    /// <summary>A deletion.</summary>
    /// <param name="replica">The participant.</param>
    /// <param name="sequence">The participant's number for the operation.</param>
    /// <param name="clock">The clock of the deletion.</param>
    /// <param name="deleted">The characters it removes; at least one range.</param>
    public static TextOperation Delete(int replica, long sequence, long clock, IReadOnlyList<CharRange> deleted)
    {
        ArgumentNullException.ThrowIfNull(deleted);
        if (deleted.Count == 0)
        {
            throw new ArgumentException("A deletion removes at least one character.", nameof(deleted));
        }

        return new TextOperation(replica, sequence, clock, null, null, deleted);
    }

    /// <summary>Whether the operation is well formed: a positive participant, number and clock, and something to insert or delete.</summary>
    public bool IsValid() =>
        Replica > 0 && Sequence > 0 && Clock > 0 && (Text is { Length: > 0 }
            ? Deleted is null
            : Text is null && Deleted is { Count: > 0 } ranges && ranges.All(r => r.Replica > 0 && r.Clock > 0 && r.Length > 0));
}
