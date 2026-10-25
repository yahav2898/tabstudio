using Microsoft.CodeAnalysis;
using TabStudio.Compilation.Building;
using TabStudio.Compilation.Wpf;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using ProjectInfo = TabStudio.Core.Workspaces.ProjectInfo;
using TabStudio.Packages;
using TabStudio.Web;
using TabStudio.Web.WebAssembly;

namespace TabStudio.Wpf.Browser;

/// <summary>A WPF app ready to serve, or why it is not.</summary>
/// <param name="App">The app, or <see langword="null"/> when it could not be put together.</param>
/// <param name="Diagnostics">Problems, such as OpenSilver's browser host not being on the device without a network.</param>
public sealed record PreparedWpfApp(WebAssemblyApp? App, IReadOnlyList<BuildDiagnostic> Diagnostics);

/// <summary>
/// Gets a built WPF app ready for the web preview the way OpenSilver's tools get an OpenSilver app ready: its browser
/// host's packages and .NET for WebAssembly restored from NuGet the first time, and the app described for the
/// <see cref="WebAssemblyAppHost"/>.
/// </summary>
/// <remarks>Downloads and file work run on the thread pool, so it may be called from the UI thread.</remarks>
/// <param name="restorer">Restores the packages.</param>
public sealed class WpfAppPreparer(PackageRestorer restorer)
{
    /// <summary>Gets a built WPF app ready to serve.</summary>
    /// <param name="project">The WPF project.</param>
    /// <param name="build">Its build, with the projects it references.</param>
    /// <param name="frameworkReferences">.NET's reference assemblies, which its browser host compiles against.</param>
    /// <param name="progress">Receives what is being done, such as a first download.</param>
    /// <param name="cancellationToken">Cancels the downloads.</param>
    public async Task<PreparedWpfApp> PrepareAsync(
        ProjectInfo project,
        BuildResult build,
        IReadOnlyList<MetadataReference> frameworkReferences,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(frameworkReferences);
        if (build.EntryAssembly is not { } entry)
        {
            return new PreparedWpfApp(null, [new BuildDiagnostic(WpfDiagnosticIds.OpenSilverMissing, DiagnosticSeverityLevel.Error, $"{project.Name} was not built.", null, project.Name)]);
        }

        progress?.Report("Getting OpenSilver's browser host");
        var host = await Task.Run(() => restorer.RestoreAsync(OpenSilverPackages.BrowserHost(project), null, cancellationToken), cancellationToken).ConfigureAwait(false);
        if (!host.Success)
        {
            return new PreparedWpfApp(null, host.Diagnostics);
        }

        progress?.Report("Getting .NET for WebAssembly");
        var (pack, problems) = await Task.Run(() => restorer.RestoreBrowserRuntimeAsync(project.Name, cancellationToken), cancellationToken).ConfigureAwait(false);
        if (pack is null)
        {
            return new PreparedWpfApp(null, problems);
        }

        BrowserRuntime runtime;
        try
        {
            runtime = new BrowserRuntime(pack.DirectoryPath);
        }
        catch (DirectoryNotFoundException ex)
        {
            return new PreparedWpfApp(null, [.. problems, new BuildDiagnostic(WebDiagnosticIds.WebAssemblyBundle, DiagnosticSeverityLevel.Error, ex.Message, null, project.Name)]);
        }

        progress?.Report($"Preparing {project.Name} for the browser");
        var request = new WpfBrowserRequest
        {
            Project = project,
            App = entry,
            Dependencies = build.Dependencies,
            Host = host,
            Runtime = runtime,
            FrameworkReferences = frameworkReferences,
        };
        var (app, diagnostics) = await Task.Run(() => WpfBrowserApps.Describe(request), cancellationToken).ConfigureAwait(false);
        return new PreparedWpfApp(app, [.. problems, .. diagnostics]);
    }
}
