using TabStudio.Compilation.Building;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.Packages;
using TabStudio.Web.StaticWebAssets;
using TabStudio.Web.WebAssembly;

namespace TabStudio.Web.RazorApps;

/// <summary>A web app built from a workspace, to get ready to start.</summary>
public sealed record RazorAppBuild
{
    /// <summary>The app: an ASP.NET Core app, a Blazor Web App, or a Blazor WebAssembly app.</summary>
    public required ProjectInfo Project { get; init; }

    /// <summary>The projects of the workspace, in which its project references are found.</summary>
    public required IReadOnlyList<ProjectInfo> Projects { get; init; }

    /// <summary>The build of the app and the projects it references.</summary>
    public required BuildResult Build { get; init; }

    /// <summary>The package assemblies of a project, from its restore, by project file.</summary>
    public required Func<string, IReadOnlyList<string>> PackageAssemblies { get; init; }

    /// <summary>The hosting environment, such as the launch profile's.</summary>
    public string Environment { get; init; } = "Development";
}

/// <summary>An ASP.NET Core app ready to start: the arguments it needs and what went wrong getting there.</summary>
/// <param name="Assets">Its static web assets; dispose them when the app stops.</param>
/// <param name="Diagnostics">Problems, such as Blazor's scripts not being on the device without a network.</param>
public sealed record PreparedServerApp(ServerAppAssets Assets, IReadOnlyList<BuildDiagnostic> Diagnostics) : IDisposable
{
    /// <summary>The command-line arguments to start the app with, before the launch profile's.</summary>
    public IReadOnlyList<string> Arguments => Assets.Arguments;

    /// <summary>Whether the app's Blazor WebAssembly client was built for the browser, to run its WebAssembly components.</summary>
    public bool HasWebAssemblyClient { get; init; }

    /// <inheritdoc />
    public void Dispose() => Assets.Dispose();
}

/// <summary>A Blazor WebAssembly app ready to serve, or why it is not.</summary>
/// <param name="App">The app, or <see langword="null"/> when it could not be put together.</param>
/// <param name="Diagnostics">Problems, such as .NET for WebAssembly not being on the device without a network.</param>
public sealed record PreparedWebAssemblyApp(WebAssemblyApp? App, IReadOnlyList<BuildDiagnostic> Diagnostics);

/// <summary>
/// Gets built web apps ready to start the way the SDK gets them ready in development: Blazor's scripts and .NET for
/// WebAssembly restored from NuGet the first time, the scoped CSS bundled, the static web asset manifests written, a
/// Blazor Web App's WebAssembly client built for the browser, and the runtime switches the project sets.
/// </summary>
/// <remarks>Downloads and file work run on the thread pool, so the methods may be called from the UI thread.</remarks>
/// <param name="restorer">Restores the packages the SDK would bring itself.</param>
public sealed class RazorAppPreparer(PackageRestorer restorer)
{
    /// <summary>The folder of a project where TabStudio writes what it builds for the web, as the SDK writes to <c>obj</c>.</summary>
    /// <param name="project">The project.</param>
    public static string OutputDirectory(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return Path.Combine(project.DirectoryPath, "obj", "TabStudio", "web");
    }

    /// <summary>Gets an ASP.NET Core app ready: a Blazor Web App, Razor Pages, MVC or any app on the Web SDK.</summary>
    /// <param name="app">The app's build.</param>
    /// <param name="progress">Receives what is being done, such as a first download.</param>
    /// <param name="cancellationToken">Cancels the downloads.</param>
    public async Task<PreparedServerApp> PrepareServerAppAsync(RazorAppBuild app, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(app);
        var project = app.Project;
        var references = ReferencedProjects(project, app.Projects);
        var packages = app.PackageAssemblies(project.FilePath);
        var diagnostics = new List<BuildDiagnostic>();

        string? scripts = null;
        if (project.HasRazorComponents)
        {
            progress?.Report("Getting Blazor's scripts");
            var version = AspNetCoreVersion(packages);
            var (pack, problems) = await Task.Run(() => restorer.RestoreAspNetCoreWebAssetsAsync(version, project.Name, cancellationToken), cancellationToken).ConfigureAwait(false);
            diagnostics.AddRange(problems);
            scripts = pack?.DirectoryPath;
        }

        WebAssemblyBundle? client = null;
        if (references.FirstOrDefault(r => r.Kind == ProjectKind.BlazorWebAssembly) is { } clientProject)
        {
            var described = await DescribeWebAssemblyAppAsync(app, clientProject, isStandalone: false, progress, cancellationToken).ConfigureAwait(false);
            diagnostics.AddRange(described.Diagnostics);
            if (described.App is { } wasm)
            {
                client = await BundleAsync(wasm, clientProject, diagnostics, cancellationToken).ConfigureAwait(false);
            }
        }

        var request = new ServerAppAssetsRequest
        {
            App = project,
            ApplicationName = app.Build.EntryAssembly?.Name,
            References = references,
            PackageAssemblies = packages,
            BlazorScriptsPackage = scripts,
            Client = client,
            OutputDirectory = OutputDirectory(project),
        };
        var assets = await Task.Run(() => ServerAppAssets.Prepare(request), cancellationToken).ConfigureAwait(false);
        diagnostics.AddRange(assets.Diagnostics);
        RazorAppSwitches.Apply(project);
        return new PreparedServerApp(assets, diagnostics) { HasWebAssemblyClient = client is not null };
    }

    /// <summary>Gets a standalone Blazor WebAssembly app ready, for the <see cref="WebAssemblyAppHost"/>.</summary>
    /// <param name="app">The app's build.</param>
    /// <param name="progress">Receives what is being done, such as the first download of .NET for WebAssembly.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    public Task<PreparedWebAssemblyApp> PrepareWebAssemblyAppAsync(RazorAppBuild app, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(app);
        return DescribeWebAssemblyAppAsync(app, app.Project, isStandalone: true, progress, cancellationToken);
    }

    /// <summary>The projects a project references, directly or not, each after the projects it references.</summary>
    /// <param name="project">The project.</param>
    /// <param name="projects">The projects of the workspace.</param>
    public static IReadOnlyList<ProjectInfo> ReferencedProjects(ProjectInfo project, IReadOnlyList<ProjectInfo> projects)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(projects);
        var byPath = projects.GroupBy(p => p.FilePath, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var ordered = new List<ProjectInfo>();
        var visited = new HashSet<string>(StringComparer.Ordinal) { project.FilePath };

        void Visit(ProjectInfo current)
        {
            foreach (var path in current.ProjectReferences)
            {
                if (byPath.TryGetValue(path, out var referenced) && visited.Add(path))
                {
                    Visit(referenced);
                    if (referenced.Language == ProjectLanguage.CSharp)
                    {
                        ordered.Add(referenced);
                    }
                }
            }
        }

        Visit(project);
        return ordered;
    }

    /// <summary>The version of ASP.NET Core in a set of runtime assemblies, from the runtime pack's folder.</summary>
    /// <param name="runtimeAssemblies">An app's runtime assemblies, with ASP.NET Core's.</param>
    internal static string? AspNetCoreVersion(IReadOnlyList<string> runtimeAssemblies)
    {
        var aspNetCore = runtimeAssemblies.FirstOrDefault(a => string.Equals(Path.GetFileName(a), "Microsoft.AspNetCore.dll", StringComparison.OrdinalIgnoreCase));
        return aspNetCore is not null && PackageWebAssets.FindPackageDirectory(aspNetCore) is { } package ? Path.GetFileName(package) : null;
    }

    private async Task<PreparedWebAssemblyApp> DescribeWebAssemblyAppAsync(
        RazorAppBuild app,
        ProjectInfo project,
        bool isStandalone,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var name = project.GetProperty("AssemblyName") ?? project.Name;
        var entry = isStandalone ? app.Build.EntryAssembly : app.Build.Dependencies.FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.Ordinal));
        if (entry is null)
        {
            return new PreparedWebAssemblyApp(null, [Problem(WebDiagnosticIds.WebAssemblyClient, $"{project.Name} was not built, so its WebAssembly components cannot start.", app.Project.Name)]);
        }

        progress?.Report("Getting .NET for WebAssembly");
        var (pack, problems) = await Task.Run(() => restorer.RestoreBrowserRuntimeAsync(app.Project.Name, cancellationToken), cancellationToken).ConfigureAwait(false);
        if (pack is null)
        {
            return new PreparedWebAssemblyApp(null, problems);
        }

        BrowserRuntime runtime;
        try
        {
            runtime = new BrowserRuntime(pack.DirectoryPath);
        }
        catch (DirectoryNotFoundException ex)
        {
            return new PreparedWebAssemblyApp(null, [.. problems, Problem(WebDiagnosticIds.WebAssemblyBundle, ex.Message, app.Project.Name)]);
        }

        var references = ReferencedProjects(project, app.Projects);
        var referencedNames = references.Select(r => r.GetProperty("AssemblyName") ?? r.Name).ToHashSet(StringComparer.Ordinal);
        var request = new BlazorWebAssemblyRequest
        {
            Project = project,
            Entry = entry,
            Dependencies = [.. app.Build.Dependencies.Where(d => referencedNames.Contains(d.Name))],
            References = references,
            PackageAssemblies = app.PackageAssemblies(project.FilePath),
            Runtime = runtime,
            Environment = app.Environment,
            IsStandalone = isStandalone,
            OutputDirectory = OutputDirectory(project),
        };
        var (described, diagnostics) = await Task.Run(() => BlazorWebAssemblyApps.Describe(request), cancellationToken).ConfigureAwait(false);
        return new PreparedWebAssemblyApp(described, [.. problems, .. diagnostics]);
    }

    private static async Task<WebAssemblyBundle?> BundleAsync(WebAssemblyApp app, ProjectInfo project, List<BuildDiagnostic> diagnostics, CancellationToken cancellationToken)
    {
        try
        {
            return await Task.Run(() => WebAssemblyBundle.Create(app), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            diagnostics.Add(Problem(WebDiagnosticIds.WebAssemblyBundle, $"{project.Name} could not be prepared for the browser: {ex.Message}", project.Name));
            return null;
        }
    }

    private static BuildDiagnostic Problem(string id, string message, string projectName) =>
        new(id, DiagnosticSeverityLevel.Error, message, null, projectName);
}
