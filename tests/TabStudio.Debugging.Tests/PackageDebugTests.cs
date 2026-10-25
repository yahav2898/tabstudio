using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Compilation.Running;
using TabStudio.Core.Workspaces;
using TabStudio.Debugging.Building;
using TabStudio.Testing;

namespace TabStudio.Debugging.Tests;

/// <summary>Debugging a project that uses NuGet packages, with a library on disk standing in for a restored package.</summary>
public sealed class PackageDebugTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly RoslynWorkspaceHost _host = new();
    private readonly string _projectPath;
    private readonly string _programPath;
    private readonly string _packageAssembly;

    public PackageDebugTests()
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
        _programPath = _temp.Write("App/Program.cs", """
            var greeting = Tablet.Greetings.Greeter.Greet("tablet");
            Console.WriteLine(greeting);
            """);

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

    [Fact]
    public async Task Debugs_a_program_that_uses_a_restored_package()
    {
        _host.SetPackageReferences(_projectPath, [_packageAssembly]);
        var build = await new DebugBuilder(_host).BuildAsync(_host.FindProject(_projectPath)!);
        Assert.True(build.Success, string.Join('\n', build.Result.Diagnostics));
        Assert.DoesNotContain(build.Result.Diagnostics, d => d.Id == ProjectBuilder.UnrestoredPackageId);

        using var session = new DebugSession(build);
        Assert.True(session.SetBreakpoint(_programPath, 2).IsBound);
        var paused = new TaskCompletionSource<DebugPausedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Paused += (_, e) => paused.TrySetResult(e);
        var console = new CapturingConsole();

        var run = Task.Run(() => session.RunAsync([], [_packageAssembly], console));
        var pause = await paused.Task.WaitAsync(DebuggedProgram.Timeout);

        Assert.Equal(2, pause.Location?.Line);
        Assert.Equal("\"Hello from a package, tablet\"", DebuggedProgram.Local(pause, "greeting").Value);

        Assert.True(session.Continue());
        var result = await run.WaitAsync(DebuggedProgram.Timeout);
        Assert.Equal(RunOutcome.Completed, result.Outcome);
        Assert.Equal("Hello from a package, tablet\n", console.Output);
        Assert.Empty(console.Error);
    }

    [Fact]
    public async Task Without_the_package_assemblies_the_program_cannot_load_the_package()
    {
        _host.SetPackageReferences(_projectPath, [_packageAssembly]);
        var build = await new DebugBuilder(_host).BuildAsync(_host.FindProject(_projectPath)!);
        using var session = new DebugSession(build) { BreakOnUnhandledExceptions = false };

        var result = await session.RunAsync([], new CapturingConsole()).WaitAsync(DebuggedProgram.Timeout);

        Assert.Equal(RunOutcome.Crashed, result.Outcome);
        Assert.IsAssignableFrom<IOException>(result.Exception);
    }

    [Fact]
    public async Task Unrestored_packages_are_reported_by_the_debug_build()
    {
        var build = await new DebugBuilder(_host).BuildAsync(_host.FindProject(_projectPath)!);

        Assert.False(build.Success);
        Assert.Contains(build.Result.Diagnostics, d => d.Id == ProjectBuilder.UnrestoredPackageId);
    }

    public void Dispose()
    {
        _host.Dispose();
        _temp.Dispose();
    }
}
