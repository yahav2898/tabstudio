using TabStudio.Compilation;
using TabStudio.Packages;
using TabStudio.Testing;

namespace TabStudio.Wpf.Tests;

/// <summary>
/// One packages folder for the tests that restore from the real nuget.org, so OpenSilver, Blazor and .NET for
/// WebAssembly are downloaded once for all of them, and one folder of .NET's reference assemblies as files.
/// </summary>
public sealed class NuGetFixture : IDisposable
{
    private readonly TempDirectory _folder = new();

    public NuGetFixture()
    {
        Restorer = new PackageRestorer(
            new PackageRestoreOptions
            {
                GlobalPackagesFolder = Path.Combine(_folder.Path, "packages"),
                FrameworkAssemblyNames = ReferenceAssemblyNames.Net100,
            },
            Http);
        FrameworkReferenceDirectory = ReferenceAssemblies.ExtractTo(Path.Combine(_folder.Path, "references"));
    }

    public HttpClient Http { get; } = new() { Timeout = TimeSpan.FromMinutes(5) };

    public PackageRestorer Restorer { get; }

    /// <summary>.NET's reference assemblies as files, which OpenSilver's XAML compiler reads.</summary>
    public string FrameworkReferenceDirectory { get; }

    public void Dispose()
    {
        Http.Dispose();
        _folder.Dispose();
    }
}

/// <summary>The tests that restore from nuget.org, one after another over one packages folder.</summary>
[CollectionDefinition(Name)]
public sealed class NuGetTestGroup : ICollectionFixture<NuGetFixture>
{
    public const string Name = "nuget.org";
}
