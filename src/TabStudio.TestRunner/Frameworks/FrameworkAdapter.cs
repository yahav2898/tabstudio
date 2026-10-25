using System.Reflection;
using TabStudio.TestRunner.Execution;

namespace TabStudio.TestRunner.Frameworks;

/// <summary>What an adapter needs while it discovers the tests of one type.</summary>
/// <param name="locate">Finds a method's source location in the PDB of whichever user assembly declares it.</param>
internal sealed class DiscoveryContext(Func<MethodBase, TestSourceLocation?> locate)
{
    /// <summary>Finds where a method's code starts, or returns <see langword="null"/>.</summary>
    /// <param name="method">The method.</param>
    public TestSourceLocation? Locate(MethodBase method) => locate(method);
}

/// <summary>
/// Teaches the runner one test framework: which types and methods are tests, which data they take, and how
/// a class's tests are run.
/// </summary>
internal abstract class FrameworkAdapter
{
    /// <summary>The framework.</summary>
    public abstract TestFramework Framework { get; }

    /// <summary>Discovers the tests of one type.</summary>
    /// <param name="type">A concrete class, or a static class.</param>
    /// <param name="context">Discovery services.</param>
    /// <returns>The tests, or an empty list when the type has no tests of this framework.</returns>
    public abstract List<RunnableTest> Discover(Type type, DiscoveryContext context);

    /// <summary>Creates the object that runs a class's tests during one run.</summary>
    /// <param name="testClass">The test class.</param>
    /// <param name="scope">State shared by the whole run.</param>
    public abstract ClassLifecycle CreateLifecycle(Type testClass, RunScope scope);

    /// <summary>The methods of a type that could be tests, in declaration order.</summary>
    /// <param name="type">The type.</param>
    /// <param name="includeNonPublic">Whether non-public methods count, as they do for xUnit.</param>
    protected static List<MethodInfo> CandidateMethods(Type type, bool includeNonPublic)
    {
        var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
        if (includeNonPublic)
        {
            flags |= BindingFlags.NonPublic;
        }

        return type.GetMethods(flags)
            .Where(m => m.DeclaringType != typeof(object) && !m.IsAbstract && !m.IsSpecialName && !m.IsConstructor)
            .OrderBy(m => TypeDepth(m.DeclaringType))
            .ThenBy(m => m.MetadataToken)
            .ToList();
    }

    /// <summary>Returns <see langword="null"/> for an empty string, so an empty skip reason means "not skipped".</summary>
    /// <param name="text">The text.</param>
    protected static string? NonEmpty(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;

    private static int TypeDepth(Type? type)
    {
        var depth = 0;
        for (var current = type; current is not null; current = current.BaseType)
        {
            depth++;
        }

        return depth;
    }
}
