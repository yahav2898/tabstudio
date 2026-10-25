namespace TabStudio.TestRunner;

/// <summary>
/// One runnable test found in a compiled test assembly: a test method, or one data row of a parameterized test.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Id"/> identifies the test across builds, so Test Explorer can keep its state and "Run failed tests"
/// works after a rebuild. A test without data uses its <see cref="FullyQualifiedName"/>; a data row appends its
/// arguments, as in <c>Calculator.Tests.Adds(1, 2)</c>, which is also what
/// <see cref="SourceDataRow.Id"/> computes from source.
/// </para>
/// <para>
/// <see cref="FullyQualifiedName"/> is the same for every row of a method and is what the editor's
/// "Run test" marker and source discovery (<see cref="SourceTestMethod.FullyQualifiedName"/>) use.
/// </para>
/// </remarks>
public sealed record TestCase
{
    /// <summary>A stable, unique identifier of this test within its session.</summary>
    public required string Id { get; init; }

    /// <summary>The namespace, type and method name, for example <c>Calculator.Tests.MathTests.Adds</c>. Nested types are joined with <c>+</c>.</summary>
    public required string FullyQualifiedName { get; init; }

    /// <summary>The name shown in Test Explorer: a custom display name, or the method name with its arguments.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The framework the test is written with.</summary>
    public required TestFramework Framework { get; init; }

    /// <summary>The name of the assembly that contains the test.</summary>
    public required string AssemblyName { get; init; }

    /// <summary>The full name of the test class, for example <c>Calculator.Tests.MathTests</c>.</summary>
    public required string TypeFullName { get; init; }

    /// <summary>The name of the test method.</summary>
    public required string MethodName { get; init; }

    /// <summary>The arguments of a data row as shown to the user, for example <c>(1, "two")</c>, or <see langword="null"/> for a test without data.</summary>
    public string? DataRow { get; init; }

    /// <summary>Why the test is skipped, or <see langword="null"/> when it runs.</summary>
    public string? SkipReason { get; init; }

    /// <summary>
    /// Whether the test only runs when it is selected on its own, like NUnit's <c>[Explicit]</c>.
    /// "Run all" leaves explicit tests out.
    /// </summary>
    public bool IsExplicit { get; init; }

    /// <summary>The traits and categories of the test.</summary>
    public IReadOnlyList<TestTrait> Traits { get; init; } = [];

    /// <summary>
    /// Where the test method's body starts, read from the assembly's portable PDB, or <see langword="null"/> when
    /// the assembly has no symbols.
    /// </summary>
    public TestSourceLocation? Location { get; init; }

    /// <summary>The timeout set on the test itself, or <see langword="null"/> to use the run's default.</summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>The namespace of the test class, or an empty string for the global namespace.</summary>
    public string NamespaceName
    {
        get
        {
            var outer = TypeFullName.Split('+')[0];
            var dot = outer.LastIndexOf('.');
            return dot < 0 ? string.Empty : outer[..dot];
        }
    }

    /// <summary>The name of the test class without its namespace, for example <c>MathTests</c> or <c>Outer+Inner</c>.</summary>
    public string TypeName => NamespaceName.Length == 0 ? TypeFullName : TypeFullName[(NamespaceName.Length + 1)..];
}
