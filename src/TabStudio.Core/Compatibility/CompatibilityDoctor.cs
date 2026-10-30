using TabStudio.Core.Workspaces;

namespace TabStudio.Core.Compatibility;

/// <summary>
/// The Compatibility Doctor's verdict for one project.
/// </summary>
/// <param name="ProjectName">The project.</param>
/// <param name="Kind">What the project produces.</param>
/// <param name="Host">Where the project runs on a tablet.</param>
/// <param name="Status">Whether that works today.</param>
/// <param name="RunsVia">A short label for the run path, for example <c>In-process</c> or <c>OpenSilver in a WebView</c>.</param>
/// <param name="Explanation">One or two sentences the user can read, explaining the verdict.</param>
public sealed record CompatibilityReport(
    string ProjectName,
    ProjectKind Kind,
    RunHost Host,
    CompatibilityStatus Status,
    string RunsVia,
    string Explanation)
{
    /// <summary>Whether F5 works for this project today.</summary>
    public bool CanRunToday => Status == CompatibilityStatus.Ready;

    /// <summary>
    /// Whether the project is a legacy .NET Framework project, which Upgrade project converts to an SDK-style one
    /// (see <see cref="LegacyProjectUpgrader"/>).
    /// </summary>
    public bool CanUpgradeProject { get; init; }
}

/// <summary>
/// Explains, for every project in a workspace, where it can run on a tablet and whether that works today.
/// </summary>
/// <remarks>
/// The verdicts are deliberately honest: anything that is on the roadmap but not shipped is reported as
/// <see cref="CompatibilityStatus.Planned"/>, never as ready. As run hosts ship, the rules here change
/// and the UI follows automatically.
/// </remarks>
public static class CompatibilityDoctor
{
    /// <summary>
    /// Diagnoses every project of a solution. A project that compiles against one TabStudio does not build here (see
    /// <see cref="BuildExclusions"/>) is not built either, and its verdict names the project it needs and why.
    /// </summary>
    /// <param name="projects">The projects to diagnose.</param>
    public static IReadOnlyList<CompatibilityReport> Diagnose(IEnumerable<ProjectInfo> projects)
    {
        ArgumentNullException.ThrowIfNull(projects);
        var all = projects.ToList();
        var excluded = BuildExclusions.Of(all);
        return [.. all.Select(project => excluded.TryGetValue(project.FilePath, out var needed) ? Excluded(project, needed) : Diagnose(project))];
    }

    /// <summary>The verdict for a project that is left out of builds (see <see cref="BuildExclusions"/>).</summary>
    /// <param name="project">The project.</param>
    /// <param name="needed">The project it compiles against that is not built here, or <see langword="null"/> when it is left out for itself.</param>
    private static CompatibilityReport Excluded(ProjectInfo project, ProjectInfo? needed)
    {
        var report = Diagnose(project);
        if (needed is not null)
        {
            return report.Status == CompatibilityStatus.Unsupported
                ? report
                : report with
                {
                    Host = RunHost.None,
                    Status = CompatibilityStatus.Unsupported,
                    RunsVia = "Needs " + needed.Name,
                    Explanation = $"Not supported here: it compiles against {needed.Name}, which {BuildExclusions.Reason(needed)}. TabStudio leaves both out of builds.",
                };
        }

        if (report.Status is CompatibilityStatus.Unsupported or CompatibilityStatus.NeedsWindows)
        {
            return report;
        }

        return BuildExclusions.IsNotBuiltHere(project)
            ? report with
            {
                Host = RunHost.None,
                Status = CompatibilityStatus.Unsupported,
                RunsVia = "On a computer",
                Explanation = $"Not supported here: it {BuildExclusions.Reason(project)}. TabStudio leaves it out of builds.",
            }
            : report with
            {
                Host = RunHost.None,
                Status = CompatibilityStatus.NotRunnable,
                RunsVia = "Not in the solution's build",
                Explanation = "The solution's Debug configuration does not build it, so TabStudio leaves it out of builds, as Visual Studio does.",
            };
    }

    /// <summary>Diagnoses one project.</summary>
    /// <param name="project">The project to diagnose.</param>
    public static CompatibilityReport Diagnose(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        // A Xamarin.Android project looks like a legacy .NET Framework one (TargetFrameworkVersion v13.0), but Upgrade
        // project would not make it build: it needs its platform's workload.
        if (DiagnoseWorkload(project) is { } workload)
        {
            return workload;
        }

        var report = DiagnoseFramework(project);
        return ProjectParser.IsLegacyFrameworkProject(project) ? report with { CanUpgradeProject = true } : report;
    }

    /// <summary>
    /// The verdict for a project that needs a platform workload TabStudio does not have (see
    /// <see cref="ProjectClassifier.RequiredWorkload"/>); <see langword="null"/> for any other project.
    /// </summary>
    private static CompatibilityReport? DiagnoseWorkload(ProjectInfo project) => ProjectClassifier.RequiredWorkload(project) switch
    {
        PlatformWorkload.Maui => Report(
            project, RunHost.None, CompatibilityStatus.Unsupported, ".NET MAUI, on a computer",
            "Not supported: a .NET MAUI project is built by .NET's MAUI workload, on a computer. TabStudio leaves it out of builds."),

        PlatformWorkload.MobilePlatform => DiagnosePlatformHead(project),

        PlatformWorkload.Xamarin => Report(
            project, RunHost.None, CompatibilityStatus.Unsupported, "Xamarin, on a computer",
            "Not supported: a Xamarin project is built by Xamarin's tools in Visual Studio, on a computer. TabStudio leaves it out of builds."),

        PlatformWorkload.Uwp => Report(
            project, RunHost.None, CompatibilityStatus.Unsupported, "UWP, on Windows",
            "Not supported: a UWP project is built by Visual Studio's Universal Windows Platform tools, on Windows. TabStudio leaves it out of builds."),

        PlatformWorkload.WebAssemblyTools => Report(
            project, RunHost.None, CompatibilityStatus.Unsupported, "Browser head, on a computer",
            "The browser head of a cross-platform app, compiled ahead of time to WebAssembly: it builds only with .NET's wasm-tools workload, on a computer. "
            + "TabStudio leaves it out of builds; the app's shared project and its desktop head run here."),

        PlatformWorkload.Protoc => Report(
            project, RunHost.None, CompatibilityStatus.Unsupported, "protoc, on a computer",
            $"Not supported here: it {ProjectClassifier.PlatformWorkloadReason(project)}. TabStudio leaves it out of builds."),

        PlatformWorkload.NewerDotNet => Report(
            project, RunHost.None, CompatibilityStatus.Unsupported, "A newer .NET SDK, on a computer",
            $"Not supported yet: it {ProjectClassifier.PlatformWorkloadReason(project)}. TabStudio leaves it out of builds."),

        _ => null,
    };

    /// <summary>The verdict for the Android, iOS, Mac or Tizen head of a cross-platform app.</summary>
    private static CompatibilityReport DiagnosePlatformHead(ProjectInfo project)
    {
        var platform = string.Join(" and ", project.TargetFrameworks.Select(PlatformName).OfType<string>().Distinct(StringComparer.Ordinal));
        return Report(
            project, RunHost.None, CompatibilityStatus.Unsupported, $"{platform} head, on a computer",
            $"The {platform} head of a cross-platform app: it builds only with .NET's {platform} workload, on a computer. TabStudio leaves it out of builds; "
            + "the app's shared project and its desktop head run here.");
    }

    /// <summary>The verdict, with .NET Framework programs first: they run here when their items allow, or need Windows.</summary>
    private static CompatibilityReport DiagnoseFramework(ProjectInfo project)
    {
        if (WindowsBridgeCompatibility.IsNetFramework(project) && project.IsExecutable && !project.IsTestProject)
        {
            if (NetFrameworkCompatibility.RunsHere(project))
            {
                // Read as MSBuild reads its items, it runs on the path of the SDK-style project it would be upgraded to.
                var report = DiagnoseKind(project);
                return report.Status == CompatibilityStatus.Ready
                    ? report with { Explanation = NetFrameworkCompatibility.RunsHereSummary + " " + report.Explanation }
                    : report;
            }

            if (ProjectParser.IsLegacyFrameworkProject(project))
            {
                return Report(
                    project, RunHost.WindowsBridge, CompatibilityStatus.NeedsWindows, "Windows Bridge",
                    $"A .NET Framework project that needs Windows: it {NetFrameworkCompatibility.WhyWindows(project)}. "
                    + "It builds and runs on your PC through the Windows Bridge, and Upgrade project makes it an SDK-style .NET project.");
            }

            return Report(
                project, RunHost.WindowsBridge, CompatibilityStatus.NeedsWindows, "Windows Bridge",
                ".NET Framework apps run on Windows only. They build and run on your PC through the Windows Bridge.");
        }

        return DiagnoseKind(project);
    }

    /// <summary>
    /// The platform of a mobile target framework, such as Android for <c>net10.0-android</c>; <see langword="null"/> for
    /// one of another platform, such as Windows beside them.
    /// </summary>
    private static string? PlatformName(string targetFramework) =>
        targetFramework.Contains("-android", StringComparison.OrdinalIgnoreCase) ? "Android"
        : targetFramework.Contains("-maccatalyst", StringComparison.OrdinalIgnoreCase) ? "Mac Catalyst"
        : targetFramework.Contains("-tvos", StringComparison.OrdinalIgnoreCase) ? "tvOS"
        : targetFramework.Contains("-macos", StringComparison.OrdinalIgnoreCase) ? "macOS"
        : targetFramework.Contains("-tizen", StringComparison.OrdinalIgnoreCase) ? "Tizen"
        : targetFramework.Contains("-ios", StringComparison.OrdinalIgnoreCase) ? "iOS"
        : null;

    /// <summary>The verdict for what a project produces.</summary>
    private static CompatibilityReport DiagnoseKind(ProjectInfo project)
    {
        return project.Kind switch
        {
            ProjectKind.EmbeddedFirmware => Report(
                project, RunHost.Toolchain, CompatibilityStatus.NeedsToolchain, "Embedded project tools",
                "Builds firmware with the project's native toolchain. Upload requires a selected board and a granted device; firmware never runs as a tablet executable. Hardware support and verification depend on the chosen probe or bootloader."),

            ProjectKind.AspireAppHost => Report(
                project, RunHost.None, CompatibilityStatus.Unsupported, "Not on a tablet",
                "An Aspire AppHost starts the app's projects and containers through Aspire's developer control plane, which runs on a computer, "
                + "and it compiles against code its SDK generates for the projects it references. TabStudio leaves it out of builds; run the projects it starts on their own."),

            ProjectKind.BuildScript => Report(
                project, RunHost.None, CompatibilityStatus.Unsupported, "Build script, on a computer",
                $"The repository's build script{BuildScriptLibrary(project)}: it builds, tests and publishes the other projects by starting "
                + "dotnet and other tools as processes, which a program cannot do on a tablet. TabStudio builds it with the solution but never starts it; "
                + "build and run the other projects here, or run the script on a computer with the .NET SDK."),

            ProjectKind.Benchmark => Report(
                project, RunHost.None, CompatibilityStatus.Unsupported, "Benchmarks, on a computer",
                "BenchmarkDotNet benchmarks: BenchmarkDotNet builds and starts separate processes with the .NET SDK, on a computer."),

            ProjectKind.ConsoleApp when project.IsRoslynLanguage => Report(
                project, RunHost.InProcess, CompatibilityStatus.Ready, "In-process",
                "Compiled in memory and run right here, with its output in the Output panel."),

            ProjectKind.ConsoleApp => Report(
                project, RunHost.InProcess, CompatibilityStatus.Planned, "In-process",
                $"Console apps run in-process. {NotCompiledYet(project)}"),

            ProjectKind.ClassLibrary => Report(
                project, RunHost.None, CompatibilityStatus.NotRunnable, "Built with its users",
                "A library is compiled together with the projects that reference it."),

            ProjectKind.Test when ProjectClassifier.UsesTUnit(project) => Report(
                project, RunHost.None, CompatibilityStatus.Unsupported, "TUnit, on a computer",
                "Not supported: Test Explorer runs xUnit, NUnit and MSTest tests; TUnit tests run with dotnet test, on a computer."),

            ProjectKind.Test when project.IsRoslynLanguage => Report(
                project, RunHost.InProcess, CompatibilityStatus.Ready, "Test Explorer",
                "Its tests run in-process from the Test Explorer, with their results beside each test."),

            ProjectKind.Test => Report(
                project, RunHost.InProcess, CompatibilityStatus.Planned, "Test Explorer",
                $"Tests run in-process from the Test Explorer. {NotCompiledYet(project)}"),

            ProjectKind.AspNetCore when project.Language == ProjectLanguage.CSharp => Report(
                project, RunHost.WebView, CompatibilityStatus.Ready, "Local server + WebView",
                "Served on this device and shown in the web preview beside the code. Saving a file restarts it."),

            ProjectKind.AspNetCore => Report(
                project, RunHost.WebView, CompatibilityStatus.Planned, "Local server + WebView",
                $"Web apps are served on this device. {NotCompiledYet(project)}"),

            ProjectKind.BlazorWebApp when project.Language == ProjectLanguage.CSharp => Report(
                project, RunHost.WebView, CompatibilityStatus.Ready, "Local server + WebView",
                "Served on this device and shown in the web preview. Interactive Server components run here over a live circuit; WebAssembly components run in the preview on .NET for WebAssembly, without the debugger. Saving a file restarts it."),

            ProjectKind.BlazorWebApp => Report(
                project, RunHost.WebView, CompatibilityStatus.Planned, "Local server + WebView",
                $"Blazor apps are served on this device. {NotCompiledYet(project)}"),

            ProjectKind.BlazorWebAssembly when project.Language == ProjectLanguage.CSharp && ProjectClassifier.IsBlazorWebAppClient(project) => Report(
                project, RunHost.WebView, CompatibilityStatus.NotRunnable, "WebAssembly, in its web app",
                "The WebAssembly part of a Blazor Web App: it is compiled for the browser and runs in the web preview when the app that references it runs."),

            ProjectKind.BlazorWebAssembly when project.Language == ProjectLanguage.CSharp => Report(
                project, RunHost.WebView, CompatibilityStatus.Ready, "WebAssembly in the WebView",
                "Compiled for the browser and run on .NET for WebAssembly in the web preview, served from this device. It starts without the debugger: code running in the browser cannot be debugged here. Saving a file reloads it."),

            ProjectKind.BlazorWebAssembly => Report(
                project, RunHost.WebView, CompatibilityStatus.Planned, "WebAssembly in the WebView",
                $"Blazor WebAssembly apps run in the web preview. {NotCompiledYet(project)}"),

            ProjectKind.Worker => Report(
                project, RunHost.InProcess, CompatibilityStatus.Unsupported, "In-process",
                "A worker service starts in-process like a console app, but Stop cannot shut down its host: its background services keep running until TabStudio closes."),

            ProjectKind.Avalonia when project.Language == ProjectLanguage.CSharp => Report(
                project, RunHost.AndroidView, CompatibilityStatus.Ready, "Native view in the app preview",
                "Rendered natively beside the code by TabStudio's own Avalonia. Saving a file restarts it."),

            ProjectKind.Avalonia => Report(
                project, RunHost.AndroidView, CompatibilityStatus.Planned, "Native view in the app preview",
                $"Avalonia apps render beside the code. {NotCompiledYet(project)}"),

            ProjectKind.Uno or ProjectKind.WinUI when project.Language == ProjectLanguage.CSharp => Report(
                project, RunHost.AndroidView, CompatibilityStatus.Ready, "Uno Platform in the app preview",
                "Compiled on this device and drawn natively beside the code by Uno Platform's Skia renderer. Saving a XAML or C# file restarts it. Windows-only APIs require the Windows Bridge."),

            ProjectKind.Uno or ProjectKind.WinUI => Report(
                project, RunHost.AndroidView, CompatibilityStatus.Planned, "Uno Platform in the app preview",
                $"Uno and WinUI apps render beside the code. {NotCompiledYet(project)}"),

            ProjectKind.Maui => Report(
                project, RunHost.None, CompatibilityStatus.Unsupported, ".NET MAUI, on a computer",
                "Not supported: a .NET MAUI project is built by .NET's MAUI workload, on a computer. TabStudio leaves it out of builds."),

            ProjectKind.Wpf when project.Language == ProjectLanguage.CSharp => Report(
                project, RunHost.WebView, CompatibilityStatus.Ready, "OpenSilver in the web preview",
                "Built on OpenSilver, which implements WPF's API, and run in the web preview. What OpenSilver lacks, such as 3D, "
                + "Window.ShowDialog and some members, is named in the Error List; that code runs on your PC through the Windows Bridge."),

            ProjectKind.Wpf => Report(
                project, RunHost.WindowsBridge, CompatibilityStatus.NeedsWindows, "Windows Bridge",
                $"OpenSilver builds C# WPF projects. A WPF project in {LanguageName(project.Language)} builds and runs on your PC through the Windows Bridge."),

            ProjectKind.WinForms when project.IsRoslynLanguage => Report(
                project, RunHost.AndroidView, CompatibilityStatus.Ready, "Cross-platform Forms layer in the app preview",
                "Runs beside the code on TabStudio's own Windows Forms layer, drawn as on Windows 10, and its forms open in the Forms designer. Saving a file restarts it. Code that calls Windows itself, such as P/Invoke, COM or the registry, does not run here."),

            ProjectKind.WinForms => Report(
                project, RunHost.AndroidView, CompatibilityStatus.Planned, "Cross-platform Forms layer in the app preview",
                $"Windows Forms apps run on TabStudio's own Forms layer. {NotCompiledYet(project)}"),

            ProjectKind.NativeCpp => Report(
                project, RunHost.WindowsBridge, CompatibilityStatus.NeedsWindows, "Windows Bridge",
                "Visual C++ projects need the MSVC toolchain. They build and run on your PC through the Windows Bridge."),

            ProjectKind.Cpp => Report(
                project, RunHost.Toolchain, CompatibilityStatus.NeedsToolchain, "C and C++ toolchain",
                "Builds with CMake, make or a C/C++ compiler from the Toolchain Store and runs with its console in the Output panel."),

            ProjectKind.Python => Report(
                project, RunHost.Toolchain, CompatibilityStatus.NeedsToolchain, "Python toolchain",
                "Runs with Python from the Toolchain Store, with its output in the Output panel. Install Python to run it."),

            ProjectKind.Go => Report(
                project, RunHost.Toolchain, CompatibilityStatus.NeedsToolchain, "Go toolchain",
                "Runs with Go from the Toolchain Store, with console input and output in the Output panel. Install Go to run it."),

            ProjectKind.Rust => Report(
                project, RunHost.Toolchain, CompatibilityStatus.NeedsToolchain, "Rust toolchain",
                "Runs with Rust from the Toolchain Store, with console input and output in the Output panel. Install Rust to run it."),

            ProjectKind.Java => Report(
                project, RunHost.Toolchain, CompatibilityStatus.NeedsToolchain, "Java toolchain",
                "Runs with Java from the Toolchain Store, with console input and output in the Output panel. Install Java to run it."),

            ProjectKind.Php => Report(
                project, RunHost.Toolchain, CompatibilityStatus.NeedsToolchain, "PHP toolchain",
                "Runs with PHP from the Toolchain Store: scripts with their output in the Output panel, and Laravel, Symfony, Slim and plain PHP sites served on this device in the web preview. Install PHP to run it."),

            ProjectKind.Kotlin => Report(
                project, RunHost.Toolchain, CompatibilityStatus.NeedsToolchain, "Kotlin toolchain",
                "Builds with Gradle, Maven or the Kotlin compiler from the Toolchain Store and runs on the JVM with console input and output in the Output panel; servers such as Ktor and Spring Boot open in the web preview. Install Kotlin to run it."),

            ProjectKind.Swift => Report(
                project, RunHost.Toolchain, CompatibilityStatus.NeedsToolchain, "Swift toolchain (experimental)",
                "Builds with SwiftPM from the official Swift toolchain in the Toolchain Store, which runs in TabStudio's Ubuntu environment, and runs with console input and output in the Output panel; Vapor servers open in the web preview. Swift on a tablet is experimental. Install Swift to run it."),

            ProjectKind.Node => Report(
                project, RunHost.Toolchain, CompatibilityStatus.NeedsToolchain, "Node.js toolchain",
                "Runs with Node.js from the Toolchain Store, with its output in the Output panel. Install Node.js to run it."),

            ProjectKind.StaticWebSite => Report(
                project, RunHost.WebView, CompatibilityStatus.Ready, "Web preview",
                "Served from the tablet and shown in the web preview beside the code. Saving a file reloads the page, and saved CSS applies without a reload."),
            ProjectKind.Android => Report(
                project, RunHost.Toolchain, CompatibilityStatus.NeedsToolchain, "Android SDK toolchain",
                "Builds with Gradle and the Android SDK on this tablet, installs with Android's confirmation, and streams its debug logcat."),

            _ => Report(
                project, RunHost.None, CompatibilityStatus.Unsupported, "Unknown",
                "TabStudio does not recognize this project type yet."),
        };
    }

    private static CompatibilityReport Report(
        ProjectInfo project, RunHost host, CompatibilityStatus status, string runsVia, string explanation) =>
        new(project.Name, project.Kind, host, status, runsVia, explanation);

    /// <summary>The build-script library a project uses, as " (on Bullseye)", or nothing when it is known by its name only.</summary>
    private static string BuildScriptLibrary(ProjectInfo project) =>
        ProjectClassifier.BuildScriptLibrary(project) is { } library ? $" (on {library})" : string.Empty;

    /// <summary>Why a project in a language other than C# does not run yet.</summary>
    private static string NotCompiledYet(ProjectInfo project) => project.Language == ProjectLanguage.VisualBasic
        ? "TabStudio compiles Visual Basic console apps, libraries, tests and Windows Forms apps; this kind of Visual Basic project is not supported yet."
        : $"The {LanguageName(project.Language)} compiler is not bundled yet.";

    private static string LanguageName(ProjectLanguage language) => language switch
    {
        ProjectLanguage.VisualBasic => "Visual Basic",
        ProjectLanguage.FSharp => "F#",
        ProjectLanguage.Cpp => "C++",
        ProjectLanguage.CSharp => "C#",
        _ => "language",
    };
}
