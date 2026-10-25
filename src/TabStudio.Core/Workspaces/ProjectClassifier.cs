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
        "Microsoft.NET.Test.Sdk", "xunit", "xunit.v3", "NUnit", "MSTest.TestFramework", "MSTest",
    ];

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

    private static ProjectKind ClassifyApplication(ProjectInfo project, string sdk)
    {
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
            return ProjectKind.AspNetCore;
        }

        if (sdk.Equals("Microsoft.NET.Sdk.Worker", StringComparison.OrdinalIgnoreCase))
        {
            return ProjectKind.Worker;
        }

        if (project.IsExecutable && project.HasPackage("Avalonia"))
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

    private static bool IsTrue(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "enable", StringComparison.OrdinalIgnoreCase);
}
