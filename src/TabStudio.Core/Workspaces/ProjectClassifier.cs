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
    /// <summary>The packages of TUnit, whose tests Test Explorer does not run.</summary>
    private static readonly string[] TUnitPackages = ["TUnit", "TUnit.Core", "TUnit.Engine"];

    private static readonly string[] TestPackages =
    [
        "Microsoft.NET.Test.Sdk", "xunit", "xunit.v3", "xunit.v3.mtp-v1", "xunit.v3.mtp-v2", "xunit.v3.mtp-off", "xunit.v3.core",
        "NUnit", "MSTest.TestFramework", "MSTest", .. TUnitPackages,
    ];

    /// <summary>The SDK of MSTest projects, which makes a project a test project without any package.</summary>
    private const string MSTestSdk = "MSTest.Sdk";

    /// <summary>The package BenchmarkDotNet benchmarks are written with.</summary>
    private const string BenchmarkDotNetPackage = "BenchmarkDotNet";

    /// <summary>
    /// The folders whose programs F5 picks only when nothing else can start: documentation, scripts, tools, the
    /// repository's build, benchmarks and tests.
    /// </summary>
    private static readonly string[] AuxiliaryFolders = ["docs", "docfx", "scripts", "tools", "build", "eng", "benchmarks", "test", "tests"];

    /// <summary>The characters that separate folders in a path, on every platform.</summary>
    private static readonly char[] PathSeparators = ['/', '\\'];

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
    private static readonly string[] MobilePlatforms = ["-android", "-ios", "-maccatalyst", "-tvos", "-macos", "-tizen"];

    /// <summary>
    /// The project type GUIDs Visual Studio writes into <c>ProjectTypeGuids</c> for Xamarin.iOS and Xamarin.Android
    /// projects, as in CommunityToolkit's <c>MvvmSampleXF.iOS</c> and <c>MvvmSampleXF.Android</c>.
    /// </summary>
    private static readonly string[] XamarinProjectTypeGuids = ["FEACFBD2-3405-455C-9665-78FE426C6842", "EFBA0AD7-5A72-4C68-AF49-83D382785DCF"];

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
    /// The platform workload a project needs to build, which TabStudio does not have: a .NET MAUI project
    /// (<c>UseMaui</c>); the Android, iOS, Mac or Tizen head of a cross-platform app, such as an Avalonia app's
    /// <c>AdvancedToDoList.Android</c>, every framework of which is such a platform (<c>net10.0-android</c>,
    /// <c>net10.0-ios</c>) or Windows beside them (<c>net10.0-windows10.0.19041.0</c>); a Xamarin project (its project
    /// type, its <c>Xamarin.*.CSharp.targets</c> import, or <c>AndroidApplication</c>); or a UWP project
    /// (<c>TargetPlatformIdentifier</c> <c>UAP</c>, or an <c>AppContainerExe</c>). A project for Windows alone, such as
    /// a WPF app on <c>net10.0-windows</c> or a WinUI 3 app on <c>net10.0-windows10.0.19041.0</c>, needs none.
    /// </summary>
    /// <param name="project">The project.</param>
    public static PlatformWorkload RequiredWorkload(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);

        // An Uno.Sdk project is an Uno app whatever else it sets, as Classify decides; TabStudio builds it for Uno's desktop target.
        if (IsTrue(project.GetProperty("UseMaui")) && project.Kind != ProjectKind.Uno)
        {
            return PlatformWorkload.Maui;
        }

        if (IsMobilePlatformHead(project))
        {
            return PlatformWorkload.MobilePlatform;
        }

        // Xamarin.Forms generates its pages' InitializeComponent with its own MSBuild task, from Xamarin's tools.
        var projectTypes = project.GetProperty("ProjectTypeGuids") ?? string.Empty;
        if (XamarinProjectTypeGuids.Any(guid => projectTypes.Contains(guid, StringComparison.OrdinalIgnoreCase))
            || project.ImportPaths.Any(IsXamarinTargets)
            || IsTrue(project.GetProperty("AndroidApplication"))
            || project.HasPackage("Xamarin.Forms"))
        {
            return PlatformWorkload.Xamarin;
        }

        if (string.Equals(project.GetProperty("TargetPlatformIdentifier")?.Trim(), "UAP", StringComparison.OrdinalIgnoreCase)
            || string.Equals(project.OutputType, "AppContainerExe", StringComparison.OrdinalIgnoreCase))
        {
            return PlatformWorkload.Uwp;
        }

        if (IsBrowserHead(project) && (IsTrue(project.GetProperty("RunAOTCompilation")) || IsTrue(project.GetProperty("WasmBuildNative"))))
        {
            return PlatformWorkload.WebAssemblyTools;
        }

        // The .NET 10 SDK cannot target a newer .NET (NETSDK1045); a project with an older framework beside it is compiled for that one.
        if (project.TargetFrameworks.Count > 0 && project.TargetFrameworks.All(tf => DotNetMajor(tf) is > CompiledDotNetMajor))
        {
            return PlatformWorkload.NewerDotNet;
        }

        return PlatformWorkload.None;
    }

    /// <summary>
    /// Whether a project needs a platform workload to build (see <see cref="RequiredWorkload"/>). TabStudio leaves such a
    /// project out of builds and never starts it, and the Compatibility Doctor says it builds on a computer.
    /// </summary>
    /// <param name="project">The project.</param>
    public static bool NeedsPlatformWorkload(ProjectInfo project) => RequiredWorkload(project) != PlatformWorkload.None;

    /// <summary>
    /// Why a project that needs a platform workload is not built here, as a phrase after "it", such as
    /// <c>is a .NET MAUI project, which only .NET's MAUI workload builds, on a computer</c>; <see langword="null"/> for a
    /// project that needs none.
    /// </summary>
    /// <param name="project">The project.</param>
    public static string? PlatformWorkloadReason(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return RequiredWorkload(project) switch
        {
            PlatformWorkload.Maui => "is a .NET MAUI project, which only .NET's MAUI workload builds, on a computer",
            PlatformWorkload.MobilePlatform => $"targets {string.Join(", ", project.TargetFrameworks)}, which only .NET's mobile workloads build, on a computer",
            PlatformWorkload.Xamarin => "is a Xamarin project, which only Xamarin's tools in Visual Studio build, on a computer",
            PlatformWorkload.Uwp => "is a UWP project, which only Visual Studio's Universal Windows Platform tools build, on Windows",
            PlatformWorkload.WebAssemblyTools => "is a browser head compiled ahead of time to WebAssembly, which only .NET's wasm-tools workload builds, on a computer",
            PlatformWorkload.NewerDotNet => $"targets {string.Join(", ", project.TargetFrameworks)}, which only the .NET {NewestDotNet(project)} SDK builds; TabStudio compiles with .NET {CompiledDotNetMajor}",
            _ => null,
        };
    }

    /// <summary>The major version of the .NET TabStudio compiles with (<see cref="ProjectParser.CompiledTargetFramework"/>).</summary>
    private const int CompiledDotNetMajor = 10;

    /// <summary>
    /// Whether a project is the browser head of a cross-platform app, such as Avalonia's: it targets the browser
    /// (<c>net10.0-browser</c>, <c>browser-wasm</c>) or builds with the WebAssembly SDK, and is not a Blazor app.
    /// </summary>
    /// <param name="project">The project.</param>
    public static bool IsBrowserHead(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project.Kind != ProjectKind.BlazorWebAssembly
            && (project.UsesSdk("Microsoft.NET.Sdk.WebAssembly")
                || project.TargetFrameworks.Any(tf => tf.EndsWith("-browser", StringComparison.OrdinalIgnoreCase))
                || string.Equals(project.GetProperty("RuntimeIdentifier")?.Trim(), "browser-wasm", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The major version of the newest .NET (5 and later) a project targets; <see langword="null"/> when it targets none.</summary>
    private static int? NewestDotNet(ProjectInfo project) =>
        project.TargetFrameworks.Select(DotNetMajor).Where(major => major is not null).Max();

    /// <summary>The major version of a .NET 5 or later framework, such as 11 for <c>net11.0</c> or <c>net11.0-android</c>.</summary>
    private static int? DotNetMajor(string targetFramework)
    {
        var name = targetFramework.Trim();
        if (!name.StartsWith("net", StringComparison.OrdinalIgnoreCase) || name.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("netcoreapp", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var dot = name.IndexOf('.', StringComparison.Ordinal);
        return dot > 3 && int.TryParse(name.AsSpan(3, dot - 3), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var major) && major >= 5
            ? major
            : null;
    }

    /// <summary>
    /// Whether a project is the Android, iOS, Mac or Tizen head of a cross-platform app: every framework it targets is
    /// such a platform, or Windows beside one. An Uno Platform or WinUI project, which TabStudio builds for Uno's desktop
    /// target, counts only when every framework is a mobile platform, as before Windows was allowed.
    /// </summary>
    private static bool IsMobilePlatformHead(ProjectInfo project)
    {
        if (project.TargetFrameworks.Count == 0 || !project.TargetFrameworks.Any(IsMobilePlatform))
        {
            return false;
        }

        var windowsAllowed = project.Kind is not (ProjectKind.Uno or ProjectKind.WinUI);
        return project.TargetFrameworks.All(tf => IsMobilePlatform(tf) || (windowsAllowed && tf.Contains("-windows", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Whether a target framework is one of a mobile workload, such as <c>net10.0-android</c>.</summary>
    private static bool IsMobilePlatform(string targetFramework) =>
        MobilePlatforms.Any(p => targetFramework.Contains(p, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether an import is one of Xamarin's, such as <c>$(MSBuildExtensionsPath)\Xamarin\iOS\Xamarin.iOS.CSharp.targets</c>.</summary>
    private static bool IsXamarinTargets(string importPath)
    {
        var name = importPath[(importPath.LastIndexOfAny(PathSeparators) + 1)..];
        return name.StartsWith("Xamarin.", StringComparison.OrdinalIgnoreCase)
            && name.EndsWith(".CSharp.targets", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Whether a project's tests are written with TUnit, which Test Explorer does not run.</summary>
    /// <param name="project">The project.</param>
    public static bool UsesTUnit(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return TUnitPackages.Any(project.HasPackage);
    }

    /// <summary>
    /// Whether a project is in a folder of documentation, scripts, tools, the repository's build, benchmarks or tests:
    /// one named <c>docs</c>, <c>docfx</c>, <c>scripts</c>, <c>tools</c>, <c>build</c>, <c>eng</c>, <c>benchmarks</c>,
    /// <c>test</c> or <c>tests</c>, in any letter case, at any depth below the workspace's folder, such as Terminal.Gui's
    /// <c>docfx/scripts/OutputView</c>. F5 picks such a program only when no other can start.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <param name="rootDirectory">The workspace's folder, which the folders are counted from.</param>
    public static bool IsInAuxiliaryFolder(ProjectInfo project, string rootDirectory)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(rootDirectory);
        if (rootDirectory.Length == 0 || project.DirectoryPath.Length == 0)
        {
            return false;
        }

        var relative = Path.GetRelativePath(rootDirectory, project.DirectoryPath);
        var folders = relative.Split(PathSeparators, StringSplitOptions.RemoveEmptyEntries);
        if (Path.IsPathRooted(relative) || folders is [".." or ".", ..])
        {
            // The workspace's own folder, or a project outside it, whose folders are not the workspace's.
            return false;
        }

        return folders.Any(folder => AuxiliaryFolders.Contains(folder, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>The build-script library or SDK a project uses, such as <c>Bullseye</c>; <see langword="null"/> when it uses none.</summary>
    /// <param name="project">The project.</param>
    public static string? BuildScriptLibrary(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return BuildScriptPackages.FirstOrDefault(project.HasPackage) ?? BuildScriptSdks.FirstOrDefault(project.UsesSdk);
    }

    /// <summary>
    /// How good a project is as the one F5 starts when nobody has picked one, lower first: web apps with a UI (Blazor
    /// apps, then MVC or Razor Pages apps, which have <c>.cshtml</c> views, as eShop's Identity.API has for its sign-in
    /// pages beside the Blazor WebApp), then apps with a UI, then web APIs, then console apps, then other programs such
    /// as worker services; <see langword="null"/> for a project that is never a
    /// startup project (a library, tests, a build script, benchmarks, an Aspire AppHost, a project that needs a platform
    /// workload, the WebAssembly client of a Blazor Web App). <see cref="Workspace.DefaultStartupProject"/> also puts
    /// programs in folders such as <c>docs</c> or <c>tests</c> last.
    /// </summary>
    /// <param name="project">The project.</param>
    public static int? StartupRank(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!project.IsExecutable || project.IsTestProject || IsBlazorWebAppClient(project) || NeedsPlatformWorkload(project))
        {
            return null;
        }

        return project.Kind switch
        {
            ProjectKind.BuildScript or ProjectKind.Benchmark or ProjectKind.AspireAppHost or ProjectKind.Test or ProjectKind.ClassLibrary => null,
            ProjectKind.BlazorWebApp or ProjectKind.BlazorWebAssembly => 0,
            ProjectKind.AspNetCore when project.HasRazorViews => 1,
            ProjectKind.Avalonia or ProjectKind.WinForms or ProjectKind.Wpf or ProjectKind.Uno or ProjectKind.WinUI => 2,
            ProjectKind.AspNetCore => 3,
            ProjectKind.ConsoleApp => 4,
            _ => 5,
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
                     project.UsesSdk(MSTestSdk) ||
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

        // So are benchmarks: BenchmarkDotNet builds every benchmark into a program of its own and starts it.
        if (project.IsExecutable && project.HasPackage(BenchmarkDotNetPackage))
        {
            return ProjectKind.Benchmark;
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
