using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TabStudio.Compilation.Wpf.Xaml;
using TabStudio.Core.Diagnostics;
using RoslynCompilation = Microsoft.CodeAnalysis.Compilation;
using TsProject = TabStudio.Core.Workspaces.ProjectInfo;

namespace TabStudio.Compilation.Wpf;

/// <summary>
/// What a WPF project's diagnostics say beyond the C# compiler's: an error in the code OpenSilver's XAML compiler
/// generated is placed in the XAML file it came from, a WPF type or member OpenSilver does not have is named as such
/// (<see cref="WpfDiagnosticIds.UnsupportedApi"/>), and code that uses what OpenSilver marks as not implemented gets
/// a warning (<see cref="WpfDiagnosticIds.NotImplementedApi"/>). Both point to the Windows Bridge, where the project
/// runs on real WPF.
/// </summary>
internal static class WpfDiagnostics
{
    /// <summary>The attribute OpenSilver marks the API it only declares with.</summary>
    private const string NotImplementedAttribute = "OpenSilver.NotImplementedAttribute";

    /// <summary>OpenSilver's not-implemented members whose effect TabStudio provides itself: it shows a window's title.</summary>
    private static readonly HashSet<string> ProvidedByTabStudio = new(StringComparer.Ordinal) { "System.Windows.Window.Title" };

    /// <summary>The names <see cref="Collect"/> found, by assembly identity and module version, which builds of the same files share.</summary>
    private static readonly ConcurrentDictionary<string, IReadOnlySet<string>> NotImplementedNames = new(StringComparer.Ordinal);

    /// <summary>
    /// Converts a WPF project's compiler diagnostics, placing errors in the second pass's generated code in their XAML
    /// files, sorted as <see cref="DiagnosticMapper"/> sorts them.
    /// </summary>
    /// <param name="diagnostics">The compiler's diagnostics.</param>
    /// <param name="project">The project.</param>
    public static IReadOnlyList<BuildDiagnostic> Map(IEnumerable<Diagnostic> diagnostics, TsProject project)
    {
        var texts = new ConcurrentDictionary<string, string?>(StringComparer.Ordinal);
        return diagnostics
            .Where(d => d.Severity != DiagnosticSeverity.Hidden)
            .Select(d =>
            {
                var mapped = DiagnosticMapper.Map(d, project.Name);
                return mapped.Location is { } location && WpfProject.SecondPassSource(project, location.FilePath) is { } xaml
                    ? InXaml(mapped, d, xaml, texts.GetOrAdd(xaml, Read))
                    : mapped;
            })
            .OrderByDescending(d => d.Severity)
            .ThenBy(d => d.Location?.FilePath, StringComparer.Ordinal)
            .ThenBy(d => d.Location?.Start ?? 0)
            .ToList();
    }

    /// <summary>Adds the WPF explanations to the diagnostics of one file, as the editor shows them.</summary>
    /// <param name="diagnostics">The file's diagnostics.</param>
    /// <param name="model">The file's semantic model.</param>
    /// <param name="project">The project.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    public static IReadOnlyList<BuildDiagnostic> Explain(IReadOnlyList<BuildDiagnostic> diagnostics, SemanticModel model, TsProject project, CancellationToken cancellationToken) =>
        [.. WpfUnsupportedApi.Explain(diagnostics, model, cancellationToken), .. NotImplementedUsages(model, project, cancellationToken)];

    /// <summary>Adds the WPF explanations to the diagnostics of a whole project.</summary>
    /// <param name="diagnostics">The project's diagnostics.</param>
    /// <param name="compilation">Its compilation.</param>
    /// <param name="project">The project.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    public static IReadOnlyList<BuildDiagnostic> Explain(IReadOnlyList<BuildDiagnostic> diagnostics, RoslynCompilation compilation, TsProject project, CancellationToken cancellationToken) =>
    [
        .. WpfUnsupportedApi.Explain(diagnostics, compilation, cancellationToken),
        .. compilation.SyntaxTrees
            .Where(tree => !tree.FilePath.Contains(nameof(WpfXamlGenerator), StringComparison.Ordinal) && WpfProject.SecondPassSource(project, tree.FilePath) is null)
            .SelectMany(tree => NotImplementedUsages(compilation.GetSemanticModel(tree), project, cancellationToken)),
    ];

    /// <summary>
    /// Every use in a file of a type or member OpenSilver declares but marks as not implemented: it compiles, and does
    /// nothing in the browser.
    /// </summary>
    private static IEnumerable<BuildDiagnostic> NotImplementedUsages(SemanticModel model, TsProject project, CancellationToken cancellationToken)
    {
        var names = NotImplementedNamesOf(model.Compilation);
        if (names.Count == 0)
        {
            yield break;
        }

        var reported = new HashSet<int>();
        foreach (var name in model.SyntaxTree.GetRoot(cancellationToken).DescendantNodes().OfType<SimpleNameSyntax>())
        {
            if (!names.Contains(name.Identifier.ValueText) || name.Parent is QualifiedNameSyntax { Right: var right } && right != name)
            {
                continue;
            }

            var symbol = model.GetSymbolInfo(name, cancellationToken).Symbol;
            if (symbol is IMethodSymbol { MethodKind: MethodKind.Constructor } constructor)
            {
                symbol = constructor.ContainingType;
            }

            if (symbol is null || !IsNotImplemented(symbol) || !reported.Add(name.SpanStart))
            {
                continue;
            }

            var location = name.GetLocation();
            var line = location.GetLineSpan().StartLinePosition;
            yield return new BuildDiagnostic(
                WpfDiagnosticIds.NotImplementedApi,
                DiagnosticSeverityLevel.Warning,
                NotImplementedMessage(Display(symbol)),
                new DiagnosticLocation(model.SyntaxTree.FilePath, location.SourceSpan.Start, location.SourceSpan.Length, line.Line + 1, line.Character + 1),
                project.Name);
        }
    }

    /// <summary>The message for API OpenSilver declares and does not implement.</summary>
    /// <param name="api">The API, such as <c>Window.Show()</c>.</param>
    public static string NotImplementedMessage(string api) =>
        $"{api} is in OpenSilver's WPF API but not implemented, so it does nothing in the web preview. To run this code on WPF itself, run the project on your PC through the Windows Bridge.";

    /// <summary>Whether a symbol, its type or its assembly carries OpenSilver's <c>[NotImplemented]</c>, unless TabStudio provides it.</summary>
    public static bool IsNotImplemented(ISymbol symbol) => !ProvidedByTabStudio.Contains(Qualified(symbol)) && CarriesNotImplemented(symbol);

    private static bool CarriesNotImplemented(ISymbol symbol)
    {
        for (var current = symbol; current is not null; current = current.ContainingType)
        {
            if (HasNotImplemented(current) || (current is IMethodSymbol { AssociatedSymbol: { } associated } && HasNotImplemented(associated)))
            {
                return true;
            }
        }

        return symbol.ContainingAssembly is { } assembly && HasNotImplemented(assembly);
    }

    private static bool HasNotImplemented(ISymbol symbol) =>
        symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == NotImplementedAttribute);

    /// <summary>
    /// The names of everything OpenSilver marks as not implemented, so only the uses of those names are looked up.
    /// Read once per OpenSilver assembly.
    /// </summary>
    private static HashSet<string> NotImplementedNamesOf(RoslynCompilation compilation)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            if (assembly.Name.StartsWith("OpenSilver", StringComparison.Ordinal))
            {
                var key = $"{assembly.Identity.GetDisplayName()} {assembly.Modules.FirstOrDefault()?.GetMetadata()?.GetModuleVersionId()}";
                names.UnionWith(NotImplementedNames.GetOrAdd(key, _ => Collect(assembly)));
            }
        }

        return names;
    }

    private static HashSet<string> Collect(IAssemblySymbol assembly)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<INamespaceOrTypeSymbol>([assembly.GlobalNamespace]);
        while (pending.TryPop(out var container))
        {
            foreach (var member in container.GetMembers())
            {
                if (member is INamespaceSymbol or INamedTypeSymbol { DeclaredAccessibility: Accessibility.Public })
                {
                    pending.Push((INamespaceOrTypeSymbol)member);
                }

                if (member is not INamespaceSymbol && member.DeclaredAccessibility == Accessibility.Public && HasNotImplemented(member))
                {
                    names.Add(member.Name);
                }
            }
        }

        return names;
    }

    /// <summary>A diagnostic in generated code, placed at the XAML that made it: an event handler at its attribute.</summary>
    private static BuildDiagnostic InXaml(BuildDiagnostic diagnostic, Diagnostic original, string xamlPath, string? xaml)
    {
        var message = $"In the code OpenSilver's XAML compiler made from {Path.GetFileName(xamlPath)}: {diagnostic.Message} ({diagnostic.Id})";
        var location = new DiagnosticLocation(xamlPath, 0, 0, 1, 1);
        if (xaml is not null && HandlerName(original) is { } handler && XamlDocument.Parse(xaml) is var document)
        {
            foreach (var element in document.Elements)
            {
                if (element.Attributes.FirstOrDefault(a => a.HasValue && a.Value.Trim() == handler && !a.IsNamespaceDeclaration) is { } attribute)
                {
                    var (line, column) = document.LineAndColumn(attribute.ValueStart);
                    location = new DiagnosticLocation(xamlPath, attribute.ValueStart, attribute.ValueEnd - attribute.ValueStart, line, column);
                    message = $"{attribute.LocalName}=\"{handler}\" names a method the class does not have: {diagnostic.Message}";
                    break;
                }
            }
        }

        return diagnostic with { Id = WpfDiagnosticIds.GeneratedCodeError, Message = message, Location = location };
    }

    /// <summary>The name a "does not contain a definition for" error is about, such as an event handler's.</summary>
    private static string? HandlerName(Diagnostic diagnostic) =>
        diagnostic.Id is "CS1061" or "CS0117" or "CS0103" or "CS0123" && diagnostic.Location.SourceTree is { } tree
            ? tree.GetRoot().FindNode(diagnostic.Location.SourceSpan) switch
            {
                MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
                SimpleNameSyntax simple => simple.Identifier.ValueText,
                var node => node.DescendantNodesAndSelf().OfType<SimpleNameSyntax>().LastOrDefault()?.Identifier.ValueText,
            }
            : null;

    private static string Qualified(ISymbol symbol) =>
        symbol is INamedTypeSymbol type ? type.ToDisplayString() : $"{symbol.ContainingType?.ToDisplayString()}.{symbol.Name}";

    private static string Display(ISymbol symbol) => symbol switch
    {
        INamedTypeSymbol type => type.Name,
        IMethodSymbol method => string.Create(CultureInfo.InvariantCulture, $"{method.ContainingType.Name}.{method.Name}()"),
        _ => $"{symbol.ContainingType?.Name}.{symbol.Name}",
    };

    private static string? Read(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
