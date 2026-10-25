using TabStudio.LiveTesting.Coverage;

namespace TabStudio.LiveTesting.Instrumentation;

/// <summary>
/// Numbers members, statements and coverage points while one project is instrumented, then creates its
/// <see cref="CoverageModule"/>.
/// </summary>
/// <remarks>Not thread-safe: a project is instrumented one syntax tree at a time.</remarks>
internal sealed class CoverageModuleBuilder
{
    private static readonly Lock IndexGate = new();
    private static readonly Dictionary<string, int> ModuleIndexes = new(StringComparer.Ordinal);

    private readonly List<CoverageMember> _members = [];
    private readonly List<CoverageStatement> _statements = [];
    private readonly List<int> _pointMembers = [];
    private readonly List<int> _pointOrdinals = [];
    private readonly List<int> _statementCounts = [];

    /// <summary>Creates a builder for one project.</summary>
    /// <param name="projectFilePath">The project being instrumented.</param>
    /// <param name="index">Its slot in the hit table of the process that runs the tests, usually <see cref="IndexOf"/>.</param>
    public CoverageModuleBuilder(string projectFilePath, int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ProjectFilePath = projectFilePath;
        Index = index;
    }

    /// <summary>The project being instrumented.</summary>
    public string ProjectFilePath { get; }

    /// <summary>The project's slot in a test's hit table.</summary>
    public int Index { get; }

    /// <summary>
    /// The slot of a project in the hit table: the same for every build of the project for as long as TabStudio
    /// runs, so every assembly ever built for it writes to its own slot.
    /// </summary>
    /// <param name="projectFilePath">The project.</param>
    public static int IndexOf(string projectFilePath)
    {
        lock (IndexGate)
        {
            if (!ModuleIndexes.TryGetValue(projectFilePath, out var index))
            {
                index = ModuleIndexes.Count;
                ModuleIndexes[projectFilePath] = index;
            }

            return index;
        }
    }

    /// <summary>Registers a member and returns its index.</summary>
    public int AddMember(string filePath, string typeName, string name, string key, int line)
    {
        var index = _members.Count;
        _members.Add(new CoverageMember(index, filePath, typeName, name, key, line));
        _statementCounts.Add(0);
        return index;
    }

    /// <summary>Registers an entry point of a member, hit when one of its bodies starts, and returns the point.</summary>
    /// <param name="member">The member's index.</param>
    public int AddEntry(int member)
    {
        _pointMembers.Add(member);
        _pointOrdinals.Add(-1);
        return _pointMembers.Count - 1;
    }

    /// <summary>Registers a statement of a member and returns its point.</summary>
    /// <param name="member">The member's index.</param>
    /// <param name="line">One-based line where the statement starts.</param>
    public int AddStatement(int member, int line)
    {
        var point = _pointMembers.Count;
        var ordinal = _statementCounts[member]++;
        _pointMembers.Add(member);
        _pointOrdinals.Add(ordinal);
        _statements.Add(new CoverageStatement(point, member, ordinal, line));
        return point;
    }

    /// <summary>Creates the module of everything registered.</summary>
    public CoverageModule Build() =>
        new(ProjectFilePath, Index, _members, _statements, [.. _pointMembers], [.. _pointOrdinals]);
}
