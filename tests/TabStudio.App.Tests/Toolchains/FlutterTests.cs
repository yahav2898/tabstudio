using Avalonia.Headless.XUnit;
using TabStudio.App.Services;
using TabStudio.App.Services.Toolchains;
using TabStudio.App.Tests.ViewModels;
using TabStudio.LanguageServers.Configuration;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Projects;

namespace TabStudio.App.Tests.Toolchains;

public sealed class FlutterTests
{
    [AvaloniaTheory]
    [InlineData(ProjectTemplates.FlutterId)]
    [InlineData(ProjectTemplates.FlutterWebId)]
    public async Task Flutter_templates_open_as_runnable_projects_and_show_their_source(string template)
    {
        using var fixture = new WorkspaceFixture();
        var folder = new ProjectTemplates(fixture.Paths).Create(template, "MyCounter");
        var workspace = fixture.Workspace;
        await workspace.LoadAsync(folder);
        await UiTestHelpers.WaitUntilAsync(() => workspace.ActiveDocument is not null, because: "lib/main.dart");
        Assert.Equal("flutter", Assert.Single(workspace.ToolchainProjects).Toolchain.Id);
        Assert.EndsWith("lib/main.dart", workspace.ActiveDocument!.FilePath.Replace('\\', '/'), StringComparison.Ordinal);
        Assert.Contains("FloatingActionButton", File.ReadAllText(Path.Combine(folder, "lib/main.dart")), StringComparison.Ordinal);
        Assert.Contains("flutter_bootstrap.js", File.ReadAllText(Path.Combine(folder, "web/index.html")), StringComparison.Ordinal);
    }

    [Fact]
    public void Flutter_web_launch_uses_the_adapter_without_a_Chrome_dependency()
    {
        using var temp = new TabStudio.Testing.TempDirectory();
        temp.Write("pubspec.yaml", "dependencies:\n  flutter:\n    sdk: flutter\n");
        temp.Write("lib/main.dart", "void main() {}\n");
        var project = Assert.Single(ToolchainProjectDetector.Detect(temp.Path, ToolchainCatalog.Default));
        var launch = ToolchainLaunch.Create("flutter", RunPlanner.PlanProject(project)!)!;
        Assert.Equal("flutter", launch.Type);
        Assert.Equal(Path.Combine(temp.Path, "lib/main.dart"), launch.Program);
        Assert.Contains("web-server", launch.Properties["toolArgs"]!.ToJsonString(), StringComparison.Ordinal);
        Assert.Equal(["debug-adapter"], LanguageServerRegistry.BuiltIn.FindAdapter("flutter")!.Arguments);
        Assert.Equal(["debug_adapter"], LanguageServerRegistry.BuiltIn.FindAdapter("dart")!.Arguments);
    }
}
