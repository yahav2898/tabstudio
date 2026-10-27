namespace TabStudio.Core.Workspaces;

/// <summary>
/// Decides what kind of project a parsed project file describes.
/// </summary>
/// <remarks>
/// The rules mirror how the .NET SDK itself decides: the SDK name first, then the <c>UseWPF</c>-style
/// switches, then well-known packages, and finally <c>OutputType</c>. Test projects are recognized
/// before anything else, because a test project often references the framework it tests.
/// </remarks>
public static class ProjectClassifier
{
    private static readonly string[] TestPackages =
    [
        "Microsoft.NET.Test.Sdk", "xunit", "xunit.v3", "xunit.v3.mtp-v1", "xunit.v3.mtp-v2", "xunit.v3.mtp-off", "xunit.v3.core",
        "NUnit", "MSTest.TestFramework", "MSTest",
    ];

    /// <summary>The packages build scripts are written with; a program that uses one is a build script.</summary>
    private static readonly string[] BuildScriptPackages =
    [
        "Bullseye", "SimpleExec", "Nuke.Common", "Nuke.Components", "Cake.Frosting", "Cake.Core", "FlubuCore", "Fake.Core.Target",
    ];

    /// <summary>The SDKs build scripts are written with.</summary>
    private static readonly string[] BuildScriptSdks = ["Cake.Sdk", "Cake.Frosting.Sdk", "Nuke.Sdk"];

    /// <summary>The names repositories give a build script project, in a folder of the same purpose.</summary>
    private static readonly string[] BuildScriptNames = ["build", "_build"];

    /// <summary>The folders a build script project lives in.</summary>
    private static readonly string[] BuildScriptFolders = ["build", "_build", ".build", "eng"];

    /// <summary>
    /// Whether a project is a repository's build script rather than an app: it uses a build-script library or SDK
    /// (Bullseye, SimpleExec, NUKE, Cake, FAKE), or it is named <c>build</c> or <c>_build</c> in a <c>build</c> folder,
    /// as NUKE and most build scripts are. Such a program starts <c>dotnet</c> and other tools as processes, so it never
    /// runs on a tablet and F5 never picks it.
    /// </summary>
    /// <param name="project">The project.</param>
    public static bool IsBuildScript(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (BuildScriptLibrary(project) is not null)
        {
            return true;
        }

        var folder = Path.GetFileName(Path.GetDirectoryName(project.FilePath) ?? string.Empty);
        return BuildScriptNames.Contains(project.Name, StringComparer.OrdinalIgnoreCase)
            && BuildScriptFolders.Contains(folder, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The platforms of .NET's mobile workloads, which only build with those workloads installed.</summary>
    private static readonly string[] MobilePlatforms = ["-android", "-ios", "-maccatalyst", "-tvos", "-macos"];

    /// <summary>
    /// Whether a project uses Avalonia: it references Avalonia or one of its packages, such as <c>Avalonia.Desktop</c>
    /// or <c>Avalonia.Themes.Fluent</c>, which bring Avalonia with them, or ReactiveUI's Avalonia package.
    /// </summary>
    /// <param name="project">The project.</param>
    public static bool UsesAvalonia(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project.HasPackage("Avalonia")
            || project.HasPackage("ReactiveUI.Avalonia")
            || project.PackageReferences.Any(p => p.Name.StartsWith("Avalonia.", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Whether a project is the Android, iOS or Mac head of a cross-platform app, such as an Avalonia app's
    /// <c>AdvancedToDoList.Android</c>: every framework it targets is a mobile platform (<c>net10.0-android</c>,
    /// <c>net10.0-ios</c>), which only .NET's workload for that platform builds, on a computer. A .NET MAUI project, which
    /// TabStudio treats as an app of its own, is not counted.
    /// </summary>
    /// <param name="project">The project.</param>
    public static bool IsMobilePlatformHead(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project.Kind != ProjectKind.Maui
            && project.TargetFrameworks.Count > 0
            && project.TargetFrameworks.All(tf => MobilePlatforms.Any(p => tf.Contains(p, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>The build-script library or SDK a project uses, such as <c>Bullseye</c>; <see langword="null"/> when it uses none.</summary>
    /// <param name="project">The project.</param>
    public static string? BuildScriptLibrary(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return BuildScriptPackages.FirstOrDefault(project.HasPackage) ?? BuildScriptSdks.FirstOrDefault(project.UsesSdk);
    }

    /// <summary>
    /// How good a project is as the one F5 starts when nobody has picked one, lower first: web apps, then apps with a
    /// UI, then console apps, then other programs such as worker services; <see langword="null"/> for a project that
    /// is never a startup project (a library, tests, a build script, an Aspire AppHost, the WebAssembly client of a
    /// Blazor Web App).
    /// </summary>
    /// <param name="project">The project.</param>
    public static int? StartupRank(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!project.IsExecutable || project.IsTestProject || IsBlazorWebAppClient(project) || IsMobilePlatformHead(project))
        {
            return null;
        }

        return project.Kind switch
        {
            ProjectKind.BuildScript or ProjectKind.AspireAppHost or ProjectKind.Test or ProjectKind.ClassLibrary => null,
            ProjectKind.AspNetCore or ProjectKind.BlazorWebApp or ProjectKind.BlazorWebAssembly => 0,
            ProjectKind.Avalonia or ProjectKind.WinForms or ProjectKind.Wpf or ProjectKind.Uno or ProjectKind.WinUI or ProjectKind.Maui => 1,
            ProjectKind.ConsoleApp => 2,
            _ => 3,
        };
    }

    /// <summary>Classifies a project.</summary>
    /// <param name="project">The parsed project. Its <see cref="ProjectInfo.Kind"/> is ignored.</param>
    /// <returns>The project kind, and whether it contains tests.</returns>
    public static (ProjectKind Kind, bool IsTestProject) Classify(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (project.Language == ProjectLanguage.Cpp)
        {
            return (ProjectKind.NativeCpp, false);
        }

        var sdk = project.Sdk ?? string.Empty;
        var isTest = IsTrue(project.GetProperty("IsTestProject")) ||
                     sdk.Equals("MSTest.Sdk", StringComparison.OrdinalIgnoreCase) ||
                     TestPackages.Any(project.HasPackage);

        if (isTest)
        {
            return (ProjectKind.Test, true);
        }

        var kind = ClassifyApplication(project, sdk);
        return (kind, false);
    }

    /// <summary>The SDK of .NET Aspire's AppHost projects.</summary>
    public const string AspireAppHostSdk = "Aspire.AppHost.Sdk";

    /// <summary>
    /// Whether a project is a .NET Aspire AppHost: it uses <c>Aspire.AppHost.Sdk</c>, sets <c>IsAspireHost</c>, or
    /// references <c>Aspire.Hosting.AppHost</c>, as AppHosts made before Aspire 9 do.
    /// </summary>
    /// <param name="project">The project.</param>
    public static bool IsAspireAppHost(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project.UsesSdk(AspireAppHostSdk) || IsTrue(project.GetProperty("IsAspireHost")) || project.HasPackage("Aspire.Hosting.AppHost");
    }

    private static ProjectKind ClassifyApplication(ProjectInfo project, string sdk)
    {
        if (IsAspireAppHost(project))
        {
            return ProjectKind.AspireAppHost;
        }

        if (sdk.Equals("Uno.Sdk", StringComparison.OrdinalIgnoreCase) || sdk.StartsWith("Uno.Sdk.", StringComparison.OrdinalIgnoreCase))
        {
            return ProjectKind.Uno;
        }

        if (IsTrue(project.GetProperty("UseMaui")))
        {
            return ProjectKind.Maui;
        }

        if (IsTrue(project.GetProperty("UseWinUI")) || project.HasPackage("Microsoft.WindowsAppSDK"))
        {
            return ProjectKind.WinUI;
        }

        if (IsTrue(project.GetProperty("UseWPF")))
        {
            return ProjectKind.Wpf;
        }

        if (IsTrue(project.GetProperty("UseWindowsForms")))
        {
            return ProjectKind.WinForms;
        }

        if (sdk.Equals("Microsoft.NET.Sdk.BlazorWebAssembly", StringComparison.OrdinalIgnoreCase))
        {
            return ProjectKind.BlazorWebAssembly;
        }

        if (sdk.Equals("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase) ||
            (sdk.Equals("Microsoft.NET.Sdk.Razor", StringComparison.OrdinalIgnoreCase) && project.IsExecutable))
        {
            // As the Web SDK decides whether an app needs Blazor's script: by its .razor files.
            return project.HasRazorComponents && project.IsExecutable ? ProjectKind.BlazorWebApp : ProjectKind.AspNetCore;
        }

        if (sdk.Equals("Microsoft.NET.Sdk.Worker", StringComparison.OrdinalIgnoreCase))
        {
            return ProjectKind.Worker;
        }

        // Before the UI frameworks and console apps: a build script is a console program, but never an app.
        if (project.IsExecutable && IsBuildScript(project))
        {
            return ProjectKind.BuildScript;
        }

        if (project.IsExecutable && UsesAvalonia(project))
        {
            return ProjectKind.Avalonia;
        }

        if (project.IsExecutable)
        {
            return ProjectKind.ConsoleApp;
        }

        // SDK-style projects default to a library; a project with no SDK and no OutputType is unknown.
        return sdk.Length > 0 || project.OutputType is not null ? ProjectKind.ClassLibrary : ProjectKind.Unknown;
    }

    /// <summary>
    /// Whether a Blazor WebAssembly project is the client of a Blazor Web App, which runs in the app that references it,
    /// rather than an app of its own: the template marks it with <c>StaticWebAssetProjectMode</c> <c>Default</c>.
    /// </summary>
    /// <param name="project">The project.</param>
    public static bool IsBlazorWebAppClient(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project.Kind == ProjectKind.BlazorWebAssembly
            && string.Equals(project.GetProperty("StaticWebAssetProjectMode")?.Trim(), "Default", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTrue(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "enable", StringComparison.OrdinalIgnoreCase);
}
