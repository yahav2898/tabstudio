using TabStudio.Compilation;
using TabStudio.Packages;
using TabStudio.Testing;

namespace TabStudio.Web.Tests.Apps;

/// <summary>
/// One packages folder for the tests that restore from the real nuget.org, so ASP.NET Core, Blazor's scripts and .NET
/// for WebAssembly are downloaded once for all of them.
/// </summary>
public sealed class NuGetFixture : IDisposable
{
    private readonly TempDirectory _packages = new();

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

    public HttpClient Http { get; } = new() { Timeout = TimeSpan.FromMinutes(5) };

    public PackageRestorer Restorer { get; }

    public void Dispose()
    {
        Http.Dispose();
        _packages.Dispose();
    }
}

/// <summary>The tests that restore from nuget.org, one after another over one packages folder.</summary>
[CollectionDefinition(Name)]
public sealed class NuGetCollection : ICollectionFixture<NuGetFixture>
{
    public const string Name = "nuget.org";
}
