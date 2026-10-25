using System.Collections.Concurrent;
using TabStudio.Core.Workspaces;
using TabStudio.Packages;

namespace TabStudio.WindowsCompatibility.Building;

/// <summary>
/// The Windows Desktop reference assemblies from NuGet: Microsoft.WindowsDesktop.App.Ref, through TabStudio's own
/// restore, which keeps it in the global packages folder so builds work offline after the first.
/// </summary>
/// <param name="restorer">The NuGet restore.</param>
public sealed class NuGetWindowsReferencePack(PackageRestorer restorer) : IWindowsReferencePack
{
    private readonly ConcurrentDictionary<string, WindowsReferences> _resolved = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public async Task<WindowsReferences> ResolveAsync(WindowsDesktopProject project, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        var framework = Framework(project);
        if (_resolved.TryGetValue(framework, out var known))
        {
            return known;
        }

        var (assets, diagnostics) = await restorer.RestoreSharedFrameworkAsync(framework, project.Project.Name, cancellationToken).ConfigureAwait(false);
        var result = new WindowsReferences(assets?.Version, assets?.CompileAssemblies ?? [], diagnostics);
        if (result.Success)
        {
            _resolved[framework] = result;
        }

        return result;
    }

    /// <summary>The framework reference the Windows Desktop SDK gives a project: its profile, or the whole framework.</summary>
    /// <param name="project">The project.</param>
    public static string Framework(WindowsDesktopProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return (project.UsesWpf, project.UsesWindowsForms) switch
        {
            (true, true) => ProjectParser.WindowsDesktopFramework,
            (true, false) => ProjectParser.WindowsDesktopWpfFramework,
            _ => ProjectParser.WindowsDesktopWindowsFormsFramework,
        };
    }
}
