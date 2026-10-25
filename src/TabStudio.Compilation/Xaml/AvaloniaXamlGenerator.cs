using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using RoslynCompilation = Microsoft.CodeAnalysis.Compilation;

namespace TabStudio.Compilation.Xaml;

/// <summary>
/// Gives Avalonia projects their XAML inside TabStudio. Avalonia normally compiles XAML into the assembly after
/// the C# compiler runs, a build step TabStudio does not have; instead, this generator puts each <c>.axaml</c> file
/// into the assembly as text, and the app preview loads it at run time with Avalonia's XAML loader.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>A file that is not well-formed XML is a compiler error naming the file (an <c>#error</c>, CS1029).</item>
///   <item>For every file with an <c>x:Class</c>, it generates what Avalonia's own generator does:
///   <c>InitializeComponent</c> and a field for every <c>x:Name</c>.</item>
///   <item>Calls to <c>AvaloniaXamlLoader.Load(this)</c>, such as the one in <c>App.Initialize</c>, are intercepted
///   and load the class's XAML the same way.</item>
///   <item>The loader is what the host puts in <see cref="AppContext"/> under <see cref="LoaderKey"/>: a delegate
///   taking the root object, its XAML and the file's path in the app (<c>/Views/MainView.axaml</c>), which relative
///   addresses in the XAML, such as <c>/Assets/logo.png</c>, are resolved against.</item>
/// </list>
/// <para>
/// It runs inside TabStudio's own Roslyn (see <see cref="GeneratorReference"/>) rather than shipping as an
/// analyzer assembly, so it is not marked with <c>[Generator]</c>.
/// </para>
/// </remarks>
public sealed class AvaloniaXamlGenerator : IIncrementalGenerator
{
    /// <summary>
    /// The <see cref="AppContext"/> data key of the <c>Action&lt;object, string, string&gt;</c> that loads XAML into an
    /// object: the object, the XAML, and the file's path in the app.
    /// </summary>
    public const string LoaderKey = "TabStudio.Avalonia.LoadXaml";

    /// <summary>The namespace of the generated helpers, which the interceptors feature is turned on for.</summary>
    public const string GeneratedNamespace = "TabStudio.Generated";

    /// <summary>The XAML namespace of Avalonia's controls.</summary>
    private const string AvaloniaNamespace = "https://github.com/avaloniaui";

    private const string XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var documents = context.AdditionalTextsProvider
            .Where(static text => text.Path.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase))
            .Select(static (text, cancellationToken) => XamlDocument.Read(text, cancellationToken))
            .Collect();

        var loads = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Load" },
                    ArgumentList.Arguments.Count: 1,
                },
                static (syntax, cancellationToken) => LoaderCall(syntax, cancellationToken))
            .Where(static location => location is not null)
            .Collect();

        // The project folder, from the build properties, so each file's path in the app is known (/Views/MainView.axaml).
        var projectDirectory = context.AnalyzerConfigOptionsProvider.Select(static (options, _) =>
            options.GlobalOptions.TryGetValue("build_property.ProjectDir", out var directory) ? directory : null);

        context.RegisterSourceOutput(
            documents.Combine(loads).Combine(context.CompilationProvider).Combine(projectDirectory),
            static (output, input) => Generate(output, input.Left.Left.Left, input.Left.Left.Right!, input.Left.Right, input.Right));
    }

    /// <summary>The interceptable location of a call to <c>AvaloniaXamlLoader.Load(object)</c>, or null for any other call.</summary>
    private static InterceptableLocation? LoaderCall(GeneratorSyntaxContext syntax, CancellationToken cancellationToken)
    {
        var invocation = (InvocationExpressionSyntax)syntax.Node;
        return syntax.SemanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol
        {
            Name: "Load",
            ContainingType: { Name: "AvaloniaXamlLoader", ContainingNamespace: { } ns },
            Parameters: [{ Type.SpecialType: SpecialType.System_Object }],
        } && ns.ToDisplayString() == "Avalonia.Markup.Xaml"
            ? syntax.SemanticModel.GetInterceptableLocation(invocation, cancellationToken)
            : null;
    }

    private static void Generate(
        SourceProductionContext output,
        ImmutableArray<XamlDocument> documents,
        ImmutableArray<InterceptableLocation?> loads,
        RoslynCompilation compilation,
        string? projectDirectory)
    {
        var classes = new List<XamlDocument>();
        foreach (var document in documents)
        {
            if (document.Error is { } error)
            {
                // An #error directive, so the problem is a compiler error like any other, in the Error List and in builds.
                var message = $"{Path.GetFileName(document.Path)} is not valid XAML: {error}".ReplaceLineEndings(" ");
                output.AddSource($"{Path.GetFileNameWithoutExtension(document.Path)}.InvalidXaml.g.cs", $"// <auto-generated/>\n#error {message}\n");
            }
            else if (document.ClassName is not null)
            {
                classes.Add(document);
            }
        }

        if (classes.Count == 0 && loads.IsEmpty)
        {
            return;
        }

        var types = new XamlTypeResolver(compilation);
        foreach (var document in classes)
        {
            output.AddSource($"{document.ClassName}.g.cs", GenerateClass(document, types, DeclaresInitializeComponent(compilation, document.ClassName!)));
        }

        output.AddSource("TabStudioXaml.g.cs", GenerateLoader(classes, loads.OfType<InterceptableLocation>().ToList(), projectDirectory));
    }

    /// <summary>
    /// A file's path in the app, as Avalonia's build gives it to resources and the base address of its XAML: from the
    /// project folder, with forward slashes and a leading one, such as <c>/Views/MainView.axaml</c>.
    /// </summary>
    private static string AppPath(string path, string? projectDirectory)
    {
        var relative = projectDirectory is { Length: > 0 } && path.StartsWith(projectDirectory, StringComparison.Ordinal)
            ? path[projectDirectory.Length..]
            : Path.GetFileName(path);
        return "/" + relative.Replace('\\', '/').TrimStart('/');
    }

    /// <summary>Whether the class's own code declares <c>InitializeComponent</c>, as code written for Avalonia 0.10 does.</summary>
    private static bool DeclaresInitializeComponent(RoslynCompilation compilation, string className) =>
        compilation.GetTypeByMetadataName(className)?.GetMembers("InitializeComponent").Any(m => !m.IsImplicitlyDeclared) == true;

    /// <summary>The partial class of one XAML file: its named elements and <c>InitializeComponent</c>.</summary>
    private static string GenerateClass(XamlDocument document, XamlTypeResolver types, bool hasOwnInitializeComponent)
    {
        var className = document.ClassName!;
        var dot = className.LastIndexOf('.');
        var ns = dot < 0 ? null : className[..dot];
        var name = dot < 0 ? className : className[(dot + 1)..];

        var text = new StringBuilder("// <auto-generated/>\n#nullable enable\n");
        if (ns is not null)
        {
            text.Append(CultureInfo.InvariantCulture, $"namespace {ns}\n{{\n");
        }

        text.Append(CultureInfo.InvariantCulture, $"partial class {name}\n{{\n");
        foreach (var element in document.NamedElements)
        {
            var type = types.Resolve(element.XmlNamespace, element.TypeName);
            text.Append(CultureInfo.InvariantCulture, $"    {element.Modifier} {type}? {element.Name};\n");
        }

        if (hasOwnInitializeComponent)
        {
            text.Append("}\n");
            return ns is null ? text.ToString() : text.Append("}\n").ToString();
        }

        text.Append("""

                /// <summary>Loads this class's XAML and finds its named elements, as Avalonia's generated method does.</summary>
                /// <param name="loadXaml">Whether to load the XAML; the named elements are found either way.</param>
                public void InitializeComponent(bool loadXaml = true)
                {
                    if (loadXaml)
                    {
                        global::TabStudio.Generated.TabStudioXaml.Load(this);
                    }

            """);
        if (document.NamedElements.Count > 0)
        {
            text.Append("        var __scope = (object)this is global::Avalonia.StyledElement __element ? global::Avalonia.Controls.NameScope.GetNameScope(__element) : null;\n");
            foreach (var element in document.NamedElements)
            {
                var type = types.Resolve(element.XmlNamespace, element.TypeName);
                text.Append(CultureInfo.InvariantCulture, $"        {element.Name} = __scope?.Find(\"{element.Name}\") as {type};\n");
            }
        }

        text.Append("    }\n}\n");
        if (ns is not null)
        {
            text.Append("}\n");
        }

        return text.ToString();
    }

    /// <summary>The XAML of every class with its path in the app, the loader that hands it to the host, and the interceptors.</summary>
    private static string GenerateLoader(List<XamlDocument> classes, List<InterceptableLocation> loads, string? projectDirectory)
    {
        var text = new StringBuilder($$"""
            // <auto-generated/>
            #nullable enable
            namespace {{GeneratedNamespace}}
            {
                /// <summary>The XAML of the project's classes, loaded at run time by the host that runs the app.</summary>
                internal static class TabStudioXaml
                {
                    private static readonly global::System.Collections.Generic.Dictionary<global::System.Type, (string Xaml, string Path)> Documents = new()
                    {

            """);
        foreach (var document in classes)
        {
            var xaml = SymbolDisplay.FormatLiteral(document.Text, quote: true);
            var path = SymbolDisplay.FormatLiteral(AppPath(document.Path, projectDirectory), quote: true);
            text.Append(CultureInfo.InvariantCulture, $"            [typeof(global::{document.ClassName})] = ({xaml}, {path}),\n");
        }

        text.Append(CultureInfo.InvariantCulture, $$"""
                    };

                    /// <summary>Loads the XAML of an object's class into it.</summary>
                    public static void Load(object root)
                    {
                        if (!Documents.TryGetValue(root.GetType(), out var document))
                        {
                            throw new global::System.InvalidOperationException($"{root.GetType()} has no XAML file with x:Class=\"{root.GetType()}\".");
                        }

                        var load = global::System.AppContext.GetData("{{LoaderKey}}") as global::System.Action<object, string, string>
                            ?? throw new global::System.InvalidOperationException("Avalonia XAML compiled by TabStudio loads in TabStudio's app preview.");
                        load(root, document.Xaml, document.Path);
                    }
                }

            """);

        if (loads.Count > 0)
        {
            text.Append("""
                    /// <summary>Calls to AvaloniaXamlLoader.Load(this), redirected to the XAML compiled into the project.</summary>
                    file static class TabStudioXamlInterceptors
                    {

                """);
            for (var i = 0; i < loads.Count; i++)
            {
                text.Append(CultureInfo.InvariantCulture, $"        {loads[i].GetInterceptsLocationAttributeSyntax()}\n");
                text.Append(CultureInfo.InvariantCulture, $"        public static void Load{i}(object root) => TabStudioXaml.Load(root);\n\n");
            }

            text.Append("    }\n");
        }

        text.Append("""
            }

            namespace System.Runtime.CompilerServices
            {
                [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]
                file sealed class InterceptsLocationAttribute : global::System.Attribute
                {
                    // Only the compiler reads the arguments; a primary constructor would warn that they are unused.
                    public InterceptsLocationAttribute(int version, string data)
                    {
                    }
                }
            }

            """);
        return text.ToString();
    }

    /// <summary>One <c>.axaml</c> file, as the generator needs it.</summary>
    private sealed record XamlDocument(string Path, string Text, string? ClassName, IReadOnlyList<NamedElement> NamedElements, string? Error)
    {
        public static XamlDocument Read(AdditionalText file, CancellationToken cancellationToken)
        {
            var text = file.GetText(cancellationToken)?.ToString() ?? string.Empty;
            try
            {
                var root = XDocument.Parse(text).Root!;
                var className = root.Attribute(XName.Get("Class", XamlNamespace))?.Value;
                var named = new List<NamedElement>();
                foreach (var element in root.DescendantsAndSelf().Skip(1))
                {
                    var name = element.Attribute(XName.Get("Name", XamlNamespace))?.Value ?? element.Attribute("Name")?.Value;
                    if (name is null || !SyntaxFacts.IsValidIdentifier(name) || element.Name.LocalName.Contains('.', StringComparison.Ordinal)
                        || InsideTemplate(element) || named.Any(n => n.Name == name))
                    {
                        continue;
                    }

                    var modifier = element.Attribute(XName.Get("FieldModifier", XamlNamespace))?.Value.ToLowerInvariant() switch
                    {
                        "public" => "public",
                        "private" => "private",
                        "protected" => "protected",
                        _ => "internal",
                    };
                    named.Add(new NamedElement(name, element.Name.NamespaceName, element.Name.LocalName, modifier));
                }

                return new XamlDocument(file.Path, text, className, named, null);
            }
            catch (XmlException ex)
            {
                return new XamlDocument(file.Path, text, null, [], ex.Message);
            }
        }

        /// <summary>Elements in templates belong to the controls the template makes, not to the class.</summary>
        private static bool InsideTemplate(XElement element) =>
            element.Ancestors().Any(a => a.Name.LocalName.EndsWith("Template", StringComparison.Ordinal) || a.Name.LocalName is "Style" or "ControlTheme");
    }

    /// <summary>An element with <c>x:Name</c>.</summary>
    private sealed record NamedElement(string Name, string XmlNamespace, string TypeName, string Modifier);

    /// <summary>Finds the .NET type of a XAML element, from its XML namespace and name.</summary>
    private sealed class XamlTypeResolver(RoslynCompilation compilation)
    {
        private readonly Dictionary<string, List<string>> _xmlnsDefinitions = ReadXmlnsDefinitions(compilation);

        public string Resolve(string xmlNamespace, string typeName)
        {
            foreach (var ns in ClrNamespaces(xmlNamespace))
            {
                if (compilation.GetTypeByMetadataName($"{ns}.{typeName}") is { } type)
                {
                    return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                }
            }

            return compilation.GetTypeByMetadataName("Avalonia.Controls.Control") is not null
                ? "global::Avalonia.Controls.Control"
                : "object";
        }

        private List<string> ClrNamespaces(string xmlNamespace)
        {
            if (xmlNamespace.StartsWith("using:", StringComparison.Ordinal))
            {
                return [xmlNamespace["using:".Length..]];
            }

            if (xmlNamespace.StartsWith("clr-namespace:", StringComparison.Ordinal))
            {
                var value = xmlNamespace["clr-namespace:".Length..];
                var semicolon = value.IndexOf(';', StringComparison.Ordinal);
                return [semicolon < 0 ? value : value[..semicolon]];
            }

            return _xmlnsDefinitions.TryGetValue(xmlNamespace.Length == 0 ? AvaloniaNamespace : xmlNamespace, out var namespaces) ? namespaces : [];
        }

        /// <summary>The <c>[XmlnsDefinition]</c> attributes of every referenced assembly, such as Avalonia's.</summary>
        private static Dictionary<string, List<string>> ReadXmlnsDefinitions(RoslynCompilation compilation)
        {
            var definitions = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols.Append(compilation.Assembly))
            {
                foreach (var attribute in assembly.GetAttributes())
                {
                    if (attribute.AttributeClass?.Name == "XmlnsDefinitionAttribute"
                        && attribute.ConstructorArguments is [{ Value: string xml }, { Value: string clr }])
                    {
                        if (!definitions.TryGetValue(xml, out var namespaces))
                        {
                            namespaces = [];
                            definitions[xml] = namespaces;
                        }

                        namespaces.Add(clr);
                    }
                }
            }

            return definitions;
        }
    }
}
