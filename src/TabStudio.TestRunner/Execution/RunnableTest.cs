using System.Reflection;
using TabStudio.TestRunner.Frameworks;

namespace TabStudio.TestRunner.Execution;

/// <summary>
/// A discovered test together with everything needed to run it: its method, its data row and its framework.
/// </summary>
internal sealed class RunnableTest
{
    /// <summary>What Test Explorer sees.</summary>
    public required TestCase Case { get; init; }

    /// <summary>The framework adapter that discovered the test and runs its class.</summary>
    public required FrameworkAdapter Adapter { get; init; }

    /// <summary>The test class the test was discovered on, which may derive from the class that declares the method.</summary>
    public required Type TestClass { get; init; }

    /// <summary>The test method.</summary>
    public required MethodInfo Method { get; init; }

    /// <summary>The data row's values, before conversion to the parameter types.</summary>
    public IReadOnlyList<object?> Arguments { get; init; } = [];

    /// <summary>Whether the row declares an expected return value (NUnit's <c>ExpectedResult</c>).</summary>
    public bool HasExpectedResult { get; init; }

    /// <summary>The expected return value, when <see cref="HasExpectedResult"/> is set.</summary>
    public object? ExpectedResult { get; init; }

    /// <summary>The exception the test must throw to pass (MSTest's <c>[ExpectedException]</c>).</summary>
    public Type? ExpectedException { get; init; }

    /// <summary>Whether an exception derived from <see cref="ExpectedException"/> also passes.</summary>
    public bool AllowDerivedExceptions { get; init; }

    /// <summary>
    /// A problem found while discovering the test, such as a data source that threw. The test is reported as
    /// errored with this message instead of running.
    /// </summary>
    public string? DiscoveryError { get; init; }

    /// <summary>The xUnit collection the test class belongs to, which never runs in parallel with itself.</summary>
    public string? CollectionName { get; init; }
}
