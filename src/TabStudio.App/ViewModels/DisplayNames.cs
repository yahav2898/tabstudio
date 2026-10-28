using TabStudio.Core.Compatibility;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.ViewModels;

/// <summary>User-facing names for project kinds.</summary>
public static class ProjectKindNames
{
    /// <summary>The name of a project kind, as Visual Studio would show it.</summary>
    /// <param name="kind">The kind.</param>
    public static string For(ProjectKind kind) => kind switch
    {
        ProjectKind.ConsoleApp => "Console App",
        ProjectKind.ClassLibrary => "Class Library",
        ProjectKind.Test => "Test Project",
        ProjectKind.AspNetCore => "ASP.NET Core",
        ProjectKind.BlazorWebAssembly => "Blazor WebAssembly",
        ProjectKind.Worker => "Worker Service",
        ProjectKind.WinForms => "Windows Forms",
        ProjectKind.Wpf => "WPF",
        ProjectKind.WinUI => "WinUI",
        ProjectKind.Maui => ".NET MAUI",
        ProjectKind.Avalonia => "Avalonia",
        ProjectKind.Uno => "Uno Platform",
        ProjectKind.NativeCpp => "C++",
        ProjectKind.BlazorWebApp => "Blazor Web App",
        ProjectKind.Python => "Python",
        ProjectKind.Node => "Node.js",
        ProjectKind.StaticWebSite => "Static web site",
        ProjectKind.BuildScript => "Build script",
        ProjectKind.Benchmark => "Benchmarks",
        _ => "Project",
    };
}

/// <summary>User-facing names for Compatibility Doctor verdicts.</summary>
public static class CompatibilityStatusNames
{
    /// <summary>The short status label shown in the Compatibility Doctor.</summary>
    /// <param name="status">The status.</param>
    public static string For(CompatibilityStatus status) => status switch
    {
        CompatibilityStatus.Ready => "Ready",
        CompatibilityStatus.Planned => "Coming in an update",
        CompatibilityStatus.NeedsChanges => "Needs changes",
        CompatibilityStatus.NeedsWindows => "Needs Windows",
        CompatibilityStatus.NotRunnable => "Library",
        CompatibilityStatus.NeedsToolchain => "Needs a toolchain",
        CompatibilityStatus.NeedsDatabase => "Needs a database",
        _ => "Not supported",
    };
}
