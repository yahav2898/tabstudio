using System.Collections;
using System.Globalization;
using System.Reflection;
using TabStudio.TestRunner.Execution;
using TabStudio.TestRunner.Reflection;

namespace TabStudio.TestRunner.Frameworks;

/// <summary>
/// MSTest: <c>[TestMethod]</c> and <c>[DataTestMethod]</c> methods in a <c>[TestClass]</c>, with
/// <c>[DataRow]</c>, <c>[DynamicData]</c> and any other <c>ITestDataSource</c>, <c>[Ignore]</c>,
/// <c>[Timeout]</c>, <c>[TestCategory]</c> and <c>[ExpectedException]</c>.
/// </summary>
internal sealed class MSTestAdapter : FrameworkAdapter
{
    /// <inheritdoc />
    public override TestFramework Framework => TestFramework.MSTest;

    /// <inheritdoc />
    public override List<RunnableTest> Discover(Type type, DiscoveryContext context)
    {
        var classAttributes = AttributeReader.Of(type);
        if (!classAttributes.Exists(a => a.Is(FrameworkNames.MSTestTestClass)))
        {
            return [];
        }

        var tests = new List<RunnableTest>();
        foreach (var method in CandidateMethods(type, includeNonPublic: false))
        {
            var attributes = AttributeReader.Of(method);
            var testMethod = attributes.Find(a => a.Is(FrameworkNames.MSTestTestMethod));
            if (testMethod is null || method.IsStatic)
            {
                continue;
            }

            var expectedException = attributes.Find(a => a.Is(FrameworkNames.MSTestExpectedException));
            var builder = new TestMethodBuilder(this, type, method, context)
            {
                DisplayName = NonEmpty(testMethod.Named("DisplayName") as string ?? testMethod.Argument<string>()),
                SkipReason = IgnoreReason(attributes) ?? IgnoreReason(classAttributes),
                Traits = [.. Traits(classAttributes), .. Traits(attributes)],
                Timeout = TimeoutOf(attributes),
                ExpectedException = expectedException?.Argument<Type>(),
                AllowDerivedExceptions = expectedException?.Named("AllowDerivedTypes") is true,
            };

            var sources = attributes.Where(a => a.Implements(FrameworkNames.MSTestDataSource)).ToList();
            if (sources.Count == 0)
            {
                tests.Add(builder.Single(method.GetParameters().Length > 0
                    ? "The test method has parameters but no data source such as [DataRow] or [DynamicData]."
                    : null));
                continue;
            }

            foreach (var source in sources)
            {
                if (source.Is(FrameworkNames.MSTestDataRow))
                {
                    tests.Add(builder.Row(source.ExpandedArguments(), new RowOptions(
                        DisplayName: NonEmpty(source.Named("DisplayName") as string),
                        SkipReason: NonEmpty(source.Named("IgnoreMessage") as string))));
                    continue;
                }

                try
                {
                    foreach (var row in ReadRows(source, method))
                    {
                        tests.Add(builder.Row(row, new RowOptions(SkipReason: NonEmpty(source.Named("IgnoreMessage") as string))));
                    }
                }
                catch (Exception ex)
                {
                    var problem = OutcomeClassifier.Describe(OutcomeClassifier.Unwrap(ex));
                    tests.Add(builder.Single($"[{source.Type.Name}] could not provide data: {problem}"));
                }
            }
        }

        return tests;
    }

    /// <inheritdoc />
    public override ClassLifecycle CreateLifecycle(Type testClass, RunScope scope) => new MSTestClassLifecycle(testClass, scope);

    /// <summary>Asks a data source attribute such as <c>[DynamicData]</c> for its rows, through <c>ITestDataSource.GetData</c>.</summary>
    private static List<object?[]> ReadRows(AttributeReader source, MethodInfo method)
    {
        var getData = source.Type.GetMethod("GetData", [typeof(MethodInfo)])
            ?? throw new InvalidOperationException($"[{source.Type.Name}] has no GetData(MethodInfo) method.");
        var instance = source.Instance ?? throw new InvalidOperationException($"[{source.Type.Name}] could not be created.");
        var items = getData.Invoke(instance, [method]) as IEnumerable
            ?? throw new InvalidOperationException($"[{source.Type.Name}] returned no data.");

        var rows = new List<object?[]>();
        foreach (var item in items)
        {
            rows.Add(item as object?[] ?? DataSources.SpreadTuple(item) ?? new object?[] { item });
        }

        return rows;
    }

    private static string? IgnoreReason(List<AttributeReader> attributes)
    {
        var ignore = attributes.Find(a => a.Is(FrameworkNames.MSTestIgnore));
        return ignore is null ? null : NonEmpty(ignore.Named("IgnoreMessage") as string ?? ignore.Argument<string>()) ?? "Ignored";
    }

    private static List<TestTrait> Traits(List<AttributeReader> attributes)
    {
        var traits = new List<TestTrait>();
        foreach (var attribute in attributes)
        {
            var values = attribute.ConstructorArguments();
            if (attribute.Is(FrameworkNames.MSTestTestCategory) && values is [string category])
            {
                traits.Add(new TestTrait("Category", category));
            }
            else if (attribute.Is(FrameworkNames.MSTestTestProperty) && values is [string name, string value])
            {
                traits.Add(new TestTrait(name, value));
            }
            else if (attribute.Is(FrameworkNames.MSTestOwner) && values is [string owner])
            {
                traits.Add(new TestTrait("Owner", owner));
            }
            else if (attribute.Is(FrameworkNames.MSTestPriority) && values is [int priority])
            {
                traits.Add(new TestTrait("Priority", priority.ToString(CultureInfo.InvariantCulture)));
            }
        }

        return traits;
    }

    private static TimeSpan? TimeoutOf(List<AttributeReader> attributes)
    {
        var timeout = attributes.Find(a => a.Is(FrameworkNames.MSTestTimeout));
        var milliseconds = timeout?.ConstructorArguments() switch
        {
            [int value] => value,
            [Enum value] => Convert.ToInt32(value, CultureInfo.InvariantCulture),
            _ => 0,
        };

        // TestTimeout.Infinite is int.MaxValue.
        return milliseconds > 0 && milliseconds < int.MaxValue ? TimeSpan.FromMilliseconds(milliseconds) : null;
    }
}
