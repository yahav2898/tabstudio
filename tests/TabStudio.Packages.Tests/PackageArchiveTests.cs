using System.IO.Compression;
using System.Text;
using TabStudio.Packages.Installation;
using TabStudio.Testing;

namespace TabStudio.Packages.Tests;

public sealed class PackageArchiveTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("lib/../../evil.txt")]
    [InlineData("..%2Fevil.txt")]
    [InlineData("lib\\..\\..\\evil.txt")]
    [InlineData("/tmp/evil.txt")]
    public void Rejects_entries_that_point_outside_the_package_folder(string entryName)
    {
        var package = Zip(("Evil.nuspec", new TestPackage("Evil", "1.0.0").CreateNuspec()), ("lib/net8.0/Fine.dll", "fine"), (entryName, "evil"));
        var target = _temp.Resolve("extract/evil");

        var error = Assert.Throws<InvalidPackageException>(() => PackageArchive.Extract(package, target));

        Assert.Contains("outside", error.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(_temp.Resolve("extract/evil.txt")));
        Assert.False(File.Exists(_temp.Resolve("evil.txt")));
        Assert.False(Directory.Exists(target), "Nothing is written when any entry is unsafe.");
    }

    [Theory]
    [InlineData("../../escape")]
    [InlineData("/data/escape")]
    [InlineData("a\\..\\..\\b")]
    public void Rejects_package_and_dependency_ids_that_are_paths(string id)
    {
        var own = $"<package><metadata><id>{id}</id><version>1.0.0</version></metadata></package>";
        Assert.Throws<InvalidPackageException>(() => Manifests.NuspecReader.Read(new MemoryStream(Encoding.UTF8.GetBytes(own))));
        var dependency = $"<package><metadata><id>Fine</id><version>1.0.0</version><dependencies><dependency id=\"{id}\" version=\"1.0.0\" /></dependencies></metadata></package>";
        Assert.Throws<InvalidPackageException>(() => Manifests.NuspecReader.Read(new MemoryStream(Encoding.UTF8.GetBytes(dependency))));
        Assert.Throws<ArgumentException>(() => new GlobalPackagesFolder(_temp.Resolve("packages")).GetPackageDirectory(id, NuGet.Versioning.NuGetVersion.Parse("1.0.0")));
    }

    [Fact]
    public void Rejects_a_file_that_is_not_a_zip_archive()
    {
        var path = _temp.Write("broken.nupkg", "this is not a zip file");

        var error = Assert.Throws<InvalidPackageException>(() => PackageArchive.Extract(path, _temp.Resolve("out")));

        Assert.Contains("not a zip archive", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_a_package_without_a_manifest()
    {
        var path = Zip(("lib/net8.0/Lib.dll", "x"));

        Assert.Throws<InvalidPackageException>(() => PackageArchive.Extract(path, _temp.Resolve("out")));
    }

    [Fact]
    public void Extracts_files_with_percent_encoded_names_and_skips_packaging_metadata()
    {
        var path = Zip(
            ("Spaced.nuspec", new TestPackage("Spaced", "1.0.0").CreateNuspec()),
            ("content/a%20b.txt", "spaced"),
            ("[Content_Types].xml", "<Types />"),
            ("_rels/.rels", "<Relationships />"),
            ("package/services/metadata/core-properties/x.psmdcp", "x"));
        var target = _temp.Resolve("out");

        var manifest = PackageArchive.Extract(path, target);

        Assert.Equal("Spaced", manifest.Id);
        Assert.Equal("spaced", File.ReadAllText(Path.Combine(target, "content", "a b.txt")));
        Assert.False(File.Exists(Path.Combine(target, "[Content_Types].xml")));
        Assert.False(Directory.Exists(Path.Combine(target, "_rels")));
        Assert.False(Directory.Exists(Path.Combine(target, "package")));
    }

    [Fact]
    public void Reads_the_manifest_without_extracting()
    {
        var path = new TestPackage("Reader", "2.1.0-beta").DependsOn("Other", "[1.0.0, 2.0.0)").SaveTo(_temp.Resolve("feed"));

        var manifest = PackageArchive.ReadManifest(path);

        Assert.Equal("Reader", manifest.Id);
        Assert.Equal("2.1.0-beta", manifest.Version.ToNormalizedString());
        var dependency = Assert.Single(Assert.Single(manifest.DependencyGroups).Dependencies);
        Assert.Equal("Other", dependency.Id);
        Assert.Equal("[1.0.0, 2.0.0)", dependency.Range.ToNormalizedString());
    }

    public void Dispose() => _temp.Dispose();

    private string Zip(params (string Name, string Content)[] entries)
    {
        var path = _temp.Resolve($"packages/{Guid.NewGuid():N}.nupkg");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            foreach (var (name, content) in entries)
            {
                using var stream = zip.CreateEntry(name).Open();
                stream.Write(Encoding.UTF8.GetBytes(content));
            }
        }

        return path;
    }
}
