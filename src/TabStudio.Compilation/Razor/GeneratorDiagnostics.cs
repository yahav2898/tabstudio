using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace TabStudio.Compilation.Razor;

/// <summary>
/// The diagnostics source generators report, such as Razor's syntax errors (<c>RZ1006</c>) and unknown components
/// (<c>RZ10012</c>). Roslyn keeps them with the generator run of every project, but its workspace exposes them only
/// to the IDE layers it was built with, through <c>Project.GetSourceGeneratorDiagnosticsAsync</c>; this calls that
/// method of the Roslyn version TabStudio pins.
/// </summary>
internal static class GeneratorDiagnostics
{
    private static readonly Func<Project, CancellationToken, ValueTask<ImmutableArray<Diagnostic>>>? GetDiagnostics = Bind();

    /// <summary>Whether the pinned Roslyn has the method; a test keeps this true across Roslyn updates.</summary>
    public static bool IsAvailable => GetDiagnostics is not null;

    /// <summary>The diagnostics the project's source generators reported in its current compilation.</summary>
    /// <param name="project">The project.</param>
    /// <param name="cancellationToken">Cancels the work, which may run the generators.</param>
    /// <returns>The diagnostics, or none when the method is not available.</returns>
    public static async Task<ImmutableArray<Diagnostic>> GetAsync(Project project, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        return GetDiagnostics is null ? [] : await GetDiagnostics(project, cancellationToken).ConfigureAwait(false);
    }

    private static Func<Project, CancellationToken, ValueTask<ImmutableArray<Diagnostic>>>? Bind()
    {
        var method = typeof(Project).GetMethod(
            "GetSourceGeneratorDiagnosticsAsync",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            [typeof(CancellationToken)]);
        return method?.ReturnType == typeof(ValueTask<ImmutableArray<Diagnostic>>)
            ? method.CreateDelegate<Func<Project, CancellationToken, ValueTask<ImmutableArray<Diagnostic>>>>()
            : null;
    }
}
