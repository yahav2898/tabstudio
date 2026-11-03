using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TabStudio.Compilation.Razor;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Compilation.Tests;

/// <summary>The framework symbols of a project built for .NET Standard, as the SDK defines them.</summary>
public sealed class NetStandardSymbolsTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public void A_NET_Standard_project_gets_the_NET_Standard_symbols_and_not_those_of_NET()
    {
        var project = Parse("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>netstandard2.0</TargetFramework></PropertyGroup>
            </Project>
            """);

        var symbols = RazorCompiler.ParseOptionsFor(project, optimize: false).PreprocessorSymbolNames.ToList();

        Assert.Contains("NETSTANDARD", symbols);
        Assert.Contains("NETSTANDARD2_0", symbols);
        Assert.Contains("NETSTANDARD2_0_OR_GREATER", symbols);
        Assert.Contains("NETSTANDARD1_6_OR_GREATER", symbols);
        Assert.DoesNotContain("NETSTANDARD2_1_OR_GREATER", symbols);
        Assert.Contains("DEBUG", symbols);
        Assert.Contains("TRACE", symbols);
        Assert.DoesNotContain("NET", symbols);
        Assert.DoesNotContain("NETCOREAPP", symbols);
        Assert.DoesNotContain("NET10_0_OR_GREATER", symbols);
    }

    [Fact]
    public void A_NET_project_keeps_the_NET_symbols()
    {
        var project = Parse("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework><DefineConstants>$(DefineConstants);FEATURE</DefineConstants></PropertyGroup>
            </Project>
            """);

        var symbols = RazorCompiler.ParseOptionsFor(project, optimize: false).PreprocessorSymbolNames.ToList();

        Assert.Contains("NET", symbols);
        Assert.Contains("NET10_0_OR_GREATER", symbols);
        Assert.Contains("FEATURE", symbols);
        Assert.DoesNotContain("NETSTANDARD", symbols);
    }

    [Fact]
    public void The_polyfills_of_a_NET_Standard_project_are_public_types_another_project_reaches_through_an_alias()
    {
        // Refit's analyzers, for .NET Standard 2.0, define System.Index and System.Range under #if NETSTANDARD2_0;
        // its tooling project references them with Aliases="AnalyzerTooling" and names AnalyzerTooling::System.Index.
        // Compiled with .NET's symbols the polyfills were left out: 7 CS0234.
        var analyzers = Parse("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>netstandard2.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        const string Polyfill = """
            #if NETSTANDARD2_0 || NET462
            namespace System
            {
                public readonly record struct Index(int Value);
            }
            #endif
            namespace System.Runtime.CompilerServices
            {
                internal static class IsExternalInit
                {
                }
            }
            """;
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
        var analyzerCompilation = CSharpCompilation.Create(
            "Refit.Analyzers",
            [CSharpSyntaxTree.ParseText(Polyfill, RazorCompiler.ParseOptionsFor(analyzers, optimize: false))],
            Basic.Reference.Assemblies.Net100.References.All,
            CSharpCompiler.WithProjectWarnings(options, analyzers));
        Assert.Empty(analyzerCompilation.GetDiagnostics().Where(d => d.Severity != DiagnosticSeverity.Hidden));

        const string Tooling = """
            extern alias AnalyzerTooling;

            public static class Uses
            {
                public static AnalyzerTooling::System.Index First() => new(0);
            }
            """;
        var tooling = CSharpCompilation.Create(
            "Refit.Tooling",
            [CSharpSyntaxTree.ParseText(Tooling, CSharpCompiler.CreateParseOptions())],
            [.. Basic.Reference.Assemblies.Net100.References.All, analyzerCompilation.ToMetadataReference(["AnalyzerTooling"])],
            options);
        Assert.Empty(tooling.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    public void Dispose() => _temp.Dispose();

    private TabStudio.Core.Workspaces.ProjectInfo Parse(string xml) => ProjectParser.Parse(_temp.Write("src/Lib/Lib.csproj", xml), xml);
}
