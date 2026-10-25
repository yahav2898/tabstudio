using System.Reflection;
using TabStudio.TestRunner.Execution;
using TabStudio.TestRunner.Naming;

namespace TabStudio.TestRunner.Frameworks;

/// <summary>Per-row settings of a data row, such as NUnit's <c>[TestCase(..., TestName = "...")]</c>.</summary>
/// <param name="DisplayName">A display name that replaces the generated one.</param>
/// <param name="SkipReason">Why this row is skipped.</param>
/// <param name="IsExplicit">Whether this row only runs when selected on its own.</param>
/// <param name="Traits">Traits added to the method's.</param>
/// <param name="HasExpectedResult">Whether the row declares an expected return value.</param>
/// <param name="ExpectedResult">The expected return value.</param>
internal sealed record RowOptions(
    string? DisplayName = null,
    string? SkipReason = null,
    bool IsExplicit = false,
    IReadOnlyList<TestTrait>? Traits = null,
    bool HasExpectedResult = false,
    object? ExpectedResult = null);

/// <summary>
/// Builds the <see cref="RunnableTest"/>s of one test method: one for a plain test, one per data row for a
/// parameterized test. Keeps ids unique within the method.
/// </summary>
internal sealed class TestMethodBuilder
{
    private readonly Dictionary<string, int> _ids = new(StringComparer.Ordinal);
    private readonly FrameworkAdapter _adapter;
    private readonly Type _type;
    private readonly MethodInfo _method;
    private readonly TestSourceLocation? _location;
    private readonly List<string> _parameterNames;

    /// <summary>Starts building the tests of a method.</summary>
    /// <param name="adapter">The framework adapter.</param>
    /// <param name="type">The test class the method was found on.</param>
    /// <param name="method">The test method.</param>
    /// <param name="context">Discovery services.</param>
    public TestMethodBuilder(FrameworkAdapter adapter, Type type, MethodInfo method, DiscoveryContext context)
    {
        _adapter = adapter;
        _type = type;
        _method = method;
        _location = context.Locate(method);
        _parameterNames = method.GetParameters().Select(p => p.Name ?? "?").ToList();
        FullyQualifiedName = TestNaming.FullyQualifiedName(type.FullName ?? type.Name, method.Name);
    }

    /// <summary>The method's fully qualified name.</summary>
    public string FullyQualifiedName { get; }

    /// <summary>A custom display name for the method.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Why every test of the method is skipped.</summary>
    public string? SkipReason { get; init; }

    /// <summary>Whether the method only runs when selected on its own.</summary>
    public bool IsExplicit { get; init; }

    /// <summary>The method's traits, including its class's.</summary>
    public IReadOnlyList<TestTrait> Traits { get; init; } = [];

    /// <summary>The method's own timeout.</summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>The exception the method must throw.</summary>
    public Type? ExpectedException { get; init; }

    /// <summary>Whether derived exceptions also pass.</summary>
    public bool AllowDerivedExceptions { get; init; }

    /// <summary>The xUnit collection of the class.</summary>
    public string? CollectionName { get; init; }

    /// <summary>Builds the one test of a method without data.</summary>
    /// <param name="error">A discovery problem to report instead of running the test.</param>
    /// <param name="hasExpectedResult">Whether an expected return value is declared.</param>
    /// <param name="expectedResult">The expected return value.</param>
    public RunnableTest Single(string? error = null, bool hasExpectedResult = false, object? expectedResult = null) =>
        Create(TestNaming.Unique(FullyQualifiedName, _ids), DisplayName ?? _method.Name, null, [], new RowOptions(HasExpectedResult: hasExpectedResult, ExpectedResult: expectedResult), error);

    /// <summary>Builds the test of one data row.</summary>
    /// <param name="values">The row's values.</param>
    /// <param name="options">Per-row settings.</param>
    public RunnableTest Row(IReadOnlyList<object?> values, RowOptions? options = null)
    {
        options ??= new RowOptions();
        var row = TestNaming.Row(values);
        var id = TestNaming.Unique(FullyQualifiedName + row, _ids);
        var display = options.DisplayName ?? TestNaming.RowDisplayName(_adapter.Framework, DisplayName ?? _method.Name, _parameterNames, values);
        return Create(id, display, row, values, options, null);
    }

    private RunnableTest Create(string id, string displayName, string? row, IReadOnlyList<object?> values, RowOptions options, string? error)
    {
        var testCase = new TestCase
        {
            Id = id,
            FullyQualifiedName = FullyQualifiedName,
            DisplayName = displayName,
            Framework = _adapter.Framework,
            AssemblyName = _type.Assembly.GetName().Name ?? string.Empty,
            TypeFullName = _type.FullName ?? _type.Name,
            MethodName = _method.Name,
            DataRow = row,
            SkipReason = options.SkipReason ?? SkipReason,
            IsExplicit = options.IsExplicit || IsExplicit,
            Traits = options.Traits is { Count: > 0 } extra ? [.. Traits, .. extra] : Traits,
            Location = _location,
            Timeout = Timeout,
        };

        return new RunnableTest
        {
            Case = testCase,
            Adapter = _adapter,
            TestClass = _type,
            Method = _method,
            Arguments = values,
            HasExpectedResult = options.HasExpectedResult,
            ExpectedResult = options.ExpectedResult,
            ExpectedException = ExpectedException,
            AllowDerivedExceptions = AllowDerivedExceptions,
            DiscoveryError = error,
            CollectionName = CollectionName,
        };
    }
}
