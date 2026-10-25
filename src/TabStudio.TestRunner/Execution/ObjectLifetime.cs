using System.Reflection;
using TabStudio.TestRunner.Reflection;

namespace TabStudio.TestRunner.Execution;

/// <summary>
/// Creates, initializes and disposes test classes and fixtures, including xUnit's <c>IAsyncLifetime</c>.
/// </summary>
internal static class ObjectLifetime
{
    /// <summary>
    /// Creates an object with its public parameterless constructor, or, failing that, its only constructor with
    /// <see langword="null"/> for every parameter (MSTest passes a <c>TestContext</c> there, which TabStudio
    /// does not provide).
    /// </summary>
    /// <param name="type">The type to create.</param>
    /// <exception cref="InvalidOperationException">The type has no usable constructor.</exception>
    public static object Create(Type type)
    {
        if (type.GetConstructor(Type.EmptyTypes) is { } parameterless)
        {
            return parameterless.Invoke(BindingFlags.DoNotWrapExceptions, binder: null, parameters: [], culture: null);
        }

        var constructors = type.GetConstructors();
        if (constructors.Length == 1)
        {
            var parameters = new object?[constructors[0].GetParameters().Length];
            return constructors[0].Invoke(BindingFlags.DoNotWrapExceptions, binder: null, parameters, culture: null);
        }

        throw new InvalidOperationException($"'{type.FullName}' has no public parameterless constructor.");
    }

    /// <summary>Calls <c>IAsyncLifetime.InitializeAsync</c> when the object implements xUnit's interface.</summary>
    /// <param name="instance">The test class or fixture.</param>
    public static void Initialize(object instance)
    {
        if (AsyncLifetimeMethod(instance, "InitializeAsync") is { } initialize)
        {
            UserCode.Invoke(initialize, instance, null);
        }
    }

    /// <summary>
    /// Disposes an object the way xUnit, NUnit and MSTest do: <c>IAsyncLifetime.DisposeAsync</c>, then
    /// <see cref="IAsyncDisposable"/> or, when it is not implemented, <see cref="IDisposable"/>.
    /// </summary>
    /// <param name="instance">The object, or <see langword="null"/>.</param>
    public static void Dispose(object? instance)
    {
        if (instance is null)
        {
            return;
        }

        var lifetimeDispose = AsyncLifetimeMethod(instance, "DisposeAsync");
        if (lifetimeDispose is not null && !IsAsyncDisposableImplementation(instance, lifetimeDispose))
        {
            UserCode.Invoke(lifetimeDispose, instance, null);
        }

        switch (instance)
        {
            case IAsyncDisposable asyncDisposable:
                asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }
    }

    /// <summary>Finds the class's implementation of a method of <c>Xunit.IAsyncLifetime</c>.</summary>
    private static MethodInfo? AsyncLifetimeMethod(object instance, string name)
    {
        var type = instance.GetType();
        var lifetime = TypeMatching.FindInterface(type, FrameworkNames.XunitAsyncLifetime);
        if (lifetime is null)
        {
            return null;
        }

        var map = type.GetInterfaceMap(lifetime);
        for (var i = 0; i < map.InterfaceMethods.Length; i++)
        {
            if (map.InterfaceMethods[i].Name == name)
            {
                return map.TargetMethods[i];
            }
        }

        // xUnit v3's IAsyncLifetime inherits DisposeAsync from IAsyncDisposable.
        return null;
    }

    private static bool IsAsyncDisposableImplementation(object instance, MethodInfo method)
    {
        if (instance is not IAsyncDisposable)
        {
            return false;
        }

        var map = instance.GetType().GetInterfaceMap(typeof(IAsyncDisposable));
        return map.TargetMethods.Contains(method);
    }
}
