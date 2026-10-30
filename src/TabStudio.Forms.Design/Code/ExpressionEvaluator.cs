using System.Globalization;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TabStudio.Forms.Design.Code;

/// <summary>
/// Works out the values designer code assigns, such as <c>new Point(12, 12)</c>, <c>AnchorStyles.Top | AnchorStyles.Left</c>,
/// <c>Color.FromArgb(255, 128, 0)</c> or <c>new Font("Segoe UI", 12F, FontStyle.Bold)</c>, without compiling or running
/// anything: only constructors, constants, enum values and static members of the Forms layer's and .NET's types.
/// </summary>
/// <param name="components">Finds a component's live object by its field's name; <c>this</c> is the root.</param>
/// <remarks>The reflection it works values out with is shared with <see cref="VisualBasicExpressionEvaluator"/>.</remarks>
internal sealed class ExpressionEvaluator(Func<string, object?> components) : IValueEvaluator
{
    /// <summary>Why an expression has no value the designer can use.</summary>
    private sealed class NotEvaluableException : Exception
    {
    }

    /// <inheritdoc />
    public bool TryEvaluate(string code, out object? value)
    {
        ArgumentNullException.ThrowIfNull(code);
        return TryEvaluate(SyntaxFactory.ParseExpression(code), out value);
    }

    /// <inheritdoc />
    public bool TryEvaluateArguments(string arguments, out object?[] values)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var list = SyntaxFactory.ParseArgumentList("(" + arguments + ")");
        values = new object?[list.Arguments.Count];
        for (var i = 0; i < values.Length; i++)
        {
            if (!TryEvaluate(list.Arguments[i].Expression, out values[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Works out an expression's value.</summary>
    /// <param name="expression">The expression.</param>
    /// <param name="value">Its value.</param>
    /// <returns>Whether it has a value the designer can work out.</returns>
    public bool TryEvaluate(ExpressionSyntax expression, out object? value)
    {
        ArgumentNullException.ThrowIfNull(expression);
        try
        {
            value = Evaluate(expression);
            return true;
        }
        catch (Exception ex) when (IsFailure(ex))
        {
            value = null;
            return false;
        }
    }

    /// <summary>Whether an exception means an expression has no value the designer can work out, rather than a bug.</summary>
    /// <param name="exception">The exception.</param>
    internal static bool IsFailure(Exception exception) =>
        exception is NotEvaluableException or ArgumentException or InvalidCastException or FormatException or OverflowException
            or TargetInvocationException or MissingMethodException or MemberAccessException or NotSupportedException or InvalidOperationException;

    /// <summary>The exception that says an expression has no value the designer can work out.</summary>
    internal static Exception NotEvaluable() => new NotEvaluableException();

    /// <summary>Converts a value to a type, as an assignment in C# converts it.</summary>
    /// <param name="value">The value.</param>
    /// <param name="type">The type.</param>
    public static object? Convert(object? value, Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (value is null || type.IsInstanceOfType(value))
        {
            return value;
        }

        var target = Nullable.GetUnderlyingType(type) ?? type;
        if (target.IsEnum)
        {
            return Enum.ToObject(target, System.Convert.ToInt64(value, CultureInfo.InvariantCulture));
        }

        if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(target))
        {
            return System.Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
        }

        throw new InvalidCastException($"{value.GetType().Name} is not a {type.Name}.");
    }

    private static NotEvaluableException Unsupported() => new();

    private object? Evaluate(ExpressionSyntax expression) => expression switch
    {
        LiteralExpressionSyntax literal => literal.Token.Value,
        ParenthesizedExpressionSyntax parenthesized => Evaluate(parenthesized.Expression),
        PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.UnaryMinusExpression } minus => Negate(Evaluate(minus.Operand)),
        PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.UnaryPlusExpression } plus => Evaluate(plus.Operand),
        PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } not => Evaluate(not.Operand) is bool b ? !b : throw Unsupported(),
        BinaryExpressionSyntax binary => EvaluateBinary(binary),
        CastExpressionSyntax cast => Convert(Evaluate(cast.Expression), ResolveType(cast.Type)),
        ObjectCreationExpressionSyntax creation => CreateInstance(ResolveType(creation.Type), [.. (creation.ArgumentList?.Arguments ?? default).Select(a => Evaluate(a.Expression))]),
        ImplicitObjectCreationExpressionSyntax => throw Unsupported(),
        ArrayCreationExpressionSyntax array => CreateArray(ResolveType(array.Type.ElementType), array.Initializer?.Expressions.Select(Evaluate) ?? []),
        ThisExpressionSyntax => components("this"),
        IdentifierNameSyntax identifier => Identifier(identifier.Identifier.ValueText),
        MemberAccessExpressionSyntax member => MemberAccess(member),
        InvocationExpressionSyntax invocation => Invoke(invocation),
        TypeOfExpressionSyntax typeOf => ResolveType(typeOf.Type),
        DefaultExpressionSyntax defaultValue => ResolveType(defaultValue.Type) is { IsValueType: true } valueType ? Activator.CreateInstance(valueType) : null,
        _ => throw Unsupported(),
    };

    /// <summary>A number negated.</summary>
    /// <param name="value">The number.</param>
    internal static object Negate(object? value) => value switch
    {
        int i => -i,
        long l => -l,
        float f => -f,
        double d => -d,
        decimal m => -m,
        short s => -s,
        _ => throw Unsupported(),
    };

    /// <summary>Flags or numbers combined bit by bit, as <c>|</c> and Visual Basic's <c>Or</c> combine them.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    internal static object BitwiseOr(object? left, object? right)
    {
        if (left is null || right is null)
        {
            throw Unsupported();
        }

        var bits = System.Convert.ToInt64(left, CultureInfo.InvariantCulture) | System.Convert.ToInt64(right, CultureInfo.InvariantCulture);
        return left.GetType().IsEnum ? Enum.ToObject(left.GetType(), bits) : System.Convert.ChangeType(bits, left.GetType(), CultureInfo.InvariantCulture);
    }

    private object? EvaluateBinary(BinaryExpressionSyntax binary)
    {
        var left = Evaluate(binary.Left);
        var right = Evaluate(binary.Right);
        if (binary.IsKind(SyntaxKind.BitwiseOrExpression) && left is not null && right is not null)
        {
            return BitwiseOr(left, right);
        }

        if (binary.IsKind(SyntaxKind.AddExpression) && left is string first && right is string second)
        {
            return first + second;
        }

        throw Unsupported();
    }

    private object? Identifier(string name)
    {
        if (components(name) is { } component)
        {
            return component;
        }

        throw Unsupported();
    }

    private object? MemberAccess(MemberAccessExpressionSyntax member)
    {
        // this.button1, or a component's property, such as this.button1.Font
        if (member.Expression is ThisExpressionSyntax)
        {
            return components(member.Name.Identifier.ValueText) ?? throw Unsupported();
        }

        // A static member or an enum value: Color.Red, SystemColors.Control, AnchorStyles.Top, Point.Empty.
        if (TypeResolver.Resolve(member.Expression.ToString()) is { } type)
        {
            return StaticMember(type, member.Name.Identifier.ValueText);
        }

        var target = Evaluate(member.Expression) ?? throw Unsupported();
        var property = target.GetType().GetProperty(member.Name.Identifier.ValueText, BindingFlags.Public | BindingFlags.Instance) ?? throw Unsupported();
        return property.GetValue(target);
    }

    /// <summary>A static property's or field's value, or an enum's value, by name.</summary>
    /// <param name="type">The type.</param>
    /// <param name="name">The member's name.</param>
    internal static object? StaticMember(Type type, string name)
    {
        if (type.IsEnum)
        {
            return Enum.Parse(type, name);
        }

        if (type.GetProperty(name, BindingFlags.Public | BindingFlags.Static) is { } property)
        {
            return property.GetValue(null);
        }

        if (type.GetField(name, BindingFlags.Public | BindingFlags.Static) is { } field)
        {
            return field.GetValue(null);
        }

        throw Unsupported();
    }

    private object? Invoke(InvocationExpressionSyntax invocation)
    {
        // Only static factory methods of known types, such as Color.FromArgb or Color.FromName.
        if (invocation.Expression is not MemberAccessExpressionSyntax { Expression: var typeSyntax, Name: var name }
            || TypeResolver.Resolve(typeSyntax.ToString()) is not { } type
            || type.Namespace is not ("System.Drawing" or "System.Windows.Forms" or "System"))
        {
            throw Unsupported();
        }

        return InvokeStatic(type, name.Identifier.ValueText, [.. invocation.ArgumentList.Arguments.Select(a => Evaluate(a.Expression))]);
    }

    /// <summary>Calls a static factory method, such as <c>Color.FromArgb</c>, with arguments it takes.</summary>
    /// <param name="type">The type.</param>
    /// <param name="name">The method's name.</param>
    /// <param name="arguments">The arguments' values.</param>
    internal static object? InvokeStatic(Type type, string name, object?[] arguments)
    {
        var method = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == name && m.GetParameters().Length == arguments.Length)
            .Select(m => (Method: m, Arguments: TryConvert(arguments, m.GetParameters())))
            .FirstOrDefault(m => m.Arguments is not null);
        return method.Method is null ? throw Unsupported() : method.Method.Invoke(null, method.Arguments);
    }

    /// <summary>Creates an object with the constructor its arguments fit, as <c>new Point(12, 12)</c> does.</summary>
    /// <param name="type">The type.</param>
    /// <param name="arguments">The arguments' values.</param>
    internal static object CreateInstance(Type type, object?[] arguments)
    {
        foreach (var constructor in type.GetConstructors().OrderBy(c => c.GetParameters().Length))
        {
            if (constructor.GetParameters().Length == arguments.Length && TryConvert(arguments, constructor.GetParameters()) is { } converted)
            {
                return constructor.Invoke(converted);
            }
        }

        throw Unsupported();
    }

    /// <summary>Creates an array of items, as <c>new int[] { 1, 0, 0, 0 }</c> does.</summary>
    /// <param name="elementType">The items' type.</param>
    /// <param name="values">The items' values.</param>
    internal static Array CreateArray(Type elementType, IEnumerable<object?> values)
    {
        var items = values.Select(v => Convert(v, elementType)).ToArray();
        var array = Array.CreateInstance(elementType, items.Length);
        for (var i = 0; i < items.Length; i++)
        {
            array.SetValue(items[i], i);
        }

        return array;
    }

    private static object?[]? TryConvert(object?[] arguments, ParameterInfo[] parameters)
    {
        var converted = new object?[arguments.Length];
        for (var i = 0; i < arguments.Length; i++)
        {
            var type = parameters[i].ParameterType;
            var argument = arguments[i];
            if (argument is null)
            {
                if (type.IsValueType && Nullable.GetUnderlyingType(type) is null)
                {
                    return null;
                }

                continue;
            }

            if (type.IsInstanceOfType(argument))
            {
                converted[i] = argument;
            }
            else if (IsNumeric(argument) && (IsNumeric(type) || type.IsEnum) && IsWidening(argument, type))
            {
                converted[i] = Convert(argument, type);
            }
            else
            {
                return null;
            }
        }

        return converted;
    }

    private static bool IsNumeric(object value) => value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;

    private static bool IsNumeric(Type type) => type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort)
        || type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong)
        || type == typeof(float) || type == typeof(double) || type == typeof(decimal);

    /// <summary>
    /// Whether a numeric argument converts to a parameter as C# allows: an integer to any number, as constants do,
    /// and a float or double only to a wider floating type.
    /// </summary>
    private static bool IsWidening(object argument, Type type) => argument switch
    {
        float => type == typeof(float) || type == typeof(double),
        double => type == typeof(double),
        decimal => type == typeof(decimal),
        _ => true,
    };

    private static Type ResolveType(TypeSyntax syntax) => TypeResolver.Resolve(syntax.ToString()) ?? throw Unsupported();
}
