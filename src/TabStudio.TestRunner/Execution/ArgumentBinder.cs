using System.Globalization;
using System.Reflection;

namespace TabStudio.TestRunner.Execution;

/// <summary>
/// Turns a data row into the arguments of a test method, converting values the way the frameworks do.
/// </summary>
/// <remarks>
/// Attribute arguments can only be constants, so frameworks convert them: <c>[InlineData(1)]</c> feeds a
/// <c>long</c> or <c>double</c> parameter, <c>[TestCase("1.5")]</c> a <c>decimal</c>, a string a <c>Guid</c>,
/// <c>DateTime</c> or enum, and an <c>int</c> an enum. Missing optional arguments take their defaults, extra
/// arguments fill a <c>params</c> array, and the type arguments of a generic test method are inferred from
/// the values.
/// </remarks>
internal static class ArgumentBinder
{
    /// <summary>Binds a row to a method.</summary>
    /// <param name="method">The test method, which may be a generic method definition.</param>
    /// <param name="values">The row's values.</param>
    /// <param name="bound">The method to call: <paramref name="method"/>, or its inferred generic instantiation.</param>
    /// <returns>The converted arguments.</returns>
    /// <exception cref="InvalidOperationException">The row does not fit the method's parameters.</exception>
    public static object?[] Bind(MethodInfo method, IReadOnlyList<object?> values, out MethodInfo bound)
    {
        bound = method.IsGenericMethodDefinition ? InferGenericArguments(method, values) : method;
        var parameters = bound.GetParameters();
        var arguments = new object?[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            var isParams = i == parameters.Length - 1 && parameter.IsDefined(typeof(ParamArrayAttribute), inherit: false);
            if (isParams && !(values.Count == parameters.Length && (values[i] is null || parameter.ParameterType.IsInstanceOfType(values[i]))))
            {
                var elementType = parameter.ParameterType.GetElementType() ?? typeof(object);
                var rest = Array.CreateInstance(elementType, Math.Max(values.Count - i, 0));
                for (var j = i; j < values.Count; j++)
                {
                    rest.SetValue(Convert(values[j], elementType), j - i);
                }

                arguments[i] = rest;
                return arguments;
            }

            if (i < values.Count)
            {
                arguments[i] = Convert(values[i], parameter.ParameterType);
            }
            else if (parameter.IsOptional)
            {
                arguments[i] = Type.Missing;
            }
            else
            {
                throw new InvalidOperationException(
                    $"The test method expected {parameters.Length} parameter value{Plural(parameters.Length)}, but {values.Count} parameter value{Plural(values.Count)} {(values.Count == 1 ? "was" : "were")} provided.");
            }
        }

        if (values.Count > parameters.Length)
        {
            throw new InvalidOperationException(
                $"The test method expected {parameters.Length} parameter value{Plural(parameters.Length)}, but {values.Count} parameter value{Plural(values.Count)} {(values.Count == 1 ? "was" : "were")} provided.");
        }

        return arguments;
    }

    /// <summary>Converts a value to a parameter type, or returns it unchanged when no conversion applies.</summary>
    /// <param name="value">The value from the data row.</param>
    /// <param name="target">The parameter type.</param>
    public static object? Convert(object? value, Type target)
    {
        if (value is null)
        {
            return null;
        }

        if (target.IsByRef)
        {
            target = target.GetElementType() ?? target;
        }

        var type = Nullable.GetUnderlyingType(target) ?? target;
        if (type == typeof(object) || type.IsInstanceOfType(value))
        {
            return value;
        }

        try
        {
            return ConvertCore(value, type);
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException or ArgumentException or TargetInvocationException)
        {
            // Leave the value as it is: invoking the method reports the mismatch with the parameter it is for.
            return value;
        }
    }

    private static object? ConvertCore(object value, Type type)
    {
        if (type.IsEnum)
        {
            return value is string name ? Enum.Parse(type, name) : Enum.ToObject(type, value);
        }

        if (value is string text)
        {
            if (type == typeof(Guid))
            {
                return Guid.Parse(text, CultureInfo.InvariantCulture);
            }

            if (type == typeof(DateTime))
            {
                return DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            }

            if (type == typeof(DateTimeOffset))
            {
                return DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.None);
            }

            if (type == typeof(TimeSpan))
            {
                return TimeSpan.Parse(text, CultureInfo.InvariantCulture);
            }

            if (type == typeof(decimal))
            {
                return decimal.Parse(text, NumberStyles.Number | NumberStyles.AllowExponent, CultureInfo.InvariantCulture);
            }

            if (type == typeof(Uri))
            {
                return new Uri(text, UriKind.RelativeOrAbsolute);
            }
        }

        if (value is Array source && type.IsArray && type.GetElementType() is { } elementType)
        {
            var converted = Array.CreateInstance(elementType, source.Length);
            for (var i = 0; i < source.Length; i++)
            {
                converted.SetValue(Convert(source.GetValue(i), elementType), i);
            }

            return converted;
        }

        if (IsNumeric(type) && value is IConvertible && (IsNumeric(value.GetType()) || value is char))
        {
            return System.Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
        }

        if (FindConversionOperator(type, value.GetType()) is { } conversion)
        {
            return conversion.Invoke(null, [value]);
        }

        return value;
    }

    private static bool IsNumeric(Type type) =>
        type == typeof(char) || type == typeof(decimal) || (type.IsPrimitive && type != typeof(bool) && type != typeof(nint) && type != typeof(nuint));

    private static MethodInfo? FindConversionOperator(Type target, Type source)
    {
        foreach (var declaring in new[] { target, source })
        {
            foreach (var method in declaring.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if ((method.Name == "op_Implicit" || method.Name == "op_Explicit") &&
                    method.ReturnType == target &&
                    method.GetParameters() is [var parameter] &&
                    parameter.ParameterType.IsAssignableFrom(source))
                {
                    return method;
                }
            }
        }

        return null;
    }

    private static MethodInfo InferGenericArguments(MethodInfo method, IReadOnlyList<object?> values)
    {
        var genericArguments = method.GetGenericArguments();
        var inferred = new Type[genericArguments.Length];
        var parameters = method.GetParameters();
        for (var g = 0; g < genericArguments.Length; g++)
        {
            inferred[g] = typeof(object);
            for (var p = 0; p < parameters.Length && p < values.Count; p++)
            {
                if (parameters[p].ParameterType == genericArguments[g] && values[p] is { } value)
                {
                    inferred[g] = value.GetType();
                    break;
                }
            }
        }

        try
        {
            return method.MakeGenericMethod(inferred);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException($"TabStudio could not infer the type arguments of generic test method '{method.Name}' from its data.", ex);
        }
    }

    private static string Plural(int count) => count == 1 ? string.Empty : "s";
}
