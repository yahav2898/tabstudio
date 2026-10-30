using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TabStudio.Compilation;

/// <summary>
/// The source generators of a project of the workspace that another project references with
/// <c>OutputItemType="Analyzer"</c>, as Mediator, Terminal.Gui and Spectre.Console reference their own generators. It is
/// given to Roslyn like a package's analyzer assembly; the generator project is compiled into memory and loaded the first
/// time Roslyn asks for its generators, and the host replaces the reference when the project changes.
/// </summary>
/// <remarks>
/// Diagnostic analyzers in the project are not run, as with a package's: on a tablet the compiler's own diagnostics are
/// what the Error List shows.
/// </remarks>
internal sealed class ProjectGeneratorReference : AnalyzerReference
{
    private readonly string _display;
    private readonly Func<ProjectGeneratorBuild> _builder;
    private readonly Lock _gate = new();
    private ProjectGeneratorBuild? _build;
    private bool _building;

    /// <summary>Creates a reference whose generators <paramref name="build"/> compiles and loads, once, when they are first needed.</summary>
    /// <param name="projectName">The generator project's name, as diagnostics show it.</param>
    /// <param name="build">Compiles and loads the generator project. It must not throw.</param>
    public ProjectGeneratorReference(string projectName, Func<ProjectGeneratorBuild> build)
    {
        _display = projectName;
        _builder = build;
    }

    /// <summary>
    /// Why the generators are missing, such as the first error of a generator project that does not compile, or
    /// <see langword="null"/> when they loaded or the reason passes (see <see cref="ProjectGeneratorBuild.IsTransient"/>).
    /// Builds the generator project if that has not happened yet.
    /// </summary>
    public string? Failure => Build is { IsTransient: false } build ? build.Failure : null;

    /// <summary>The build, made on first use and kept unless it says it may succeed when asked again.</summary>
    private ProjectGeneratorBuild Build
    {
        get
        {
            lock (_gate)
            {
                if (_build is { } known)
                {
                    return known;
                }

                // The lock is reentrant: a project the build compiles that, through other generator projects, needs these
                // generators again gets none, rather than building them inside their own build.
                if (_building)
                {
                    return ProjectGeneratorBuild.Failed(string.Empty, "building it needs its own generators.") with { IsTransient = true };
                }

                _building = true;
                try
                {
                    var build = _builder();
                    if (!build.IsTransient)
                    {
                        _build = build;
                    }

                    return build;
                }
                finally
                {
                    _building = false;
                }
            }
        }
    }

    /// <inheritdoc />
    public override string? FullPath => null;

    /// <inheritdoc />
    public override string Display => _display;

    /// <inheritdoc />
    public override object Id => this;

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzers(string language) => [];

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzersForAllLanguages() => [];

    /// <inheritdoc />
    public override ImmutableArray<ISourceGenerator> GetGenerators(string language) =>
        language == LanguageNames.CSharp ? Build.Generators : [];

    /// <inheritdoc />
    public override ImmutableArray<ISourceGenerator> GetGeneratorsForAllLanguages() => Build.Generators;
}

/// <summary>A generator project compiled and loaded, or why it could not be.</summary>
/// <param name="Key">A hash of everything the build was made from, so an unchanged project is not built again; empty when it is not to be reused.</param>
/// <param name="Generators">The generators it has, ready for Roslyn.</param>
/// <param name="Failure">Why some or all of its generators are missing, or <see langword="null"/>.</param>
internal sealed record ProjectGeneratorBuild(string Key, ImmutableArray<ISourceGenerator> Generators, string? Failure)
{
    /// <summary>
    /// Whether the build failed for a reason that passes, such as the workspace being loaded again at that moment, so
    /// it is made again the next time the generators are asked for.
    /// </summary>
    public bool IsTransient { get; init; }

    /// <summary>A build that gives no generators, with the reason.</summary>
    /// <param name="key">See <see cref="Key"/>.</param>
    /// <param name="failure">Why, naming the generator project.</param>
    public static ProjectGeneratorBuild Failed(string key, string failure) => new(key, [], failure);
}
