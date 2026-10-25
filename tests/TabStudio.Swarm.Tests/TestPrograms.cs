using TabStudio.Compilation;
using TabStudio.Debugging.Building;

// Console apps share the process-wide console, and debug sessions the debugger's sink: tests never run in parallel.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace TabStudio.Swarm.Tests;

/// <summary>Small programs compiled in memory, as the tablet would build them.</summary>
internal static class TestPrograms
{
    /// <summary>The file name the programs' source has, as the tablet's debugger knows it.</summary>
    public const string FilePath = "/tablet/Hello/Program.cs";

    /// <summary>A console program, without the debugger.</summary>
    public static CompiledAssembly Console(string source, string name = "Hello")
    {
        var result = new CSharpCompiler().Compile(Request(source, name));
        Assert.True(result.Assembly is not null, string.Join('\n', result.Diagnostics));
        return result.Assembly!;
    }

    /// <summary>A console program instrumented for the debugger.</summary>
    public static DebugBuild Debug(string source, string name = "Hello")
    {
        var build = DebugBuilder.Build(new CSharpCompiler().CreateCompilation(Request(source, name)));
        Assert.True(build.Success, string.Join('\n', build.Result.Diagnostics));
        return build;
    }

    /// <summary>A library, as a referenced project or a package would be.</summary>
    public static CompiledAssembly Library(string source, string name)
    {
        var result = new CSharpCompiler().Compile(Request(source, name) with { Kind = AssemblyKind.Library });
        Assert.True(result.Assembly is not null, string.Join('\n', result.Diagnostics));
        return result.Assembly!;
    }

    private static CompilationRequest Request(string source, string name) => new()
    {
        AssemblyName = name,
        Documents = [new SourceDocument(FilePath, source)],
    };
}
