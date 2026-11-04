using System.Collections;
using System.Reflection;
using TabStudio.TestRunner.Execution;
using TabStudio.TestRunner.Reflection;

namespace TabStudio.TestRunner.Frameworks;

/// <summary>
/// NUnit 3 and 4: <c>[Test]</c>, <c>[TestCase]</c> and <c>[TestCaseSource]</c> methods in a fixture, with
/// <c>[Ignore]</c>, <c>[Explicit]</c>, <c>[Category]</c> and <c>[Timeout]</c>.
/// </summary>
internal sealed class NUnitAdapter : FrameworkAdapter
{
    private const string SkipReasonProperty = "_SKIPREASON";

    /// <inheritdoc />
    public override TestFramework Framework => TestFramework.NUnit;

    /// <inheritdoc />
    public override List<RunnableTest> Discover(Type type, DiscoveryContext context)
    {
        var methods = CandidateMethods(type, includeNonPublic: false)
            .Select(m => (Method: m, Attributes: AttributeReader.Of(m)))
            .Where(m => m.Attributes.Exists(IsTestAttribute))
            .ToList();
        if (methods.Count == 0)
        {
            return [];
        }

        // NUnit's fixture attributes are inherited, so a base class's [Category] or [Ignore] applies too.
        var classAttributes = TypeMatching.HierarchyFromBase(type).SelectMany(t => AttributeReader.Of(t)).ToList();
        var fixtureError = classAttributes.Exists(a => a.Is(FrameworkNames.NUnitTestFixture) && a.ConstructorArguments().Length > 0)
            ? "Parameterized fixtures ([TestFixture(arguments)]) are not supported by TabStudio yet."
            : null;

        var tests = new List<RunnableTest>();
        foreach (var (method, attributes) in methods)
        {
            var builder = new TestMethodBuilder(this, type, method, context)
            {
                SkipReason = IgnoreReason(attributes) ?? IgnoreReason(classAttributes),
                IsExplicit = attributes.Exists(a => a.Is(FrameworkNames.NUnitExplicit)) || classAttributes.Exists(a => a.Is(FrameworkNames.NUnitExplicit)),
                Traits = [.. Categories(classAttributes), .. Categories(attributes)],
                Timeout = TimeoutOf(attributes) ?? TimeoutOf(classAttributes),
            };

            if (fixtureError is not null)
            {
                tests.Add(builder.Single(fixtureError));
                continue;
            }

            var cases = attributes.Where(a => a.Is(FrameworkNames.NUnitTestCase)).ToList();
            var sources = attributes.Where(a => a.Is(FrameworkNames.NUnitTestCaseSource)).ToList();
            if (cases.Count == 0 && sources.Count == 0)
            {
                tests.AddRange(SingleTest(builder, type, method, attributes));
                continue;
            }

            foreach (var testCase in cases)
            {
                tests.Add(builder.Row(testCase.ExpandedArguments(), CaseOptions(testCase)));
            }

            foreach (var source in sources)
            {
                try
                {
                    foreach (var item in ReadSource(source, type))
                    {
                        var (values, options) = SourceRow(item, method);
                        tests.Add(builder.Row(values, options));
                    }
                }
                catch (Exception ex)
                {
                    var problem = OutcomeClassifier.Describe(OutcomeClassifier.Unwrap(ex));
                    tests.Add(builder.Single($"[TestCaseSource] could not provide data: {problem}"));
                }
            }
        }

        return tests;
    }

    /// <inheritdoc />
    public override ClassLifecycle CreateLifecycle(Type testClass, RunScope scope) => new NUnitClassLifecycle(testClass);

    private static bool IsTestAttribute(AttributeReader attribute) =>
        attribute.Is(FrameworkNames.NUnitTest) || attribute.Is(FrameworkNames.NUnitTestCase) || attribute.Is(FrameworkNames.NUnitTestCaseSource);

    private static List<RunnableTest> SingleTest(TestMethodBuilder builder, Type type, MethodInfo method, List<AttributeReader> attributes)
    {
        if (method.GetParameters().Any(p => !p.IsOptional))
        {
            return ParameterDataTests(builder, type, method, attributes);
        }

        var test = attributes.Find(a => a.Is(FrameworkNames.NUnitTest));
        var hasExpected = test?.IsSet("ExpectedResult") ?? false;
        return [builder.Single(hasExpectedResult: hasExpected, expectedResult: hasExpected ? test!.Named("ExpectedResult") : null)];
    }

    /// <summary>
    /// The tests of a method whose parameters carry their data, as <c>[Values]</c>, <c>[Range]</c>, <c>[Random]</c> and
    /// <c>[ValueSource]</c> do: NUnit's own attributes give each parameter's values, and NUnit's own strategy combines
    /// them, every combination by default, in step with <c>[Sequential]</c>, or in pairs with <c>[Pairwise]</c>.
    /// </summary>
    private static List<RunnableTest> ParameterDataTests(TestMethodBuilder builder, Type type, MethodInfo method, List<AttributeReader> attributes)
    {
        try
        {
            var nunit = attributes.First(IsTestAttribute).Type.Assembly;
            Type Required(string name) => nunit.GetType(name, throwOnError: true)!;
            var methodInfo = Activator.CreateInstance(Required("NUnit.Framework.Internal.MethodWrapper"), type, method);
            var sources = new List<IEnumerable>();
            foreach (var parameter in method.GetParameters())
            {
                var providers = parameter.GetCustomAttributes(inherit: false)
                    .Where(a => TypeMatching.FindInterface(a.GetType(), "NUnit.Framework.Interfaces.IParameterDataSource") is not null)
                    .ToList();
                if (providers.Count == 0)
                {
                    return [builder.Single($"Parameter '{parameter.Name}' has no data: give it [Values], [Range], [Random] or [ValueSource], or give the method [TestCase] or [TestCaseSource].")];
                }

                var wrapped = Activator.CreateInstance(Required("NUnit.Framework.Internal.ParameterWrapper"), methodInfo, parameter);
                var values = new List<object?>();
                foreach (var provider in providers)
                {
                    var getData = TypeMatching.FindInterface(provider.GetType(), "NUnit.Framework.Interfaces.IParameterDataSource")!.GetMethod("GetData")!;
                    values.AddRange(((IEnumerable)getData.Invoke(provider, BindingFlags.DoNotWrapExceptions, binder: null, [wrapped], culture: null)!).Cast<object?>());
                }

                sources.Add(values);
            }

            var strategyName = attributes.Exists(a => a.Is("NUnit.Framework.SequentialAttribute")) ? "SequentialStrategy"
                : attributes.Exists(a => a.Is("NUnit.Framework.PairwiseAttribute")) ? "PairwiseStrategy"
                : "CombinatorialStrategy";
            var strategy = Activator.CreateInstance(Required("NUnit.Framework.Internal.Builders." + strategyName))!;
            var cases = (IEnumerable)strategy.GetType().GetMethod("GetTestCases")!.Invoke(strategy, BindingFlags.DoNotWrapExceptions, binder: null, [sources.ToArray()], culture: null)!;
            var tests = new List<RunnableTest>();
            foreach (var testCase in cases)
            {
                var arguments = (object?[])testCase.GetType().GetProperty("Arguments")!.GetValue(testCase)!;
                tests.Add(builder.Row(arguments));
            }

            return tests.Count > 0 ? tests : [builder.Single("The parameters' data gave no values.")];
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return [builder.Single($"The parameters' data could not be read: {OutcomeClassifier.Describe(OutcomeClassifier.Unwrap(ex))}")];
        }
    }

    private static RowOptions CaseOptions(AttributeReader testCase) => new(
        DisplayName: NonEmpty(testCase.Named("TestName") as string),
        SkipReason: NonEmpty(testCase.Named("Ignore") as string) ?? NonEmpty(testCase.Named("IgnoreReason") as string),
        IsExplicit: testCase.Named("Explicit") is true,
        Traits: SplitCategories(testCase.Named("Category") as string),
        HasExpectedResult: testCase.IsSet("ExpectedResult"),
        ExpectedResult: testCase.Named("ExpectedResult"));

    private static IEnumerable ReadSource(AttributeReader source, Type type)
    {
        string? sourceName = null;
        Type? sourceType = null;
        object?[]? parameters = null;
        foreach (var value in source.ConstructorArguments())
        {
            switch (value)
            {
                case string name:
                    sourceName = name;
                    break;
                case Type declaring:
                    sourceType = declaring;
                    break;
                case object?[] values:
                    parameters = values;
                    break;
            }
        }

        if (sourceName is not null)
        {
            return DataSources.Read(sourceType ?? type, sourceName, parameters);
        }

        if (sourceType is null)
        {
            throw new InvalidOperationException("[TestCaseSource] needs a source name or a source type.");
        }

        return ObjectLifetime.Create(sourceType) as IEnumerable
            ?? throw new InvalidOperationException($"'{sourceType.FullName}' must be a sequence of test cases.");
    }

    /// <summary>Turns an item of a test case source into a row: a <c>TestCaseData</c>, an argument array or a single argument.</summary>
    private static (object?[] Values, RowOptions Options) SourceRow(object? item, MethodInfo method)
    {
        if (item is not null && TypeMatching.Is(item.GetType(), FrameworkNames.NUnitTestParameters))
        {
            return TestCaseData(item);
        }

        var parameters = method.GetParameters();
        if (item is object?[] array)
        {
            var isSingleArrayArgument = parameters.Length == 1 && parameters[0].ParameterType.IsArray && parameters[0].ParameterType.IsInstanceOfType(array);
            return (isSingleArrayArgument ? new object?[] { array } : array, new RowOptions());
        }

        return (new object?[] { item }, new RowOptions());
    }

    /// <summary>Reads NUnit's <c>TestCaseData</c> by its property names.</summary>
    private static (object?[] Values, RowOptions Options) TestCaseData(object data)
    {
        var type = data.GetType();
        object? Read(string name) => type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(data);

        var values = Read("Arguments") as object?[] ?? Array.Empty<object?>();
        var runState = Read("RunState")?.ToString();
        string? skip = null;
        if (runState is "Ignored" or "Skipped")
        {
            var properties = Read("Properties");
            skip = properties?.GetType().GetMethod("Get", [typeof(string)])?.Invoke(properties, [SkipReasonProperty]) as string ?? "Ignored";
        }

        var options = new RowOptions(
            DisplayName: NonEmpty(Read("TestName") as string),
            SkipReason: skip,
            IsExplicit: runState == "Explicit",
            HasExpectedResult: Read("HasExpectedResult") is true,
            ExpectedResult: Read("ExpectedResult"));
        return (values, options);
    }

    private static string? IgnoreReason(List<AttributeReader> attributes)
    {
        var ignore = attributes.Find(a => a.Is(FrameworkNames.NUnitIgnore));
        return ignore is null ? null : NonEmpty(ignore.Argument<string>()) ?? "Ignored";
    }

    private static List<TestTrait> Categories(List<AttributeReader> attributes) =>
        attributes
            .Where(a => a.Is(FrameworkNames.NUnitCategory))
            .Select(a => a.Argument<string>() ?? a.Named("Name") as string)
            .OfType<string>()
            .Select(name => new TestTrait("Category", name))
            .ToList();

    private static List<TestTrait>? SplitCategories(string? categories) =>
        categories is null
            ? null
            : categories.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(c => new TestTrait("Category", c)).ToList();

    private static TimeSpan? TimeoutOf(List<AttributeReader> attributes) =>
        attributes.Find(a => a.Is(FrameworkNames.NUnitTimeout))?.ConstructorArguments() is [int milliseconds, ..] && milliseconds > 0
            ? TimeSpan.FromMilliseconds(milliseconds)
            : null;
}
