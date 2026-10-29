using System.Globalization;
using System.Reflection;
using TabStudio.TestRunner.Execution;
using TabStudio.TestRunner.Naming;
using TabStudio.TestRunner.Reflection;

namespace TabStudio.TestRunner.Frameworks;

/// <summary>
/// Runs an NUnit fixture: one instance for all its tests (or one per test with
/// <c>[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]</c>), <c>[OneTimeSetUp]</c> and
/// <c>[OneTimeTearDown]</c> around them, <c>[SetUp]</c> and <c>[TearDown]</c> around each, base classes first
/// for setups and last for teardowns.
/// </summary>
internal sealed class NUnitClassLifecycle : ClassLifecycle
{
    private const int InstancePerTestCase = 1;

    private readonly Type _fixtureType;
    private readonly bool _isStatic;
    private readonly bool _instancePerTest;
    private readonly List<List<MethodInfo>> _oneTimeSetUps;
    private readonly List<List<MethodInfo>> _oneTimeTearDowns;
    private readonly List<List<MethodInfo>> _setUps;
    private readonly List<List<MethodInfo>> _tearDowns;
    private object? _fixture;
    private int _oneTimeLevelsDone;

    /// <summary>Prepares to run a fixture.</summary>
    /// <param name="fixtureType">The fixture class.</param>
    public NUnitClassLifecycle(Type fixtureType)
    {
        _fixtureType = fixtureType;
        _isStatic = TypeMatching.IsStatic(fixtureType);
        var lifeCycle = AttributeReader.Of(fixtureType).Find(a => a.Is(FrameworkNames.NUnitFixtureLifeCycle));
        _instancePerTest = lifeCycle?.ConstructorArguments() is [Enum value] &&
            Convert.ToInt32(value, CultureInfo.InvariantCulture) == InstancePerTestCase;

        var hierarchy = TypeMatching.HierarchyFromBase(fixtureType);
        _oneTimeSetUps = LifecycleMethods.ByLevel(hierarchy, FrameworkNames.NUnitOneTimeSetUp);
        _oneTimeTearDowns = LifecycleMethods.ByLevel(hierarchy, FrameworkNames.NUnitOneTimeTearDown);
        _setUps = LifecycleMethods.ByLevel(hierarchy, FrameworkNames.NUnitSetUp);
        _tearDowns = LifecycleMethods.ByLevel(hierarchy, FrameworkNames.NUnitTearDown);
    }

    /// <inheritdoc />
    public override StageFailure? SetUpClass()
    {
        try
        {
            if (!_isStatic && !_instancePerTest)
            {
                _fixture = ObjectLifetime.Create(_fixtureType);
            }
        }
        catch (Exception ex)
        {
            return new StageFailure("Creating the fixture", ex);
        }

        foreach (var level in _oneTimeSetUps)
        {
            foreach (var method in level)
            {
                try
                {
                    UserCode.Invoke(method, method.IsStatic ? null : _fixture, null);
                }
                catch (Exception ex)
                {
                    return new StageFailure("OneTimeSetUp", ex);
                }
            }

            _oneTimeLevelsDone++;
        }

        return null;
    }

    /// <inheritdoc />
    public override TestStepOutcome RunTest(RunnableTest test, TestOutputSink sink)
    {
        var failures = new FailureCollector();
        var target = _fixture;
        var ownsTarget = false;
        if (_instancePerTest && !test.Method.IsStatic)
        {
            try
            {
                target = ObjectLifetime.Create(_fixtureType);
                ownsTarget = true;
            }
            catch (Exception ex)
            {
                failures.Add(ex, "Creating the fixture");
                return failures.ToOutcome();
            }
        }

        var levelsDone = 0;
        try
        {
            foreach (var level in _setUps)
            {
                foreach (var method in level)
                {
                    UserCode.Invoke(method, method.IsStatic ? null : target, null);
                }

                levelsDone++;
            }
        }
        catch (Exception ex)
        {
            failures.Add(ex, "SetUp");
        }

        if (!failures.HasFailed)
        {
            try
            {
                var arguments = ArgumentBinder.Bind(test.Method, test.Arguments, out var method);
                var result = UserCode.Invoke(method, method.IsStatic ? null : target, arguments);
                if (CheckResult(test, method, result) is { } mismatch)
                {
                    failures.Add(mismatch);
                }
            }
            catch (Exception ex)
            {
                failures.Add(ex);
            }
        }

        for (var level = levelsDone - 1; level >= 0; level--)
        {
            foreach (var method in _tearDowns[level])
            {
                try
                {
                    UserCode.Invoke(method, method.IsStatic ? null : target, null);
                }
                catch (Exception ex)
                {
                    failures.Add(ex, "TearDown");
                }
            }
        }

        if (ownsTarget)
        {
            try
            {
                ObjectLifetime.Dispose(target);
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
        for (var level = _oneTimeLevelsDone - 1; level >= 0; level--)
        {
            foreach (var method in _oneTimeTearDowns[level])
            {
                try
                {
                    UserCode.Invoke(method, method.IsStatic ? null : _fixture, null);
                }
                catch (Exception ex)
                {
                    failure ??= new StageFailure("OneTimeTearDown", ex);
                }
            }
        }

        try
        {
            ObjectLifetime.Dispose(_fixture);
        }
        catch (Exception ex)
        {
            failure ??= new StageFailure("Disposing the fixture", ex);
        }

        return failure;
    }

    /// <summary>Compares a test's return value with its <c>ExpectedResult</c>.</summary>
    private static TestStepOutcome? CheckResult(RunnableTest test, MethodInfo method, object? result)
    {
        var resultType = ResultType(method.ReturnType);
        if (!test.HasExpectedResult)
        {
            return resultType is null
                ? null
                : new TestStepOutcome(TestOutcome.Errored, "Method has non-void return value, but no result is expected.");
        }

        if (resultType is null)
        {
            return new TestStepOutcome(TestOutcome.Errored, "Method has no return value, but an ExpectedResult is set.");
        }

        var expected = ArgumentBinder.Convert(test.ExpectedResult, resultType);
        return ValueComparer.AreEqual(expected, result)
            ? null
            : new TestStepOutcome(TestOutcome.Failed, $"  Expected: {TestNaming.Format(expected)}\n  But was:  {TestNaming.Format(result)}");
    }

    /// <summary>The type of value a method produces, looking through <c>Task&lt;T&gt;</c> and <c>ValueTask&lt;T&gt;</c>; <see langword="null"/> for none.</summary>
    private static Type? ResultType(Type returnType)
    {
        if (returnType == typeof(void) || returnType == typeof(Task) || returnType == typeof(ValueTask))
        {
            return null;
        }

        if (returnType.IsGenericType && (returnType.GetGenericTypeDefinition() == typeof(Task<>) || returnType.GetGenericTypeDefinition() == typeof(ValueTask<>)))
        {
            return returnType.GetGenericArguments()[0];
        }

        return returnType;
    }
}
