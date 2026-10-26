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
    /// <summary>Diagnoses every project.</summary>
    /// <param name="projects">The projects to diagnose.</param>
    public static IReadOnlyList<CompatibilityReport> Diagnose(IEnumerable<ProjectInfo> projects)
    {
        ArgumentNullException.ThrowIfNull(projects);
        return projects.Select(Diagnose).ToList();
    }

    /// <summary>Diagnoses one project.</summary>
    /// <param name="project">The project to diagnose.</param>
    public static CompatibilityReport Diagnose(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (WindowsBridgeCompatibility.IsNetFramework(project) && project.IsExecutable && !project.IsTestProject)
        {
            return Report(
                project, RunHost.WindowsBridge, CompatibilityStatus.NeedsWindows, "Windows Bridge",
                ".NET Framework apps run on Windows only. They build and run on your PC through the Windows Bridge.");
        }

        return project.Kind switch
        {
            ProjectKind.ConsoleApp when project.Language == ProjectLanguage.CSharp => Report(
                project, RunHost.InProcess, CompatibilityStatus.Ready, "In-process",
                "Compiled in memory and run right here, with its output in the Output panel."),

            ProjectKind.ConsoleApp => Report(
                project, RunHost.InProcess, CompatibilityStatus.Planned, "In-process",
                $"Console apps run in-process. The {LanguageName(project.Language)} compiler is not bundled yet."),

            ProjectKind.ClassLibrary => Report(
                project, RunHost.None, CompatibilityStatus.NotRunnable, "Built with its users",
                "A library is compiled together with the projects that reference it."),

            ProjectKind.Test when project.Language == ProjectLanguage.CSharp => Report(
                project, RunHost.InProcess, CompatibilityStatus.Ready, "Test Explorer",
                "Its tests run in-process from the Test Explorer, with their results beside each test."),

            ProjectKind.Test => Report(
                project, RunHost.InProcess, CompatibilityStatus.Planned, "Test Explorer",
                $"Tests run in-process from the Test Explorer. The {LanguageName(project.Language)} compiler is not bundled yet."),

            ProjectKind.AspNetCore when project.Language == ProjectLanguage.CSharp => Report(
                project, RunHost.WebView, CompatibilityStatus.Ready, "Local server + WebView",
                "Served on this device and shown in the web preview beside the code. Saving a file restarts it."),

            ProjectKind.AspNetCore => Report(
                project, RunHost.WebView, CompatibilityStatus.Planned, "Local server + WebView",
                $"Web apps are served on this device. The {LanguageName(project.Language)} compiler is not bundled yet."),

            ProjectKind.BlazorWebApp when project.Language == ProjectLanguage.CSharp => Report(
                project, RunHost.WebView, CompatibilityStatus.Ready, "Local server + WebView",
                "Served on this device and shown in the web preview. Interactive Server components run here over a live circuit; WebAssembly components run in the preview on .NET for WebAssembly, without the debugger. Saving a file restarts it."),

            ProjectKind.BlazorWebApp => Report(
                project, RunHost.WebView, CompatibilityStatus.Planned, "Local server + WebView",
                $"Blazor apps are served on this device. The {LanguageName(project.Language)} compiler is not bundled yet."),

            ProjectKind.BlazorWebAssembly when project.Language == ProjectLanguage.CSharp && ProjectClassifier.IsBlazorWebAppClient(project) => Report(
                project, RunHost.WebView, CompatibilityStatus.NotRunnable, "WebAssembly, in its web app",
                "The WebAssembly part of a Blazor Web App: it is compiled for the browser and runs in the web preview when the app that references it runs."),

            ProjectKind.BlazorWebAssembly when project.Language == ProjectLanguage.CSharp => Report(
                project, RunHost.WebView, CompatibilityStatus.Ready, "WebAssembly in the WebView",
                "Compiled for the browser and run on .NET for WebAssembly in the web preview, served from this device. It starts without the debugger: code running in the browser cannot be debugged here. Saving a file reloads it."),

            ProjectKind.BlazorWebAssembly => Report(
                project, RunHost.WebView, CompatibilityStatus.Planned, "WebAssembly in the WebView",
                $"Blazor WebAssembly apps run in the web preview. The {LanguageName(project.Language)} compiler is not bundled yet."),

            ProjectKind.Worker => Report(
                project, RunHost.InProcess, CompatibilityStatus.Planned, "In-process",
                "Background services will run in-process, with logs in the Output panel."),

            ProjectKind.Avalonia when project.Language == ProjectLanguage.CSharp => Report(
                project, RunHost.AndroidView, CompatibilityStatus.Ready, "Native view in the app preview",
                "Rendered natively beside the code by TabStudio's own Avalonia. Saving a file restarts it."),

            ProjectKind.Avalonia => Report(
                project, RunHost.AndroidView, CompatibilityStatus.Planned, "Native view in the app preview",
                $"Avalonia apps render beside the code. The {LanguageName(project.Language)} compiler is not bundled yet."),

            ProjectKind.Uno or ProjectKind.WinUI when project.Language == ProjectLanguage.CSharp => Report(
                project, RunHost.AndroidView, CompatibilityStatus.Ready, "Uno Platform in the app preview",
                "Compiled on this device and drawn natively beside the code by Uno Platform's Skia renderer. Saving a XAML or C# file restarts it. Windows-only APIs require the Windows Bridge."),

            ProjectKind.Uno or ProjectKind.WinUI => Report(
                project, RunHost.AndroidView, CompatibilityStatus.Planned, "Uno Platform in the app preview",
                $"Uno and WinUI apps render beside the code. The {LanguageName(project.Language)} compiler is not bundled yet."),

            ProjectKind.Maui => Report(
                project, RunHost.AndroidView, CompatibilityStatus.Planned, "Android app",
                "MAUI targets Android directly; the app will be packaged and installed on this tablet."),

            ProjectKind.Wpf when project.Language == ProjectLanguage.CSharp => Report(
                project, RunHost.WebView, CompatibilityStatus.Ready, "OpenSilver in the web preview",
                "Built on OpenSilver, which implements WPF's API, and run in the web preview. What OpenSilver lacks, such as 3D, "
                + "Window.ShowDialog and some members, is named in the Error List; that code runs on your PC through the Windows Bridge."),

            ProjectKind.Wpf => Report(
                project, RunHost.WindowsBridge, CompatibilityStatus.NeedsWindows, "Windows Bridge",
                $"OpenSilver builds C# WPF projects. A WPF project in {LanguageName(project.Language)} builds and runs on your PC through the Windows Bridge."),

            ProjectKind.WinForms when project.Language == ProjectLanguage.CSharp => Report(
                project, RunHost.AndroidView, CompatibilityStatus.Ready, "Cross-platform Forms layer in the app preview",
                "Runs beside the code on TabStudio's own Windows Forms layer, drawn as on Windows 10, and its forms open in the Forms designer. Saving a file restarts it. Code that calls Windows itself, such as P/Invoke, COM or the registry, does not run here."),

            ProjectKind.WinForms => Report(
                project, RunHost.AndroidView, CompatibilityStatus.Planned, "Cross-platform Forms layer in the app preview",
                $"Windows Forms apps run on TabStudio's own Forms layer. The {LanguageName(project.Language)} compiler is not bundled yet."),

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

            ProjectKind.Node => Report(
                project, RunHost.Toolchain, CompatibilityStatus.NeedsToolchain, "Node.js toolchain",
                "Runs with Node.js from the Toolchain Store, with its output in the Output panel. Install Node.js to run it."),

            ProjectKind.StaticWebSite => Report(
                project, RunHost.WebView, CompatibilityStatus.Ready, "Web preview",
                "Served from the tablet and shown in the web preview beside the code. Saving a file reloads the page, and saved CSS applies without a reload."),

            _ => Report(
                project, RunHost.None, CompatibilityStatus.Unsupported, "Unknown",
                "TabStudio does not recognize this project type yet."),
        };
    }

    private static CompatibilityReport Report(
        ProjectInfo project, RunHost host, CompatibilityStatus status, string runsVia, string explanation) =>
        new(project.Name, project.Kind, host, status, runsVia, explanation);

    private static string LanguageName(ProjectLanguage language) => language switch
    {
        ProjectLanguage.VisualBasic => "Visual Basic",
        ProjectLanguage.FSharp => "F#",
        ProjectLanguage.Cpp => "C++",
        ProjectLanguage.CSharp => "C#",
        _ => "language",
    };
}
