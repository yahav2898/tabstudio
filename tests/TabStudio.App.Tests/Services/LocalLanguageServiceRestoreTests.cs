using System.IO.Compression;
using System.Text;
using TabStudio.App.Services;
using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.Packages;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Services;

/// <summary>
/// A project whose restore fails for one package, as real repositories do (a version given by a property TabStudio
/// does not know, a package that is not published): the language service still gives the project every package that
/// was restored, so live errors and builds name the missing package instead of every use of the others.
/// </summary>
public sealed class LocalLanguageServiceRestoreTests : IDisposable
{
    private const string PackageId = "Tablet.Greetings";

    private readonly TempDirectory _temp = new();
    private readonly HttpClient _http = new();

    [Fact]
    public async Task A_restore_that_fails_for_one_package_keeps_the_others_for_live_errors_builds_and_runs()
    {
        WritePackage(_temp.Resolve("feed"));
        var projectPath = _temp.Write("App/App.csproj", $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="{PackageId}" Version="1.0.0" />
                <PackageReference Include="No.Such.Package" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """);
        var programPath = _temp.Write("App/Program.cs", "Console.WriteLine(Tablet.Greetings.Hello.World());");
        var restorer = new PackageRestorer(
            new PackageRestoreOptions
            {
                GlobalPackagesFolder = _temp.Resolve("packages"),
                Sources = [new PackageSource("test", _temp.Resolve("feed"))],
                FrameworkAssemblyNames = ReferenceAssemblyNames.Net100,
            },
            _http);
        using var language = new LocalLanguageService(new RoslynWorkspaceHost(), restorer);
        var workspace = WorkspaceLoader.Load(projectPath);
        language.Reload(workspace, FileSystemTextProvider.Instance);
        var project = Assert.Single(workspace.Projects);

        var restore = await language.RestorePackagesAsync(project, null, CancellationToken.None);

        // The restore's error stays, for the Error List, and is the only one.
        Assert.False(restore.Success);
        Assert.Equal(PackageDiagnosticIds.PackageNotFound, Assert.Single(restore.Diagnostics).Id);
        Assert.Equal(new[] { "No.Such.Package" }, restore.UnrestoredReferences);

        var live = await language.GetDocumentDiagnosticsAsync(programPath, CancellationToken.None);
        Assert.DoesNotContain(live, d => d.Severity == DiagnosticSeverityLevel.Error);

        var build = await language.BuildAsync(project, CancellationToken.None);
        Assert.True(build.Success, string.Join('\n', build.Diagnostics));
        var unrestored = Assert.Single(build.Diagnostics, d => d.Id == ProjectBuilder.UnrestoredPackageId);
        Assert.Contains("'No.Such.Package'", unrestored.Message, StringComparison.Ordinal);
        Assert.Contains(language.GetPackageRuntimeAssemblies(project.FilePath), path => Path.GetFileName(path) == $"{PackageId}.dll");
    }

    public void Dispose()
    {
        _http.Dispose();
        _temp.Dispose();
    }

    private static void WritePackage(string feed)
    {
        const string source = """
            namespace Tablet.Greetings;

            public static class Hello
            {
                public static string World() => "Hello from a package";
            }
            """;
        var nuspec = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>{PackageId}</id>
                <version>1.0.0</version>
                <authors>TabStudio tests</authors>
                <description>A package made by a test.</description>
              </metadata>
            </package>
            """;

        Directory.CreateDirectory(feed);
        using var zip = ZipFile.Open(Path.Combine(feed, $"{PackageId}.1.0.0.nupkg"), ZipArchiveMode.Create);
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
