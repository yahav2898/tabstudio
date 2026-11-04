using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Compilation.Tests;

/// <summary>The C# version a project compiles with when it asks for the latest of an SDK newer than TabStudio's Roslyn.</summary>
public sealed class LanguageVersionTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public void A_repository_for_a_newer_NET_gets_the_newest_C_sharp_its_SDK_calls_latest()
    {
        // Refit targets .NET 11 and its generator project asks for the latest C#: the .NET 11 SDK's is C# 15, whose
        // collection expression arguments ([with(StringComparer.Ordinal)]) this Roslyn has as a preview. Compiled at its
        // own latest, the 25 'with' calls were CS0103.
        var project = Parse("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>netstandard2.0</TargetFramework>
                <LangVersion>latest</LangVersion>
                <ModernTargets>net8.0;net9.0;net10.0;net11.0</ModernTargets>
              </PropertyGroup>
            </Project>
            """);

        var version = CSharpCompiler.LanguageVersionOf(project);

        Assert.Equal(ExpectedForNet11, version);
        const string Code = """
            using System;
            using System.Collections.Generic;

            public static class Names
            {
                public static HashSet<string> Create() => [with(StringComparer.OrdinalIgnoreCase), "a", "A"];
            }
            """;
        var compilation = CSharpCompilation.Create(
            "Names",
            [CSharpSyntaxTree.ParseText(Code, CSharpCompiler.CreateParseOptions(languageVersion: version))],
            Basic.Reference.Assemblies.Net100.References.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Theory]
    [InlineData("net10.0", null)]
    [InlineData("net9.0", null)]
    [InlineData("net10.0", "10.0.100")]
    public void A_repository_for_this_NET_keeps_this_Roslyn_s_latest(string targetFramework, string? sdk)
    {
        if (sdk is not null)
        {
            _temp.Write("global.json", $$"""{ "sdk": { "version": "{{sdk}}" } }""");
        }

        var project = Parse($"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>{targetFramework}</TargetFramework><LangVersion>latest</LangVersion></PropertyGroup>
            </Project>
            """);

        Assert.False(CSharpCompiler.NeedsNewerCompiler(project));
        Assert.Equal(LanguageVersion.Latest, CSharpCompiler.LanguageVersionOf(project));
    }

    [Fact]
    public void A_global_json_that_pins_a_newer_SDK_counts_too()
    {
        _temp.Write("global.json", """{ "sdk": { "version": "11.0.100", "rollForward": "latestFeature" } }""");
        var project = Parse("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework><LangVersion>latest</LangVersion></PropertyGroup>
            </Project>
            """);

        Assert.Equal(ExpectedForNet11, CSharpCompiler.LanguageVersionOf(project));
    }

    /// <summary>What "latest" is for the .NET 11 SDK here: this Roslyn's preview while its latest is older than C# 15.</summary>
    private static LanguageVersion ExpectedForNet11 =>
        (int)LanguageVersionFacts.MapSpecifiedToEffectiveVersion(LanguageVersion.Latest) >= 1500 ? LanguageVersion.Latest : LanguageVersion.Preview;

    public void Dispose() => _temp.Dispose();

    private TabStudio.Core.Workspaces.ProjectInfo Parse(string xml) => ProjectParser.Parse(_temp.Write("src/App/App.csproj", xml), xml);
}
