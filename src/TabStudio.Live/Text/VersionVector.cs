namespace TabStudio.Live.Text;

/// <summary>
/// Which operations of a <see cref="SharedText"/> a device has: for every participant, how many of the participant's
/// operations it applied (<see cref="TextOperation.Sequence"/>). A device applies a participant's operations in the
/// order the participant made them, so the first that many are applied and the others are not.
/// </summary>
public sealed class VersionVector
{
    private readonly Dictionary<int, long> _sequences;

    /// <summary>Creates an empty version: no operation applied.</summary>
    public VersionVector()
    {
        _sequences = [];
    }

    /// <summary>Creates a version from the numbers a message carries.</summary>
    /// <param name="sequences">How many operations of each participant are applied.</param>
    public VersionVector(IReadOnlyDictionary<int, long> sequences)
    {
        ArgumentNullException.ThrowIfNull(sequences);
        _sequences = new Dictionary<int, long>(sequences);
    }

    /// <summary>How many of a participant's operations are applied; 0 when none is.</summary>
    /// <param name="replica">The participant.</param>
    public long this[int replica] => _sequences.GetValueOrDefault(replica);

    /// <summary>The participants with an applied operation.</summary>
    public IReadOnlyCollection<int> Replicas => _sequences.Keys;

    /// <summary>Whether an operation is applied.</summary>
    /// <param name="operation">The operation.</param>
    public bool Contains(TextOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return this[operation.Replica] >= operation.Sequence;
    }

    /// <summary>Records that a participant's operations up to a number are applied.</summary>
    /// <param name="replica">The participant.</param>
    /// <param name="sequence">The number of the last applied operation.</param>
    public void Advance(int replica, long sequence)
    {
        if (sequence > this[replica])
        {
            _sequences[replica] = sequence;
        }
    }

    /// <summary>The numbers, for a message.</summary>
    public Dictionary<int, long> ToDictionary() => new(_sequences);

    /// <summary>A copy of this version.</summary>
    public VersionVector Clone() => new(_sequences);
}
