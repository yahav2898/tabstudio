using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TabStudio.Compilation;
using TabStudio.Testing;
using TabStudio.Uno.Hosting;
using TabStudio.Uno.Templates;

namespace TabStudio.Uno.Tests;

/// <summary>
/// TabStudio's Uno Platform host, compiled on the device against the Uno assemblies an app restored, through Uno's
/// internal hosting API.
/// </summary>
[Collection(NuGetTestGroup.Name)]
public sealed class UnoHostCompilerTests(NuGetFixture nuget) : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public void The_pinned_Roslyn_still_binds_without_accessibility_checks()
    {
        var library = CSharpCompilation.Create(
            "Hidden",
            [CSharpSyntaxTree.ParseText("namespace Hidden; internal static class Secret { internal static int Value => 42; }")],
            ReferenceAssemblies.Net100,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var image = new MemoryStream();
        Assert.True(library.Emit(image).Success);

        var options = UnoHostCompiler.WithoutAccessibilityChecks(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithMetadataImportOptions(MetadataImportOptions.All));
        var host = CSharpCompilation.Create(
            "Host",
            [CSharpSyntaxTree.ParseText("public static class Reader { public static int Read() => Hidden.Secret.Value; }")],
            [.. ReferenceAssemblies.Net100, MetadataReference.CreateFromImage(image.ToArray())],
            options);

        Assert.Empty(host.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public async Task The_host_compiles_against_the_restored_Uno_Platform_once_per_version()
    {
        using var app = await UnoAppWorkspace.CreateAsync(nuget, _temp, UnoTemplates.UnoAppId, "HelloUno");
        var uno = app.Assets.Assemblies.Where(path => Path.GetFileName(path).StartsWith("Uno", StringComparison.Ordinal)).ToList();
        Assert.Contains(uno, path => path.Replace('\\', '/').Contains("/uno-runtime/net10.0/skia/Uno.UI.dll", StringComparison.Ordinal));

        var output = _temp.Resolve("host");
        var path = UnoHostCompiler.Compile(uno, output);
        var written = File.GetLastWriteTimeUtc(path);
        var again = UnoHostCompiler.Compile(uno, output);

        Assert.Equal(Path.Combine(output, UnoHostCompiler.AssemblyName + ".dll"), path);
        Assert.Equal(path, again);
        Assert.Equal(written, File.GetLastWriteTimeUtc(again));
        Assert.Contains(UnoHostCompiler.Sources, source => source.Name == "PreviewHost.cs");
        Assert.Contains(UnoHostCompiler.Sources, source => source.Name == "PreviewIcu.cs");
    }

    [Fact]
    public void Uno_SkiaSharp_HarfBuzz_and_the_host_are_shared_and_an_apps_own_libraries_are_not()
    {
        Assert.True(UnoRuntime.IsShared("Uno.UI"));
        Assert.True(UnoRuntime.IsShared("Uno"));
        Assert.True(UnoRuntime.IsShared("SkiaSharp"));
        Assert.True(UnoRuntime.IsShared("HarfBuzzSharp"));
        Assert.True(UnoRuntime.IsShared(UnoHostCompiler.AssemblyName));
        Assert.False(UnoRuntime.IsShared("HelloUno"));
        Assert.False(UnoRuntime.IsShared("CommunityToolkit.Mvvm"));
        Assert.False(UnoRuntime.IsShared("UnoApp"));
    }

    public void Dispose() => _temp.Dispose();
}
