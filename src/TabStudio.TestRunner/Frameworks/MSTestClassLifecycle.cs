using System.Reflection;
using TabStudio.TestRunner.Execution;
using TabStudio.TestRunner.Reflection;

namespace TabStudio.TestRunner.Frameworks;

/// <summary>
/// Runs an MSTest class: <c>[AssemblyInitialize]</c> once per run, <c>[ClassInitialize]</c> and
/// <c>[ClassCleanup]</c> around the class, and a new instance per test with <c>[TestInitialize]</c> and
/// <c>[TestCleanup]</c> around it.
/// </summary>
/// <remarks>
/// MSTest passes a <c>TestContext</c> to initializers and constructors that ask for one. TabStudio does not
/// implement it, so those parameters receive <see langword="null"/>.
/// </remarks>
internal sealed class MSTestClassLifecycle : ClassLifecycle
{
    private readonly Type _testClass;
    private readonly RunScope _scope;
    private readonly List<List<MethodInfo>> _initializers;
    private readonly List<List<MethodInfo>> _cleanups;

    /// <summary>Prepares to run a test class.</summary>
    /// <param name="testClass">The test class.</param>
    /// <param name="scope">State shared by the whole run.</param>
    public MSTestClassLifecycle(Type testClass, RunScope scope)
    {
        _testClass = testClass;
        _scope = scope;
        var hierarchy = TypeMatching.HierarchyFromBase(testClass);
        _initializers = LifecycleMethods.ByLevel(hierarchy, FrameworkNames.MSTestTestInitialize);
        _cleanups = LifecycleMethods.ByLevel(hierarchy, FrameworkNames.MSTestTestCleanup);
    }

    /// <inheritdoc />
    public override StageFailure? SetUpClass()
    {
        try
        {
            _scope.GetOrCreate(("mstest assembly", _testClass.Assembly), () => InitializeAssembly(_testClass.Assembly));
        }
        catch (Exception ex)
        {
            return new StageFailure("AssemblyInitialize", ex);
        }

        foreach (var method in LifecycleMethods.Marked(_testClass, FrameworkNames.MSTestClassInitialize))
        {
            try
            {
                UserCode.Invoke(method, null, NullArguments(method));
            }
            catch (Exception ex)
            {
                return new StageFailure("ClassInitialize", ex);
            }
        }

        return null;
    }

    /// <inheritdoc />
    public override TestStepOutcome RunTest(RunnableTest test, TestOutputSink sink)
    {
        var failures = new FailureCollector();
        object? instance = null;
        var levelsDone = 0;
        try
        {
            instance = ObjectLifetime.Create(_testClass);
            foreach (var level in _initializers)
            {
                foreach (var method in level)
                {
                    UserCode.Invoke(method, instance, NullArguments(method));
                }

                levelsDone++;
            }
        }
        catch (Exception ex)
        {
            failures.Add(ex, instance is null ? "Creating the test class" : "TestInitialize");
        }

        if (!failures.HasFailed)
        {
            RunMethod(test, instance, failures);
        }

        for (var level = levelsDone - 1; level >= 0; level--)
        {
            foreach (var method in _cleanups[level])
            {
                try
                {
                    UserCode.Invoke(method, instance, NullArguments(method));
                }
                catch (Exception ex)
                {
                    failures.Add(ex, "TestCleanup");
                }
            }
        }

        try
        {
            ObjectLifetime.Dispose(instance);
        }
        catch (Exception ex)
        {
            failures.Add(ex, "Dispose");
        }

        return failures.ToOutcome();
    }

    /// <inheritdoc />
    public override StageFailure? TearDownClass()
    {
        StageFailure? failure = null;
        foreach (var method in LifecycleMethods.Marked(_testClass, FrameworkNames.MSTestClassCleanup))
        {
            try
            {
                UserCode.Invoke(method, null, NullArguments(method));
            }
            catch (Exception ex)
            {
                failure ??= new StageFailure("ClassCleanup", ex);
            }
        }

        return failure;
    }

    /// <summary>Runs the test method, honoring <c>[ExpectedException]</c>.</summary>
    private static void RunMethod(RunnableTest test, object? instance, FailureCollector failures)
    {
        var expected = test.ExpectedException;
        try
        {
            var arguments = ArgumentBinder.Bind(test.Method, test.Arguments, out var method);
            UserCode.Invoke(method, instance, arguments);
            if (expected is not null)
            {
                failures.Add(new TestStepOutcome(TestOutcome.Failed, $"Test method {test.Case.FullyQualifiedName} did not throw expected exception {expected.FullName}."));
            }
        }
        catch (Exception ex) when (expected is not null)
        {
            var actual = OutcomeClassifier.Unwrap(ex);
            var matches = test.AllowDerivedExceptions ? expected.IsInstanceOfType(actual) : actual.GetType() == expected;
            if (matches)
            {
                return;
            }

            if (OutcomeClassifier.IsAssertionFailure(actual.GetType()))
            {
                failures.Add(actual);
            }
            else
            {
                failures.Add(new TestStepOutcome(
                    TestOutcome.Failed,
                    $"Test method {test.Case.FullyQualifiedName} threw exception {actual.GetType().FullName}, but exception {expected.FullName} was expected. Exception message: {actual.Message}",
                    actual));
            }
        }
        catch (Exception ex)
        {
            failures.Add(ex);
        }
    }

    /// <summary>Runs <c>[AssemblyInitialize]</c> and schedules <c>[AssemblyCleanup]</c> for the end of the run.</summary>
    private Assembly InitializeAssembly(Assembly assembly)
    {
        var testClasses = AssemblyTypes.Of(assembly)
            .Where(t => AttributeReader.Of(t).Exists(a => a.Is(FrameworkNames.MSTestTestClass)))
            .ToList();

        var cleanups = testClasses.SelectMany(t => LifecycleMethods.Marked(t, FrameworkNames.MSTestAssemblyCleanup)).ToList();
        if (cleanups.Count > 0)
        {
            _scope.AddCleanup($"AssemblyCleanup of '{assembly.GetName().Name}'", () =>
            {
                foreach (var cleanup in cleanups)
                {
                    UserCode.Invoke(cleanup, null, NullArguments(cleanup));
                }
            });
        }

        foreach (var initializer in testClasses.SelectMany(t => LifecycleMethods.Marked(t, FrameworkNames.MSTestAssemblyInitialize)))
        {
            UserCode.Invoke(initializer, null, NullArguments(initializer));
        }

        return assembly;
    }

    /// <summary><see langword="null"/> for every parameter, such as the <c>TestContext</c> of an initializer.</summary>
    private static object?[] NullArguments(MethodInfo method) => new object?[method.GetParameters().Length];
}
