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
    /// Full paths of the native libraries a running program may load, from every package: the
    /// <c>runtimes/{rid}/native/</c> files for the device's nearest RID. A file name appears once; the first package wins.
    /// </summary>
    public IReadOnlyList<string> NativeLibraries { get; init; } = [];

    /// <summary>
    /// Everything a running program loads from packages: <see cref="RuntimeAssemblies"/>, then
    /// <see cref="NativeLibraries"/>. This is what the program runner, test runner and debugger are given; they tell the
    /// two apart by the <c>runtimes/{rid}/native/</c> folder a native library is in.
    /// </summary>
    public IReadOnlyList<string> RuntimeFiles => NativeLibraries.Count == 0 ? RuntimeAssemblies : [.. RuntimeAssemblies, .. NativeLibraries];

    /// <summary>
    /// Full paths of the analyzers and source generators the compiler runs for the project, from every package and
    /// shared framework, such as CommunityToolkit.Mvvm's.
    /// </summary>
    public IReadOnlyList<string> Analyzers { get; init; } = [];

    /// <summary>
    /// Full paths of the props and targets files the project imports from its packages, in MSBuild's order: every
    /// package's props, then every package's targets, a package's dependencies before the package.
    /// </summary>
    public IReadOnlyList<string> BuildFiles =>
    [
        .. ImportOrder().SelectMany(p => p.BuildFiles).Where(f => f.EndsWith(".props", StringComparison.OrdinalIgnoreCase)),
        .. ImportOrder().SelectMany(p => p.BuildFiles).Where(f => !f.EndsWith(".props", StringComparison.OrdinalIgnoreCase)),
    ];

    /// <summary>Errors and warnings, with NuGet's ids where NuGet has one (such as NU1101), most severe first.</summary>
    public IReadOnlyList<BuildDiagnostic> Diagnostics { get; init; } = [];

    /// <summary>Whether the result was read from the project's restore cache, without contacting any source.</summary>
    public bool FromCache { get; init; }

    /// <summary>How long the restore took.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>The packages farthest from the project first, as NuGet imports a package's dependencies before it.</summary>
    private IEnumerable<RestoredPackage> ImportOrder() =>
        Packages.OrderByDescending(p => p.Depth).ThenBy(p => p.Id, StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether every package was restored: there are no errors.</summary>
    public bool Success => Diagnostics.All(d => d.Severity != DiagnosticSeverityLevel.Error);
}
