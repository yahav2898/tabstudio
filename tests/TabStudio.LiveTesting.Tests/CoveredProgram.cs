using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using TabStudio.Compilation;
using TabStudio.Compilation.Running;
using TabStudio.LiveTesting.Coverage;
using TabStudio.LiveTesting.Instrumentation;

namespace TabStudio.LiveTesting.Tests;

/// <summary>Compiles a program in memory with and without coverage instrumentation, and runs it.</summary>
internal static class CoveredProgram
{
    /// <summary>The path the program's source is compiled under; stack traces point here.</summary>
    public static readonly string FilePath = Path.Combine(Path.GetTempPath(), "tabstudio-coverage", "Program.cs");

    public static CSharpCompilation Compilation(string source) =>
        new CSharpCompiler().CreateCompilation(new CompilationRequest
        {
            AssemblyName = "Covered" + Guid.NewGuid().ToString("N")[..8],
            Documents = [new SourceDocument(FilePath, source)],
        });

    /// <summary>Instruments and emits a program; the compilation must succeed.</summary>
    public static (CompiledAssembly Assembly, CoverageModule Module, string Text) Instrument(string source)
    {
        var compilation = Compilation(source);
        Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        var (instrumented, module) = CoverageInstrumenter.Instrument(compilation, "/projects/" + compilation.AssemblyName + ".csproj", CancellationToken.None);
        var text = instrumented.SyntaxTrees.First(t => t.FilePath == FilePath).ToString();
        return (Emit(instrumented), module, text);
    }

    /// <summary>Emits a program as it is.</summary>
    public static CompiledAssembly Plain(string source) => Emit(Compilation(source));

    /// <summary>Runs a program and returns its output and outcome, recording coverage into a table when one is given.</summary>
    public static async Task<(RunResult Result, string Output)> RunAsync(CompiledAssembly assembly, bool[][]? table = null)
    {
        var console = new CapturingConsole();
        RunResult result;
        if (table is null)
        {
            result = await new ProgramRunner().RunAsync(assembly, [], [], console);
        }
        else
        {
            using (CoverageRecorder.Enter(table))
            {
                result = await new ProgramRunner().RunAsync(assembly, [], [], console);
            }
        }

        return (result, console.Output);
    }

    /// <summary>A hit table with one slot for a module.</summary>
    public static bool[][] TableFor(CoverageModule module)
    {
        var table = new bool[module.Index + 1][];
        table[module.Index] = new bool[module.PointCount];
        return table;
    }

    /// <summary>The lines of the statements a table records as hit.</summary>
    public static HashSet<int> HitLines(CoverageModule module, bool[][] table) =>
        module.Statements.Where(s => table[module.Index][s.Point]).Select(s => s.Line).ToHashSet();

    /// <summary>The one-based line of the first occurrence of a piece of text.</summary>
    public static int LineOf(string source, string text)
    {
        var index = source.IndexOf(text, StringComparison.Ordinal);
        Assert.True(index >= 0, $"'{text}' is not in the source.");
        return source[..index].Count(c => c == '\n') + 1;
    }

    private static CompiledAssembly Emit(CSharpCompilation compilation)
    {
        using var image = new MemoryStream();
        using var symbols = new MemoryStream();
        var emit = compilation.Emit(image, symbols, options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));
        Assert.True(emit.Success, string.Join('\n', emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        return new CompiledAssembly(compilation.AssemblyName!, image.ToArray(), symbols.ToArray());
    }
}
