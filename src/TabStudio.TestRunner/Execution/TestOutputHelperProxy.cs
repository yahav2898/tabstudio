using System.Globalization;
using System.Reflection;

namespace TabStudio.TestRunner.Execution;

// Not sealed, and seemingly never instantiated: DispatchProxy derives a class from it and creates that at run time.
#pragma warning disable CA1852, CA1812

/// <summary>
/// Implements xUnit's <c>ITestOutputHelper</c> for a test class constructor that asks for one, without
/// referencing xUnit.
/// </summary>
/// <remarks>
/// <para>
/// The interface type is the one in the test's own load context, taken from the constructor parameter, and
/// <see cref="DispatchProxy.Create(Type, Type)"/> generates a class that implements it and forwards every call to
/// <see cref="Invoke"/>. That works for xUnit v2 (<c>Xunit.Abstractions.ITestOutputHelper</c> with two
/// <c>WriteLine</c> overloads) and v3 (<c>Xunit.ITestOutputHelper</c>, which adds <c>Write</c> and
/// <c>Output</c>), because the calls are dispatched by name.
/// </para>
/// <para>
/// <see cref="DispatchProxy"/> was chosen over compiling an adapter with Roslyn because it costs no compilation
/// and needs no reference to the framework's files. It does need the runtime to generate code, which CoreCLR
/// and Mono on Android both do; and its generated assembly is not collectible, which is one reason the test
/// load context is not either.
/// </para>
/// </remarks>
internal class TestOutputHelperProxy : DispatchProxy
{
    private TestOutputSink? _sink;

    /// <summary>Creates an object that implements <paramref name="interfaceType"/> and writes to a test's output.</summary>
    /// <param name="interfaceType">The <c>ITestOutputHelper</c> interface from the test's load context.</param>
    /// <param name="sink">The test's output.</param>
    public static object CreateFor(Type interfaceType, TestOutputSink sink)
    {
        var proxy = DispatchProxy.Create(interfaceType, typeof(TestOutputHelperProxy));
        ((TestOutputHelperProxy)proxy)._sink = sink;
        return proxy;
    }

    /// <inheritdoc />
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        switch (targetMethod?.Name)
        {
            case "WriteLine":
                _sink?.Write(Format(args) + "\n");
                return null;
            case "Write":
                _sink?.Write(Format(args));
                return null;
            case "get_Output":
                return _sink?.Text ?? string.Empty;
            default:
                throw new NotSupportedException($"TabStudio's test output helper does not implement '{targetMethod?.Name}'.");
        }
    }

    private static string Format(object?[]? args) => args switch
    {
        [string message] => message,
        [string format, object?[] values] => string.Format(CultureInfo.CurrentCulture, format, values),
        [var value, ..] => value?.ToString() ?? string.Empty,
        _ => string.Empty,
    };
}
