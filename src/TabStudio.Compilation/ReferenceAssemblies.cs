using Microsoft.CodeAnalysis;

namespace TabStudio.Compilation;

/// <summary>
/// The .NET reference assemblies that user code compiles against.
/// </summary>
/// <remarks>
/// Reference assemblies describe the public API of the base class library without its implementation.
/// They ship inside TabStudio (through <c>Basic.Reference.Assemblies.Net100</c>), so compiling never needs
/// the network or an installed SDK. At run time, compiled code binds to the real libraries that run
/// TabStudio itself, which is why both must be the same .NET version.
/// </remarks>
public static class ReferenceAssemblies
{
    private static readonly Lazy<IReadOnlyList<MetadataReference>> LazyNet100 =
        new(() => Basic.Reference.Assemblies.Net100.References.All.Cast<MetadataReference>().ToList());

    /// <summary>The .NET 10 reference assemblies. Loaded once, on first use.</summary>
    public static IReadOnlyList<MetadataReference> Net100 => LazyNet100.Value;

    /// <summary>The target framework moniker the references describe.</summary>
    public const string TargetFramework = "net10.0";
}
