using TabStudio.Core.Diagnostics;

namespace TabStudio.Packages;

/// <summary>
/// The outcome of restoring the packages of one project.
/// </summary>
/// <remarks>
/// Pass <see cref="CompileAssemblies"/> and <see cref="Analyzers"/> to the language service so code that uses the
/// packages compiles, and <see cref="RuntimeAssemblies"/> to the program runner so the program finds them when it runs.
/// </remarks>
public sealed record RestoreResult
{
    /// <summary>The project that was restored.</summary>
    public required string ProjectName { get; init; }

    /// <summary>Every package in the project's dependency graph, ordered by id.</summary>
    public IReadOnlyList<RestoredPackage> Packages { get; init; } = [];

    /// <summary>The shared frameworks the project uses besides .NET, such as ASP.NET Core.</summary>
    public IReadOnlyList<SharedFrameworkAssets> Frameworks { get; init; } = [];

    /// <summary>Full paths of the assemblies the project compiles against, from every package.</summary>
    public IReadOnlyList<string> CompileAssemblies { get; init; } = [];

    /// <summary>Full paths of the assemblies the project loads when it runs, from every package.</summary>
    public IReadOnlyList<string> RuntimeAssemblies { get; init; } = [];

    /// <summary>
    /// Full paths of the analyzers and source generators the compiler runs for the project, from every package and
    /// shared framework, such as CommunityToolkit.Mvvm's.
    /// </summary>
    public IReadOnlyList<string> Analyzers { get; init; } = [];

    /// <summary>Errors and warnings, with NuGet's ids where NuGet has one (such as NU1101), most severe first.</summary>
    public IReadOnlyList<BuildDiagnostic> Diagnostics { get; init; } = [];

    /// <summary>Whether the result was read from the project's restore cache, without contacting any source.</summary>
    public bool FromCache { get; init; }

    /// <summary>How long the restore took.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Whether every package was restored: there are no errors.</summary>
    public bool Success => Diagnostics.All(d => d.Severity != DiagnosticSeverityLevel.Error);
}
