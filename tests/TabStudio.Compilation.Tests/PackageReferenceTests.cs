using TabStudio.Compilation.Building;
using TabStudio.Compilation.Running;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Compilation.Tests;

public sealed class PackageReferenceTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly RoslynWorkspaceHost _host = new();
    private readonly string _projectPath;
    private readonly string _packageAssembly;

    public PackageReferenceTests()
    {
        _projectPath = _temp.Write("App/App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup><PackageReference Include="Tablet.Greetings" Version="1.0.0" /></ItemGroup>
            </Project>
            """);
        _temp.Write("App/Program.cs", "Console.WriteLine(Tablet.Greetings.Greeter.Greet(\"tablet\"));");

        // A stand-in for a restored package: a library compiled to a file in a packages folder.
        var library = new CSharpCompiler().Compile(new CompilationRequest
        {
            AssemblyName = "Tablet.Greetings",
            Kind = AssemblyKind.Library,
            Documents = [new SourceDocument("Greeter.cs", "namespace Tablet.Greetings; public static class Greeter { public static string Greet(string name) => $\"Hello from a package, {name}\"; }")],
        });
        Assert.True(library.Success, string.Join('\n', library.Diagnostics));
        _packageAssembly = _temp.Resolve("packages/tablet.greetings/1.0.0/lib/net10.0/Tablet.Greetings.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(_packageAssembly)!);
        File.WriteAllBytes(_packageAssembly, library.Assembly!.Image);

        _host.Load(WorkspaceLoader.Load(_temp.Resolve("App")), FileSystemTextProvider.Instance);
    }

    private ProjectInfo Project => _host.FindProject(_projectPath)!;

    [Fact]
    public async Task Unrestored_package_references_are_reported_and_their_code_does_not_compile()
    {
        var build = await new ProjectBuilder(_host).BuildAsync(Project);

        Assert.False(build.Success);
        Assert.False(_host.ArePackagesRestored(_projectPath));
        Assert.Contains(build.Diagnostics, d => d.Id == ProjectBuilder.UnrestoredPackageId);
    }

    [Fact]
    public async Task Restored_package_assemblies_compile_and_load_when_the_program_runs()
    {
        _host.SetPackageReferences(_projectPath, [_packageAssembly]);

        var build = await new ProjectBuilder(_host).BuildAsync(Project);

        Assert.True(build.Success, string.Join('\n', build.Diagnostics));
        Assert.DoesNotContain(build.Diagnostics, d => d.Id == ProjectBuilder.UnrestoredPackageId);

        var console = new CapturingConsole();
        var run = await new ProgramRunner().RunAsync(build.EntryAssembly!, build.Dependencies, [_packageAssembly], [], console);

        Assert.Empty(console.Error);
        Assert.Equal(RunOutcome.Completed, run.Outcome);
        Assert.Equal("Hello from a package, tablet\n", console.Output);
    }

    [Fact]
    public async Task Live_diagnostics_see_package_references()
    {
        _host.SetPackageReferences(_projectPath, [_packageAssembly]);

        var diagnostics = await _host.GetDocumentDiagnosticsAsync(_temp.Resolve("App/Program.cs"));

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverityLevel.Error);
    }

    [Fact]
    public async Task Package_references_survive_trimming_memory_and_reloading_the_workspace()
    {
        _host.SetPackageReferences(_projectPath, [_packageAssembly]);

        _host.TrimMemory();
        var afterTrim = await _host.GetProjectDiagnosticsAsync(_projectPath);
        _host.Load(WorkspaceLoader.Load(_temp.Resolve("App")), FileSystemTextProvider.Instance);
        var afterReload = await _host.GetProjectDiagnosticsAsync(_projectPath);

        Assert.DoesNotContain(afterTrim, d => d.Severity == DiagnosticSeverityLevel.Error);
        Assert.DoesNotContain(afterReload, d => d.Severity == DiagnosticSeverityLevel.Error);
        Assert.Equal(new[] { _packageAssembly }, _host.GetPackageReferencePaths(_projectPath));
    }

    [Fact]
    public async Task Package_references_can_be_set_before_the_project_is_loaded()
    {
        using var host = new RoslynWorkspaceHost();
        host.SetPackageReferences(_projectPath, [_packageAssembly]);
        host.Load(WorkspaceLoader.Load(_temp.Resolve("App")), FileSystemTextProvider.Instance);

        var build = await new ProjectBuilder(host).BuildAsync(host.FindProject(_projectPath)!);

        Assert.True(build.Success, string.Join('\n', build.Diagnostics));
    }

    [Fact]
    public async Task A_restore_without_assemblies_still_counts_as_restored()
    {
        _host.UpdateText(_temp.Resolve("App/Program.cs"), "Console.WriteLine(1);");
        _host.SetPackageReferences(_projectPath, []);

        var build = await new ProjectBuilder(_host).BuildAsync(Project);

        Assert.True(build.Success, string.Join('\n', build.Diagnostics));
        Assert.DoesNotContain(build.Diagnostics, d => d.Id == ProjectBuilder.UnrestoredPackageId);
    }

    [Fact]
    public async Task Clearing_package_references_brings_the_errors_back()
    {
        _host.SetPackageReferences(_projectPath, [_packageAssembly]);
        _host.ClearPackageReferences(_projectPath);

        var diagnostics = await _host.GetProjectDiagnosticsAsync(_projectPath);

        Assert.False(_host.ArePackagesRestored(_projectPath));
        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverityLevel.Error);
    }

    [Fact]
    public void Reference_assembly_names_cover_the_framework_libraries()
    {
        var names = ReferenceAssemblyNames.Net100;

        // Package restore relies on the set's own case-insensitive comparer, so ask the set itself.
        var known = new[] { "System.Runtime", "system.text.json", "System.Console" }.Where(names.Contains).ToList();
        var unknown = new[] { "Newtonsoft.Json" }.Where(names.Contains).ToList();

        Assert.Equal(3, known.Count);
        Assert.Empty(unknown);
    }

    public void Dispose()
    {
        _host.Dispose();
        _temp.Dispose();
    }
}
