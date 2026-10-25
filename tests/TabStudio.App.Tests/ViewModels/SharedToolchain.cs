using TabStudio.App.Services;
using TabStudio.Testing;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>
/// The compiler and the NuGet packages folder, shared by the tests of a class as an xUnit class fixture, so packages
/// are downloaded and reference assemblies extracted once.
/// </summary>
public sealed class SharedToolchain : IDisposable
{
    private readonly TempDirectory _temp = new();

    public SharedToolchain()
    {
        Paths = new AppPaths(_temp.Path);
        Compiler = new CompilerServices(Paths);
    }

    public AppPaths Paths { get; }

    public CompilerServices Compiler { get; }

    public void Dispose() => _temp.Dispose();
}
