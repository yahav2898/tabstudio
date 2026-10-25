using TabStudio.Core.Compatibility;
using TabStudio.Core.Workspaces;

namespace TabStudio.Core.Tests;

public sealed class CompatibilityDoctorTests
{
    [Fact]
    public void A_csharp_console_app_runs_in_process_today()
    {
        var report = CompatibilityDoctor.Diagnose(Project(ProjectKind.ConsoleApp));

        Assert.Equal(RunHost.InProcess, report.Host);
        Assert.Equal(CompatibilityStatus.Ready, report.Status);
        Assert.True(report.CanRunToday);
    }

    [Fact]
    public void A_console_app_in_another_language_is_planned_not_ready()
    {
        var report = CompatibilityDoctor.Diagnose(Project(ProjectKind.ConsoleApp, ProjectLanguage.FSharp));

        Assert.Equal(CompatibilityStatus.Planned, report.Status);
        Assert.Contains("F#", report.Explanation, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ProjectKind.Test)]
    [InlineData(ProjectKind.AspNetCore)]
    public void Tests_and_web_apps_in_another_language_are_planned_not_ready(ProjectKind kind)
    {
        var report = CompatibilityDoctor.Diagnose(Project(kind, ProjectLanguage.VisualBasic));

        Assert.Equal(CompatibilityStatus.Planned, report.Status);
        Assert.Contains("Visual Basic", report.Explanation, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ProjectKind.ClassLibrary, RunHost.None, CompatibilityStatus.NotRunnable)]
    [InlineData(ProjectKind.AspNetCore, RunHost.WebView, CompatibilityStatus.Ready)]
    [InlineData(ProjectKind.Test, RunHost.InProcess, CompatibilityStatus.Ready)]
    [InlineData(ProjectKind.BlazorWebAssembly, RunHost.WebView, CompatibilityStatus.Planned)]
    [InlineData(ProjectKind.Wpf, RunHost.WebView, CompatibilityStatus.NeedsChanges)]
    [InlineData(ProjectKind.WinUI, RunHost.AndroidView, CompatibilityStatus.NeedsChanges)]
    [InlineData(ProjectKind.NativeCpp, RunHost.WindowsBridge, CompatibilityStatus.NeedsWindows)]
    [InlineData(ProjectKind.Unknown, RunHost.None, CompatibilityStatus.Unsupported)]
    public void Each_project_kind_gets_an_honest_verdict(ProjectKind kind, RunHost host, CompatibilityStatus status)
    {
        var report = CompatibilityDoctor.Diagnose(Project(kind));

        Assert.Equal(host, report.Host);
        Assert.Equal(status, report.Status);
        Assert.False(string.IsNullOrWhiteSpace(report.Explanation));
    }

    [Fact]
    public void Only_ready_projects_claim_they_can_run_today()
    {
        var reports = CompatibilityDoctor.Diagnose(Enum.GetValues<ProjectKind>().Select(k => Project(k)));

        Assert.All(reports, r => Assert.Equal(r.Status == CompatibilityStatus.Ready, r.CanRunToday));
        Assert.Equal(
            new[] { ProjectKind.ConsoleApp, ProjectKind.Test, ProjectKind.AspNetCore },
            reports.Where(r => r.CanRunToday).Select(r => r.Kind));
    }

    private static ProjectInfo Project(ProjectKind kind, ProjectLanguage language = ProjectLanguage.CSharp) => new()
    {
        Name = kind.ToString(),
        FilePath = Path.Combine(Path.GetTempPath(), kind + ".csproj"),
        Kind = kind,
        Language = language,
    };
}
