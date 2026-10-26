using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;

namespace TabStudio.Compilation;

/// <summary>
/// The .NET reference assemblies that user code compiles against.
/// </summary>
/// <remarks>
/// <para>
/// Reference assemblies describe the public API of the base class library without its implementation.
/// They ship inside TabStudio (through <c>Basic.Reference.Assemblies.Net100</c>), so compiling never needs
/// the network or an installed SDK. At run time, compiled code binds to the real libraries that run
/// TabStudio itself, which is why both must be the same .NET version.
/// </para>
/// <para>
/// <see cref="Net100"/> keeps the images on the managed heap, which is simple and right for tests and for
/// devices with plenty of memory. <see cref="LoadMapped"/> writes them to storage once and memory-maps them,
/// so their pages are clean and the operating system can drop and reload them under memory pressure.
/// </para>
/// </remarks>
public static class ReferenceAssemblies
{
    /// <summary>The target framework moniker the references describe.</summary>
    public const string TargetFramework = "net10.0";

    private const string ResourcePrefix = "net100.";

    private static readonly Lazy<IReadOnlyList<MetadataReference>> LazyNet100 =
        new(() => Basic.Reference.Assemblies.Net100.References.All.Cast<MetadataReference>().ToList());

    /// <summary>The .NET 10 reference assemblies, held in memory. Loaded once, on first use.</summary>
    public static IReadOnlyList<MetadataReference> Net100 => LazyNet100.Value;

    /// <summary>
    /// Returns the .NET 10 reference assemblies as memory-mapped files in a cache folder, extracting them
    /// the first time.
    /// </summary>
    /// <param name="cacheDirectory">A folder the app owns. A subfolder per package version is created in it.</param>
    /// <returns>The references. Their files stay open for the lifetime of the process.</returns>
    public static IReadOnlyList<MetadataReference> LoadMapped(string cacheDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(cacheDirectory);

        var directory = ExtractTo(cacheDirectory);
        return Directory.EnumerateFiles(directory, "*.dll")
            .Order(StringComparer.Ordinal)
            .Select(CreateMappedReference)
            .ToList();
    }

    /// <summary>
    /// Writes the reference assemblies to a versioned subfolder, unless they are already there, for tools that read
    /// them as files, such as OpenSilver's XAML compiler.
    /// </summary>
    /// <param name="cacheDirectory">A folder the app owns. A subfolder per package version is created in it.</param>
    /// <returns>The folder that holds the assemblies.</returns>
    public static string ExtractTo(string cacheDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(cacheDirectory);
        var assembly = typeof(Basic.Reference.Assemblies.Net100).Assembly;
        var version = assembly.ManifestModule.ModuleVersionId.ToString("N")[..12];
        var directory = Path.Combine(cacheDirectory, $"{TargetFramework}-{version}");
        var marker = Path.Combine(directory, ".complete");
        if (File.Exists(marker))
        {
            return directory;
        }

        // Extract into a temporary folder and rename it, so an interrupted extraction is never mistaken
        // for a complete one.
        var staging = directory + ".partial-" + Guid.NewGuid().ToString("N")[..8];
        Directory.CreateDirectory(staging);
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)))
        {
            using var source = assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException($"The reference assembly resource '{name}' is missing.");
            using var target = File.Create(Path.Combine(staging, name[ResourcePrefix.Length..] + ".dll"));
            source.CopyTo(target);
        }

        File.WriteAllText(Path.Combine(staging, ".complete"), version);
        try
        {
            Directory.Move(staging, directory);
        }
        catch (IOException) when (File.Exists(marker))
        {
            // Another thread or process finished first; its copy is identical.
            Directory.Delete(staging, recursive: true);
        }

        return directory;
    }

    private static PortableExecutableReference CreateMappedReference(string path)
    {
        // PEStreamOptions.Default reads lazily through a memory map instead of copying the image into memory.
        // The metadata object owns the stream and keeps it open.
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var module = ModuleMetadata.CreateFromStream(stream, PEStreamOptions.Default);
        return AssemblyMetadata.Create(module).GetReference(filePath: path, display: Path.GetFileNameWithoutExtension(path));
    }
}
