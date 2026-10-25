using TabStudio.Core.Diagnostics;

namespace TabStudio.App.ViewModels;

/// <summary>An Error List row that a build on the Windows Bridge reported, with the PC it came from.</summary>
public sealed partial record ErrorItemViewModel
{
    /// <summary>The PC whose build reported it, such as <c>DESKTOP-7Q2M</c>; <see langword="null"/> for this device's own.</summary>
    public string? BridgePc { get; init; }

    /// <summary>Whether a build on the Windows Bridge reported it.</summary>
    public bool IsFromBridge => BridgePc is not null;

    /// <summary>The Windows Bridge badge's tooltip.</summary>
    public string? BridgeToolTip => BridgePc is { } pc ? $"Reported by the build on {pc}, through the Windows Bridge" : null;
}

/// <summary>
/// The Windows Bridge's builds in the Error List: MSBuild's and the compilers' errors and warnings from a build on the
/// PC, on the tablet's files, with the PC's name as a badge. A project's next build on the PC replaces its rows. They
/// are not squiggles: the editor's squiggles stay this device's own analysis.
/// </summary>
public sealed partial class ErrorListViewModel
{
    private readonly Dictionary<string, IReadOnlyList<BuildDiagnostic>> _bridgeBuilds = new(StringComparer.Ordinal);
    private readonly Dictionary<BuildDiagnostic, string> _bridgePcs = new(ReferenceEqualityComparer.Instance);

    /// <summary>Shows a build on the Windows Bridge, replacing the project's previous build there.</summary>
    /// <param name="projectPath">The project built, on this device's paths.</param>
    /// <param name="pcName">The PC that built it.</param>
    /// <param name="diagnostics">The build's errors and warnings, on this device's paths; none clears the project's.</param>
    public void SetWindowsBridgeDiagnostics(string projectPath, string pcName, IReadOnlyList<BuildDiagnostic> diagnostics)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectPath);
        ArgumentNullException.ThrowIfNull(diagnostics);
        if (_bridgeBuilds.Remove(projectPath, out var previous))
        {
            foreach (var diagnostic in previous)
            {
                _bridgePcs.Remove(diagnostic);
            }
        }

        if (diagnostics.Count > 0)
        {
            _bridgeBuilds[projectPath] = diagnostics;
            foreach (var diagnostic in diagnostics)
            {
                _bridgePcs[diagnostic] = pcName;
            }
        }

        Refresh();
    }

    /// <summary>The PC whose build reported a diagnostic, when one did.</summary>
    /// <param name="diagnostic">A diagnostic of a row.</param>
    public string? BridgePcOf(BuildDiagnostic diagnostic) =>
        diagnostic is not null && _bridgePcs.TryGetValue(diagnostic, out var pc) ? pc : null;

    /// <summary>Every build on the Windows Bridge's diagnostics, for the list.</summary>
    private IEnumerable<BuildDiagnostic> WindowsBridgeDiagnostics => _bridgeBuilds.Values.SelectMany(d => d);
}
