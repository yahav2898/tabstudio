using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TabStudio.Compilation;
using TabStudio.Compilation.Running;
using TabStudio.Debugging.Building;

namespace TabStudio.Debugging.Tests;

/// <summary>
/// Calls that a generated interceptor replaces, as TabStudio's XAML generator replaces <c>AvaloniaXamlLoader.Load(this)</c>,
/// are still replaced in the debug build, whose rewrite moves them.
/// </summary>
public sealed class InterceptorTests
{
    private const string Program = """
        var name = "Ada";
        Console.WriteLine(Greeter.Greet(name));

        static class Greeter
        {
            public static string Greet(string name) => "Hello, " + name;
        }
        """;

    [Fact]
    public async Task A_call_a_generated_interceptor_replaces_is_still_replaced_in_the_debug_build()
    {
        var compilation = new CSharpCompiler().CreateCompilation(new CompilationRequest
        {
            AssemblyName = "Intercepted" + Guid.NewGuid().ToString("N")[..8],
            Documents = [new SourceDocument(DebuggedProgram.FilePath, Program)],
        });
        var tree = compilation.SyntaxTrees.First(t => t.FilePath == DebuggedProgram.FilePath);
        var call = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().First(i => i.ToString().StartsWith("Greeter.Greet", StringComparison.Ordinal));
        var location = compilation.GetSemanticModel(tree).GetInterceptableLocation(call) ?? throw new InvalidOperationException("The call cannot be intercepted.");
        var interceptor = $$"""
            namespace {{TabStudio.Compilation.Xaml.AvaloniaXamlGenerator.GeneratedNamespace}}
            {
                file static class Interceptors
                {
                    {{location.GetInterceptsLocationAttributeSyntax()}}
                    public static string Greet(string name) => "Intercepted, " + name;
                }
            }

            namespace System.Runtime.CompilerServices
            {
                [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]
                file sealed class InterceptsLocationAttribute : global::System.Attribute
                {
                    public InterceptsLocationAttribute(int version, string data)
                    {
                    }
                }
            }
            """;
        compilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(interceptor, (CSharpParseOptions)tree.Options, "Interceptors.g.cs"));
        Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

        var build = DebugBuilder.Build(compilation);

        Assert.True(build.Success, string.Join('\n', build.Result.Diagnostics));
        var console = new CapturingConsole();
        await new ProgramRunner().RunAsync(build.Result.EntryAssembly!, [], [], console);
        Assert.Equal("Intercepted, Ada\n", console.Output.ReplaceLineEndings("\n"));

        // Under the debugger, a breakpoint on the intercepted call's line still stops there.
        using var debugged = new DebuggedProgram(build, 2);
        debugged.Start();
        var pause = await debugged.NextPauseAsync();
        Assert.Equal(2, pause.Location?.Line);
        Assert.Equal("\"Ada\"", DebuggedProgram.Local(pause, "name").Value);
        debugged.Session.Continue();
        await debugged.FinishAsync();
        Assert.Contains("Intercepted, Ada", debugged.Console.Output, StringComparison.Ordinal);
    }
}
