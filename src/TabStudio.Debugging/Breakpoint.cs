using TabStudio.Debugging.Symbols;

namespace TabStudio.Debugging;

/// <summary>
/// A breakpoint on a line, the statement it is bound to, and an optional condition.
/// </summary>
/// <remarks>
/// <para>
/// A breakpoint binds to the first statement that starts on its line or, when none does, to the next statement in
/// the file, as a breakpoint on a blank line or a brace would in Visual Studio. When no statement follows, it is
/// unbound and never hits.
/// </para>
/// <para>
/// A condition such as <c>count == 3</c> or <c>name != null &amp;&amp; total &gt; 10</c> compares variables in scope,
/// their members (<c>order.Total</c>) and items (<c>items[0]</c>) with literals or with each other, joined by
/// <c>&amp;&amp;</c> and <c>||</c>. It is not a full C# expression: there are no method calls or arithmetic. A condition
/// that cannot be evaluated stops the program and says why in <see cref="DebugPausedEventArgs.ConditionError"/>.
/// </para>
/// <para>
/// In Visual Basic code the condition is written as Visual Basic writes it, such as <c>count = 3</c> or
/// <c>name IsNot Nothing AndAlso total &gt; 10</c>, and names are not case-sensitive.
/// </para>
/// <para>Everything but <see cref="HitCount"/> is immutable; instances are thread-safe.</para>
/// </remarks>
public sealed class Breakpoint
{
    private readonly DebugStatement? _statement;
    private int _hitCount;

    internal Breakpoint(string filePath, int line, DebugStatement? statement, string? condition)
    {
        FilePath = filePath;
        Line = line;
        _statement = statement;
        Condition = string.IsNullOrWhiteSpace(condition) ? null : condition.Trim();
    }

    /// <summary>The file the user set it in.</summary>
    public string FilePath { get; }

    /// <summary>The one-based line the user set it on.</summary>
    public int Line { get; }

    /// <summary>The condition it stops on, or <see langword="null"/> to stop every time.</summary>
    public string? Condition { get; }

    /// <summary>How many times it stopped the program.</summary>
    public int HitCount => Volatile.Read(ref _hitCount);

    /// <summary>Whether it is bound to a statement and can hit.</summary>
    public bool IsBound => _statement is not null;

    /// <summary>The statement it is bound to, which may start on a later line than <see cref="Line"/>.</summary>
    public DebugSourceSpan? Location => _statement?.Location;

    /// <summary>The id of the bound statement, or 0.</summary>
    internal int StatementId => _statement?.Id ?? 0;

    /// <summary>Counts a hit.</summary>
    internal void RecordHit() => Interlocked.Increment(ref _hitCount);
}
