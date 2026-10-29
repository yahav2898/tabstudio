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
    [InlineData(ProjectKind.AspNetCore)]
    [InlineData(ProjectKind.BlazorWebApp)]
    [InlineData(ProjectKind.BlazorWebAssembly)]
    [InlineData(ProjectKind.Uno)]
    [InlineData(ProjectKind.WinUI)]
    public void Apps_in_Visual_Basic_that_need_a_CSharp_only_framework_are_planned_not_ready(ProjectKind kind)
    {
        var report = CompatibilityDoctor.Diagnose(Project(kind, ProjectLanguage.VisualBasic));

        Assert.Equal(CompatibilityStatus.Planned, report.Status);
        Assert.Contains("Visual Basic", report.Explanation, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ProjectKind.ConsoleApp, RunHost.InProcess)]
    [InlineData(ProjectKind.Test, RunHost.InProcess)]
    [InlineData(ProjectKind.WinForms, RunHost.AndroidView)]
    public void Visual_Basic_console_apps_tests_and_Windows_Forms_apps_are_ready_like_CSharp(ProjectKind kind, RunHost host)
    {
        var report = CompatibilityDoctor.Diagnose(Project(kind, ProjectLanguage.VisualBasic));

        Assert.Equal(CompatibilityStatus.Ready, report.Status);
        Assert.Equal(host, report.Host);
    }

    [Theory]
    [InlineData(ProjectKind.ClassLibrary, RunHost.None, CompatibilityStatus.NotRunnable)]
    [InlineData(ProjectKind.AspNetCore, RunHost.WebView, CompatibilityStatus.Ready)]
    [InlineData(ProjectKind.Test, RunHost.InProcess, CompatibilityStatus.Ready)]
    [InlineData(ProjectKind.Avalonia, RunHost.AndroidView, CompatibilityStatus.Ready)]
    [InlineData(ProjectKind.BlazorWebAssembly, RunHost.WebView, CompatibilityStatus.Ready)]
    [InlineData(ProjectKind.BlazorWebApp, RunHost.WebView, CompatibilityStatus.Ready)]
    [InlineData(ProjectKind.Wpf, RunHost.WebView, CompatibilityStatus.Ready)]
    [InlineData(ProjectKind.WinForms, RunHost.AndroidView, CompatibilityStatus.Ready)]
    [InlineData(ProjectKind.Uno, RunHost.AndroidView, CompatibilityStatus.Ready)]
    [InlineData(ProjectKind.WinUI, RunHost.AndroidView, CompatibilityStatus.Ready)]
    [InlineData(ProjectKind.NativeCpp, RunHost.WindowsBridge, CompatibilityStatus.NeedsWindows)]
    [InlineData(ProjectKind.StaticWebSite, RunHost.WebView, CompatibilityStatus.Ready)]
    [InlineData(ProjectKind.Unknown, RunHost.None, CompatibilityStatus.Unsupported)]
    public void Each_project_kind_gets_an_honest_verdict(ProjectKind kind, RunHost host, CompatibilityStatus status)
    {
        var report = CompatibilityDoctor.Diagnose(Project(kind));

        Assert.Equal(host, report.Host);
        Assert.Equal(status, report.Status);
        Assert.False(string.IsNullOrWhiteSpace(report.Explanation));
    }

    /// <summary>
    /// The Doctor says Planned only for what docs/ROADMAP.md commits to. It promised worker services and MAUI apps in an
    /// update, which the roadmap does not; their rows now say what is true today.
    /// </summary>
    [Theory]
    [InlineData(ProjectKind.Worker)]
    [InlineData(ProjectKind.Maui)]
    [InlineData(ProjectKind.Benchmark)]
    public void Kinds_the_roadmap_does_not_commit_to_are_not_reported_planned(ProjectKind kind)
    {
        var report = CompatibilityDoctor.Diagnose(Project(kind));

        Assert.Equal(CompatibilityStatus.Unsupported, report.Status);
        Assert.DoesNotContain(" will ", report.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void Only_ready_projects_claim_they_can_run_today()
    {
        var reports = CompatibilityDoctor.Diagnose(Enum.GetValues<ProjectKind>().Select(k => Project(k)));

        Assert.All(reports, r => Assert.Equal(r.Status == CompatibilityStatus.Ready, r.CanRunToday));
        Assert.Equal(
            new[] { ProjectKind.ConsoleApp, ProjectKind.Test, ProjectKind.AspNetCore, ProjectKind.BlazorWebAssembly, ProjectKind.WinForms, ProjectKind.Wpf, ProjectKind.WinUI, ProjectKind.Avalonia, ProjectKind.Uno, ProjectKind.BlazorWebApp, ProjectKind.StaticWebSite },
            reports.Where(r => r.CanRunToday).Select(r => r.Kind));
    }

    [Fact]
    public void Blazor_apps_say_where_their_code_runs_and_that_WebAssembly_starts_without_the_debugger()
    {
        var webApp = CompatibilityDoctor.Diagnose(Project(ProjectKind.BlazorWebApp));
        var standalone = CompatibilityDoctor.Diagnose(Project(ProjectKind.BlazorWebAssembly));

        Assert.Equal("Local server + WebView", webApp.RunsVia);
        Assert.Contains("Interactive Server components run here", webApp.Explanation, StringComparison.Ordinal);
        Assert.Contains("without the debugger", webApp.Explanation, StringComparison.Ordinal);
        Assert.Equal("WebAssembly in the WebView", standalone.RunsVia);
        Assert.Contains("cannot be debugged", standalone.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void WPF_runs_on_OpenSilver_in_the_web_preview_and_names_what_goes_to_the_Windows_Bridge()
    {
        var csharp = CompatibilityDoctor.Diagnose(Project(ProjectKind.Wpf));
        var visualBasic = CompatibilityDoctor.Diagnose(Project(ProjectKind.Wpf, ProjectLanguage.VisualBasic));

        Assert.Equal("OpenSilver in the web preview", csharp.RunsVia);
        Assert.Contains("Windows Bridge", csharp.Explanation, StringComparison.Ordinal);
        Assert.Equal(RunHost.WindowsBridge, visualBasic.Host);
        Assert.Equal(CompatibilityStatus.NeedsWindows, visualBasic.Status);
    }

    [Fact]
    public void The_WebAssembly_client_of_a_Blazor_Web_App_runs_inside_its_app()
    {
        var client = Project(ProjectKind.BlazorWebAssembly) with
        {
            Properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["StaticWebAssetProjectMode"] = "Default" },
        };

        var report = CompatibilityDoctor.Diagnose(client);

        Assert.Equal(CompatibilityStatus.NotRunnable, report.Status);
        Assert.Equal("WebAssembly, in its web app", report.RunsVia);
    }

    private static ProjectInfo Project(ProjectKind kind, ProjectLanguage language = ProjectLanguage.CSharp) => new()
    {
        Name = kind.ToString(),
        FilePath = Path.Combine(Path.GetTempPath(), kind + ".csproj"),
        Kind = kind,
        Language = language,
    };
}
