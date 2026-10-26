using TabStudio.Core.Compatibility;

namespace TabStudio.App.ViewModels;

/// <summary>One project in the Compatibility Doctor table.</summary>
/// <param name="Report">The doctor's verdict.</param>
public sealed record CompatibilityRowViewModel(CompatibilityReport Report)
{
    /// <summary>The project name.</summary>
    public string Project => Report.ProjectName;

    /// <summary>The project type, such as "Console App".</summary>
    public string Type => ProjectKindNames.For(Report.Kind);

    /// <summary>How it runs, such as "In-process".</summary>
    public string RunsVia => Report.RunsVia;

    /// <summary>The short status label.</summary>
    public string Status => CompatibilityStatusNames.For(Report.Status);

    /// <summary>Why, in a sentence or two.</summary>
    public string Explanation => Report.Explanation;

    /// <summary>Whether F5 runs it on this device.</summary>
    public bool IsReady => Report.Status == CompatibilityStatus.Ready;

    /// <summary>Whether it is a library, built together with the projects that use it.</summary>
    public bool IsLibrary => Report.Status == CompatibilityStatus.NotRunnable;

    /// <summary>Whether it cannot run on this device as it is.</summary>
    public bool IsBlocked => !IsReady && !IsLibrary;

    /// <summary>Whether code changes would make it run here, which "Fix with Agent" asks Claude to make.</summary>
    public bool CanFixWithAgent => Report.Status == CompatibilityStatus.NeedsChanges;

    /// <summary>Whether this framework has an Avalonia conversion.</summary>
    public bool CanPortToAvalonia => Report.Kind is TabStudio.Core.Workspaces.ProjectKind.Wpf or TabStudio.Core.Workspaces.ProjectKind.WinForms;

    /// <summary>The toolchain that runs the project, such as <c>python</c>, for Python and Node.js projects.</summary>
    public string? ToolchainId { get; init; }

    /// <summary>The label of the fix that installs the project's toolchain, such as "Install Python", when it can be installed.</summary>
    public string? InstallToolchainText { get; init; }

    /// <summary>Whether installing a toolchain from the Toolchain Store makes it run here.</summary>
    public bool CanInstallToolchain => InstallToolchainText is not null;
}

/// <summary>One technology detected in a workspace, shown as a chip.</summary>
/// <param name="Name">The technology, such as ".NET 10" or "React".</param>
/// <param name="Evidence">The file that revealed it, such as "global.json".</param>
public sealed record StackChipViewModel(string Name, string Evidence);
