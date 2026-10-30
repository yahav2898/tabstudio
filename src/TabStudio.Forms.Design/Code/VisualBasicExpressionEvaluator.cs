using System.Globalization;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace TabStudio.Forms.Design.Code;

/// <summary>
/// Works out the values Visual Basic designer code assigns, such as <c>New Point(12, 12)</c>,
/// <c>AnchorStyles.Top Or AnchorStyles.Left</c>, <c>CType(CType(0, Byte), Integer)</c>, <c>8.25!</c> or
/// <c>"Line 1" &amp; Global.Microsoft.VisualBasic.ChrW(13)</c>, without compiling or running anything, with the
/// same reflection as <see cref="ExpressionEvaluator"/>.
/// </summary>
/// <param name="components">Finds a component's live object by its field's name; <c>this</c> is the root.</param>
internal sealed class VisualBasicExpressionEvaluator(Func<string, object?> components) : IValueEvaluator
{
    /// <summary>Visual Basic's conversion functions, such as <c>CInt</c>, and the types they convert to.</summary>
    private static readonly Dictionary<string, Type> Conversions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CBool"] = typeof(bool), ["CByte"] = typeof(byte), ["CChar"] = typeof(char), ["CDate"] = typeof(DateTime),
        ["CDbl"] = typeof(double), ["CDec"] = typeof(decimal), ["CInt"] = typeof(int), ["CLng"] = typeof(long),
        ["CObj"] = typeof(object), ["CSByte"] = typeof(sbyte), ["CShort"] = typeof(short), ["CSng"] = typeof(float),
        ["CStr"] = typeof(string), ["CUInt"] = typeof(uint), ["CULng"] = typeof(ulong), ["CUShort"] = typeof(ushort),
    };

    /// <summary>Visual Basic's constants for control characters, as <c>vbCrLf</c> and <c>ControlChars.CrLf</c> name them.</summary>
    private static readonly Dictionary<string, string> ControlCharacters = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CrLf"] = "\r\n", ["NewLine"] = "\r\n", ["Cr"] = "\r", ["Lf"] = "\n", ["Tab"] = "\t", ["Quote"] = "\"",
        ["NullChar"] = "\0", ["Back"] = "\b", ["FormFeed"] = "\f", ["VerticalTab"] = "\v",
    };

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
        if (SyntaxFactory.ParseExpression("F(" + arguments + ")") is not InvocationExpressionSyntax { ArgumentList: { } list })
        {
            values = [];
            return false;
        }

        values = new object?[list.Arguments.Count];
        for (var i = 0; i < values.Length; i++)
        {
            if (list.Arguments[i] is not SimpleArgumentSyntax { NameColonEquals: null } argument || !TryEvaluate(argument.Expression, out values[i]))
            {
                return false;
            }
        }

        return true;
    }

    private bool TryEvaluate(ExpressionSyntax expression, out object? value)
    {
        try
        {
            value = Evaluate(expression);
            return true;
        }
        catch (Exception ex) when (ExpressionEvaluator.IsFailure(ex))
        {
            value = null;
            return false;
        }
    }

    private static Exception Unsupported() => ExpressionEvaluator.NotEvaluable();

    private object? Evaluate(ExpressionSyntax expression) => expression switch
    {
        LiteralExpressionSyntax literal => Literal(literal),
        ParenthesizedExpressionSyntax parenthesized => Evaluate(parenthesized.Expression),
        UnaryExpressionSyntax unary => EvaluateUnary(unary),
        BinaryExpressionSyntax binary => EvaluateBinary(binary),
        CastExpressionSyntax cast => ExpressionEvaluator.Convert(Evaluate(cast.Expression), ResolveType(cast.Type)),
        PredefinedCastExpressionSyntax cast => Conversions.TryGetValue(cast.Keyword.ValueText, out var target)
            ? ExpressionEvaluator.Convert(Evaluate(cast.Expression), target)
            : throw Unsupported(),
        ObjectCreationExpressionSyntax creation => ExpressionEvaluator.CreateInstance(ResolveType(creation.Type), Arguments(creation.ArgumentList)),
        ArrayCreationExpressionSyntax array when array.RankSpecifiers.Count <= (array.ArrayBounds is null ? 1 : 0) =>
            ExpressionEvaluator.CreateArray(ResolveType(array.Type), array.Initializer?.Initializers.Select(Evaluate) ?? []),
        MeExpressionSyntax or MyBaseExpressionSyntax or MyClassExpressionSyntax => components("this"),
        IdentifierNameSyntax identifier => Identifier(identifier.Identifier.ValueText),
        MemberAccessExpressionSyntax member => MemberAccess(member),
        InvocationExpressionSyntax invocation => Invoke(invocation),
        GetTypeExpressionSyntax getType => ResolveType(getType.Type),
        _ => throw Unsupported(),
    };

    private static object? Literal(LiteralExpressionSyntax literal)
    {
        if (literal.IsKind(SyntaxKind.TrueLiteralExpression))
        {
            return true;
        }

        if (literal.IsKind(SyntaxKind.FalseLiteralExpression))
        {
            return false;
        }

        return literal.IsKind(SyntaxKind.NothingLiteralExpression) ? null : literal.Token.Value;
    }

    private object? EvaluateUnary(UnaryExpressionSyntax unary)
    {
        if (unary.IsKind(SyntaxKind.UnaryMinusExpression))
        {
            return ExpressionEvaluator.Negate(Evaluate(unary.Operand));
        }

        if (unary.IsKind(SyntaxKind.UnaryPlusExpression))
        {
            return Evaluate(unary.Operand);
        }

        if (unary.IsKind(SyntaxKind.NotExpression) && Evaluate(unary.Operand) is bool flag)
        {
            return !flag;
        }

        throw Unsupported();
    }

    private object? EvaluateBinary(BinaryExpressionSyntax binary)
    {
        var left = Evaluate(binary.Left);
        var right = Evaluate(binary.Right);
        if (binary.IsKind(SyntaxKind.OrExpression) && left is not bool && right is not bool)
        {
            return ExpressionEvaluator.BitwiseOr(left, right);
        }

        if (binary.IsKind(SyntaxKind.ConcatenateExpression) || (binary.IsKind(SyntaxKind.AddExpression) && (left is string || right is string)))
        {
            return Text(left) + Text(right);
        }

        throw Unsupported();
    }

    private static string Text(object? value) => value switch
    {
        null => string.Empty,
        string text => text,
        char character => new string(character, 1),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    private object? Identifier(string name)
    {
        if (components(name) is { } component)
        {
            return component;
        }

        // vbCrLf, vbTab and the other control characters' constants.
        if (name.StartsWith("vb", StringComparison.OrdinalIgnoreCase) && ControlCharacters.TryGetValue(name[2..], out var text))
        {
            return text;
        }

        throw Unsupported();
    }

    private object? MemberAccess(MemberAccessExpressionSyntax member)
    {
        var name = member.Name.Identifier.ValueText;
        if (member.Expression is not { } container)
        {
            throw Unsupported();
        }

        // Me.Button1, or a component's property, such as Me.Button1.Font
        if (container is MeExpressionSyntax or MyClassExpressionSyntax or MyBaseExpressionSyntax)
        {
            return components(name) ?? throw Unsupported();
        }

        // ControlChars.CrLf
        if (IsVisualBasicModule(container, "ControlChars") && ControlCharacters.TryGetValue(name, out var text))
        {
            return text;
        }

        // A static member or an enum value: Color.Red, SystemColors.Control, AnchorStyles.Top, Point.Empty.
        if (TypeResolver.Resolve(container.ToString()) is { } type)
        {
            return ExpressionEvaluator.StaticMember(type, name);
        }

        var target = Evaluate(container) ?? throw Unsupported();
        var property = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase) ?? throw Unsupported();
        return property.GetValue(target);
    }

    private object? Invoke(InvocationExpressionSyntax invocation)
    {
        ExpressionSyntax? receiver;
        string name;
        switch (invocation.Expression)
        {
            case MemberAccessExpressionSyntax { Expression: { } container, Name: var member }:
                receiver = container;
                name = member.Identifier.ValueText;
                break;
            case IdentifierNameSyntax identifier:
                receiver = null;
                name = identifier.Identifier.ValueText;
                break;
            default:
                throw Unsupported();
        }

        var arguments = Arguments(invocation.ArgumentList);

        // ChrW(13), as Visual Studio writes a string's line breaks: Global.Microsoft.VisualBasic.ChrW(13).
        if ((string.Equals(name, "ChrW", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "Chr", StringComparison.OrdinalIgnoreCase))
            && arguments.Length == 1 && (receiver is null || IsVisualBasicModule(receiver, "Strings") || IsVisualBasicNamespace(receiver)))
        {
            return (char)Convert.ToInt32(arguments[0], CultureInfo.InvariantCulture);
        }

        // Only static factory methods of known types, such as Color.FromArgb or Color.FromName.
        if (receiver is null || TypeResolver.Resolve(receiver.ToString()) is not { } type || type.Namespace is not ("System.Drawing" or "System.Windows.Forms" or "System"))
        {
            throw Unsupported();
        }

        return ExpressionEvaluator.InvokeStatic(type, name, arguments);
    }

    private object?[] Arguments(ArgumentListSyntax? list) =>
        list is null
            ? []
            : [.. list.Arguments.Select(a => a is SimpleArgumentSyntax { NameColonEquals: null } simple ? Evaluate(simple.Expression) : throw Unsupported())];

    /// <summary>Whether an expression names the Microsoft.VisualBasic namespace, as <c>Global.Microsoft.VisualBasic</c>.</summary>
    private static bool IsVisualBasicNamespace(ExpressionSyntax expression) =>
        string.Equals(WithoutGlobal(expression.ToString()), "Microsoft.VisualBasic", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether an expression names a module of Microsoft.VisualBasic, as <c>ControlChars</c> or <c>Microsoft.VisualBasic.ControlChars</c>.</summary>
    private static bool IsVisualBasicModule(ExpressionSyntax expression, string module)
    {
        var name = WithoutGlobal(expression.ToString());
        return string.Equals(name, module, StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "Microsoft.VisualBasic." + module, StringComparison.OrdinalIgnoreCase);
    }

    private static string WithoutGlobal(string name) =>
        name.StartsWith("Global.", StringComparison.OrdinalIgnoreCase) ? name["Global.".Length..] : name;

    private static Type ResolveType(TypeSyntax syntax) => TypeResolver.Resolve(syntax.ToString()) ?? throw Unsupported();
}
