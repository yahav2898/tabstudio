namespace TabStudio.Debugging.Symbols;

/// <summary>
/// What the instrumenter recorded about a debug build: every statement and method it numbered, where they are in
/// the original source and which variables each statement reports.
/// </summary>
/// <remarks>
/// The map is the debugger's equivalent of a PDB. It is immutable and safe to share between threads.
/// </remarks>
public sealed class DebugInfoMap
{
    private readonly DebugStatement[] _statements;
    private readonly DebugMethod[] _methods;
    private readonly Dictionary<string, DebugStatement[]> _statementsByFile;

    /// <summary>An empty map, for builds that failed before instrumentation.</summary>
    public static DebugInfoMap Empty { get; } = new([], []);

    internal DebugInfoMap(IReadOnlyList<DebugStatement> statements, IReadOnlyList<DebugMethod> methods)
    {
        _statements = [.. statements];
        _methods = [.. methods];
        _statementsByFile = _statements
            .GroupBy(s => s.Location.FilePath, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(s => s.Location.Line).ThenBy(s => s.Location.Column).ToArray(),
                StringComparer.Ordinal);
    }

    /// <summary>
    /// Creates a map from statements and methods numbered elsewhere, such as by the TabStudio Link host that built the
    /// program.
    /// </summary>
    /// <param name="statements">Every statement, in id order, with ids starting at 1.</param>
    /// <param name="methods">Every method, in id order, with ids starting at 1.</param>
    /// <exception cref="ArgumentException">The ids are not 1, 2, 3 and so on.</exception>
    public static DebugInfoMap Create(IReadOnlyList<DebugStatement> statements, IReadOnlyList<DebugMethod> methods)
    {
        ArgumentNullException.ThrowIfNull(statements);
        ArgumentNullException.ThrowIfNull(methods);
        for (var i = 0; i < statements.Count; i++)
        {
            if (statements[i].Id != i + 1)
            {
                throw new ArgumentException("Statement ids must count up from 1.", nameof(statements));
            }
        }

        for (var i = 0; i < methods.Count; i++)
        {
            if (methods[i].Id != i + 1)
            {
                throw new ArgumentException("Method ids must count up from 1.", nameof(methods));
            }
        }

        return new DebugInfoMap(statements, methods);
    }

    /// <summary>Every statement, in id order.</summary>
    public IReadOnlyList<DebugStatement> Statements => _statements;

    /// <summary>Every method, in id order.</summary>
    public IReadOnlyList<DebugMethod> Methods => _methods;

    /// <summary>The highest statement id, which sizes per-statement tables. Ids start at 1.</summary>
    internal int MaxStatementId => _statements.Length;

    /// <summary>Returns a statement by id.</summary>
    /// <param name="id">The statement id.</param>
    /// <returns>The statement, or <see langword="null"/> for an unknown id.</returns>
    public DebugStatement? GetStatement(int id) => id >= 1 && id <= _statements.Length ? _statements[id - 1] : null;

    /// <summary>Returns a method by id.</summary>
    /// <param name="id">The method id.</param>
    /// <returns>The method, or <see langword="null"/> for an unknown id.</returns>
    public DebugMethod? GetMethod(int id) => id >= 1 && id <= _methods.Length ? _methods[id - 1] : null;

    /// <summary>Returns the statements of one source file, in source order.</summary>
    /// <param name="filePath">Full path of the file.</param>
    public IReadOnlyList<DebugStatement> GetStatements(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        return _statementsByFile.TryGetValue(filePath, out var statements) ? statements : [];
    }

    /// <summary>
    /// Finds where a breakpoint on a line binds: the first statement that starts on that line or, when none does,
    /// the first statement after it in the same file.
    /// </summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="line">One-based line number.</param>
    /// <returns>The statement, or <see langword="null"/> when no statement starts on or after the line.</returns>
    public DebugStatement? FindStatement(string filePath, int line)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        if (!_statementsByFile.TryGetValue(filePath, out var statements))
        {
            return null;
        }

        foreach (var statement in statements)
        {
            if (statement.Location.Line >= line)
            {
                return statement;
            }
        }

        return null;
    }
}
