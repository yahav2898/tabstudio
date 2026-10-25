using TabStudio.Debugging.Symbols;

namespace TabStudio.Debugging;

/// <summary>
/// A breakpoint on a line, and the statement it is bound to.
/// </summary>
/// <remarks>
/// A breakpoint binds to the first statement that starts on its line or, when none does, to the next statement in
/// the file, as a breakpoint on a blank line or a brace would in Visual Studio. When no statement follows, it is
/// unbound and never hits. Instances are immutable.
/// </remarks>
public sealed class Breakpoint
{
    private readonly DebugStatement? _statement;

    internal Breakpoint(string filePath, int line, DebugStatement? statement)
    {
        FilePath = filePath;
        Line = line;
        _statement = statement;
    }

    /// <summary>The file the user set it in.</summary>
    public string FilePath { get; }

    /// <summary>The one-based line the user set it on.</summary>
    public int Line { get; }

    /// <summary>Whether it is bound to a statement and can hit.</summary>
    public bool IsBound => _statement is not null;

    /// <summary>The statement it is bound to, which may start on a later line than <see cref="Line"/>.</summary>
    public DebugSourceSpan? Location => _statement?.Location;

    /// <summary>The id of the bound statement, or 0.</summary>
    internal int StatementId => _statement?.Id ?? 0;
}
