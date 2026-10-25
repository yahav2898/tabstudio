using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TabStudio.Porting.Wpf;

namespace TabStudio.Porting.CSharp;

/// <summary>
/// WPF's dependency properties and routed events in code: <c>DependencyProperty.Register</c> becomes Avalonia's
/// <c>AvaloniaProperty.Register&lt;Owner, T&gt;</c>, its metadata named arguments and a static constructor (changed
/// callbacks become class handlers, AffectsRender a call), and <c>EventManager.RegisterRoutedEvent</c> becomes
/// <c>RoutedEvent.Register&lt;Owner, TArgs&gt;</c>.
/// </summary>
internal static class DependencyProperties
{
    private static readonly string[] Registrations =
    [
        "DependencyProperty.Register", "DependencyProperty.RegisterAttached", "DependencyProperty.RegisterReadOnly",
        "DependencyProperty.RegisterAttachedReadOnly", "EventManager.RegisterRoutedEvent",
    ];

    /// <summary>Converts the registrations of every class in a file.</summary>
    /// <param name="file">The file, for its notes and conversions.</param>
    /// <param name="root">Its syntax.</param>
    /// <param name="usings">The namespaces the file needs, added to.</param>
    public static CompilationUnitSyntax Convert(CodeFile file, CompilationUnitSyntax root, HashSet<string> usings)
    {
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Where(c => c.Members.OfType<FieldDeclarationSyntax>().Any(f => Registration(f) is not null)).ToList();
        return classes.Count == 0 ? root : root.ReplaceNodes(classes, (_, rewritten) => ConvertClass(file, rewritten, usings));
    }

    /// <summary>The registration call of a field, such as <c>DependencyProperty.Register(...)</c>, or <see langword="null"/>.</summary>
    private static InvocationExpressionSyntax? Registration(FieldDeclarationSyntax field) =>
        field.Declaration.Variables is [{ Initializer.Value: InvocationExpressionSyntax call }]
        && Registrations.Any(r => call.Expression.ToString().EndsWith(r, StringComparison.Ordinal))
            ? call
            : null;

    private static ClassDeclarationSyntax ConvertClass(CodeFile file, ClassDeclarationSyntax type, HashSet<string> usings)
    {
        var statements = new List<StatementSyntax>();
        var members = new List<MemberDeclarationSyntax>();
        var className = type.Identifier.ValueText;
        foreach (var member in type.Members)
        {
            if (member is FieldDeclarationSyntax field && Registration(field) is { } call)
            {
                members.Add(ConvertField(file, field, call, className, statements, usings));
            }
            else
            {
                members.Add(member);
            }
        }

        type = type.WithMembers(SyntaxFactory.List(members));
        if (statements.Count == 0)
        {
            return type;
        }

        // Class handlers and AffectsRender go in the static constructor, which is created after the fields when missing.
        var constructor = type.Members.OfType<ConstructorDeclarationSyntax>().FirstOrDefault(c => c.Modifiers.Any(SyntaxKind.StaticKeyword));
        if (constructor?.Body is { } body)
        {
            return type.ReplaceNode(constructor, constructor.WithBody(body.AddStatements([.. statements])));
        }

        var indent = type.Members.FirstOrDefault()?.GetLeadingTrivia().LastOrDefault(t => t.IsKind(SyntaxKind.WhitespaceTrivia)).ToString() ?? "    ";
        var text = $"{indent}static {className}(){file.Newline}{indent}{{{file.Newline}"
            + string.Concat(statements.Select(s => $"{indent}    {s.ToString().Trim()}{file.Newline}"))
            + $"{indent}}}{file.Newline}";
        var created = SyntaxFactory.ParseMemberDeclaration(text)!.WithLeadingTrivia(SyntaxFactory.EndOfLine(file.Newline), SyntaxFactory.Whitespace(indent));
        var lastField = type.Members.LastOrDefault(m => m is FieldDeclarationSyntax);
        var index = lastField is null ? 0 : type.Members.IndexOf(lastField) + 1;
        file.Report.Converted("Property metadata → static constructor");
        return type.WithMembers(type.Members.Insert(index, created));
    }

    private static FieldDeclarationSyntax ConvertField(CodeFile file, FieldDeclarationSyntax field, InvocationExpressionSyntax call, string className, List<StatementSyntax> statements, HashSet<string> usings)
    {
        var method = call.Expression.ToString();
        var arguments = call.ArgumentList.Arguments;
        var fieldName = field.Declaration.Variables[0].Identifier.ValueText;
        usings.Add("Avalonia");
        if (method.EndsWith("RegisterRoutedEvent", StringComparison.Ordinal))
        {
            return ConvertRoutedEvent(file, field, arguments, className, usings);
        }

        if (arguments.Count < 3 || TypeOf(arguments[1].Expression) is not { } valueType || TypeOf(arguments[2].Expression) is not { } ownerType)
        {
            return file.CommentOut(field, "A dependency property registered in a way the converter does not know", "Register it with AvaloniaProperty.Register<Owner, T>(name, defaultValue) as a StyledProperty<T>");
        }

        var attached = method.Contains("Attached", StringComparison.Ordinal);
        var readOnly = method.Contains("ReadOnly", StringComparison.Ordinal);
        var metadata = arguments.Count > 3 ? Metadata(arguments[3].Expression) : new MetadataParts(null, [], null, null);
        var validate = arguments.Count > 4 ? Callback(arguments[4].Expression) : null;

        var named = new List<string>();
        if (metadata.Default is { } defaultValue)
        {
            named.Add(defaultValue.ToString());
        }

        if (metadata.Flags.Contains("Inherits"))
        {
            named.Add("inherits: true");
        }

        if (metadata.Flags.Contains("BindsTwoWayByDefault"))
        {
            named.Add("defaultBindingMode: BindingMode.TwoWay");
            usings.Add("Avalonia.Data");
        }

        if (validate is not null)
        {
            named.Add($"validate: value => {validate}(value)");
        }

        if (metadata.Coerce is { } coerce)
        {
            named.Add($"coerce: (o, value) => ({valueType}){coerce}(o, value)!");
        }

        var name = arguments[0].Expression.ToString();
        var rest = string.Concat(named.Select(n => ", " + n));
        var (fieldType, register) = attached
            ? ($"AttachedProperty<{valueType}>", $"AvaloniaProperty.RegisterAttached<AvaloniaObject, {valueType}>({name}, typeof({ownerType}){rest})")
            : ($"StyledProperty<{valueType}>", $"AvaloniaProperty.Register<{ownerType}, {valueType}>({name}{rest})");

        if (metadata.Changed is { } changed)
        {
            statements.Add(SyntaxFactory.ParseStatement($"{fieldName}.Changed.AddClassHandler<{(attached ? "AvaloniaObject" : ownerType.ToString())}>({changed});"));
        }

        foreach (var flag in metadata.Flags.Where(f => f is "AffectsRender" or "AffectsMeasure" or "AffectsArrange"))
        {
            statements.Add(SyntaxFactory.ParseStatement($"{flag}<{ownerType}>({fieldName});"));
        }

        var converted = field
            .WithDeclaration(field.Declaration
                .WithType(SyntaxFactory.ParseTypeName(fieldType).WithTriviaFrom(field.Declaration.Type))
                .WithVariables(SyntaxFactory.SingletonSeparatedList(field.Declaration.Variables[0]
                    .WithInitializer(field.Declaration.Variables[0].Initializer!.WithValue(SyntaxFactory.ParseExpression(register).WithTriviaFrom(call))))));
        file.Report.Converted(attached ? "Attached DependencyProperty → AttachedProperty" : "DependencyProperty → StyledProperty");
        return readOnly
            ? file.Todo(converted, "A read-only dependency property", "Avalonia's styled properties can be set from outside; make it a DirectProperty with a private setter to keep it read-only")
            : converted;
    }

    private static FieldDeclarationSyntax ConvertRoutedEvent(CodeFile file, FieldDeclarationSyntax field, SeparatedSyntaxList<ArgumentSyntax> arguments, string className, HashSet<string> usings)
    {
        if (arguments.Count < 4 || TypeOf(arguments[2].Expression) is not { } handler || TypeOf(arguments[3].Expression) is not { } owner)
        {
            return file.CommentOut(field, "A routed event registered in a way the converter does not know", "Register it with RoutedEvent.Register<Owner, TArgs>(name, RoutingStrategies.Bubble)");
        }

        usings.Add("Avalonia.Interactivity");
        var handlerName = handler.ToString();
        var args = WpfTypes.HandlerArguments.TryGetValue(handlerName, out var known) ? known : "RoutedEventArgs";
        var register = $"RoutedEvent.Register<{owner}, {args}>({arguments[0].Expression}, {arguments[1].Expression})";
        var converted = field.WithDeclaration(field.Declaration
            .WithType(SyntaxFactory.ParseTypeName($"RoutedEvent<{args}>").WithTriviaFrom(field.Declaration.Type))
            .WithVariables(SyntaxFactory.SingletonSeparatedList(field.Declaration.Variables[0]
                .WithInitializer(field.Declaration.Variables[0].Initializer!.WithValue(SyntaxFactory.ParseExpression(register).WithTriviaFrom(field.Declaration.Variables[0].Initializer!.Value))))));
        file.Report.Converted("Routed event → RoutedEvent.Register");
        return known is null && handlerName != "RoutedEventHandler" && className.Length > 0
            ? file.Todo(converted, $"A routed event of {handlerName}", "Its arguments became RoutedEventArgs; give it an Avalonia RoutedEventArgs subclass of its own if it carried data")
            : converted;
    }

    /// <summary>The parts of WPF property metadata: the default, the options, and the callbacks.</summary>
    private sealed record MetadataParts(ExpressionSyntax? Default, List<string> Flags, string? Changed, string? Coerce);

    private static MetadataParts Metadata(ExpressionSyntax expression)
    {
        if (expression is not BaseObjectCreationExpressionSyntax { ArgumentList: { } list })
        {
            return new MetadataParts(null, [], null, null);
        }

        ExpressionSyntax? defaultValue = null;
        var flags = new List<string>();
        var callbacks = new List<string>();
        foreach (var argument in list.Arguments)
        {
            var value = argument.Expression;
            if (value.ToString().Contains("FrameworkPropertyMetadataOptions", StringComparison.Ordinal))
            {
                flags.AddRange(value.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>()
                    .Where(m => m.Expression.ToString().EndsWith("FrameworkPropertyMetadataOptions", StringComparison.Ordinal))
                    .Select(m => m.Name.Identifier.ValueText));
            }
            else if (Callback(value) is { } callback && (callbacks.Count > 0 || defaultValue is not null || list.Arguments.Count == 1 || IsCallbackShaped(value)))
            {
                callbacks.Add(callback);
            }
            else if (defaultValue is null && callbacks.Count == 0)
            {
                defaultValue = value;
            }
        }

        return new MetadataParts(defaultValue, flags, callbacks.Count > 0 ? callbacks[0] : null, callbacks.Count > 1 ? callbacks[1] : null);
    }

    private static bool IsCallbackShaped(ExpressionSyntax value) =>
        value is LambdaExpressionSyntax or ObjectCreationExpressionSyntax { Type: IdentifierNameSyntax { Identifier.ValueText: "PropertyChangedCallback" or "CoerceValueCallback" } };

    /// <summary>A callback as Avalonia takes it: a method group or a lambda, unwrapped from <c>new PropertyChangedCallback(...)</c>.</summary>
    private static string? Callback(ExpressionSyntax value) => value switch
    {
        ObjectCreationExpressionSyntax { Type: IdentifierNameSyntax { Identifier.ValueText: "PropertyChangedCallback" or "CoerceValueCallback" or "ValidateValueCallback" }, ArgumentList.Arguments: [var inner] } => inner.Expression.ToString(),
        LambdaExpressionSyntax lambda => lambda.ToString(),
        IdentifierNameSyntax { Identifier.ValueText: var name } when name.Length > 0 && char.IsUpper(name[0]) && name.StartsWith("On", StringComparison.Ordinal) || name.StartsWith("Coerce", StringComparison.Ordinal) || name.StartsWith("Validate", StringComparison.Ordinal) || name.StartsWith("Is", StringComparison.Ordinal) => name,
        _ => null,
    };

    private static TypeSyntax? TypeOf(ExpressionSyntax expression) => expression is TypeOfExpressionSyntax typeOf ? typeOf.Type.WithoutTrivia() : null;
}
