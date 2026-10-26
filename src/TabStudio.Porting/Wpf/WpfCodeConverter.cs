using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TabStudio.Porting.CSharp;
using TabStudio.Porting.Xaml;

namespace TabStudio.Porting.Wpf;

/// <summary>
/// Converts one C# file of a WPF project: dependency properties and routed events, WPF's types and calls, async
/// dialogs, the Avalonia application class, Preview handlers, then comments out what Avalonia does not have and
/// updates the usings. Code with nothing of WPF in it stays as it is.
/// </summary>
internal static class WpfCodeConverter
{
    /// <summary>Converts a file.</summary>
    /// <param name="project">The project.</param>
    /// <param name="sourcePath">The file's path in the original project.</param>
    /// <param name="text">The file's text.</param>
    /// <returns>The converted text.</returns>
    public static string Convert(WpfProject project, string sourcePath, string text)
    {
        var report = project.Context.Report(WpfProject.TargetPath(sourcePath));
        var file = new CodeFile(text, report);
        var imports = Usings.Imported(file.Root);
        var xamlClass = XamlClassOf(project, sourcePath);
        var usesWpf = imports.Any(WpfTypes.Namespaces.Contains)
            || file.Root.DescendantNodes().OfType<QualifiedNameSyntax>().Any(q => q.ToString().StartsWith("System.Windows.", StringComparison.Ordinal));
        if (!usesWpf && xamlClass is null)
        {
            return text;
        }

        var usings = new HashSet<string>(StringComparer.Ordinal);
        file.Root = DependencyProperties.Convert(file, file.Root, usings);
        var rewriter = new WpfCodeRewriter(project, file, xamlClass, usings);
        file.Root = (CompilationUnitSyntax)rewriter.Visit(file.Root);
        var usesSupport = rewriter.UsesSupport;
        file.Root = AsyncCalls.MakeAsync(file, file.Root);
        if (xamlClass is { TunnelHandlers.Count: > 0 })
        {
            file.Root = AddTunnelHandlers(file, xamlClass, usings);
        }

        if (xamlClass?.RootType == "Application")
        {
            file.Root = AddApplicationMembers(project, file, xamlClass, usings);
            usesSupport |= xamlClass.StartupWindow is not null;
        }

        file.Root = UnsupportedCode.CommentOut(file, file.Root, node => Unsupported(project, xamlClass, node));
        if (usesSupport)
        {
            usings.Add(project.Context.SupportNamespace);
        }

        // ICommand is .NET's, in System.Windows.Input; the rest of that namespace was WPF's. System.Configuration
        // came with WPF too, and stays only for code that reads the app's settings.
        var usesCommand = file.Root.DescendantTokens().Any(t => t.ValueText == "ICommand");
        var usesConfiguration = Configuration.Uses(file.Root, project.Context);
        file.Root = Usings.Update(
            file.Root,
            ns => (WpfTypes.Namespaces.Contains(ns) && !(ns == "System.Windows.Input" && usesCommand))
                || (ns.StartsWith("System.Windows.", StringComparison.Ordinal) && ns != "System.Windows.Input")
                || (ns == "System.Configuration" && !usesConfiguration),
            usings.Where(ns => ns != file.Root.Members.OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString()),
            file.Newline);
        report.Converted("WPF usings → Avalonia usings", usings.Count > 0 ? 1 : 0);
        return file.Finish();
    }

    /// <summary>What the XAML converter learned about the class of a code-behind file.</summary>
    private static XamlClassInfo? XamlClassOf(WpfProject project, string sourcePath)
    {
        if (!sourcePath.EndsWith(".xaml.cs", StringComparison.OrdinalIgnoreCase)
            || !project.Xaml.TryGetValue(sourcePath[..^3], out var document)
            || document.Root?.Attribute(XamlNamespaces.Xaml + "Class")?.Value is not { } className)
        {
            return null;
        }

        return project.Classes.GetValueOrDefault(className);
    }

    /// <summary>Whether a node uses something of WPF that Avalonia does not have.</summary>
    private static Unported? Unsupported(WpfProject project, XamlClassInfo? xamlClass, SyntaxNode node)
    {
        switch (node)
        {
            case IdentifierNameSyntax identifier when WpfTypes.Unsupported.TryGetValue(identifier.Identifier.ValueText, out var why)
                && SyntaxContext.IsTypeOrStatic(identifier) && !project.DeclaredTypes.Contains(identifier.Identifier.ValueText):
                return new Unported(identifier.Identifier.ValueText, why);
            case MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "Clipboard" } } when !project.DeclaredTypes.Contains("Clipboard"):
                return new Unported("Clipboard", "Avalonia's clipboard is TopLevel.GetTopLevel(this).Clipboard, whose methods are async");
            case MemberAccessExpressionSyntax { Name.Identifier.ValueText: "OverrideMetadata" or "AddOwner" } access:
                return new Unported(access.ToString(), "Avalonia sets a property's metadata per type with OverrideDefaultValue, and finds a control's theme by its type or StyleKeyOverride");
            case MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "EventManager" } } access:
                return new Unported(access.ToString(), "Add class handlers with the routed event's AddClassHandler<T>, as in Button.ClickEvent.AddClassHandler<MyControl>(...)");
            case InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "GetTemplateChild" } }:
                return new Unported("GetTemplateChild", "Find template parts in OnApplyTemplate with e.NameScope.Find<T>(\"PART_Name\")");
            case MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: var element } } access
                when xamlClass?.ChangedElements.TryGetValue(element, out var change) == true && IsLostMember(change, access.Name.Identifier.ValueText):
                return new Unported($"{element}.{access.Name.Identifier.ValueText}", $"{element} was a {xamlClass.ChangedElements[element].Wpf}, which Avalonia does not have; it is a {xamlClass.ChangedElements[element].Avalonia} now");
            default:
                return null;
        }
    }

    /// <summary>Whether a member of an element is gone now that the element has another type.</summary>
    private static bool IsLostMember((string Wpf, string Avalonia) change, string member) => change switch
    {
        (_, "Border") => member is not ("IsVisible" or "IsEnabled" or "Margin" or "Width" or "Height" or "Focus" or "Tag" or "DataContext" or "Opacity"),
        ("ListView", "DataGrid") => member is "Items" or "View" or "ScrollIntoView" or "SelectedIndex",
        ("StatusBar" or "ToolBar", _) => member is "Items" or "ItemsSource",
        _ => false,
    };

    /// <summary>Preview handlers from XAML, added after InitializeComponent with Avalonia's Tunnel strategy.</summary>
    private static CompilationUnitSyntax AddTunnelHandlers(CodeFile file, XamlClassInfo xamlClass, HashSet<string> usings)
    {
        var shortName = xamlClass.ClassName[(xamlClass.ClassName.LastIndexOf('.') + 1)..];
        var constructors = file.Root.DescendantNodes().OfType<ConstructorDeclarationSyntax>()
            .Where(c => c.Identifier.ValueText == shortName && c.Body is not null && Initialize(c.Body) is not null)
            .ToList();
        if (constructors.Count == 0)
        {
            return file.Root;
        }

        usings.Add("Avalonia.Input");
        usings.Add("Avalonia.Interactivity");
        return file.Root.ReplaceNodes(constructors.Select(c => Initialize(c.Body!)!), (_, statement) =>
        {
            var indent = statement.GetLeadingTrivia().Where(t => t.IsKind(SyntaxKind.WhitespaceTrivia)).Select(t => t.ToString()).LastOrDefault() ?? string.Empty;
            var added = string.Concat(xamlClass.TunnelHandlers.Select(h =>
                $"{file.Newline}{indent}{(h.Element is null ? string.Empty : h.Element + ".")}AddHandler({h.Event}, {h.Handler}, RoutingStrategies.Tunnel);"));
            file.Report.Converted("Preview event → handler added with the Tunnel strategy", xamlClass.TunnelHandlers.Count);
            return statement.WithTrailingTrivia(SyntaxFactory.ParseTrailingTrivia($"{added}{file.Newline}"));
        });
    }

    private static StatementSyntax? Initialize(BlockSyntax body) =>
        body.Statements.FirstOrDefault(s => s is ExpressionStatementSyntax { Expression: InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "InitializeComponent" } } });

    /// <summary>
    /// The application class gets what Avalonia's needs: Initialize loads App.axaml, and OnFrameworkInitializationCompleted
    /// opens the window StartupUri named, as a window on the desktop and as the screen on phones. WPF's OnStartup
    /// override runs from there.
    /// </summary>
    private static CompilationUnitSyntax AddApplicationMembers(WpfProject project, CodeFile file, XamlClassInfo app, HashSet<string> usings)
    {
        var shortName = app.ClassName[(app.ClassName.LastIndexOf('.') + 1)..];
        if (file.Root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault(c => c.Identifier.ValueText == shortName) is not { } type)
        {
            return file.Root;
        }

        usings.Add("Avalonia");
        usings.Add("Avalonia.Markup.Xaml");
        var members = type.Members.ToList();
        var startup = members.OfType<MethodDeclarationSyntax>().FirstOrDefault(m => m.Identifier.ValueText == "OnStartup" && m.Modifiers.Any(SyntaxKind.OverrideKeyword));
        if (startup is not null)
        {
            members[members.IndexOf(startup)] = StartupMethod(file, startup);
        }

        var indent = Indentation(type);
        var lines = new List<string>();
        if (!members.OfType<MethodDeclarationSyntax>().Any(m => m.Identifier.ValueText == "Initialize"))
        {
            lines.Add($"{indent}public override void Initialize() => AvaloniaXamlLoader.Load(this);");
        }

        if (!members.OfType<MethodDeclarationSyntax>().Any(m => m.Identifier.ValueText == "OnFrameworkInitializationCompleted"))
        {
            if (lines.Count > 0)
            {
                lines.Add(string.Empty);
            }

            lines.Add($"{indent}public override void OnFrameworkInitializationCompleted()");
            lines.Add($"{indent}{{");
            if (startup is not null)
            {
                lines.Add($"{indent}    OnStartup();");
            }

            if (app.StartupWindow is { } window)
            {
                var ns = app.ClassName.Contains('.', StringComparison.Ordinal) ? app.ClassName[..app.ClassName.LastIndexOf('.')] : string.Empty;
                var windowName = window.StartsWith(ns + ".", StringComparison.Ordinal) ? window[(ns.Length + 1)..] : window;
                lines.Add($"{indent}    // StartupUri: the first window, a window on the desktop and the whole screen on phones and tablets.");
                lines.Add($"{indent}    WindowView.ShowMain(ApplicationLifetime, new {windowName}());");
                project.Context.Needs(Output.SupportNeeds.WpfWindow);
            }

            lines.Add($"{indent}    base.OnFrameworkInitializationCompleted();");
            lines.Add($"{indent}}}");
        }

        if (lines.Count > 0)
        {
            var text = string.Join(file.Newline, lines) + file.Newline;
            var parsed = SyntaxFactory.ParseCompilationUnit($"class C{{{file.Newline}{text}}}").Members.OfType<ClassDeclarationSyntax>().First().Members;
            var first = true;
            foreach (var member in parsed)
            {
                members.Add(first && members.Count > 0 ? member.WithLeadingTrivia(SyntaxFactory.ParseLeadingTrivia(file.Newline).AddRange(member.GetLeadingTrivia())) : member);
                first = false;
            }

            file.Report.Converted("Application → Avalonia's Initialize and OnFrameworkInitializationCompleted");
        }

        // An empty class body written on one line ("{ }") gets its members on lines of their own.
        var close = type.CloseBraceToken;
        if (type.Members.Count == 0)
        {
            close = close.WithLeadingTrivia(SyntaxFactory.ParseLeadingTrivia(indent[..Math.Max(0, indent.Length - 4)]));
        }

        return file.Root.ReplaceNode(type, type.WithMembers(SyntaxFactory.List(members)).WithCloseBraceToken(close));
    }

    /// <summary>WPF's OnStartup override as a method that OnFrameworkInitializationCompleted calls.</summary>
    private static MethodDeclarationSyntax StartupMethod(CodeFile file, MethodDeclarationSyntax startup)
    {
        var body = startup.Body?.WithStatements(SyntaxFactory.List(startup.Body.Statements.Where(s => !s.ToString().StartsWith("base.OnStartup(", StringComparison.Ordinal))));
        var parameter = startup.ParameterList.Parameters.FirstOrDefault()?.Identifier.ValueText ?? "e";
        var converted = startup
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword).WithTriviaFrom(startup.Modifiers[0]).WithTrailingTrivia(SyntaxFactory.Space)))
            .WithParameterList(SyntaxFactory.ParameterList())
            .WithBody(body);
        if (body is not null)
        {
            var uses = body.Statements.Where(s => s.DescendantNodes().OfType<IdentifierNameSyntax>().Any(i => i.Identifier.ValueText == parameter)).ToList();
            converted = converted.ReplaceNodes(
                converted.Body!.Statements.Where(s => uses.Any(u => u.IsEquivalentTo(s))),
                (_, s) => file.CommentOut(s, "OnStartup's arguments", "Avalonia's application has no StartupEventArgs; on the desktop, IClassicDesktopStyleApplicationLifetime has Args"));
        }

        file.Report.Converted("OnStartup → called from OnFrameworkInitializationCompleted");
        return converted;
    }

    private static string Indentation(ClassDeclarationSyntax type)
    {
        if (type.Members.FirstOrDefault() is { } member)
        {
            return member.GetLeadingTrivia().Where(t => t.IsKind(SyntaxKind.WhitespaceTrivia)).Select(t => t.ToString()).LastOrDefault() ?? "    ";
        }

        var classIndent = type.GetLeadingTrivia().Where(t => t.IsKind(SyntaxKind.WhitespaceTrivia)).Select(t => t.ToString()).LastOrDefault() ?? string.Empty;
        return classIndent + "    ";
    }
}
