using TabStudio.Core.Workspaces;

namespace TabStudio.Core.Compatibility;

/// <summary>
/// What the Windows Bridge changes in the Compatibility Doctor's verdicts: with a Windows PC paired, projects that
/// need real Windows (WPF, Windows Forms, WinUI 3, .NET Framework and Visual C++) build and run on it, so they are
/// ready to run.
/// </summary>
public static class WindowsBridgeCompatibility
{
    /// <summary>The run path the Doctor names for a paired PC, such as <c>Windows Bridge: DESKTOP-7Q2M</c>.</summary>
    /// <param name="pcName">The PC's name.</param>
    public static string RunsVia(string pcName) => $"Windows Bridge: {pcName}";

    /// <summary>Whether a project targets the .NET Framework rather than .NET: a legacy project, or a <c>net4x</c> target.</summary>
    /// <param name="project">The project.</param>
    public static bool IsNetFramework(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (project.Language is not (ProjectLanguage.CSharp or ProjectLanguage.VisualBasic or ProjectLanguage.FSharp))
        {
            return false;
        }

        if (project.GetProperty("TargetFrameworkVersion") is { Length: > 1 } version && version[0] is 'v' or 'V')
        {
            return true;
        }

        return project.TargetFrameworks.Count > 0 && project.TargetFrameworks.All(IsNetFrameworkMoniker);
    }

    /// <summary>Whether a C++ project builds a program, rather than a library.</summary>
    /// <param name="project">The project.</param>
    public static bool IsNativeApplication(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project.Kind == ProjectKind.NativeCpp
            && (project.GetProperty("ConfigurationType") is not { } type || string.Equals(type, "Application", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Whether a project can only run on Windows: its framework or its toolchain is Windows-only.</summary>
    /// <param name="project">The project.</param>
    public static bool IsWindowsOnly(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project.Kind is ProjectKind.Wpf or ProjectKind.WinForms or ProjectKind.WinUI or ProjectKind.NativeCpp
            || IsNetFramework(project)
            || (project.TargetsWindowsOnly && project.IsExecutable);
    }

    /// <summary>Whether the Windows Bridge can run a project: a program, with real Windows as the reason it needs a PC.</summary>
    /// <param name="project">The project.</param>
    public static bool CanRunOnBridge(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return !project.IsTestProject && (project.IsExecutable || IsNativeApplication(project));
    }

    /// <summary>
    /// A verdict with a Windows Bridge paired: a program that cannot run on the tablet because it needs Windows is
    /// ready, on the PC. Verdicts that are ready on the tablet already stay as they are; the bridge is then a choice
    /// in the Run button's menu.
    /// </summary>
    /// <param name="report">The Doctor's verdict without the bridge.</param>
    /// <param name="project">The project.</param>
    /// <param name="pcName">The paired PC's name.</param>
    public static CompatibilityReport Apply(CompatibilityReport report, ProjectInfo project, string pcName)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrEmpty(pcName);
        if (report.Status is CompatibilityStatus.Ready or CompatibilityStatus.NotRunnable || !IsWindowsOnly(project) || !CanRunOnBridge(project))
        {
            return report;
        }

        return report with
        {
            Host = RunHost.WindowsBridge,
            Status = CompatibilityStatus.Ready,
            RunsVia = RunsVia(pcName),
            Explanation = $"Builds and runs on {pcName} with the real Windows, and its windows stream to the Windows Bridge pane beside the code.",
        };
    }

    private static bool IsNetFrameworkMoniker(string moniker) =>
        moniker.Length >= 4
        && moniker.StartsWith("net", StringComparison.OrdinalIgnoreCase)
        && moniker[3] is >= '1' and <= '4'
        && !moniker.Contains('.', StringComparison.Ordinal);
}
