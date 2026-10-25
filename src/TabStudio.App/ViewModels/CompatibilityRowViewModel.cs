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
}

/// <summary>One technology detected in a workspace, shown as a chip.</summary>
/// <param name="Name">The technology, such as ".NET 10" or "React".</param>
/// <param name="Evidence">The file that revealed it, such as "global.json".</param>
public sealed record StackChipViewModel(string Name, string Evidence);
