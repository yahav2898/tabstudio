using TabStudio.Compilation.Wpf;
using TabStudio.Core.Workspaces;

namespace TabStudio.Wpf;

/// <summary>
/// The NuGet packages a WPF project builds and runs with in TabStudio: OpenSilver, the MIT-licensed reimplementation
/// of WPF and Silverlight on .NET for WebAssembly, in the release TabStudio is tested with, and the Blazor
/// WebAssembly host that runs an OpenSilver app in the browser, as OpenSilver's own <c>.Browser</c> project does.
/// </summary>
/// <remarks>
/// A WPF project names none of them: they take the place of the Windows Desktop pack its <c>UseWPF</c> asks for.
/// They are restored from the project's package sources like its own packages, once, and work offline from then on.
/// </remarks>
public static class OpenSilverPackages
{
    /// <summary>The OpenSilver release TabStudio builds WPF projects with.</summary>
    public const string Version = "3.3.3";

    /// <summary>OpenSilver itself: the runtime's assemblies, its XAML compiler (in <c>tools/</c>) and its build targets.</summary>
    public const string OpenSilver = "OpenSilver";

    /// <summary>OpenSilver's WebAssembly runner and its scripts and styles (in <c>content/</c>).</summary>
    public const string WebAssembly = "OpenSilver.WebAssembly";

    /// <summary>Blazor's WebAssembly host, which OpenSilver's scripts call into through its JavaScript interop.</summary>
    public const string BlazorWebAssembly = "Microsoft.AspNetCore.Components.WebAssembly";

    /// <summary>The release of Blazor's WebAssembly host, for .NET 10, the runtime the app runs on in the browser.</summary>
    public const string BlazorVersion = "10.0.0";

    /// <summary>
    /// The project as it compiles in TabStudio: with a reference to OpenSilver, which brings the WPF API it compiles
    /// against, unless it references OpenSilver itself.
    /// </summary>
    /// <param name="project">A WPF project.</param>
    public static ProjectInfo WithOpenSilver(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project.HasPackage(OpenSilver)
            ? project
            : project with { PackageReferences = [.. project.PackageReferences, new PackageReferenceInfo(OpenSilver, Version)] };
    }

    /// <summary>
    /// The browser host of a WPF app, as a project to restore: OpenSilver, its WebAssembly runner and Blazor's host,
    /// with a reference to the app so its own packages come along, as NuGet restores OpenSilver's <c>.Browser</c> project.
    /// </summary>
    /// <param name="app">The WPF app.</param>
    public static ProjectInfo BrowserHost(ProjectInfo app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var directory = Path.Combine(WpfProject.OutputDirectory(app), "browser");
        return new ProjectInfo
        {
            Name = $"{app.Name}.Browser",
            FilePath = Path.Combine(directory, $"{app.Name}.Browser.csproj"),
            Language = ProjectLanguage.CSharp,
            Sdk = "Microsoft.NET.Sdk.BlazorWebAssembly",
            TargetFrameworks = ["net10.0"],
            OutputType = "Exe",
            PackageReferences =
            [
                new PackageReferenceInfo(OpenSilver, app.PackageReferences.FirstOrDefault(p => string.Equals(p.Name, OpenSilver, StringComparison.OrdinalIgnoreCase))?.Version ?? Version),
                new PackageReferenceInfo(WebAssembly, Version),
                new PackageReferenceInfo(BlazorWebAssembly, BlazorVersion),
            ],
            ProjectReferences = [app.FilePath],
            Kind = ProjectKind.BlazorWebAssembly,
        };
    }
}
