using Microsoft.CodeAnalysis;
using TabStudio.Compilation;
using TabStudio.Core.Workspaces;
using TabStudio.Packages;
using TabStudio.Testing;
using TabStudio.WindowsCompatibility.Building;

namespace TabStudio.WindowsCompatibility.Tests.Infrastructure;

/// <summary>
/// What building Windows desktop projects needs, shared by the tests: .NET's reference assemblies as files, and the
/// Windows Desktop reference pack restored once from nuget.org.
/// </summary>
public sealed class WindowsBuildFixture : IDisposable
{
    private readonly TempDirectory _cache = new();
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(5) };

    /// <summary>Creates the fixture.</summary>
    public WindowsBuildFixture()
    {
        References = ReferenceAssemblies.LoadMapped(_cache.Resolve("refs"));
        ReferenceFiles = [.. References.OfType<PortableExecutableReference>().Select(r => r.FilePath).OfType<string>()];
        Restorer = new PackageRestorer(
            new PackageRestoreOptions { GlobalPackagesFolder = _cache.Resolve("packages"), FrameworkAssemblyNames = ReferenceAssemblyNames.Net100 },
            _http);
        Pack = new NuGetWindowsReferencePack(Restorer);
    }

    /// <summary>.NET's reference assemblies.</summary>
    public IReadOnlyList<MetadataReference> References { get; }

    /// <summary>The same, as files.</summary>
    public IReadOnlyList<string> ReferenceFiles { get; }

    /// <summary>The NuGet restore, into a packages folder the tests share.</summary>
    public PackageRestorer Restorer { get; }

    /// <summary>The Windows Desktop reference pack, from nuget.org.</summary>
    public NuGetWindowsReferencePack Pack { get; }

    /// <summary>Loads a project and builds it as the IDE does.</summary>
    /// <param name="projectFile">The project file.</param>
    /// <param name="configuration">The configuration.</param>
    public async Task<(ProjectInfo Project, WindowsBuildResult Result)> BuildAsync(string projectFile, string configuration = "Debug")
    {
        var workspace = WorkspaceLoader.Load(projectFile);
        var project = workspace.Projects.Single(p => p.FilePath == Path.GetFullPath(projectFile));
        using var host = new RoslynWorkspaceHost(References);
        host.Load(workspace, FileSystemTextProvider.Instance);
        var builder = new WindowsDesktopBuilder(host, Pack, ReferenceFiles);
        var result = await builder.BuildAsync(project, new WindowsBuildOptions { Configuration = configuration });
        return (project, result);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _http.Dispose();
        _cache.Dispose();
    }
}
