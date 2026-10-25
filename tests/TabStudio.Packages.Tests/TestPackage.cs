using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using TabStudio.Compilation;

namespace TabStudio.Packages.Tests;

/// <summary>Builds small, real <c>.nupkg</c> files for tests: a nuspec, dependency groups and compiled assemblies.</summary>
internal sealed class TestPackage
{
    private static readonly XNamespace Nuspec = "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd";
    private static readonly ConcurrentDictionary<string, byte[]> CompiledAssemblies = new(StringComparer.Ordinal);

    private readonly List<(string? Framework, (string Id, string Range)[] Dependencies)> _groups = [];
    private readonly List<(string Id, string Range)> _ungrouped = [];
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

    public TestPackage(string id, string version)
    {
        Id = id;
        Version = version;
    }

    public string Id { get; }

    public string Version { get; }

    /// <summary>A dependency outside any group, which applies to every framework.</summary>
    public TestPackage DependsOn(string id, string range)
    {
        _ungrouped.Add((id, range));
        return this;
    }

    /// <summary>A dependency group; <paramref name="framework"/> is a nuspec framework such as <c>net8.0</c> or <c>.NETStandard2.0</c>.</summary>
    public TestPackage Group(string? framework, params (string Id, string Range)[] dependencies)
    {
        _groups.Add((framework, dependencies));
        return this;
    }

    public TestPackage WithFile(string path, string content = "") => WithFile(path, Encoding.UTF8.GetBytes(content));

    public TestPackage WithFile(string path, byte[] content)
    {
        _files[path] = content;
        return this;
    }

    /// <summary>Adds a compiled assembly named <paramref name="assemblyName"/> (the package id by default) to a folder such as <c>lib/net8.0</c>.</summary>
    public TestPackage WithAssembly(string folder, string? assemblyName = null)
    {
        var name = assemblyName ?? Id;
        var source = $$"""
            namespace {{name}};

            public static class Marker
            {
                public static string Describe() => "{{Id}} {{Version}} from {{folder}}";
            }
            """;
        return WithFile($"{folder}/{name}.dll", CompileLibrary(name, source));
    }

    /// <summary>Compiles a library with TabStudio's own compiler.</summary>
    public static byte[] CompileLibrary(string assemblyName, string source) =>
        CompiledAssemblies.GetOrAdd($"{assemblyName}\n{source}", _ =>
        {
            var result = new CSharpCompiler().Compile(new CompilationRequest
            {
                AssemblyName = assemblyName,
                Kind = AssemblyKind.Library,
                Documents = [new SourceDocument($"{assemblyName}.cs", source)],
            });
            Assert.True(result.Success, string.Join('\n', result.Diagnostics));
            return result.Assembly!.Image;
        });

    public string CreateNuspec()
    {
        var metadata = new XElement(Nuspec + "metadata",
            new XElement(Nuspec + "id", Id),
            new XElement(Nuspec + "version", Version),
            new XElement(Nuspec + "authors", "TabStudio tests"),
            new XElement(Nuspec + "description", "A package made by a test."));

        if (_groups.Count > 0 || _ungrouped.Count > 0)
        {
            var dependencies = new XElement(Nuspec + "dependencies");
            foreach (var (framework, groupDependencies) in _groups)
            {
                var group = new XElement(Nuspec + "group", groupDependencies.Select(Dependency));
                if (framework is not null)
                {
                    group.SetAttributeValue("targetFramework", framework);
                }

                dependencies.Add(group);
            }

            dependencies.Add(_ungrouped.Select(Dependency));
            metadata.Add(dependencies);
        }

        return new XDocument(new XElement(Nuspec + "package", metadata)).ToString();
    }

    public byte[] ToNupkg()
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(zip, $"{Id}.nuspec", Encoding.UTF8.GetBytes(CreateNuspec()));
            AddEntry(zip, "[Content_Types].xml", Encoding.UTF8.GetBytes("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\" />"));
            foreach (var (path, content) in _files)
            {
                AddEntry(zip, path, content);
            }
        }

        return buffer.ToArray();
    }

    /// <summary>Writes the package into a folder feed as <c>{id}.{version}.nupkg</c>.</summary>
    public string SaveTo(string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{Id}.{Version}.nupkg");
        File.WriteAllBytes(path, ToNupkg());
        return path;
    }

    private static XElement Dependency((string Id, string Range) dependency) =>
        new(Nuspec + "dependency", new XAttribute("id", dependency.Id), new XAttribute("version", dependency.Range));

    private static void AddEntry(ZipArchive zip, string name, byte[] content)
    {
        using var stream = zip.CreateEntry(name).Open();
        stream.Write(content);
    }
}
