namespace TabStudio.LiveTesting.Coverage;

/// <summary>
/// Identifies a function body across builds: the file it is in and its key, such as
/// <c>Calculator.Arithmetic.Add(int,int)</c>. Coverage is kept per member, so it stays right while lines move.
/// </summary>
/// <param name="FilePath">Full path of the source file.</param>
/// <param name="Key">The member's key in the file: its type, name and parameter types.</param>
public readonly record struct MemberRef(string FilePath, string Key);

/// <summary>A function body that coverage tracks: a method, constructor, accessor, operator or the top-level statements.</summary>
/// <param name="Index">The member's position in its <see cref="CoverageModule"/>.</param>
/// <param name="FilePath">Full path of the source file.</param>
/// <param name="TypeName">The full name of the containing type, as reflection writes it.</param>
/// <param name="Name">The member's name, such as <c>Add</c>, <c>.ctor</c> or <c>this[]</c>.</param>
/// <param name="Key">The member's key in the file.</param>
/// <param name="Line">One-based line where the member starts.</param>
public sealed record CoverageMember(int Index, string FilePath, string TypeName, string Name, string Key, int Line)
{
    /// <summary>The member's identity across builds.</summary>
    public MemberRef Ref => new(FilePath, Key);
}

/// <summary>A statement that coverage records, which gets a marker in the editor.</summary>
/// <param name="Point">The coverage point the statement's hook reports.</param>
/// <param name="Member">The <see cref="CoverageMember.Index"/> of the member it is in.</param>
/// <param name="Ordinal">Its position among the statements of its member, the same in every build of the same code.</param>
/// <param name="Line">One-based line where the statement starts, in the source the module was built from.</param>
public readonly record struct CoverageStatement(int Point, int Member, int Ordinal, int Line);
