namespace TabStudio.Compilation.Features;

/// <summary>Where a symbol is defined, for Go to Definition and Go to Implementation.</summary>
/// <param name="SymbolDisplay">The symbol as text, such as <c>Lib.Greeter.Greet(string)</c>.</param>
/// <param name="Kind">The icon to show.</param>
/// <param name="Locations">
/// Its declarations in source, in file and position order. Partial types and methods have several. Empty when the
/// symbol comes from a compiled assembly.
/// </param>
/// <param name="AssemblyName">The assembly the symbol is defined in, such as <c>System.Console</c>, or <see langword="null"/>.</param>
public sealed record DefinitionResult(string SymbolDisplay, SymbolGlyph Kind, IReadOnlyList<SourceLocation> Locations, string? AssemblyName)
{
    /// <summary>
    /// Whether the symbol is only in metadata, such as a .NET library type. The editor can show
    /// <see cref="SymbolDisplay"/> and <see cref="AssemblyName"/> instead of opening a file.
    /// </summary>
    public bool IsInMetadata => Locations.Count == 0;
}
