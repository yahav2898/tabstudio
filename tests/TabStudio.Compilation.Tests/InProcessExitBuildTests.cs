using TabStudio.Compilation.Building;
using TabStudio.Compilation.Running;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Compilation.Tests;

/// <summary>A program runs inside TabStudio, with every library it references.</summary>
public sealed class InProcessExitBuildTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly RoslynWorkspaceHost _host = new();

    [Fact]
    public async Task Environment_exit_in_a_referenced_library_ends_the_program_not_TabStudio()
    {
        // Only the entry project's calls were redirected, so a library's Environment.Exit closed the IDE.
        _temp.Write("Shared/Shared.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        _temp.Write("Shared/Cli.cs", """
            namespace Shared;
            public static class Cli
            {
                public static void Fail(string message)
                {
                    System.Console.WriteLine(message);
                    System.Environment.Exit(2);
                }
            }
            """);
        _temp.Write("App/App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup><ProjectReference Include="..\Shared\Shared.csproj" /></ItemGroup>
            </Project>
            """);
        _temp.Write("App/Program.cs", """
            Shared.Cli.Fail("usage: app <file>");
            System.Console.WriteLine("not reached");
            """);
        var solution = _temp.Write("All.slnx", """<Solution><Project Path="App/App.csproj" /><Project Path="Shared/Shared.csproj" /></Solution>""");
        _host.Load(WorkspaceLoader.Load(solution), FileSystemTextProvider.Instance);

        var build = await new ProjectBuilder(_host).BuildAsync(_host.FindProject(_temp.Resolve("App/App.csproj"))!);
        Assert.True(build.Success, string.Join('\n', build.Diagnostics));

        var console = new CapturingConsole();
        var run = await new ProgramRunner().RunAsync(build.EntryAssembly!, build.Dependencies, [], console);

        Assert.Equal(RunOutcome.Completed, run.Outcome);
        Assert.Equal(2, run.ExitCode);
        Assert.Equal("usage: app <file>\n", console.Output);
    }

    public void Dispose()
    {
        _host.Dispose();
        _temp.Dispose();
    }
}
