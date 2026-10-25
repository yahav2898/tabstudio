using System.Reflection;
using System.Runtime.CompilerServices;

namespace TabStudio.TestRunner.Execution;

/// <summary>
/// Calls user methods by reflection and waits for whatever they return, so <c>async Task</c>,
/// <c>ValueTask</c> and other awaitable tests, setups and fixtures all run to completion on the test's thread.
/// </summary>
internal static class UserCode
{
    /// <summary>
    /// Invokes a method and waits for the task it returns, if any. Exceptions surface unwrapped, with their
    /// original stack traces.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <param name="target">The instance, or <see langword="null"/> for a static method.</param>
    /// <param name="arguments">The arguments, already converted to the parameter types.</param>
    /// <returns>The method's result, or the result of the task it returned; <see langword="null"/> for <c>void</c> and <c>Task</c>.</returns>
    /// <exception cref="InvalidOperationException">The method is <c>async void</c>, which cannot be awaited.</exception>
    public static object? Invoke(MethodInfo method, object? target, object?[]? arguments)
    {
        if (method.ReturnType == typeof(void) && method.IsDefined(typeof(AsyncStateMachineAttribute), inherit: false))
        {
            throw new InvalidOperationException($"'{method.Name}' is async void, so TabStudio cannot wait for it. Make it return Task instead.");
        }

        var returned = method.Invoke(target, BindingFlags.DoNotWrapExceptions, binder: null, arguments, culture: null);
        return Wait(returned, method.ReturnType);
    }

    /// <summary>Waits for an awaitable value and returns its result.</summary>
    /// <param name="value">What a method returned.</param>
    /// <param name="declaredType">The method's declared return type, which tells <c>Task</c> apart from <c>Task&lt;T&gt;</c>.</param>
    public static object? Wait(object? value, Type declaredType)
    {
        switch (value)
        {
            case null:
                return null;
            case Task task:
                task.GetAwaiter().GetResult();
                return ResultOf(task, declaredType);
            case ValueTask valueTask:
                valueTask.AsTask().GetAwaiter().GetResult();
                return null;
        }

        var type = value.GetType();
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueTask<>))
        {
            var task = (Task)type.GetMethod(nameof(ValueTask<int>.AsTask))!.Invoke(value, null)!;
            task.GetAwaiter().GetResult();
            return task.GetType().GetProperty(nameof(Task<int>.Result))!.GetValue(task);
        }

        return type.GetMethod("GetAwaiter", Type.EmptyTypes) is { } getAwaiter
            ? WaitForAwaiter(getAwaiter.Invoke(value, null))
            : value;
    }

    private static object? ResultOf(Task task, Type declaredType)
    {
        if (!declaredType.IsGenericType || declaredType.GetGenericTypeDefinition() != typeof(Task<>))
        {
            return null;
        }

        return declaredType.GetProperty(nameof(Task<int>.Result))!.GetValue(task);
    }

    /// <summary>Waits for a custom awaiter: anything with <c>IsCompleted</c>, <c>OnCompleted</c> and <c>GetResult</c>.</summary>
    private static object? WaitForAwaiter(object? awaiter)
    {
        if (awaiter is null)
        {
            return null;
        }

        var type = awaiter.GetType();
        var isCompleted = type.GetProperty("IsCompleted")?.GetValue(awaiter) as bool? ?? true;
        if (!isCompleted && awaiter is INotifyCompletion notify)
        {
            using var done = new ManualResetEventSlim();
            notify.OnCompleted(done.Set);
            done.Wait();
        }

        return type.GetMethod("GetResult", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes) is { } getResult
            ? InvokeUnwrapped(getResult, awaiter)
            : null;
    }

    private static object? InvokeUnwrapped(MethodInfo method, object target) =>
        method.Invoke(target, BindingFlags.DoNotWrapExceptions, binder: null, parameters: null, culture: null);
}
