using TabStudio.Compilation.Building;
using TabStudio.Compilation.Features;
using TabStudio.Compilation.Running;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Compilation.Tests;

public sealed class SkeletonReferenceTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly RoslynWorkspaceHost _host = new();

    public SkeletonReferenceTests()
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
        _temp.Write("App/Program.cs", "Console.WriteLine(Lib.Greeter.Greet(\"skeleton\"));\n");
        _temp.Write("Lib/Lib.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        _temp.Write("Lib/Greeter.cs", """
            namespace Lib;

            public static class Greeter
            {
                public static string Greet(string name) => $"Hello, {name}";

                internal static int Secret() => 42;
            }
            """);
        _host.Load(WorkspaceLoader.Load(_temp.Path), FileSystemTextProvider.Instance);
        _host.SetActiveProjects([AppProject]);
        _host.UseSkeletonReferences(_temp.Resolve("skeletons"));
        _host.WhenReloadedAsync().WaitAsync(TimeSpan.FromMinutes(2)).GetAwaiter().GetResult();
    }

    private string AppProject => _temp.Resolve("App/App.csproj");

    private string LibProject => _temp.Resolve("Lib/Lib.csproj");

    [Fact]
    public async Task A_project_nobody_edits_is_referenced_as_a_skeleton_and_everything_still_works()
    {
        Assert.True(_host.UsesSkeletonReferences);
        Assert.True(_host.IsSkeleton(LibProject));
        Assert.False(_host.IsSkeleton(AppProject));
        Assert.DoesNotContain(await _host.GetDocumentDiagnosticsAsync(_temp.Resolve("App/Program.cs")), d => d.Severity == DiagnosticSeverityLevel.Error);

        var build = await new ProjectBuilder(_host).BuildAsync(_host.FindProject(AppProject)!);
        Assert.True(build.Success, string.Join("\n", build.Diagnostics));
        Assert.Equal(new[] { "Lib", "App" }, _host.GetBuildOrder(AppProject).Select(p => p.Name));

        var console = new CapturingConsole();
        var run = await new ProgramRunner().RunAsync(build.EntryAssembly!, build.Dependencies, [], console);
        Assert.Equal(RunOutcome.Completed, run.Outcome);
        Assert.Contains("Hello, skeleton", console.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Go_to_definition_finds_the_source_behind_a_skeleton()
    {
        var program = _temp.Resolve("App/Program.cs");
        var text = File.ReadAllText(program);

        var definition = await new LanguageFeatures(_host).Navigation.FindDefinitionAsync(program, text.IndexOf("Greet(", StringComparison.Ordinal) + 1);

        Assert.NotNull(definition);
        var location = Assert.Single(definition.Locations);
        Assert.Equal(_temp.Resolve("Lib/Greeter.cs"), location.FilePath);
        Assert.Equal(5, location.Line);
    }

    [Fact]
    public async Task Editing_a_project_makes_it_active_and_its_skeleton_is_cached_by_content()
    {
        _host.SetActiveProjects([AppProject, LibProject]);
        await _host.WhenReloadedAsync();
        Assert.False(_host.IsSkeleton(LibProject));

        _host.SetActiveProjects([AppProject]);
        await _host.WhenReloadedAsync();
        Assert.True(_host.IsSkeleton(LibProject));
        Assert.Single(Directory.GetFiles(_temp.Resolve("skeletons"), "*.dll"));

        // A change to the skeleton's project makes a new image.
        _host.UpdateText(_temp.Resolve("Lib/Greeter.cs"), File.ReadAllText(_temp.Resolve("Lib/Greeter.cs")) + "\n// changed\n");
        await _host.WhenReloadedAsync();
        Assert.True(_host.IsSkeleton(LibProject));
        Assert.Equal(2, Directory.GetFiles(_temp.Resolve("skeletons"), "*.dll").Length);
    }

    [Fact]
    public async Task An_edit_to_a_skeleton_s_project_and_memory_pressure_never_wait_for_a_reload_or_a_compile()
    {
        // Rename across projects edits files of projects nobody has open, from the UI thread, and Android's memory
        // callbacks arrive there too. Both reloaded the solution and compiled skeleton images before returning.
        var greeter = _temp.Resolve("Lib/Greeter.cs");
        var program = _temp.Resolve("App/Program.cs");
        var compiling = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        _host.BeforeSkeletonImages = () =>
        {
            compiling.TrySetResult();
            release.Wait(TimeSpan.FromMinutes(1));
        };
        var images = Directory.GetFiles(_temp.Resolve("skeletons"), "*.dll").Length;

        var renamed = File.ReadAllText(greeter).Replace("Greet(string name)", "Welcome(string name)", StringComparison.Ordinal);
        Assert.True(await Task.Run(() => _host.UpdateText(greeter, renamed)).WaitAsync(TimeSpan.FromSeconds(10)));
        await compiling.Task.WaitAsync(TimeSpan.FromSeconds(30));

        // While the image compiles, nothing it does is in the way: typing in the open project, and trimming memory.
        Assert.Equal(images, Directory.GetFiles(_temp.Resolve("skeletons"), "*.dll").Length);
        Assert.True(await Task.Run(() => _host.UpdateText(program, "Console.WriteLine(Lib.Greeter.Welcome(\"skeleton\"));\n")).WaitAsync(TimeSpan.FromSeconds(10)));
        await Task.Run(_host.TrimMemory).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(_host.IsSkeleton(LibProject));

        release.Set();
        await _host.WhenReloadedAsync().WaitAsync(TimeSpan.FromMinutes(2));

        // Afterwards the open project sees the renamed method through the new image.
        Assert.True(_host.IsSkeleton(LibProject));
        Assert.Equal(images + 1, Directory.GetFiles(_temp.Resolve("skeletons"), "*.dll").Length);
        Assert.DoesNotContain(await _host.GetDocumentDiagnosticsAsync(program), d => d.Severity == DiagnosticSeverityLevel.Error);
    }

    [Fact]
    public async Task Turning_skeletons_off_restores_project_references()
    {
        _host.UseSkeletonReferences(null);
        await _host.WhenReloadedAsync();

        Assert.False(_host.UsesSkeletonReferences);
        Assert.False(_host.IsSkeleton(LibProject));
    }

    public void Dispose()
    {
        _host.Dispose();
        _temp.Dispose();
    }
}
