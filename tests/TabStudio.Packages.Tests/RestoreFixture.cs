using TabStudio.Compilation;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Packages.Tests;

/// <summary>A scratch folder with a local package feed, a global packages folder and projects to restore.</summary>
internal sealed class RestoreFixture : IDisposable
{
    private readonly TempDirectory _temp = new();

    /// <summary>A client for tests that only use local feeds; it is never asked for anything.</summary>
    public static HttpClient UnusedHttp { get; } = new();

    public string FeedDirectory => _temp.Resolve("feed");

    public string PackagesDirectory => _temp.Resolve("packages");

    public PackageSource Feed => new("test feed", FeedDirectory);

    public RestoreFixture Add(params TestPackage[] packages)
    {
        foreach (var package in packages)
        {
            package.SaveTo(FeedDirectory);
        }

        return this;
    }

    public PackageRestoreOptions Options(params PackageSource[] sources) => new()
    {
        GlobalPackagesFolder = PackagesDirectory,
        Sources = sources.Length == 0 ? [Feed] : sources,
        FrameworkAssemblyNames = ReferenceAssemblyNames.Net100,
    };

    public PackageRestorer CreateRestorer(PackageRestoreOptions? options = null, HttpClient? http = null) =>
        new(options ?? Options(), http ?? UnusedHttp);

    /// <summary>Writes a console project named App with the given package references and parses it.</summary>
    public ProjectInfo Project(params (string Id, string? Version)[] references) => Project("App", references);

    public ProjectInfo Project(string name, params (string Id, string? Version)[] references)
    {
        var items = string.Join('\n', references.Select(r => r.Version is null
            ? $"<PackageReference Include=\"{r.Id}\" />"
            : $"<PackageReference Include=\"{r.Id}\" Version=\"{r.Version}\" />"));
        var path = _temp.Write($"{name}/{name}.csproj", $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                {items}
              </ItemGroup>
            </Project>
            """);
        return ProjectParser.Load(path);
    }

    public Task<RestoreResult> RestoreAsync(params (string Id, string? Version)[] references) =>
        CreateRestorer().RestoreAsync(Project(references));

    public string Write(string relativePath, string content) => _temp.Write(relativePath, content);

    public string Resolve(string relativePath) => _temp.Resolve(relativePath);

    public void Dispose() => _temp.Dispose();
}
