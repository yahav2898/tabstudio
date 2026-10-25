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

            ProjectKind.Test => Report(
                project, RunHost.InProcess, CompatibilityStatus.Planned, "Test Explorer",
                "Tests will run in-process from the Test Explorer, which is on the roadmap."),

            ProjectKind.AspNetCore => Report(
                project, RunHost.WebView, CompatibilityStatus.Planned, "Local server + WebView",
                "The app will be served locally and shown in a WebView next to the code."),

            ProjectKind.BlazorWebAssembly => Report(
                project, RunHost.WebView, CompatibilityStatus.Planned, "WebAssembly in a WebView",
                "The app will be compiled to WebAssembly and shown in a WebView next to the code."),

            ProjectKind.Worker => Report(
                project, RunHost.InProcess, CompatibilityStatus.Planned, "In-process",
                "Background services will run in-process, with logs in the Output panel."),

            ProjectKind.Avalonia => Report(
                project, RunHost.AndroidView, CompatibilityStatus.Planned, "Native Android view",
                "Avalonia runs on Android, so the app will render natively beside the editor."),

            ProjectKind.Uno => Report(
                project, RunHost.AndroidView, CompatibilityStatus.Planned, "Native Android view",
                "Uno Platform runs on Android, so the app will render natively beside the editor."),

            ProjectKind.Maui => Report(
                project, RunHost.AndroidView, CompatibilityStatus.Planned, "Android app",
                "MAUI targets Android directly; the app will be packaged and installed on this tablet."),

            ProjectKind.WinUI => Report(
                project, RunHost.AndroidView, CompatibilityStatus.NeedsChanges, "Uno Platform",
                "WinUI is Windows-only. Uno Platform implements the WinUI API on Android, so the app can run here with an Uno head."),

            ProjectKind.Wpf => Report(
                project, RunHost.WebView, CompatibilityStatus.NeedsChanges, "OpenSilver, or your PC",
                "WPF is Windows-only. OpenSilver can run most WPF code in a WebView; the rest runs through the Windows Bridge."),

            ProjectKind.WinForms => Report(
                project, RunHost.AndroidView, CompatibilityStatus.NeedsChanges, "Cross-platform WinForms, or your PC",
                "Windows Forms is Windows-only. A cross-platform WinForms layer can render it on Android; otherwise it runs through the Windows Bridge."),

            ProjectKind.NativeCpp => Report(
                project, RunHost.WindowsBridge, CompatibilityStatus.NeedsWindows, "Windows Bridge",
                "Visual C++ projects need the MSVC toolchain. They build and run on your PC through the Windows Bridge."),

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
