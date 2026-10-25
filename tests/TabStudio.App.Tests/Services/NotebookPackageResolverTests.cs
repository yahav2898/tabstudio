using System.IO.Compression;
using System.Text;
using Avalonia.Headless.XUnit;
using TabStudio.App.Services;
using TabStudio.Compilation;
using TabStudio.Notebooks.Execution;
using TabStudio.Notebooks.Outputs;
using TabStudio.Packages;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Services;

/// <summary>
/// <c>#r "nuget: ..."</c> in notebook cells, restored from a local feed. The tests run on the UI thread, one at a
/// time, because cells use the process-wide console like programs started with F5.
/// </summary>
public sealed class NotebookPackageResolverTests : IDisposable
{
    private const string PackageId = "Tablet.Greetings";

    private readonly TempDirectory _temp = new();
    private readonly HttpClient _http = new();
    private readonly NotebookPackageResolver _resolver;

    public NotebookPackageResolverTests()
    {
        var feed = _temp.Resolve("feed");
        WritePackage(feed, "1.2.0", "Hello from a package");
        WritePackage(feed, "2.0.0", "Hello from version 2");
        var restorer = new PackageRestorer(
            new PackageRestoreOptions
            {
                GlobalPackagesFolder = _temp.Resolve("packages"),
                Sources = [new PackageSource("test", feed)],
                FrameworkAssemblyNames = ReferenceAssemblyNames.Net100,
            },
            _http);
        _resolver = new NotebookPackageResolver(restorer, _temp.Resolve("notebook"), _temp.Path);
    }

    [AvaloniaFact]
    public async Task A_nuget_reference_restores_the_package_for_the_cells_that_follow()
    {
        using var kernel = new CSharpKernel(new CSharpKernelOptions { ReferenceResolver = _resolver });

        var first = await kernel.ExecuteAsync($"#r \"nuget: {PackageId}, 1.2.0\"\nTablet.Greetings.Hello.World()");
        var again = await kernel.ExecuteAsync($"#r \"nuget: {PackageId}, 1.2.0\"\nTablet.Greetings.Hello.World()");
        var later = await kernel.ExecuteAsync("Tablet.Greetings.Hello.World().Length");

        Assert.Equal("Hello from a package", ResultOf(first));
        Assert.Equal("Hello from a package", ResultOf(again));
        Assert.Equal("20", ResultOf(later));
    }

    [AvaloniaFact]
    public async Task Another_version_needs_a_restart_and_then_loads()
    {
        using var kernel = new CSharpKernel(new CSharpKernelOptions { ReferenceResolver = _resolver });
        await kernel.ExecuteAsync($"#r \"nuget: {PackageId}, 1.2.0\"");

        var refused = await kernel.ExecuteAsync($"#r \"nuget: {PackageId}, 2.0.0\"");
        await kernel.RestartAsync();
        _resolver.ForgetReferences();
        var restarted = await kernel.ExecuteAsync($"#r \"nuget: {PackageId}, 2.0.0\"\nTablet.Greetings.Hello.World()");

        Assert.Equal(CellExecutionStatus.CompilationFailed, refused.Status);
        Assert.Contains(refused.Diagnostics, d => d.Message.Contains("Restart the kernel", StringComparison.Ordinal));
        Assert.Equal("Hello from version 2", ResultOf(restarted));
    }

    [AvaloniaFact]
    public async Task A_package_that_cannot_be_found_is_an_error_on_the_line()
    {
        using var kernel = new CSharpKernel(new CSharpKernelOptions { ReferenceResolver = _resolver });

        var result = await kernel.ExecuteAsync("#r \"nuget: No.Such.Package, 1.0.0\"");

        Assert.Equal(CellExecutionStatus.CompilationFailed, result.Status);
        var error = Assert.Single(result.Diagnostics, d => d.Id == NotebookDiagnosticIds.ReferenceNotResolved);
        Assert.Contains("No.Such.Package", error.Message, StringComparison.Ordinal);
        Assert.Equal(1, error.Location?.Line);
    }

    [AvaloniaFact]
    public async Task An_assembly_file_can_be_referenced_by_a_relative_path()
    {
        await File.WriteAllBytesAsync(_temp.Resolve("Local.dll"), Compile("Local", "namespace Local; public static class Answer { public static int Value => 42; }"));
        using var kernel = new CSharpKernel(new CSharpKernelOptions { ReferenceResolver = _resolver });

        var result = await kernel.ExecuteAsync("#r \"Local.dll\"\nLocal.Answer.Value");

        Assert.Equal("42", ResultOf(result));
    }

    public void Dispose()
    {
        _http.Dispose();
        _temp.Dispose();
    }

    private static string? ResultOf(CellExecutionResult result)
    {
        Assert.True(result.Succeeded, $"{result.Status}: " + string.Join(" | ", result.Diagnostics.Select(d => d.ToString())
            .Concat(result.Outputs.OfType<ErrorOutput>().SelectMany(e => e.Traceback))));
        return result.Outputs.OfType<DisplayOutput>().SingleOrDefault(o => o.IsResult)?.Data.PlainText;
    }

    private static void WritePackage(string feed, string version, string greeting)
    {
        var source = $$"""
            namespace Tablet.Greetings;

            public static class Hello
            {
                public static string World() => "{{greeting}}";
            }
            """;
        var nuspec = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>{PackageId}</id>
                <version>{version}</version>
                <authors>TabStudio tests</authors>
                <description>A package made by a test.</description>
              </metadata>
            </package>
            """;

        Directory.CreateDirectory(feed);
        using var zip = ZipFile.Open(Path.Combine(feed, $"{PackageId}.{version}.nupkg"), ZipArchiveMode.Create);
        Add(zip, $"{PackageId}.nuspec", Encoding.UTF8.GetBytes(nuspec));
        Add(zip, $"lib/net10.0/{PackageId}.dll", Compile(PackageId, source));
    }

    private static byte[] Compile(string assemblyName, string source)
    {
        var result = new CSharpCompiler().Compile(new CompilationRequest
        {
            AssemblyName = assemblyName,
            Kind = AssemblyKind.Library,
            Documents = [new SourceDocument($"{assemblyName}.cs", source)],
        });
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        return result.Assembly!.Image;
    }

    private static void Add(ZipArchive zip, string name, byte[] content)
    {
        using var stream = zip.CreateEntry(name).Open();
        stream.Write(content);
    }
}
