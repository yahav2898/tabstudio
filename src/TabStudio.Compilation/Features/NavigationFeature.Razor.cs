using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using TabStudio.Compilation.Razor;

namespace TabStudio.Compilation.Features;

/// <summary>
/// Go to Definition in and into Razor files. In a Razor file, a component's tag goes to the component, one of its
/// attributes to the parameter, and C# to its symbol through the generated C#. A symbol declared in a Razor file is
/// found in the Razor file, not in the C# generated for it: a component at the top of its <c>.razor</c> file, and a
/// member of its <c>@code</c> block on its line there.
/// </summary>
public sealed partial class NavigationFeature
{
    /// <summary>Finds the definition of what is at a position of a Razor file.</summary>
    private async Task<DefinitionResult?> FindRazorDefinitionAsync(string filePath, int position, CancellationToken cancellationToken)
    {
        if (await _host.GetRazorGeneratedDocumentAsync(filePath, cancellationToken).ConfigureAwait(false) is not { } razor)
        {
            return null;
        }

        var (generated, text) = razor;
        if (await generated.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false) is not { } tree
            || await generated.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false) is not { } compilation)
        {
            return null;
        }

        var caret = Math.Clamp(position, 0, text.Length);
        var context = RazorMarkupContext.At(text, caret);
        if (context.TagName is { Length: > 0 } tag
            && context.Kind is RazorMarkupKind.TagName or RazorMarkupKind.AttributeName
            && RazorComponents.Find(compilation, tree, tag) is { } component)
        {
            if (context.Kind == RazorMarkupKind.TagName)
            {
                return await ToDefinitionAsync(component, cancellationToken).ConfigureAwait(false);
            }

            var attribute = text.ToString(new TextSpan(context.WordStart, context.WordLength)).Replace("@bind-", string.Empty, StringComparison.Ordinal).Split(':')[0];
            if (RazorComponents.Parameters(component).FirstOrDefault(p => string.Equals(p.Name, attribute, StringComparison.Ordinal)) is { } parameter)
            {
                return await ToDefinitionAsync(parameter, cancellationToken).ConfigureAwait(false);
            }
        }

        if (RazorSourceMap.ToGenerated(tree, filePath, text, caret, cancellationToken) is not { } mapped
            || await SymbolResolver.FindSymbolAsync(generated, mapped, cancellationToken).ConfigureAwait(false) is not { } symbol)
        {
            return null;
        }

        if (symbol is IMethodSymbol { MethodKind: MethodKind.Constructor, IsImplicitlyDeclared: true } constructor)
        {
            symbol = constructor.ContainingType;
        }

        var source = await SymbolFinder.FindSourceDefinitionAsync(symbol, generated.Project.Solution, cancellationToken).ConfigureAwait(false);
        return await ToDefinitionAsync(source ?? symbol, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Describes where a symbol is defined, placing what a Razor file declares in that file.</summary>
    private async Task<DefinitionResult> ToDefinitionAsync(ISymbol symbol, CancellationToken cancellationToken)
    {
        var definition = SymbolResolver.ToDefinition(symbol);
        if (!symbol.Locations.Any(l => l.SourceTree is { } tree && RazorSourceMap.SourceFileOf(tree) is not null))
        {
            return definition;
        }

        var locations = new List<SourceLocation>();
        foreach (var location in symbol.Locations)
        {
            if (location.SourceTree is { } tree && RazorSourceMap.SourceFileOf(tree) is { } razorFile)
            {
                locations.Add(await InRazorFileAsync(location, razorFile, cancellationToken).ConfigureAwait(false));
            }
            else if (SymbolResolver.ToSourceLocation(location) is { } source)
            {
                locations.Add(source);
            }
        }

        return definition with
        {
            Locations = [.. locations.DistinctBy(l => (l.FilePath, l.Start)).OrderBy(l => l.FilePath, StringComparer.Ordinal).ThenBy(l => l.Start)],
        };
    }

    /// <summary>
    /// Where a declaration in a Razor file's generated C# is in the file: on its line when the Razor compiler mapped
    /// it there, as for the members of <c>@code</c>, and at the top for what it wrote itself, such as the class.
    /// </summary>
    private async Task<SourceLocation> InRazorFileAsync(Location location, string razorFile, CancellationToken cancellationToken)
    {
        var mapped = location.GetMappedLineSpan();
        var text = await _host.GetRazorTextAsync(razorFile, cancellationToken).ConfigureAwait(false);
        if (text is null || !mapped.HasMappedPath || !string.Equals(mapped.Path.Replace('\\', '/'), razorFile.Replace('\\', '/'), StringComparison.Ordinal)
            || mapped.StartLinePosition.Line >= text.Lines.Count)
        {
            return new SourceLocation(razorFile, 0, 0, 1, 1);
        }

        var line = text.Lines[mapped.StartLinePosition.Line];
        var start = Math.Min(line.Start + mapped.StartLinePosition.Character, line.End);
        return new SourceLocation(razorFile, start, location.SourceSpan.Length, mapped.StartLinePosition.Line + 1, mapped.StartLinePosition.Character + 1);
    }
}
