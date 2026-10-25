using TabStudio.Compilation;
using TabStudio.Compilation.Running;
using TabStudio.Core.Workspaces;
using TabStudio.Debugging.Building;
using TabStudio.Testing;

namespace TabStudio.Debugging.Tests;

public sealed class WorkspaceDebugTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly RoslynWorkspaceHost _host = new();

    public WorkspaceDebugTests()
    {
        _temp.Write("Shop.slnx", """
            <Solution>
              <Project Path="App/App.csproj" />
              <Project Path="Lib/Lib.csproj" />
            </Solution>
            """);
        _temp.Write("App/App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup><ProjectReference Include="..\Lib\Lib.csproj" /></ItemGroup>
            </Project>
            """);
        _temp.Write("App/Program.cs", """
            using Lib;
            var message = Greeter.Greet("tablet");
            Console.WriteLine(message);
            """);
        _temp.Write("Lib/Lib.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        _temp.Write("Lib/Greeter.cs", """
            namespace Lib;
            public static class Greeter
            {
                public static string Greet(string name) => $"Hello, {name}";
            }
            """);

        _host.Load(WorkspaceLoader.Load(_temp.Path), FileSystemTextProvider.Instance);
    }

    [Fact]
    public async Task Breakpoints_work_in_a_referenced_project()
    {
        var build = await new DebugBuilder(_host).BuildAsync(_host.FindProject(_temp.Resolve("App/App.csproj"))!);
        Assert.True(build.Success, string.Join('\n', build.Result.Diagnostics));
        Assert.Equal("Lib", Assert.Single(build.Result.Dependencies).Name);

        using var session = new DebugSession(build);
        var greeter = _temp.Resolve("Lib/Greeter.cs");
        Assert.True(session.SetBreakpoint(greeter, 4).IsBound);
        var paused = new TaskCompletionSource<DebugPausedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Paused += (_, e) => paused.TrySetResult(e);
        var console = new CapturingConsole();

        var run = Task.Run(() => session.RunAsync([], console));
        var pause = await paused.Task.WaitAsync(DebuggedProgram.Timeout);

        Assert.Equal(greeter, pause.Location?.FilePath);
        Assert.Equal(4, pause.Location?.Line);
        Assert.Equal("\"tablet\"", DebuggedProgram.Local(pause, "name").Value);
        Assert.Equal(new[] { "Greeter.Greet(string)", "<top-level statements>" }, pause.CallStack.Select(f => f.MethodName));
        Assert.Equal(_temp.Resolve("App/Program.cs"), pause.CallStack[1].Location?.FilePath);
        Assert.Equal(2, pause.CallStack[1].Location?.Line);

        session.Continue();
        var result = await run.WaitAsync(DebuggedProgram.Timeout);
        Assert.Equal(RunOutcome.Completed, result.Outcome);
        Assert.Equal("Hello, tablet\n", console.Output);
    }

    [Fact]
    public async Task A_project_with_errors_is_not_instrumented()
    {
        _host.UpdateText(_temp.Resolve("App/Program.cs"), "Console.WriteLine(missing);");

        var build = await new DebugBuilder(_host).BuildAsync(_host.FindProject(_temp.Resolve("App/App.csproj"))!);

        Assert.False(build.Success);
        Assert.Contains(build.Result.Diagnostics, d => d.Id == "CS0103");
        Assert.Empty(build.DebugInfo.Statements);
    }

    public void Dispose()
    {
        _host.Dispose();
        _temp.Dispose();
    }
}
