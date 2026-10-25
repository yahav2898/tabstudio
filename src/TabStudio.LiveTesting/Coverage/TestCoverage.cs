namespace TabStudio.LiveTesting.Coverage;

/// <summary>
/// The code one test ran: the members it touched and, for each, the statements it reached, by ordinal.
/// </summary>
/// <remarks>
/// Coverage is kept per member rather than per line, so it stays right when lines move: a member whose text did
/// not change has the same statements in the same order in every build, wherever it is in its file.
/// </remarks>
public sealed class TestCoverage
{
    private readonly Dictionary<MemberRef, int[]> _members;
    private readonly HashSet<string> _types;

    internal TestCoverage(Dictionary<MemberRef, int[]> members, HashSet<string> types)
    {
        _members = members;
        _types = types;
    }

    /// <summary>Coverage of a test that ran no instrumented code, such as a skipped test.</summary>
    public static TestCoverage Empty { get; } = new([], new HashSet<string>(StringComparer.Ordinal));

    /// <summary>The members the test touched.</summary>
    public IReadOnlyCollection<MemberRef> Members => _members.Keys;

    /// <summary>The number of statements the test reached.</summary>
    public int StatementCount => _members.Values.Sum(o => o.Length);

    /// <summary>Whether the test ran any code of a member.</summary>
    /// <param name="member">The member.</param>
    public bool Touches(MemberRef member) => _members.ContainsKey(member);

    /// <summary>Whether the test ran any code of a type, in any of its files.</summary>
    /// <param name="typeName">The type's full name, as reflection writes it.</param>
    public bool TouchesType(string typeName) => _types.Contains(typeName);

    /// <summary>Whether the test reached a statement.</summary>
    /// <param name="member">The statement's member.</param>
    /// <param name="ordinal">The statement's ordinal in the member.</param>
    public bool Covers(MemberRef member, int ordinal) =>
        _members.TryGetValue(member, out var ordinals) && Array.BinarySearch(ordinals, ordinal) >= 0;

    /// <summary>The ordinals of the statements of a member the test reached, in order.</summary>
    /// <param name="member">The member.</param>
    public IReadOnlyList<int> StatementsOf(MemberRef member) =>
        _members.TryGetValue(member, out var ordinals) ? ordinals : [];
}
