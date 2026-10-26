using TabStudio.Compilation;
using TabStudio.Packages;
using TabStudio.Testing;

namespace TabStudio.Uno.Tests;

/// <summary>
/// One packages folder for the tests that restore from the real nuget.org, so Uno Platform is downloaded once for all
/// of them, and one cache folder for the Uno Platform runtime, which runs once per process as in the IDE.
/// </summary>
public sealed class NuGetFixture : IDisposable
{
    private readonly TempDirectory _packages = new();
    private readonly TempDirectory _cache = new();

    public NuGetFixture()
    {
        Restorer = new PackageRestorer(
            new PackageRestoreOptions
            {
                GlobalPackagesFolder = _packages.Path,
                FrameworkAssemblyNames = ReferenceAssemblyNames.Net100,
            },
            Http);
    }

    public HttpClient Http { get; } = new() { Timeout = TimeSpan.FromMinutes(10) };

    public PackageRestorer Restorer { get; }

    /// <summary>Where the Uno Platform runtime keeps its compiled host and the running app's files.</summary>
    public string CacheDirectory => _cache.Path;

    public void Dispose()
    {
        Http.Dispose();
        _packages.Dispose();
        _cache.Dispose();
    }
}

/// <summary>The tests that restore from nuget.org and run Uno Platform, one after another.</summary>
[CollectionDefinition(Name)]
public sealed class NuGetTestGroup : ICollectionFixture<NuGetFixture>
{
    public const string Name = "nuget.org";
}
