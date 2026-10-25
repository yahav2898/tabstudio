using TabStudio.TestRunner;

namespace TabStudio.App.ViewModels;

/// <summary>Where a test stands in the Test Explorer.</summary>
public enum TestState
{
    /// <summary>The test has not run since it was found, or its last run was stopped first.</summary>
    NotRun,

    /// <summary>The test is part of the current run and waits for its turn.</summary>
    Queued,

    /// <summary>The test is running.</summary>
    Running,

    /// <summary>The test passed.</summary>
    Passed,

    /// <summary>The test failed an assertion, threw an exception or timed out.</summary>
    Failed,

    /// <summary>The test was skipped, ignored or inconclusive.</summary>
    Skipped,
}

/// <summary>
/// One test in the Test Explorer, a test method or one data row of a parameterized test, with its latest result.
/// </summary>
/// <remarks>
/// Entries come from the source before a build and from the test runner after one. Both name a test with the same
/// <see cref="Id"/>, so its result carries over from one to the other, across edits and rebuilds.
/// </remarks>
public sealed class TestEntry
{
    private string? _namespaceName;

    /// <summary>The test's id, the same as <see cref="TestCase.Id"/>.</summary>
    public required string Id { get; init; }

    /// <summary>The test project.</summary>
    public required string ProjectFilePath { get; init; }

    /// <summary>The test project's name.</summary>
    public required string ProjectName { get; init; }

    /// <summary>The full name of the test class, with <c>+</c> between nested classes.</summary>
    public required string TypeFullName { get; init; }

    /// <summary>The method's full name, the same for every data row of the method.</summary>
    public required string MethodFullName { get; init; }

    /// <summary>The method name.</summary>
    public required string MethodName { get; init; }

    /// <summary>The name shown for the test, with a data row's arguments.</summary>
    public required string DisplayName { get; init; }

    /// <summary>A data row's arguments, such as <c>(1, 2)</c>, or <see langword="null"/> for a test without data.</summary>
    public string? DataRow { get; init; }

    /// <summary>Why the test is skipped, when its source says so.</summary>
    public string? SkipReason { get; init; }

    /// <summary>The test's traits and categories.</summary>
    public IReadOnlyList<TestTrait> Traits { get; init; } = [];

    /// <summary>
    /// Whether this stands for every row of a method whose rows only a build reveals, such as <c>[MemberData]</c>.
    /// Running it runs all of them.
    /// </summary>
    public bool IsPlaceholder { get; init; }

    /// <summary>Where the test method is, for "Go To Test".</summary>
    public TestSourceLocation? Location { get; internal set; }

    /// <summary>Where the test class is, when the source has been read.</summary>
    public TestSourceLocation? ClassLocation { get; internal set; }

    /// <summary>The namespace of the test class, or an empty string for the global namespace.</summary>
    public string NamespaceName => _namespaceName ??= NamespaceOf(TypeFullName);

    /// <summary>The class name without its namespace.</summary>
    public string ClassName => NamespaceName.Length == 0 ? TypeFullName : TypeFullName[(NamespaceName.Length + 1)..];

    /// <summary>Where the test stands.</summary>
    public TestState State { get; internal set; }

    /// <summary>The latest result, or <see langword="null"/> before the test first ran.</summary>
    public TestResult? Result { get; internal set; }

    /// <summary>Whether code the test depends on changed after <see cref="Result"/> was produced.</summary>
    public bool IsStale { get; internal set; }

    /// <summary>Creates the entry for a test found in source.</summary>
    /// <param name="projectFilePath">The test project.</param>
    /// <param name="projectName">The test project's name.</param>
    /// <param name="type">The test class.</param>
    /// <param name="method">The test method.</param>
    /// <param name="row">One of its data rows, or <see langword="null"/> for the method itself.</param>
    public static TestEntry FromSource(string projectFilePath, string projectName, SourceTestClass type, SourceTestMethod method, SourceDataRow? row)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(method);
        return new TestEntry
        {
            Id = row?.Id ?? method.FullyQualifiedName,
            ProjectFilePath = projectFilePath,
            ProjectName = projectName,
            TypeFullName = method.TypeFullName,
            MethodFullName = method.FullyQualifiedName,
            MethodName = method.MethodName,
            DisplayName = row?.DisplayName ?? method.DisplayName,
            DataRow = row?.Arguments,
            SkipReason = row?.SkipReason ?? method.SkipReason ?? type.SkipReason,
            Traits = method.Traits,
            IsPlaceholder = row is null && method.HasDynamicData,
            Location = method.Location,
            ClassLocation = type.Location,
        };
    }

    /// <summary>Creates the entry for a test the runner found in a built test project.</summary>
    /// <param name="projectFilePath">The test project.</param>
    /// <param name="projectName">The test project's name.</param>
    /// <param name="test">The test.</param>
    public static TestEntry FromBuild(string projectFilePath, string projectName, TestCase test)
    {
        ArgumentNullException.ThrowIfNull(test);
        return new TestEntry
        {
            Id = test.Id,
            ProjectFilePath = projectFilePath,
            ProjectName = projectName,
            TypeFullName = test.TypeFullName,
            MethodFullName = test.FullyQualifiedName,
            MethodName = test.MethodName,
            DisplayName = test.DisplayName,
            DataRow = test.DataRow,
            SkipReason = test.SkipReason,
            Traits = test.Traits,
            Location = test.Location,
        };
    }

    /// <summary>Takes over the state and result of the entry this one replaces.</summary>
    /// <param name="previous">The entry for the same test from an earlier discovery.</param>
    internal void CarryOver(TestEntry previous)
    {
        State = previous.State;
        Result = previous.Result;
        IsStale = previous.IsStale;
    }

    /// <summary>Records a result.</summary>
    /// <param name="result">The result.</param>
    /// <returns><see langword="false"/> when this result was already recorded.</returns>
    internal bool Apply(TestResult result)
    {
        if (ReferenceEquals(Result, result))
        {
            return false;
        }

        Result = result;
        IsStale = false;
        State = result.Outcome switch
        {
            TestOutcome.Passed => TestState.Passed,
            TestOutcome.Failed or TestOutcome.Errored => TestState.Failed,
            TestOutcome.Skipped => TestState.Skipped,
            _ => TestState.NotRun,
        };
        return true;
    }

    private static string NamespaceOf(string typeFullName)
    {
        var outer = typeFullName.Split('+')[0];
        var dot = outer.LastIndexOf('.');
        return dot < 0 ? string.Empty : outer[..dot];
    }
}
