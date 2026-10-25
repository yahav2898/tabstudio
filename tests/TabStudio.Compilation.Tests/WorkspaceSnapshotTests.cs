using TabStudio.Compilation.Building;
using TabStudio.Compilation.Running;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Compilation.Tests;

public sealed class WorkspaceSnapshotTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly RoslynWorkspaceHost _host = new();

    public WorkspaceSnapshotTests()
    {
        _temp.Write("App/App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>
              <ItemGroup><ProjectReference Include="..\Lib\Lib.csproj" /></ItemGroup>
            </Project>
            """);
        _temp.Write("App/Program.cs", "Console.WriteLine(Lib.Greeter.Greet());\n");
        _temp.Write("Lib/Lib.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        _temp.Write("Lib/Greeter.cs", "namespace Lib;\npublic static class Greeter { public static string Greet() => \"Hello\"; }\n");
        _host.Load(WorkspaceLoader.Load(_temp.Path), FileSystemTextProvider.Instance);
    }

    [Fact]
    public async Task Changes_are_compiled_without_touching_the_live_workspace()
    {
        var snapshot = _host.CreateSnapshot(new Dictionary<string, string?>
        {
            [_temp.Resolve("Lib/Greeter.cs")] = "namespace Lib;\npublic static class Greeter { public static string Greet() => 42; }\n",
        });

        var errors = (await snapshot.GetDiagnosticsAsync()).Where(d => d.Severity == DiagnosticSeverityLevel.Error).ToList();

        Assert.Equal("CS0029", Assert.Single(errors).Id);
        Assert.DoesNotContain(await _host.GetProjectDiagnosticsAsync(_temp.Resolve("Lib/Lib.csproj")), d => d.Severity == DiagnosticSeverityLevel.Error);
    }

    [Fact]
    public async Task New_files_join_their_project_and_deleted_files_leave_it()
    {
        var snapshot = _host.CreateSnapshot(new Dictionary<string, string?>
        {
            [_temp.Resolve("Lib/Farewell.cs")] = "namespace Lib;\npublic static class Farewell { public static string Say() => \"Bye\"; }\n",
            [_temp.Resolve("App/Program.cs")] = "Console.WriteLine(Lib.Greeter.Greet() + Lib.Farewell.Say());\n",
        });
        Assert.Empty((await snapshot.GetDiagnosticsAsync()).Where(d => d.Severity == DiagnosticSeverityLevel.Error));

        var deleted = _host.CreateSnapshot(new Dictionary<string, string?> { [_temp.Resolve("Lib/Greeter.cs")] = null });
        Assert.Contains(await deleted.GetDiagnosticsAsync(), d => d.Id == "CS0234" || d.Id == "CS0103");
    }

    [Fact]
    public async Task A_snapshot_builds_and_runs()
    {
        var snapshot = _host.CreateSnapshot(new Dictionary<string, string?>
        {
            [_temp.Resolve("Lib/Greeter.cs")] = "namespace Lib;\npublic static class Greeter { public static string Greet() => \"Hello from the snapshot\"; }\n",
        });
        var app = snapshot.FindProject(_temp.Resolve("App/App.csproj"))!;

        var build = await new ProjectBuilder(snapshot).BuildAsync(app);
        var console = new CapturingConsole();
        var run = await new ProgramRunner().RunAsync(build.EntryAssembly!, build.Dependencies, [], console);

        Assert.True(build.Success);
        Assert.Equal(RunOutcome.Completed, run.Outcome);
        Assert.Contains("Hello from the snapshot", console.Output, StringComparison.Ordinal);
        Assert.Equal(new[] { "Lib", "App" }, snapshot.GetBuildOrder(app.FilePath).Select(p => p.Name));
    }

    public void Dispose()
    {
        _host.Dispose();
        _temp.Dispose();
    }
}
