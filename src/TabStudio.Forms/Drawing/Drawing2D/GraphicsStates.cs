namespace System.Drawing.Drawing2D;

/// <summary>The state of a <see cref="Graphics"/> saved by <see cref="Graphics.Save"/>.</summary>
public sealed class GraphicsState : MarshalByRefObject
{
    internal GraphicsState(object state) => State = state;

    internal object State { get; }
}

/// <summary>A container of a <see cref="Graphics"/> opened by <see cref="Graphics.BeginContainer()"/>.</summary>
public sealed class GraphicsContainer : MarshalByRefObject
{
    internal GraphicsContainer(object state) => State = state;

    internal object State { get; }
}

/// <summary>How clipping regions combine.</summary>
public enum CombineMode
{
    /// <summary>One region replaces the other.</summary>
    Replace = 0,

    /// <summary>The intersection of the two regions.</summary>
    Intersect = 1,

    /// <summary>The union of the two regions.</summary>
    Union = 2,

    /// <summary>The parts in exactly one of the regions.</summary>
    Xor = 3,

    /// <summary>The first region without the second.</summary>
    Exclude = 4,

    /// <summary>The second region without the first.</summary>
    Complement = 5,
}

/// <summary>Whether drawing waits for pending operations to finish.</summary>
public enum FlushIntention
{
    /// <summary>Pending operations are flushed and the call returns at once.</summary>
    Flush = 0,

    /// <summary>The call waits for pending operations to finish.</summary>
    Sync = 1,
}
