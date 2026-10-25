using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TabStudio.Porting.CSharp;
using TabStudio.Porting.Output;

namespace TabStudio.Porting.Wpf;

/// <summary>
/// Rewrites WPF code for Avalonia: WPF's types get Avalonia's (and the namespaces they live in), Visibility becomes
/// a boolean, the Dispatcher is Avalonia's UI thread, message boxes and dialogs are awaited, and event handlers take
/// Avalonia's arguments.
/// </summary>
internal sealed class WpfCodeRewriter : CSharpSyntaxRewriter
{
    private static readonly HashSet<string> DispatcherMethods = new(StringComparer.Ordinal) { "Invoke", "InvokeAsync", "BeginInvoke", "CheckAccess", "VerifyAccess" };

    /// <summary>The namespaces of the arguments of Avalonia's events.</summary>
    private static readonly Dictionary<string, string> ArgumentNamespaces = new(StringComparer.Ordinal)
    {
        ["PointerPressedEventArgs"] = "Avalonia.Input",
        ["PointerReleasedEventArgs"] = "Avalonia.Input",
        ["PointerEventArgs"] = "Avalonia.Input",
        ["PointerWheelEventArgs"] = "Avalonia.Input",
        ["TappedEventArgs"] = "Avalonia.Input",
        ["TextInputEventArgs"] = "Avalonia.Input",
        ["KeyEventArgs"] = "Avalonia.Input",
        ["GotFocusEventArgs"] = "Avalonia.Input",
        ["TextChangedEventArgs"] = "Avalonia.Controls",
        ["SelectionChangedEventArgs"] = "Avalonia.Controls",
        ["ContextRequestedEventArgs"] = "Avalonia.Controls",
        ["SizeChangedEventArgs"] = "Avalonia.Controls",
        ["RangeBaseValueChangedEventArgs"] = "Avalonia.Controls.Primitives",
        ["RoutedEventArgs"] = "Avalonia.Interactivity",
    };

    private readonly WpfProject _project;
    private readonly CodeFile _file;
    private readonly XamlClassInfo? _class;
    private readonly HashSet<string> _usings;
    private readonly bool _nullable;
    private readonly Dictionary<string, string> _handlers;

    /// <summary>Creates the rewriter of one file.</summary>
    /// <param name="project">The project.</param>
    /// <param name="file">The file.</param>
    /// <param name="xamlClass">For code-behind, what the XAML converter learned about its class.</param>
    /// <param name="usings">The namespaces the file needs, added to.</param>
    public WpfCodeRewriter(WpfProject project, CodeFile file, XamlClassInfo? xamlClass, HashSet<string> usings)
    {
        _project = project;
        _file = file;
        _class = xamlClass;
        _usings = usings;
        _nullable = project.Context.Source.NullableEnabled;
        _handlers = new Dictionary<string, string>(xamlClass?.HandlerArguments ?? new Dictionary<string, string>(), StringComparer.Ordinal);

        // Handlers subscribed in code to events that Avalonia names differently take the new event's arguments too.
        foreach (var assignment in file.Root.DescendantNodes().OfType<AssignmentExpressionSyntax>().Where(a => a.IsKind(SyntaxKind.AddAssignmentExpression)))
        {
            if (assignment.Left is MemberAccessExpressionSyntax { Name.Identifier.ValueText: var eventName } && WpfEvents.Renamed.TryGetValue(eventName, out var mapping)
                && mapping.ArgsType is not null && Handler(assignment.Right) is { } handler)
            {
                _handlers[handler] = mapping.ArgsType;
            }
        }
    }

    /// <summary>Whether the support code of the port is used.</summary>
    public bool UsesSupport { get; private set; }

    /// <inheritdoc />
    public override SyntaxNode? VisitQualifiedName(QualifiedNameSyntax node)
    {
        if (!node.AncestorsAndSelf().Any(a => a is UsingDirectiveSyntax or BaseNamespaceDeclarationSyntax)
            && WpfTypes.Namespaces.Contains(node.Left.ToString()) && WpfTypes.Mapped.TryGetValue(node.Right.Identifier.ValueText, out var mapping))
        {
            return Type(mapping).WithTriviaFrom(node);
        }

        return base.VisitQualifiedName(node);
    }

    /// <inheritdoc />
    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        var name = node.Identifier.ValueText;
        if (_project.DeclaredTypes.Contains(name))
        {
            return node;
        }

        if (!SyntaxContext.IsTypeOrStatic(node))
        {
            // The code-behind's own Visibility, as in "Visibility = Visibility.Collapsed".
            if (name == "Visibility" && _class is not null && node.Parent is AssignmentExpressionSyntax or BinaryExpressionSyntax or ArgumentSyntax or ReturnStatementSyntax)
            {
                _file.Report.Converted("Visibility → IsVisible");
                return SyntaxFactory.IdentifierName("IsVisible").WithTriviaFrom(node);
            }

            return node;
        }

        if (!WpfTypes.Mapped.TryGetValue(name, out var mapping) || mapping.Name == name && mapping.Namespace is null && !IsSupportType(name))
        {
            return node;
        }

        if (mapping.Name != name)
        {
            _file.Report.Converted($"{name} → {mapping.Name}");
        }

        return Type(mapping).WithTriviaFrom(node);
    }

    /// <inheritdoc />
    public override SyntaxNode? VisitGenericName(GenericNameSyntax node)
    {
        switch (node.Identifier.ValueText)
        {
            case "RoutedPropertyChangedEventHandler":
                _usings.Add("Avalonia.Controls.Primitives");
                _file.Report.Converted("RoutedPropertyChangedEventHandler → EventHandler<RangeBaseValueChangedEventArgs>");
                return SyntaxFactory.ParseTypeName("EventHandler<RangeBaseValueChangedEventArgs>").WithTriviaFrom(node);
            case "RoutedPropertyChangedEventArgs":
                _usings.Add("Avalonia.Controls.Primitives");
                _file.Report.Converted("RoutedPropertyChangedEventArgs → RangeBaseValueChangedEventArgs");
                return SyntaxFactory.IdentifierName("RangeBaseValueChangedEventArgs").WithTriviaFrom(node);
            default:
                return base.VisitGenericName(node);
        }
    }

    /// <inheritdoc />
    public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        var target = node.Expression.ToString();
        var member = node.Name.Identifier.ValueText;
        switch (target, member)
        {
            case ("Visibility" or "System.Windows.Visibility", "Visible" or "Collapsed" or "Hidden") when !_project.DeclaredTypes.Contains("Visibility"):
                _file.Report.Converted("Visibility value → boolean");
                var literal = SyntaxFactory.LiteralExpression(member == "Visible" ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression).WithTriviaFrom(node);
                return member == "Hidden"
                    ? _file.Todo(literal, "Visibility.Hidden", "Avalonia has no hidden-but-taking-space state; IsVisible=false collapses the element. Set Opacity to 0 and IsHitTestVisible to false if the space should stay")
                    : literal;
            case ("Cursors", _) when WpfAttributes.Cursors.TryGetValue(member, out var cursor):
                _usings.Add("Avalonia.Input");
                _file.Report.Converted("Cursors → new Cursor(StandardCursorType)");
                return SyntaxFactory.ParseExpression($"new Cursor(StandardCursorType.{cursor})").WithTriviaFrom(node);
            case ("Application.Current" or "App.Current", "MainWindow"):
                UsesSupport = true;
                _file.Report.Converted("Application.Current.MainWindow → AppWindow.Main");
                return SyntaxFactory.ParseExpression("AppWindow.Main").WithTriviaFrom(node);
            case ("Application.Current" or "App.Current" or "this", "Dispatcher"):
            case ("Dispatcher", "CurrentDispatcher"):
                _usings.Add("Avalonia.Threading");
                _file.Report.Converted("Dispatcher → Dispatcher.UIThread");
                return SyntaxFactory.ParseExpression("Dispatcher.UIThread").WithTriviaFrom(node);
            case ("Binding", "DoNothing"):
                _usings.Add("Avalonia.Data");
                return SyntaxFactory.ParseExpression("BindingOperations.DoNothing").WithTriviaFrom(node);
            case ("ModifierKeys", "Windows"):
                _usings.Add("Avalonia.Input");
                return SyntaxFactory.ParseExpression("KeyModifiers.Meta").WithTriviaFrom(node);
            case (_, "DependencyProperty") when target.EndsWith("Key", StringComparison.Ordinal):
                // A read-only property's key and its property are the same Avalonia property.
                return node.Expression.WithTriviaFrom(node);
        }

        if (DispatcherMethods.Contains(member) && IsDispatcher(node.Expression))
        {
            _usings.Add("Avalonia.Threading");
            _file.Report.Converted("Dispatcher → Dispatcher.UIThread");
            var method = member == "BeginInvoke" ? "Post" : member;
            return SyntaxFactory.ParseExpression($"Dispatcher.UIThread.{method}").WithTriviaFrom(node);
        }

        var visited = (MemberAccessExpressionSyntax)base.VisitMemberAccessExpression(node)!;

        // Events Avalonia names differently, subscribed in code.
        if (WpfEvents.Renamed.TryGetValue(member, out var mapping) && node.Parent is AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.AddAssignmentExpression or (int)SyntaxKind.SubtractAssignmentExpression } assignment && assignment.Left == node)
        {
            _file.Report.Converted($"{member} → {mapping.Name}");
            visited = visited.WithName(SyntaxFactory.IdentifierName(mapping.Name).WithTriviaFrom(visited.Name));
            return mapping.Note is null ? visited : _file.Todo(visited, $"{member} → {mapping.Name}", mapping.Note);
        }

        if (member == "Visibility" && !_project.DeclaredTypes.Contains("Visibility"))
        {
            _file.Report.Converted("Visibility → IsVisible");
            return visited.WithName(SyntaxFactory.IdentifierName("IsVisible").WithTriviaFrom(visited.Name));
        }

        // Members of elements whose type changed in the XAML.
        if (node.Expression is IdentifierNameSyntax { Identifier.ValueText: var element } && _class?.ChangedElements.TryGetValue(element, out var change) == true
            && change.Avalonia == "TextBox" && change.Wpf == "PasswordBox" && member == "Password")
        {
            return visited.WithName(SyntaxFactory.IdentifierName("Text").WithTriviaFrom(visited.Name));
        }

        return visited;
    }

    /// <inheritdoc />
    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        var expression = node.Expression as MemberAccessExpressionSyntax;
        var target = expression?.Expression.ToString();
        var member = expression?.Name.Identifier.ValueText;
        var arguments = node.ArgumentList.Arguments;
        var visited = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;
        var visitedArguments = visited.ArgumentList.Arguments;

        switch (target, member)
        {
            case ("MessageBox", "Show") when !_project.DeclaredTypes.Contains("MessageBox"):
                UsesSupport = true;
                _project.Context.Needs(SupportNeeds.WpfMessageBox);
                _file.Report.Converted("MessageBox.Show → await MessageBox.ShowAsync");
                return AsyncCalls.Await(visited.WithExpression(SyntaxFactory.ParseExpression("MessageBox.ShowAsync").WithTriviaFrom(visited.Expression)));
            case (_, "ShowDialog") when arguments.Count == 0:
                _file.Report.Converted("ShowDialog() → await ShowDialogAsync()");
                var dialog = (MemberAccessExpressionSyntax)visited.Expression;
                return AsyncCalls.Await(visited.WithExpression(dialog.WithName(SyntaxFactory.IdentifierName("ShowDialogAsync"))));
            case ("Keyboard", "Focus") when arguments.Count == 1:
            case ("FocusManager", "SetFocusedElement") when arguments.Count == 2:
                _file.Report.Converted($"{target}.{member} → Focus()");
                return SyntaxFactory.ParseExpression($"{visitedArguments[^1].Expression}.Focus()").WithTriviaFrom(node);
            case ("VisualTreeHelper", "GetParent") when arguments.Count == 1:
                _usings.Add("Avalonia.VisualTree");
                return SyntaxFactory.ParseExpression($"{visitedArguments[0].Expression}.GetVisualParent()").WithTriviaFrom(node);
            case ("VisualTreeHelper", "GetChildrenCount") when arguments.Count == 1:
                _usings.Add("Avalonia.VisualTree");
                return SyntaxFactory.ParseExpression($"{visitedArguments[0].Expression}.GetVisualChildren().Count()").WithTriviaFrom(node);
            case ("VisualTreeHelper", "GetChild") when arguments.Count == 2:
                _usings.Add("Avalonia.VisualTree");
                return SyntaxFactory.ParseExpression($"{visitedArguments[0].Expression}.GetVisualChildren().ElementAt({visitedArguments[1].Expression})").WithTriviaFrom(node);
            case ("Clipboard", "SetText") when arguments.Count == 1 && node.Parent is ExpressionStatementSyntax:
                UsesSupport = true;
                _project.Context.Needs(SupportNeeds.WpfWindow);
                _usings.Add("Avalonia.Controls");
                _file.Report.Converted("Clipboard.SetText → the top level's clipboard");
                return SyntaxFactory.ParseExpression($"_ = TopLevel.GetTopLevel(AppWindow.Main)?.Clipboard?.SetTextAsync({visitedArguments[0].Expression})").WithTriviaFrom(node);
            case ("Application.Current" or "App.Current", "Shutdown"):
                UsesSupport = true;
                return SyntaxFactory.ParseExpression("AppWindow.Shutdown()").WithTriviaFrom(node);
            case ("Window", "GetWindow") when arguments.Count == 1:
                UsesSupport = true;
                return SyntaxFactory.ParseExpression($"AppWindow.GetWindow({visitedArguments[0].Expression})").WithTriviaFrom(node);
            case ("ColorConverter", "ConvertFromString") when arguments.Count == 1:
                _usings.Add("Avalonia.Media");
                _file.Report.Converted("ColorConverter.ConvertFromString → Color.Parse");
                return SyntaxFactory.ParseExpression($"Color.Parse({visitedArguments[0].Expression})").WithTriviaFrom(node);
        }

        if (member is "BeginInvoke" or "Invoke" && expression is not null && IsDispatcher(expression.Expression))
        {
            return visited.WithArgumentList(DispatcherArguments(visited.ArgumentList));
        }

        return visited;
    }

    /// <inheritdoc />
    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var visited = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node)!;
        var parameters = visited.ParameterList.Parameters;
        var name = node.Identifier.ValueText;
        if (parameters.Count == 2 && _handlers.TryGetValue(name, out var arguments))
        {
            if (ArgumentNamespaces.TryGetValue(arguments, out var ns))
            {
                _usings.Add(ns);
            }

            parameters = parameters.Replace(parameters[1], parameters[1].WithType(SyntaxFactory.ParseTypeName(arguments).WithTriviaFrom(parameters[1].Type!)));
            _file.Report.Converted("Event handler → Avalonia's event arguments");
        }

        if (_nullable && parameters.Count == 2 && parameters[0].Identifier.ValueText == "sender" && (parameters[1].Type?.ToString().EndsWith("EventArgs", StringComparison.Ordinal) ?? false))
        {
            parameters = parameters.Replace(parameters[0], SyntaxContext.NullableSender(parameters[0]));
        }

        if (_nullable && name is "Convert" or "ConvertBack" && IsConverter(node))
        {
            parameters = SyntaxFactory.SeparatedList(parameters.Select(p => SyntaxContext.NullableSender(p)), parameters.GetSeparators());
            if (visited.ReturnType is PredefinedTypeSyntax { Keyword.ValueText: "object" } returnType)
            {
                visited = visited.WithReturnType(SyntaxFactory.NullableType(returnType.WithoutTrailingTrivia()).WithTrailingTrivia(returnType.GetTrailingTrivia()));
            }
        }

        if (name == "Convert" && parameters.Count == 4 && parameters[0].Type is ArrayTypeSyntax && IsConverter(node, multi: true))
        {
            // Avalonia's multi-value converters take a list.
            parameters = parameters.Replace(parameters[0], parameters[0].WithType(SyntaxFactory.ParseTypeName("IList<object?>").WithTriviaFrom(parameters[0].Type!)));
            _file.Report.Converted("IMultiValueConverter values → IList<object?>");
        }

        visited = visited.WithParameterList(visited.ParameterList.WithParameters(parameters));
        return OverrideOf(visited);
    }

    /// <inheritdoc />
    public override SyntaxNode? VisitAttributeList(AttributeListSyntax node)
    {
        var kept = node.Attributes.Where(a => a.Name.ToString() is not ("ValueConversion" or "ValueConversionAttribute" or "StyleTypedProperty" or "ThemeInfo")).ToList();
        if (kept.Count == node.Attributes.Count)
        {
            return base.VisitAttributeList(node);
        }

        _file.Report.Converted("WPF-only attribute removed");
        return kept.Count == 0 ? null : node.WithAttributes(SyntaxFactory.SeparatedList(kept));
    }

    /// <summary>Overrides of WPF's virtual methods that Avalonia names differently.</summary>
    private MethodDeclarationSyntax OverrideOf(MethodDeclarationSyntax method)
    {
        if (!method.Modifiers.Any(SyntaxKind.OverrideKeyword))
        {
            return method;
        }

        var name = method.Identifier.ValueText;
        (string Name, string? Args, string? Note)? renamed = name switch
        {
            "OnMouseLeftButtonDown" or "OnMouseDown" or "OnMouseRightButtonDown" => ("OnPointerPressed", "PointerPressedEventArgs", name == "OnMouseDown" ? null : "Avalonia raises it for every button, the pen and touch"),
            "OnMouseLeftButtonUp" or "OnMouseUp" or "OnMouseRightButtonUp" => ("OnPointerReleased", "PointerReleasedEventArgs", name == "OnMouseUp" ? null : "Avalonia raises it for every button, the pen and touch"),
            "OnMouseMove" => ("OnPointerMoved", "PointerEventArgs", null),
            "OnMouseEnter" => ("OnPointerEntered", "PointerEventArgs", null),
            "OnMouseLeave" => ("OnPointerExited", "PointerEventArgs", null),
            "OnMouseWheel" => ("OnPointerWheelChanged", "PointerWheelEventArgs", "e.Delta is a Vector now: e.Delta.Y is 1 per notch, not 120"),
            "OnGotFocus" => ("OnGotFocus", "GotFocusEventArgs", null),
            "OnRender" => ("Render", null, "Avalonia's DrawingContext differs: DrawText takes a FormattedText and a point, DrawRectangle a brush, a pen and a rect"),
            "OnApplyTemplate" => ("OnApplyTemplate", "TemplateAppliedEventArgs", "Find template parts with e.NameScope.Find<T>(\"PART_Name\") instead of GetTemplateChild"),
            _ => null,
        };

        if (renamed is not { } change)
        {
            return method;
        }

        var parameters = method.ParameterList.Parameters;
        if (change.Args is not null)
        {
            if (ArgumentNamespaces.TryGetValue(change.Args, out var ns))
            {
                _usings.Add(ns);
            }

            if (change.Args == "TemplateAppliedEventArgs")
            {
                _usings.Add("Avalonia.Controls.Primitives");
                parameters = SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Parameter(SyntaxFactory.Identifier("e")).WithType(SyntaxFactory.ParseTypeName("TemplateAppliedEventArgs ")));
            }
            else if (parameters.Count == 1)
            {
                parameters = parameters.Replace(parameters[0], parameters[0].WithType(SyntaxFactory.ParseTypeName(change.Args).WithTriviaFrom(parameters[0].Type!)));
            }
        }

        var converted = method.WithIdentifier(SyntaxFactory.Identifier(change.Name).WithTriviaFrom(method.Identifier))
            .WithParameterList(method.ParameterList.WithParameters(parameters));
        if (change.Name == "Render")
        {
            // Avalonia's Render is public.
            converted = converted.WithModifiers(SyntaxFactory.TokenList(converted.Modifiers.Select(m => m.IsKind(SyntaxKind.ProtectedKeyword) ? SyntaxFactory.Token(SyntaxKind.PublicKeyword).WithTriviaFrom(m) : m)));
        }

        _file.Report.Converted($"override {name} → {change.Name}");
        return change.Note is null ? converted : _file.Todo(converted, $"override {name} → {change.Name}", change.Note);
    }

    /// <summary>Whether a method belongs to a class that implements a value converter.</summary>
    private static bool IsConverter(MethodDeclarationSyntax method, bool multi = false) =>
        method.Parent is TypeDeclarationSyntax { BaseList: { } bases }
        && bases.Types.Any(t => t.Type.ToString() is var name && (multi ? name.EndsWith("IMultiValueConverter", StringComparison.Ordinal) : name.EndsWith("ValueConverter", StringComparison.Ordinal)));

    /// <summary>Whether an expression is WPF's dispatcher: an element's, the application's or the current one.</summary>
    private static bool IsDispatcher(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax { Identifier.ValueText: "Dispatcher" } => true,
        MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Dispatcher" or "CurrentDispatcher" or "UIThread" } => true,
        _ => false,
    };

    /// <summary>
    /// WPF's dispatcher calls as Avalonia's: the delegate unwrapped from <c>new Action(...)</c>, and a priority that
    /// came first moved after it.
    /// </summary>
    private static ArgumentListSyntax DispatcherArguments(ArgumentListSyntax list)
    {
        var arguments = list.Arguments.Select(a => a.WithExpression(Unwrap(a.Expression))).ToList();
        if (arguments.Count == 2 && arguments[0].Expression.ToString().Contains("DispatcherPriority", StringComparison.Ordinal))
        {
            arguments = [arguments[1].WithoutTrivia(), arguments[0].WithLeadingTrivia(SyntaxFactory.Space)];
        }

        return list.WithArguments(SyntaxFactory.SeparatedList(arguments.Take(2)));
    }

    private static ExpressionSyntax Unwrap(ExpressionSyntax expression) => expression switch
    {
        ObjectCreationExpressionSyntax { Type: IdentifierNameSyntax { Identifier.ValueText: "Action" }, ArgumentList.Arguments: [var inner] } => inner.Expression.WithTriviaFrom(expression),
        CastExpressionSyntax { Type: IdentifierNameSyntax { Identifier.ValueText: "Action" } } cast => (cast.Expression is ParenthesizedExpressionSyntax parenthesized ? parenthesized.Expression : cast.Expression).WithTriviaFrom(expression),
        _ => expression,
    };

    /// <summary>The method a handler expression names: <c>OnClick</c> or <c>new RoutedEventHandler(OnClick)</c>.</summary>
    private static string? Handler(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: var name } => name.Identifier.ValueText,
        ObjectCreationExpressionSyntax { ArgumentList.Arguments: [var inner] } => Handler(inner.Expression),
        _ => null,
    };

    private static bool IsSupportType(string name) => name is "Window" or "MessageBox" or "MessageBoxButton" or "MessageBoxImage" or "MessageBoxResult" or "CommandManager";

    /// <summary>The Avalonia type of a mapping, and the namespace the file needs for it.</summary>
    private TypeSyntax Type(TypeMapping mapping)
    {
        if (mapping.Namespace is not null)
        {
            _usings.Add(mapping.Namespace);
        }

        if (IsSupportType(mapping.Name) || mapping.Name is "AppWindow")
        {
            UsesSupport = true;
            if (mapping.Name == "CommandManager")
            {
                _project.Context.Needs(SupportNeeds.CommandManager);
            }
            else if (mapping.Name.StartsWith("MessageBox", StringComparison.Ordinal))
            {
                _project.Context.Needs(SupportNeeds.WpfMessageBox);
            }
            else
            {
                _project.Context.Needs(SupportNeeds.WpfWindow);
            }
        }

        return SyntaxFactory.ParseTypeName(mapping.Name);
    }
}
