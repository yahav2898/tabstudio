using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.VisualBasic;
using TabStudio.Compilation;
using TabStudio.Compilation.VisualBasic;
using TabStudio.Core.Diagnostics;

namespace TabStudio.TestRunner.Tests;

/// <summary>Compiles a user's test project in memory, the way TabStudio builds one, and loads it into a session.</summary>
internal static class UserProject
{
    /// <summary>The path the user's source file is compiled under; stack traces and locations point here.</summary>
    public static readonly string FilePath = Path.Combine(Path.GetTempPath(), "tabstudio-user-tests", "UserTests.cs");

    /// <summary>The path a Visual Basic test file is compiled under.</summary>
    public static readonly string VisualBasicFilePath = Path.Combine(Path.GetTempPath(), "tabstudio-user-tests", "UserTests.vb");

    /// <summary>xUnit v2's assemblies, from this test project's own package references.</summary>
    public static IReadOnlyList<string> XunitAssemblies { get; } =
    [
        typeof(FactAttribute).Assembly.Location,
        typeof(Assert).Assembly.Location,
        typeof(global::Xunit.Abstractions.ITestOutputHelper).Assembly.Location,
    ];

    /// <summary>NUnit's assembly.</summary>
    public static IReadOnlyList<string> NUnitAssemblies { get; } =
    [
        typeof(NUnit.Framework.TestAttribute).Assembly.Location,
    ];

    /// <summary>MSTest's assemblies: the framework and its extensions, which define <c>TestContext</c>.</summary>
    public static IReadOnlyList<string> MSTestAssemblies { get; } =
    [
        typeof(Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute).Assembly.Location,
        typeof(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext).Assembly.Location,
    ];

    public static CompilationRequest Request(string source, IReadOnlyList<string> frameworkAssemblies) => new()
    {
        AssemblyName = "UserTests" + Guid.NewGuid().ToString("N")[..8],
        Kind = AssemblyKind.Library,
        Documents = [new SourceDocument(FilePath, source)],
        AdditionalReferences = frameworkAssemblies.Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToList(),
    };

    public static CompiledAssembly Compile(string source, IReadOnlyList<string> frameworkAssemblies)
    {
        var result = new CSharpCompiler().Compile(Request(source, frameworkAssemblies));
        Assert.True(result.Success, string.Join('\n', result.Diagnostics.Where(d => d.Severity == DiagnosticSeverityLevel.Error)));
        return result.Assembly!;
    }

    public static TestSession Load(string source, IReadOnlyList<string> frameworkAssemblies, TestRunOptions? options = null)
    {
        var session = TestSession.Load([Compile(source, frameworkAssemblies)], frameworkAssemblies, options);
        Assert.Empty(session.DiscoveryMessages);
        return session;
    }

    public static async Task<TestRunSummary> RunAllAsync(string source, IReadOnlyList<string> frameworkAssemblies, TestRunOptions? options = null)
    {
        using var session = Load(source, frameworkAssemblies, options);
        return await session.RunAllAsync();
    }

    /// <summary>
    /// The compilation of a Visual Basic test project, as TabStudio builds one, with a root namespace that the compiler
    /// puts in front of every namespace.
    /// </summary>
    public static VisualBasicCompilation CreateVisualBasicCompilation(string source, IReadOnlyList<string> frameworkAssemblies, string rootNamespace)
    {
        var compilation = VisualBasicCompiler.CreateCompilation(Request(source, frameworkAssemblies) with
        {
            Documents = [new SourceDocument(VisualBasicFilePath, source)],
        });
        return compilation.WithOptions(compilation.Options.WithRootNamespace(rootNamespace));
    }

    /// <summary>Compiles a Visual Basic test project in memory and loads it into a session.</summary>
    public static TestSession LoadVisualBasic(string source, IReadOnlyList<string> frameworkAssemblies, string rootNamespace)
    {
        var compilation = CreateVisualBasicCompilation(source, frameworkAssemblies, rootNamespace);
        using var image = new MemoryStream();
        using var symbols = new MemoryStream();
        var emit = compilation.Emit(image, symbols, options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));
        Assert.True(emit.Success, string.Join('\n', emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));

        var session = TestSession.Load([new CompiledAssembly(compilation.AssemblyName!, image.ToArray(), symbols.ToArray())], frameworkAssemblies);
        Assert.Empty(session.DiscoveryMessages);
        return session;
    }

    /// <summary>The result of the one test with this display name.</summary>
    public static TestResult Result(this TestRunSummary summary, string displayName) =>
        Assert.Single(summary.Results, r => r.Test.DisplayName == displayName);

    /// <summary>The one-based line of the first occurrence of a piece of text.</summary>
    public static int LineOf(string source, string text)
    {
        var index = source.IndexOf(text, StringComparison.Ordinal);
        Assert.True(index >= 0, $"'{text}' is not in the source.");
        return source[..index].Count(c => c == '\n') + 1;
    }

    /// <summary>The one-based column of the first occurrence of a piece of text in its line.</summary>
    public static int ColumnOf(string source, string text)
    {
        var index = source.IndexOf(text, StringComparison.Ordinal);
        return index - source.LastIndexOf('\n', Math.Max(index - 1, 0));
    }
}
