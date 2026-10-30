using System.Threading.Channels;
using TabStudio.Compilation;
using TabStudio.Compilation.Running;
using TabStudio.Core.Workspaces;
using TabStudio.Debugging.Building;
using TabStudio.Testing;

namespace TabStudio.Debugging.Tests;

/// <summary>Debugging Visual Basic projects of a workspace, alone and referenced from a C# project.</summary>
public sealed class VisualBasicWorkspaceDebugTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly RoslynWorkspaceHost _host = new();

    public VisualBasicWorkspaceDebugTests()
    {
        _temp.Write("App/App.vbproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <RootNamespace>App</RootNamespace>
              </PropertyGroup>
              <ItemGroup><ProjectReference Include="..\Greetings\Greetings.vbproj" /></ItemGroup>
            </Project>
            """);
        _temp.Write("App/Program.vb", """
            Imports Greetings

            Module Program
                Sub Main()
                    Dim message = Greeter.Greet("tablet")
                    Console.WriteLine(message)
                End Sub
            End Module
            """);
        _temp.Write("Greetings/Greetings.vbproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        _temp.Write("Greetings/Greeter.vb", """
            Public Module Greeter
                Public Function Greet(name As String) As String
                    Dim greeting = "Hello, " & name
                    Return greeting
                End Function
            End Module
            """);
        _temp.Write("Mixed/Mixed.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup><ProjectReference Include="..\Greetings\Greetings.vbproj" /></ItemGroup>
            </Project>
            """);
        _temp.Write("Mixed/Program.cs", """
            var message = Greetings.Greeter.Greet("mixed");
            Console.WriteLine(message);
            """);

        _host.Load(WorkspaceLoader.Load(_temp.Path), FileSystemTextProvider.Instance);
    }

    [Fact]
    public async Task Breakpoints_work_in_a_referenced_visual_basic_project()
    {
        var build = await new DebugBuilder(_host).BuildAsync(_host.FindProject(_temp.Resolve("App/App.vbproj"))!);
        Assert.True(build.Success, string.Join('\n', build.Result.Diagnostics));
        Assert.Equal(ProjectLanguage.VisualBasic, build.Language);
        Assert.Equal("Greetings", Assert.Single(build.Result.Dependencies).Name);

        using var session = new DebugSession(build);
        var greeter = _temp.Resolve("Greetings/Greeter.vb");
        Assert.True(session.SetBreakpoint(greeter, 4).IsBound);
        var paused = new TaskCompletionSource<DebugPausedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Paused += (_, e) => paused.TrySetResult(e);
        var console = new CapturingConsole();

        var run = Task.Run(() => session.RunAsync([], console));
        var pause = await paused.Task.WaitAsync(DebuggedProgram.Timeout);

        Assert.Equal(greeter, pause.Location?.FilePath);
        Assert.Equal(4, pause.Location?.Line);
        Assert.Equal("\"tablet\"", DebuggedProgram.Local(pause, "name").Value);
        Assert.Equal("\"Hello, tablet\"", DebuggedProgram.Local(pause, "greeting").Value);
        Assert.Equal("String", DebuggedProgram.Local(pause, "greeting").Type);
        Assert.Equal(new[] { "Greeter.Greet(String)", "Program.Main()" }, pause.CallStack.Select(f => f.MethodName));
        Assert.Equal(_temp.Resolve("App/Program.vb"), pause.CallStack[1].Location?.FilePath);
        Assert.Equal(5, pause.CallStack[1].Location?.Line);

        session.Continue();
        var result = await run.WaitAsync(DebuggedProgram.Timeout);
        Assert.Equal(RunOutcome.Completed, result.Outcome);
        Assert.Equal("Hello, tablet\n", console.Output);
    }

    [Fact]
    public async Task A_csharp_program_steps_into_visual_basic_and_back_in_each_language()
    {
        var build = await new DebugBuilder(_host).BuildAsync(_host.FindProject(_temp.Resolve("Mixed/Mixed.csproj"))!);
        Assert.True(build.Success, string.Join('\n', build.Result.Diagnostics));
        Assert.Equal(ProjectLanguage.CSharp, build.Language);

        using var session = new DebugSession(build);
        var greeter = _temp.Resolve("Greetings/Greeter.vb");
        session.SetBreakpoint(greeter, 4);
        var pauses = Channel.CreateUnbounded<DebugPausedEventArgs>();
        session.Paused += (_, e) => pauses.Writer.TryWrite(e);
        var console = new CapturingConsole();

        var run = Task.Run(() => session.RunAsync([], console));
        var inBasic = await pauses.Reader.ReadAsync().AsTask().WaitAsync(DebuggedProgram.Timeout);

        Assert.Equal(greeter, inBasic.Location?.FilePath);
        Assert.Equal("\"mixed\"", DebuggedProgram.Local(inBasic, "name").Value);
        Assert.Equal("String", DebuggedProgram.Local(inBasic, "name").Type);
        Assert.Equal(new[] { "Greeter.Greet(String)", "<top-level statements>" }, inBasic.CallStack.Select(f => f.MethodName));

        session.StepOver();
        var inCSharp = await pauses.Reader.ReadAsync().AsTask().WaitAsync(DebuggedProgram.Timeout);

        Assert.Equal(_temp.Resolve("Mixed/Program.cs"), inCSharp.Location?.FilePath);
        Assert.Equal(2, inCSharp.Location?.Line);
        Assert.Equal("\"Hello, mixed\"", DebuggedProgram.Local(inCSharp, "message").Value);
        Assert.Equal("string", DebuggedProgram.Local(inCSharp, "message").Type);

        session.Continue();
        var result = await run.WaitAsync(DebuggedProgram.Timeout);
        Assert.Equal(RunOutcome.Completed, result.Outcome);
        Assert.Equal("Hello, mixed\n", console.Output);
    }

    public void Dispose()
    {
        _host.Dispose();
        _temp.Dispose();
    }
}
