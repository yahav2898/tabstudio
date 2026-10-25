using System.Globalization;

namespace TabStudio.LiveTesting.Coverage;

/// <summary>
/// What the coverage instrumentation of one project recorded: its members, the statements of each, and the
/// coverage points their hooks report. It is to coverage what a PDB is to the debugger.
/// </summary>
/// <remarks>
/// Every function body has an entry point, hit when it starts, and every statement a point of its own. Points are
/// numbered from 0 within the module, so a test's hits for the project fit in one <see cref="bool"/> array. The
/// module is immutable and safe to share between threads.
/// </remarks>
public sealed class CoverageModule
{
    private readonly CoverageMember[] _members;
    private readonly CoverageStatement[] _statements;
    private readonly int[] _pointMembers;
    private readonly int[] _pointOrdinals;
    private readonly Dictionary<string, CoverageMember[]> _membersByFile;
    private readonly CoverageStatement[]?[] _statementsByMember;

    internal CoverageModule(string projectFilePath, int index, IReadOnlyList<CoverageMember> members, IReadOnlyList<CoverageStatement> statements, int[] pointMembers, int[] pointOrdinals)
    {
        ProjectFilePath = projectFilePath;
        Index = index;
        _members = [.. members];
        _statements = [.. statements];
        _pointMembers = pointMembers;
        _pointOrdinals = pointOrdinals;
        _membersByFile = _members
            .GroupBy(m => m.FilePath, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.OrderBy(m => m.Line).ToArray(), StringComparer.Ordinal);
        _statementsByMember = new CoverageStatement[_members.Length][];
        foreach (var group in _statements.GroupBy(s => s.Member))
        {
            _statementsByMember[group.Key] = [.. group.OrderBy(s => s.Ordinal)];
        }
    }

    /// <summary>
    /// Recreates a module from its parts, as another device that instrumented the project sent them, such as a
    /// TabStudio Link host.
    /// </summary>
    /// <param name="projectFilePath">The project that was instrumented.</param>
    /// <param name="index">Its slot in the hit table.</param>
    /// <param name="members">Every tracked member, by index.</param>
    /// <param name="statements">Every statement.</param>
    /// <param name="pointMembers">The member of every coverage point, by point, as <see cref="PointMembers"/> gives it.</param>
    /// <exception cref="ArgumentException">The parts do not fit together.</exception>
    public static CoverageModule Create(
        string projectFilePath,
        int index,
        IReadOnlyList<CoverageMember> members,
        IReadOnlyList<CoverageStatement> statements,
        IReadOnlyList<int> pointMembers)
    {
        ArgumentNullException.ThrowIfNull(projectFilePath);
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(statements);
        ArgumentNullException.ThrowIfNull(pointMembers);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        for (var i = 0; i < members.Count; i++)
        {
            if (members[i].Index != i)
            {
                throw new ArgumentException($"Member {i.ToString(CultureInfo.InvariantCulture)} has index {members[i].Index.ToString(CultureInfo.InvariantCulture)}.", nameof(members));
            }
        }

        var pointMemberArray = pointMembers.ToArray();
        if (pointMemberArray.Any(m => m < 0 || m >= members.Count))
        {
            throw new ArgumentException("A coverage point belongs to no member.", nameof(pointMembers));
        }

        var ordinals = new int[pointMemberArray.Length];
        Array.Fill(ordinals, -1);
        foreach (var statement in statements)
        {
            if (statement.Point < 0 || statement.Point >= ordinals.Length || ordinals[statement.Point] != -1
                || pointMemberArray[statement.Point] != statement.Member || statement.Ordinal < 0)
            {
                throw new ArgumentException($"The statement at point {statement.Point.ToString(CultureInfo.InvariantCulture)} does not fit the points.", nameof(statements));
            }

            ordinals[statement.Point] = statement.Ordinal;
        }

        return new CoverageModule(projectFilePath, index, members, statements, pointMemberArray, ordinals);
    }

    /// <summary>The project that was instrumented.</summary>
    public string ProjectFilePath { get; }

    /// <summary>
    /// The project's slot in the hit table of a test. It is the same for every build of the project in this
    /// process, so assemblies of earlier builds that are still loaded never write into another project's slot.
    /// </summary>
    public int Index { get; }

    /// <summary>Every tracked member, by <see cref="CoverageMember.Index"/>.</summary>
    public IReadOnlyList<CoverageMember> Members => _members;

    /// <summary>Every statement.</summary>
    public IReadOnlyList<CoverageStatement> Statements => _statements;

    /// <summary>The number of coverage points: member entries and statements.</summary>
    public int PointCount => _pointMembers.Length;

    /// <summary>The member of every coverage point, by point: with <see cref="Members"/> and <see cref="Statements"/>, what <see cref="Create"/> needs.</summary>
    public IReadOnlyList<int> PointMembers => Array.AsReadOnly(_pointMembers);

    /// <summary>The source files of the members, in no particular order.</summary>
    public IEnumerable<string> Files => _membersByFile.Keys;

    /// <summary>The members of one file, in source order.</summary>
    /// <param name="filePath">Full path of the file.</param>
    public IReadOnlyList<CoverageMember> MembersIn(string filePath) =>
        _membersByFile.TryGetValue(filePath, out var members) ? members : [];

    /// <summary>The statements of one member, by ordinal.</summary>
    /// <param name="member">The member's <see cref="CoverageMember.Index"/>.</param>
    public IReadOnlyList<CoverageStatement> StatementsOf(int member) =>
        member >= 0 && member < _statementsByMember.Length && _statementsByMember[member] is { } statements ? statements : [];

    /// <summary>The member a point belongs to.</summary>
    /// <param name="point">A point of this module.</param>
    internal int MemberOf(int point) => _pointMembers[point];

    /// <summary>The statement ordinal of a point, or -1 for a member's entry point.</summary>
    /// <param name="point">A point of this module.</param>
    internal int OrdinalOf(int point) => _pointOrdinals[point];
}
