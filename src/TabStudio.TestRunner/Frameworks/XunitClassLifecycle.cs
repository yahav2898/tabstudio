using System.Reflection;
using TabStudio.TestRunner.Execution;
using TabStudio.TestRunner.Reflection;

namespace TabStudio.TestRunner.Frameworks;

/// <summary>
/// Runs an xUnit test class: a new instance per test, class fixtures (<c>IClassFixture&lt;T&gt;</c>) created once
/// per class, collection fixtures (<c>ICollectionFixture&lt;T&gt;</c>) once per run, and
/// <c>ITestOutputHelper</c> for constructors that ask for it.
/// </summary>
internal sealed class XunitClassLifecycle(Type testClass, RunScope scope) : ClassLifecycle
{
    private readonly List<object> _classFixtures = [];
    private Dictionary<Type, object> _collectionFixtures = [];

    /// <inheritdoc />
    public override StageFailure? SetUpClass()
    {
        foreach (var fixtureInterface in TypeMatching.FindInterfaces(testClass, FrameworkNames.XunitClassFixture))
        {
            var fixtureType = fixtureInterface.GetGenericArguments()[0];
            if (_classFixtures.Exists(f => f.GetType() == fixtureType))
            {
                continue;
            }

            try
            {
                var fixture = ObjectLifetime.Create(fixtureType);
                _classFixtures.Add(fixture);
                ObjectLifetime.Initialize(fixture);
            }
            catch (Exception ex)
            {
                return new StageFailure($"Class fixture '{fixtureType.Name}'", ex);
            }
        }

        if (XunitAdapter.CollectionName(testClass) is { } collection)
        {
            try
            {
                _collectionFixtures = scope.GetOrCreate(
                    ("xunit collection", testClass.Assembly, collection),
                    () => CreateCollectionFixtures(testClass.Assembly, collection));
            }
            catch (Exception ex)
            {
                return new StageFailure($"Collection fixture of '{collection}'", ex);
            }
        }

        return null;
    }

    /// <inheritdoc />
    public override TestStepOutcome RunTest(RunnableTest test, TestOutputSink sink)
    {
        var failures = new FailureCollector();
        object? instance = null;
        try
        {
            var arguments = ArgumentBinder.Bind(test.Method, test.Arguments, out var method);
            if (!method.IsStatic)
            {
                instance = CreateInstance(sink);
                ObjectLifetime.Initialize(instance);
            }

            UserCode.Invoke(method, instance, arguments);
        }
        catch (Exception ex)
        {
            failures.Add(ex);
        }

        if (instance is not null)
        {
            try
            {
                ObjectLifetime.Dispose(instance);
            }
            catch (Exception ex)
            {
                failures.Add(ex, "Dispose");
            }
        }

        return failures.ToOutcome();
    }

    /// <inheritdoc />
    public override StageFailure? TearDownClass()
    {
        StageFailure? failure = null;
        for (var i = _classFixtures.Count - 1; i >= 0; i--)
        {
            try
            {
                ObjectLifetime.Dispose(_classFixtures[i]);
            }
            catch (Exception ex)
            {
                failure ??= new StageFailure($"Disposing class fixture '{_classFixtures[i].GetType().Name}'", ex);
            }
        }

        return failure;
    }

    private object CreateInstance(TestOutputSink sink)
    {
        var constructors = testClass.GetConstructors();
        if (constructors.Length != 1)
        {
            throw new InvalidOperationException(constructors.Length == 0
                ? $"'{testClass.FullName}' has no public constructor."
                : "A test class may only define a single public constructor.");
        }

        var constructor = constructors[0];
        var parameters = constructor.GetParameters();
        var arguments = new object?[parameters.Length];
        var missing = new List<string>();
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameterType = parameters[i].ParameterType;
            var typeName = TypeMatching.NameOf(parameterType);
            if (typeName is FrameworkNames.XunitOutputHelper or FrameworkNames.XunitV3OutputHelper)
            {
                arguments[i] = TestOutputHelperProxy.CreateFor(parameterType, sink);
            }
            else if ((_classFixtures.Find(parameterType.IsInstanceOfType) ?? FindCollectionFixture(parameterType)) is { } fixture)
            {
                arguments[i] = fixture;
            }
            else
            {
                missing.Add($"{parameterType.Name} {parameters[i].Name}");
            }
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException($"The following constructor parameters did not have matching fixture data: {string.Join(", ", missing)}");
        }

        return constructor.Invoke(BindingFlags.DoNotWrapExceptions, binder: null, arguments, culture: null);
    }

    private object? FindCollectionFixture(Type parameterType) =>
        _collectionFixtures.Values.FirstOrDefault(parameterType.IsInstanceOfType);

    private Dictionary<Type, object> CreateCollectionFixtures(Assembly assembly, string collection)
    {
        var fixtures = new Dictionary<Type, object>();
        var definition = AssemblyTypes.Of(assembly).FirstOrDefault(type =>
            AttributeReader.Of(type).Exists(a =>
                a.Is(FrameworkNames.XunitCollectionDefinition) &&
                string.Equals(a.Argument<string>(), collection, StringComparison.Ordinal)));
        if (definition is null)
        {
            return fixtures;
        }

        foreach (var fixtureInterface in TypeMatching.FindInterfaces(definition, FrameworkNames.XunitCollectionFixture))
        {
            var fixtureType = fixtureInterface.GetGenericArguments()[0];
            var fixture = ObjectLifetime.Create(fixtureType);
            scope.AddCleanup($"Disposing collection fixture '{fixtureType.Name}'", () => ObjectLifetime.Dispose(fixture));
            ObjectLifetime.Initialize(fixture);
            fixtures[fixtureType] = fixture;
        }

        return fixtures;
    }
}
