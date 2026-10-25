namespace TabStudio.Debugging.Symbols;

/// <summary>
/// Numbers statements and methods while a debug build instruments its projects. One builder is shared by every
/// project of a build, so ids are unique across assemblies.
/// </summary>
/// <remarks>Not thread-safe: a build instruments one syntax tree at a time.</remarks>
internal sealed class DebugInfoBuilder
{
    private readonly List<DebugStatement> _statements = [];
    private readonly List<DebugMethod> _methods = [];

    /// <summary>Registers a method and returns its id.</summary>
    public int AddMethod(string name, DebugSourceSpan location, IReadOnlyList<DebugLocalSlot> parameters)
    {
        var id = _methods.Count + 1;
        _methods.Add(new DebugMethod(id, name, location, parameters));
        return id;
    }

    /// <summary>Registers a statement and returns its id.</summary>
    public int AddStatement(int methodId, DebugSourceSpan location, IReadOnlyList<DebugLocalSlot> locals)
    {
        var id = _statements.Count + 1;
        _statements.Add(new DebugStatement(id, methodId, location, locals));
        return id;
    }

    /// <summary>Creates the immutable map of everything registered so far.</summary>
    public DebugInfoMap Build() => new(_statements, _methods);
}
