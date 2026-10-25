using System.Reflection;
using System.Runtime.CompilerServices;
using TabStudio.TestRunner.Execution;
using TabStudio.TestRunner.Naming;
using TabStudio.TestRunner.Reflection;

namespace TabStudio.TestRunner.Frameworks;

/// <summary>Finds the tests of loaded assemblies with every framework adapter.</summary>
internal static class ReflectionDiscoverer
{
    private static readonly FrameworkAdapter[] Adapters = [new XunitAdapter(), new NUnitAdapter(), new MSTestAdapter()];

    /// <summary>Discovers tests in assemblies.</summary>
    /// <param name="assemblies">The user's assemblies.</param>
    /// <param name="context">Discovery services.</param>
    /// <param name="problems">Receives a message for every type that could not be examined.</param>
    /// <returns>The tests, with ids unique across all assemblies.</returns>
    public static List<RunnableTest> Discover(IReadOnlyList<Assembly> assemblies, DiscoveryContext context, List<string> problems)
    {
        var tests = new List<RunnableTest>();
        foreach (var assembly in assemblies)
        {
            foreach (var type in AssemblyTypes.Of(assembly, problems))
            {
                if (!IsCandidate(type))
                {
                    continue;
                }

                try
                {
                    foreach (var adapter in Adapters)
                    {
                        var found = adapter.Discover(type, context);
                        if (found.Count > 0)
                        {
                            tests.AddRange(found);
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // A type the runner cannot examine must not hide the tests of every other type.
                    problems.Add($"Tests in '{type.FullName}' could not be discovered: {OutcomeClassifier.Describe(OutcomeClassifier.Unwrap(ex))}");
                }
            }
        }

        return MakeIdsUnique(tests);
    }

    /// <summary>Whether a type can hold tests: a concrete or static, non-generic class written by the user.</summary>
    private static bool IsCandidate(Type type) =>
        type.IsClass &&
        !type.IsGenericTypeDefinition &&
        (!type.IsAbstract || type.IsSealed) &&
        !type.Name.StartsWith('<') &&
        !type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false);

    /// <summary>Overloaded test methods share a name; their ids get a suffix, as data rows with equal values do.</summary>
    private static List<RunnableTest> MakeIdsUnique(List<RunnableTest> tests)
    {
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        var unique = new List<RunnableTest>(tests.Count);
        foreach (var test in tests)
        {
            var id = TestNaming.Unique(test.Case.Id, seen);
            unique.Add(id == test.Case.Id ? test : CopyWithId(test, id));
        }

        return unique;
    }

    private static RunnableTest CopyWithId(RunnableTest test, string id) => new()
    {
        Case = test.Case with { Id = id },
        Adapter = test.Adapter,
        TestClass = test.TestClass,
        Method = test.Method,
        Arguments = test.Arguments,
        HasExpectedResult = test.HasExpectedResult,
        ExpectedResult = test.ExpectedResult,
        ExpectedException = test.ExpectedException,
        AllowDerivedExceptions = test.AllowDerivedExceptions,
        DiscoveryError = test.DiscoveryError,
        CollectionName = test.CollectionName,
    };
}
