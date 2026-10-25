using TabStudio.Core.Diagnostics;

namespace TabStudio.WindowsCompatibility.Building;

/// <summary>The reference assemblies of the Windows Desktop framework, as a project's profile of it selects them.</summary>
/// <param name="Version">The targeting pack's version, such as <c>10.0.13</c>.</param>
/// <param name="Assemblies">The reference assemblies' full paths.</param>
/// <param name="Diagnostics">Problems getting them, such as no network before the pack was ever downloaded.</param>
public sealed record WindowsReferences(string? Version, IReadOnlyList<string> Assemblies, IReadOnlyList<BuildDiagnostic> Diagnostics)
{
    /// <summary>Whether the assemblies are there.</summary>
    public bool Success => Assemblies.Count > 0;
}

/// <summary>
/// Gets the targeting pack Windows Forms and WPF projects compile against, Microsoft.WindowsDesktop.App.Ref, the
/// way the Windows Desktop SDK references it: the profile of the framework the project uses.
/// </summary>
public interface IWindowsReferencePack
{
    /// <summary>The reference assemblies for a project, downloaded the first time.</summary>
    /// <param name="project">The project.</param>
    /// <param name="cancellationToken">Gives up.</param>
    Task<WindowsReferences> ResolveAsync(WindowsDesktopProject project, CancellationToken cancellationToken);
}
