using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TabStudio.Compilation.Xaml;

/// <summary>A source generator built into TabStudio, given to Roslyn like a package's analyzer assembly.</summary>
/// <param name="generator">The generator.</param>
/// <param name="display">Its name, as diagnostics show it.</param>
internal sealed class GeneratorReference(IIncrementalGenerator generator, string display) : AnalyzerReference
{
    private readonly ImmutableArray<ISourceGenerator> _generators = [generator.AsSourceGenerator()];

    /// <summary>The XAML generator of Avalonia projects, shared by every project that needs it.</summary>
    public static GeneratorReference AvaloniaXaml { get; } = new(new AvaloniaXamlGenerator(), "TabStudio Avalonia XAML");

    /// <inheritdoc />
    public override string? FullPath => null;

    /// <inheritdoc />
    public override string Display => display;

    /// <inheritdoc />
    public override object Id => this;

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzers(string language) => [];

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzersForAllLanguages() => [];

    /// <inheritdoc />
    public override ImmutableArray<ISourceGenerator> GetGenerators(string language) =>
        language == LanguageNames.CSharp ? _generators : [];

    /// <inheritdoc />
    public override ImmutableArray<ISourceGenerator> GetGeneratorsForAllLanguages() => _generators;
}
