using System.Collections;
using System.Reflection;
using TabStudio.TestRunner.Execution;
using TabStudio.TestRunner.Reflection;

namespace TabStudio.TestRunner.Frameworks;

/// <summary>
/// xUnit.net v2: <c>[Fact]</c> and <c>[Theory]</c> methods, with <c>[InlineData]</c>, <c>[MemberData]</c>,
/// <c>[ClassData]</c> and any other data attribute that has a <c>GetData(MethodInfo)</c> method.
/// </summary>
internal sealed class XunitAdapter : FrameworkAdapter
{
    /// <inheritdoc />
    public override TestFramework Framework => TestFramework.XUnit;

    /// <summary>The name of the collection a test class belongs to, from <c>[Collection]</c>.</summary>
    /// <param name="testClass">The test class.</param>
    public static string? CollectionName(Type testClass)
    {
        var collection = AttributeReader.Of(testClass).Find(a => a.Is(FrameworkNames.XunitCollection));
        return collection?.Argument<string>() ?? collection?.Argument<Type>()?.FullName;
    }

    /// <inheritdoc />
    public override List<RunnableTest> Discover(Type type, DiscoveryContext context)
    {
        var tests = new List<RunnableTest>();
        var classTraits = Traits(AttributeReader.Of(type));
        string? collection = null;
        var collectionRead = false;

        foreach (var method in CandidateMethods(type, includeNonPublic: true))
        {
            var attributes = AttributeReader.Of(method);
            var fact = attributes.Find(a => a.Is(FrameworkNames.XunitFact));
            if (fact is null)
            {
                continue;
            }

            if (!collectionRead)
            {
                collection = CollectionName(type);
                collectionRead = true;
            }

            var builder = new TestMethodBuilder(this, type, method, context)
            {
                DisplayName = NonEmpty(fact.Named("DisplayName") as string),
                SkipReason = NonEmpty(fact.Named("Skip") as string),
                Traits = [.. classTraits, .. Traits(attributes)],
                Timeout = fact.Named("Timeout") is int milliseconds && milliseconds > 0 ? TimeSpan.FromMilliseconds(milliseconds) : null,
                CollectionName = collection,
            };

            if (!fact.Is(FrameworkNames.XunitTheory))
            {
                tests.Add(builder.Single(method.GetParameters().Length > 0
                    ? "[Fact] methods are not allowed to have parameters. Did you mean to use [Theory]?"
                    : null));
            }
            else if (builder.SkipReason is not null)
            {
                // A skipped theory is reported once, without reading its data.
                tests.Add(builder.Single());
            }
            else
            {
                tests.AddRange(TheoryRows(builder, type, method, attributes));
            }
        }

        return tests;
    }

    /// <inheritdoc />
    public override ClassLifecycle CreateLifecycle(Type testClass, RunScope scope) => new XunitClassLifecycle(testClass, scope);

    private static List<RunnableTest> TheoryRows(TestMethodBuilder builder, Type type, MethodInfo method, List<AttributeReader> attributes)
    {
        var tests = new List<RunnableTest>();
        foreach (var data in attributes.Where(a => a.Is(FrameworkNames.XunitData) || a.Is(FrameworkNames.XunitV3Data)))
        {
            var skip = NonEmpty(data.Named("Skip") as string);
            try
            {
                foreach (var row in ReadRows(data, type, method))
                {
                    tests.Add(builder.Row(row, new RowOptions(SkipReason: skip)));
                }
            }
            catch (Exception ex)
            {
                var problem = OutcomeClassifier.Describe(OutcomeClassifier.Unwrap(ex));
                tests.Add(builder.Single($"[{data.Type.Name}] could not provide data: {problem}"));
            }
        }

        if (tests.Count == 0)
        {
            tests.Add(builder.Single($"No data found for {builder.FullyQualifiedName}."));
        }

        return tests;
    }

    private static List<object?[]> ReadRows(AttributeReader data, Type type, MethodInfo method)
    {
        if (data.Is(FrameworkNames.XunitInlineData))
        {
            return [data.ExpandedArguments()];
        }

        if (data.Is(FrameworkNames.XunitMemberDataBase) || data.Is(FrameworkNames.XunitV3MemberDataBase))
        {
            var arguments = data.ExpandedArguments();
            var memberName = arguments.Length > 0 ? arguments[0] as string : null;
            if (memberName is null)
            {
                throw new InvalidOperationException("[MemberData] needs the name of a member.");
            }

            var memberType = data.Named("MemberType") as Type ?? type;
            return ToRows(DataSources.Read(memberType, memberName, arguments[1..]));
        }

        if (data.Is(FrameworkNames.XunitClassData))
        {
            var classType = data.Argument<Type>() ?? data.Named("Class") as Type
                ?? throw new InvalidOperationException("[ClassData] needs a type.");
            return ToRows(ObjectLifetime.Create(classType) as IEnumerable
                ?? throw new InvalidOperationException($"'{classType.FullName}' must be a sequence of rows."));
        }

        // Any other data attribute: ask it for its rows, as xUnit v2 does with GetData(MethodInfo) and v3 with
        // GetData(MethodInfo, DisposalTracker), which returns a ValueTask of rows.
        var instance = data.Instance ?? throw new InvalidOperationException($"[{data.Type.Name}] could not be created.");
        if (data.Type.GetMethod("GetData", [typeof(MethodInfo)]) is { } getData)
        {
            return ToRows(getData.Invoke(instance, [method]) as IEnumerable
                ?? throw new InvalidOperationException($"[{data.Type.Name}] returned no data."));
        }

        var getDataV3 = data.Type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "GetData" && m.GetParameters() is [{ } first, { } second]
                && first.ParameterType == typeof(MethodInfo) && second.ParameterType.Name == "DisposalTracker")
            ?? throw new InvalidOperationException($"[{data.Type.Name}] is not supported: it has no GetData method.");
        var tracker = Activator.CreateInstance(getDataV3.GetParameters()[1].ParameterType);
        return ToRows(Await(getDataV3.Invoke(instance, [method, tracker])) as IEnumerable
            ?? throw new InvalidOperationException($"[{data.Type.Name}] returned no data."));
    }

    /// <summary>The result of a <c>ValueTask&lt;T&gt;</c> or <c>Task&lt;T&gt;</c> returned through reflection.</summary>
    private static object? Await(object? awaitable)
    {
        if (awaitable is null)
        {
            return null;
        }

        var task = awaitable as Task ?? awaitable.GetType().GetMethod("AsTask", Type.EmptyTypes)?.Invoke(awaitable, null) as Task;
        if (task is null)
        {
            return awaitable;
        }

        task.GetAwaiter().GetResult();
        return task.GetType().GetProperty("Result")?.GetValue(task);
    }

    private static List<object?[]> ToRows(IEnumerable items)
    {
        var rows = new List<object?[]>();
        foreach (var item in items)
        {
            rows.Add(item switch
            {
                object?[] values => values,
                null => new object?[] { null },
                _ => DataSources.SpreadTuple(item) ?? TheoryDataRow(item) ?? new object?[] { item },
            });
        }

        return rows;
    }

    /// <summary>Reads an xUnit v3 <c>ITheoryDataRow</c>, which hands out its values from <c>GetData()</c>.</summary>
    private static object?[]? TheoryDataRow(object item) =>
        item.GetType().GetMethod("GetData", Type.EmptyTypes)?.Invoke(item, null) as object?[];

    private static List<TestTrait> Traits(List<AttributeReader> attributes) =>
        attributes
            .Where(a => a.Is(FrameworkNames.XunitTrait))
            .Select(a => a.ConstructorArguments())
            .Where(values => values is [string, string])
            .Select(values => new TestTrait((string)values[0]!, (string)values[1]!))
            .ToList();
}
