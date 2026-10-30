using TabStudio.Compilation;
using TabStudio.Compilation.Running;
using TabStudio.Compilation.VisualBasic;
using TabStudio.Debugging.Building;

namespace TabStudio.Debugging.Tests;

/// <summary>
/// Compiles a small Visual Basic program in memory and runs it under the debugger, as <see cref="DebuggedProgram"/>
/// does for C#.
/// </summary>
internal static class DebuggedVisualBasicProgram
{
    public const string FilePath = "Program.vb";

    /// <summary>Compiles a program, instruments it and runs it under the debugger with breakpoints on lines of it.</summary>
    public static DebuggedProgram Create(string source, params int[] breakpointLines) =>
        new(Compile(source), FilePath, breakpointLines);

    /// <summary>Compiles a program and instruments it, failing the test on compile errors.</summary>
    public static DebugBuild Compile(string source, string filePath = FilePath)
    {
        var compilation = VisualBasicCompiler.CreateCompilation(Request("Debuggee", source, filePath));
        var build = DebugBuilder.Build(compilation);
        Assert.True(build.Success, string.Join('\n', build.Result.Diagnostics));
        return build;
    }

    /// <summary>Compiles a program without instrumenting it.</summary>
    public static CompiledAssembly CompilePlain(string source)
    {
        var result = VisualBasicCompiler.Compile(Request("Plain", source, FilePath));
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        return result.Assembly!;
    }

    /// <summary>Runs a program without the debugger and returns what it wrote.</summary>
    public static async Task<string> RunPlainAsync(string source)
    {
        var console = new CapturingConsole();
        var result = await new ProgramRunner().RunAsync(CompilePlain(source), [], [], console);
        Assert.True(result.Outcome == RunOutcome.Completed, console.Error + result.Exception);
        return console.Output;
    }

    private static CompilationRequest Request(string prefix, string source, string filePath) => new()
    {
        AssemblyName = prefix + Guid.NewGuid().ToString("N")[..8],
        Documents = [new SourceDocument(filePath, source)],
    };
}
