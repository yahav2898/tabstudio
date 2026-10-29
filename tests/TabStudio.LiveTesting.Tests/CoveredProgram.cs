using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.VisualBasic;
using TabStudio.Compilation;
using TabStudio.Compilation.Running;
using TabStudio.Compilation.VisualBasic;
using TabStudio.LiveTesting.Coverage;
using TabStudio.LiveTesting.Instrumentation;
using RoslynCompilation = Microsoft.CodeAnalysis.Compilation;

namespace TabStudio.LiveTesting.Tests;

/// <summary>Compiles a program in memory with and without coverage instrumentation, and runs it.</summary>
internal static class CoveredProgram
{
    /// <summary>The path the program's source is compiled under; stack traces point here.</summary>
    public static readonly string FilePath = Path.Combine(Path.GetTempPath(), "tabstudio-coverage", "Program.cs");

    /// <summary>The path a Visual Basic program's source is compiled under.</summary>
    public static readonly string VisualBasicFilePath = Path.Combine(Path.GetTempPath(), "tabstudio-coverage", "Program.vb");

    public static CSharpCompilation Compilation(string source) =>
        new CSharpCompiler().CreateCompilation(new CompilationRequest
        {
            AssemblyName = "Covered" + Guid.NewGuid().ToString("N")[..8],
            Documents = [new SourceDocument(FilePath, source)],
        });

    /// <summary>
    /// A Visual Basic program or library, compiled as TabStudio compiles a Visual Basic project, with a root namespace.
    /// </summary>
    public static VisualBasicCompilation VisualBasicCompilation(string source, string rootNamespace = "", AssemblyKind kind = AssemblyKind.ConsoleApplication, params SourceDocument[] others)
    {
        var compilation = VisualBasicCompiler.CreateCompilation(new CompilationRequest
        {
            AssemblyName = "Covered" + Guid.NewGuid().ToString("N")[..8],
            Kind = kind,
            Documents = [new SourceDocument(VisualBasicFilePath, source), .. others],
        });
        return compilation.WithOptions(compilation.Options.WithRootNamespace(rootNamespace));
    }

    /// <summary>Instruments and emits a program; the compilation must succeed.</summary>
    public static (CompiledAssembly Assembly, CoverageModule Module, string Text) Instrument(string source) =>
        Instrument(Compilation(source), FilePath);

    /// <summary>Instruments and emits a Visual Basic program; the compilation must succeed.</summary>
    public static (CompiledAssembly Assembly, CoverageModule Module, string Text) InstrumentVisualBasic(string source) =>
        Instrument(VisualBasicCompilation(source), VisualBasicFilePath);

    /// <summary>Emits a program as it is.</summary>
    public static CompiledAssembly Plain(string source) => Emit(Compilation(source));

    /// <summary>Emits a Visual Basic program as it is.</summary>
    public static CompiledAssembly PlainVisualBasic(string source) => Emit(VisualBasicCompilation(source));

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

    private static (CompiledAssembly Assembly, CoverageModule Module, string Text) Instrument(RoslynCompilation compilation, string filePath)
    {
        Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        var (instrumented, module) = CoverageInstrumenter.Instrument(compilation, "/projects/" + compilation.AssemblyName + ".csproj", CancellationToken.None);
        var text = instrumented.SyntaxTrees.First(t => t.FilePath == filePath).ToString();
        return (Emit(instrumented), module, text);
    }

    private static CompiledAssembly Emit(RoslynCompilation compilation)
    {
        using var image = new MemoryStream();
        using var symbols = new MemoryStream();
        var emit = compilation.Emit(image, symbols, options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));
        Assert.True(emit.Success, string.Join('\n', emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        return new CompiledAssembly(compilation.AssemblyName!, image.ToArray(), symbols.ToArray());
    }
}
