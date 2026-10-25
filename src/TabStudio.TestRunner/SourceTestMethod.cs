namespace TabStudio.TestRunner;

/// <summary>
/// A test method found in source, before any build: what the editor's "Run test" marker and the Test Explorer
/// tree show while the code is being written.
/// </summary>
public sealed record SourceTestMethod
{
    /// <summary>
    /// The namespace, type and method name, the same as <see cref="TestCase.FullyQualifiedName"/> of the tests the
    /// runner finds for this method after a build. Pass it to <see cref="TestSession.RunSelectedAsync"/> to run it.
    /// </summary>
    public required string FullyQualifiedName { get; init; }

    /// <summary>The full name of the class the test runs in, with <c>+</c> between nested types.</summary>
    public required string TypeFullName { get; init; }

    /// <summary>The method name.</summary>
    public required string MethodName { get; init; }

    /// <summary>The custom display name, or the method name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The framework of the test.</summary>
    public required TestFramework Framework { get; init; }

    /// <summary>Where the method's name is, which is where the editor puts its marker.</summary>
    public required TestSourceLocation Location { get; init; }

    /// <summary>The zero-based offset of the method's name in its file.</summary>
    public int SpanStart { get; init; }

    /// <summary>The length of the method's name.</summary>
    public int SpanLength { get; init; }

    /// <summary>Why the test is skipped, or <see langword="null"/>.</summary>
    public string? SkipReason { get; init; }

    /// <summary>Whether the test only runs when selected on its own.</summary>
    public bool IsExplicit { get; init; }

    /// <summary>The test's traits and categories, including its class's.</summary>
    public IReadOnlyList<TestTrait> Traits { get; init; } = [];

    /// <summary>Whether the test takes data: it has parameters or data attributes.</summary>
    public bool IsParameterized { get; init; }

    /// <summary>
    /// Whether some of its data comes from code, such as <c>[MemberData]</c>, <c>[TestCaseSource]</c> or
    /// <c>[DynamicData]</c>. Those rows are only known after a build.
    /// </summary>
    public bool HasDynamicData { get; init; }

    /// <summary>The data rows written as attributes.</summary>
    public IReadOnlyList<SourceDataRow> DataRows { get; init; } = [];

    /// <summary>Whether the method is declared in a base class, possibly in another file.</summary>
    public bool IsInherited { get; init; }
}
