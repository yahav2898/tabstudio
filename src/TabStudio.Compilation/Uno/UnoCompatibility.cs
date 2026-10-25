using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TabStudio.Core.Diagnostics;
using RoslynCompilation = Microsoft.CodeAnalysis.Compilation;
using TsProject = TabStudio.Core.Workspaces.ProjectInfo;

namespace TabStudio.Compilation.Uno;

/// <summary>
/// What of an app's WinUI code does not run on Uno Platform's Skia renderer, named in the Error List with what to do
/// about it, the Windows Bridge for code that needs Windows:
/// <list type="bullet">
///   <item>WinUI APIs that Uno declares but does not implement, which it marks <c>[Uno.NotImplemented]</c>: they compile,
///   and do nothing or throw when the app runs (<see cref="NotImplementedId"/>, a warning, as Uno's own analyzer does).</item>
///   <item>Windows App SDK and Win32 APIs that Uno does not have at all, such as <c>WinRT.Interop</c>: the compiler's
///   "type not found" error is replaced with one that says so (<see cref="WindowsOnlyId"/>).</item>
///   <item>Calls into Windows' native libraries, such as <c>user32.dll</c>, which fail when the app runs (<see cref="WindowsLibraryId"/>).</item>
/// </list>
/// </summary>
public static class UnoCompatibility
{
    /// <summary>An API Uno Platform does not implement for the Skia renderer.</summary>
    public const string NotImplementedId = "TS7101";

    /// <summary>A Windows-only API that Uno Platform does not provide.</summary>
    public const string WindowsOnlyId = "TS7102";

    /// <summary>A call into one of Windows' native libraries.</summary>
    public const string WindowsLibraryId = "TS7103";

    private const string NotImplementedAttribute = "Uno.NotImplementedAttribute";

    /// <summary>The platform names Uno's <c>[NotImplemented]</c> lists that cover the Skia renderer TabStudio runs.</summary>
    private static readonly string[] SkiaPlatforms = ["__SKIA__", "__NETSTD_REFERENCE__"];

    /// <summary>Namespaces of the Windows App SDK and of Windows interop that only Windows has, with what each is for.</summary>
    private static readonly (string Namespace, string What)[] WindowsOnlyNamespaces =
    [
        ("WinRT.Interop", "Windows window handles (WinRT.Interop)"),
        ("WinRT", "C#/WinRT interop"),
        ("Microsoft.Windows.AppLifecycle", "the Windows App SDK's app lifecycle"),
        ("Microsoft.Windows.AppNotifications", "Windows notifications"),
        ("Microsoft.Windows.PushNotifications", "Windows push notifications"),
        ("Microsoft.Windows.ApplicationModel.DynamicDependency", "the Windows App SDK's bootstrapper"),
        ("Microsoft.Windows.ApplicationModel.WindowsAppRuntime", "the Windows App SDK runtime"),
        ("Microsoft.Windows.AppInstaller", "App Installer"),
        ("Microsoft.Windows.Management.Deployment", "Windows package deployment"),
        ("Microsoft.Windows.System.Power", "Windows power management"),
        ("Microsoft.Windows.Widgets", "Windows widgets"),
        ("Microsoft.Windows.Storage", "Windows App SDK storage"),
        ("Microsoft.Windows.AI", "Windows AI"),
        ("Microsoft.Graphics.Canvas", "Win2D"),
        ("Windows.Win32", "Win32 APIs (CsWin32)"),
    ];

    /// <summary>Windows' native libraries, which no other platform has.</summary>
    private static readonly HashSet<string> WindowsLibraries = new(StringComparer.OrdinalIgnoreCase)
    {
        "user32", "kernel32", "gdi32", "shell32", "ole32", "oleaut32", "advapi32", "comctl32", "comdlg32", "dwmapi",
        "uxtheme", "winmm", "ntdll", "shcore", "dxgi", "d3d11", "d2d1", "dwrite", "winspool", "msvcrt", "Microsoft.ui.xaml",
    };

    /// <summary>Finds the uses of WinUI APIs Uno Platform does not implement, and the calls into Windows' libraries, in one file.</summary>
    /// <param name="model">The semantic model of the file.</param>
    /// <param name="project">The file's project; nothing is reported for projects that do not use Uno.</param>
    /// <param name="cancellationToken">Cancels the analysis.</param>
    public static IReadOnlyList<BuildDiagnostic> AnalyzeTree(SemanticModel model, TsProject? project, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        return project is null ? [] : DiagnosticMapper.Map(Find(model, project, cancellationToken), project.Name);
    }

    /// <summary>
    /// Finds what <see cref="AnalyzeTree"/> finds in the files of a compilation that are the app's: its own, and the code
    /// Uno's XAML generator wrote for its XAML, which is where the elements of a XAML file use WinUI's types.
    /// </summary>
    /// <param name="compilation">The compilation.</param>
    /// <param name="project">Its project.</param>
    /// <param name="cancellationToken">Cancels the analysis.</param>
    internal static IEnumerable<Diagnostic> FindAll(RoslynCompilation compilation, TsProject project, CancellationToken cancellationToken) =>
        compilation.SyntaxTrees
            .Where(tree => Path.IsPathRooted(tree.FilePath) || tree.FilePath.Contains("XamlCodeGenerator", StringComparison.Ordinal))
            .SelectMany(tree => Find(compilation.GetSemanticModel(tree), project, cancellationToken));

    private static List<Diagnostic> Find(SemanticModel model, TsProject project, CancellationToken cancellationToken)
    {
        if (!UnoBuild.UsesUno(project) || model.Compilation.GetTypeByMetadataName(NotImplementedAttribute) is not { } attribute)
        {
            return [];
        }

        var found = new List<Diagnostic>();
        var reported = new HashSet<(int Line, string Symbol)>();
        var root = model.SyntaxTree.GetRoot(cancellationToken);
        foreach (var node in root.DescendantNodes())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (node is AttributeSyntax dllImport && WindowsLibraryOf(model, dllImport, cancellationToken) is { } library)
            {
                found.Add(Create(WindowsLibraryId, DiagnosticSeverity.Warning, dllImport.GetLocation(),
                    $"{library} is a Windows library: the call into it fails when the app runs on Android. Run the app on your PC with the Windows Bridge, or call it only on Windows (#if WINDOWS)."));
                continue;
            }

            if (node is not (IdentifierNameSyntax or GenericNameSyntax or ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax)
                || NotImplementedSymbol(model, node, attribute, cancellationToken) is not { } symbol)
            {
                continue;
            }

            var location = node is ObjectCreationExpressionSyntax creation ? creation.Type.GetLocation() : node.GetLocation();
            if (reported.Add((location.GetLineSpan().StartLinePosition.Line, symbol.ToDisplayString())))
            {
                found.Add(Create(NotImplementedId, DiagnosticSeverity.Warning, location,
                    $"{Describe(symbol)} is not implemented by Uno Platform, so it has no effect in the app preview and on Android. Run the app on your PC with the Windows Bridge to use it."));
            }
        }

        return found;
    }

    /// <summary>
    /// The compiler's diagnostic, or a clearer one when it reports that a Windows-only namespace or type does not exist,
    /// because Uno Platform does not provide it.
    /// </summary>
    /// <param name="diagnostic">A diagnostic of an Uno project's compilation.</param>
    public static Diagnostic Explain(Diagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        if (diagnostic.Id is not ("CS0246" or "CS0234" or "CS0103" or "CS0400") || !diagnostic.Location.IsInSource || diagnostic.Location.SourceTree is not { } tree)
        {
            return diagnostic;
        }

        var node = tree.GetRoot().FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        var name = FullName(node);
        foreach (var (ns, what) in WindowsOnlyNamespaces)
        {
            if (name == ns || name.StartsWith(ns + ".", StringComparison.Ordinal))
            {
                return Create(WindowsOnlyId, DiagnosticSeverity.Error, diagnostic.Location,
                    $"{name} is part of {what}, which only Windows has: Uno Platform does not provide it on Android. Run the app on your PC with the Windows Bridge, or use it only on Windows (#if WINDOWS).");
            }
        }

        return diagnostic;
    }

    /// <summary>The not-implemented API a name refers to: the member, or its type when the whole type is not implemented.</summary>
    private static ISymbol? NotImplementedSymbol(SemanticModel model, SyntaxNode node, INamedTypeSymbol attribute, CancellationToken cancellationToken)
    {
        var symbol = model.GetSymbolInfo(node, cancellationToken).Symbol;
        if (symbol is null || !IsUno(symbol))
        {
            return null;
        }

        if (symbol is IMethodSymbol { MethodKind: MethodKind.Constructor } constructor)
        {
            return IsNotImplemented(constructor.ContainingType, attribute) ? constructor.ContainingType
                : IsNotImplemented(constructor, attribute) ? constructor
                : null;
        }

        if (symbol is INamedTypeSymbol type)
        {
            return IsNotImplemented(type, attribute) ? type : null;
        }

        // A member of a type Uno does not implement at all is reported as the type, once.
        return symbol.ContainingType is { } containing && IsNotImplemented(containing, attribute) ? containing
            : IsNotImplemented(symbol, attribute) ? symbol
            : null;
    }

    /// <summary>Whether Uno marks a symbol as not implemented for the Skia renderer.</summary>
    private static bool IsNotImplemented(ISymbol symbol, INamedTypeSymbol attribute)
    {
        foreach (var data in symbol.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(data.AttributeClass, attribute))
            {
                continue;
            }

            // [NotImplemented] alone is everywhere; [NotImplemented("__ANDROID__", "__SKIA__", …)] lists the platforms.
            if (data.ConstructorArguments is not [{ Kind: TypedConstantKind.Array } platforms])
            {
                return true;
            }

            return platforms.Values.Any(p => p.Value is string name && SkiaPlatforms.Contains(name, StringComparer.Ordinal));
        }

        return false;
    }

    private static bool IsUno(ISymbol symbol) => symbol.ContainingAssembly?.Name is { } name && (name == "Uno" || name.StartsWith("Uno.", StringComparison.Ordinal));

    private static string Describe(ISymbol symbol) => symbol switch
    {
        INamedTypeSymbol type => type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
        IMethodSymbol { MethodKind: MethodKind.Constructor } constructor => $"The {constructor.ContainingType.Name} constructor",
        _ => $"{symbol.ContainingType?.Name}.{symbol.Name}",
    };

    /// <summary>The Windows library a <c>[DllImport]</c> or <c>[LibraryImport]</c> names, if it is one.</summary>
    private static string? WindowsLibraryOf(SemanticModel model, AttributeSyntax attribute, CancellationToken cancellationToken)
    {
        var name = attribute.Name.ToString();
        if (!name.EndsWith("DllImport", StringComparison.Ordinal) && !name.EndsWith("DllImportAttribute", StringComparison.Ordinal)
            && !name.EndsWith("LibraryImport", StringComparison.Ordinal) && !name.EndsWith("LibraryImportAttribute", StringComparison.Ordinal))
        {
            return null;
        }

        if (attribute.ArgumentList?.Arguments is not [var first, ..]
            || model.GetConstantValue(first.Expression, cancellationToken) is not { HasValue: true, Value: string library })
        {
            return null;
        }

        var file = Path.GetFileNameWithoutExtension(library.Trim());
        return WindowsLibraries.Contains(file) ? file + ".dll" : null;
    }

    /// <summary>The dotted name a node is part of, from its outermost qualified name or member access.</summary>
    private static string FullName(SyntaxNode node)
    {
        var current = node;
        while (current.Parent is QualifiedNameSyntax or MemberAccessExpressionSyntax or AliasQualifiedNameSyntax)
        {
            current = current.Parent;
        }

        return current switch
        {
            QualifiedNameSyntax or IdentifierNameSyntax or MemberAccessExpressionSyntax => current.ToString().Replace("global::", string.Empty, StringComparison.Ordinal),
            UsingDirectiveSyntax { NamespaceOrType: { } target } => target.ToString(),
            _ => node.ToString(),
        };
    }

    private static Diagnostic Create(string id, DiagnosticSeverity severity, Location location, string message) =>
        Diagnostic.Create(new DiagnosticDescriptor(id, id, message.Replace("{", "{{", StringComparison.Ordinal).Replace("}", "}}", StringComparison.Ordinal), "Compatibility", severity, isEnabledByDefault: true), location);
}
