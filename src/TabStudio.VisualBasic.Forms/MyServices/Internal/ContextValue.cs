using System.ComponentModel;

namespace Microsoft.VisualBasic.MyServices.Internal;

/// <summary>
/// A value kept per thread, which the My namespace of a class library uses for <c>My.Application</c>,
/// <c>My.Computer</c> and <c>My.User</c>.
/// </summary>
/// <typeparam name="T">The value's type.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public class ContextValue<T>
{
    private static readonly ThreadLocal<Dictionary<string, object?>> Values = new(() => new Dictionary<string, object?>(StringComparer.Ordinal));
    private readonly string _contextKey = Guid.NewGuid().ToString();

    /// <summary>Creates the value.</summary>
    public ContextValue()
    {
    }

    /// <summary>The value on the current thread; the type's default until it is set there.</summary>
    public T Value
    {
        get => Values.Value!.TryGetValue(_contextKey, out var value) && value is T typed ? typed : default!;
        set => Values.Value![_contextKey] = value;
    }
}
