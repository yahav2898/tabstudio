using TabStudio.Extensions.Installation;
using TabStudio.Extensions.Manifests;
using TabStudio.Extensions.Packages;
using TabStudio.Testing;
using TabStudio.Testing.Extensions;

namespace TabStudio.Extensions.Tests;

public sealed class ExtensionStoreTests : IDisposable
{
    private static readonly ExtensionIdentifier Lumen = new(LumenExtension.Publisher, LumenExtension.Name);
    private readonly TempDirectory _temp = new();

    [Fact]
    public async Task Installing_extracts_the_extension_and_records_it()
    {
        using var store = CreateStore();
        var vsix = LumenExtension.Builder().WriteTo(_temp.Path);

        var installed = await store.InstallAsync(vsix, new ExtensionSource(ExtensionOrigin.File, FileName: "lumen.vsix"));

        Assert.Equal(Lumen, installed.Id);
        Assert.True(installed.IsEnabled);
        Assert.True(File.Exists(Path.Combine(installed.Folder, "package.json")));
        Assert.True(File.Exists(Path.Combine(installed.Folder, "syntaxes", "lumen.tmLanguage.json")));
        Assert.False(File.Exists(Path.Combine(installed.Folder, "extension.vsixmanifest")));
        Assert.Equal("tabstudio-tests.lumen-1.0.0", Path.GetFileName(installed.Folder));
        Assert.Same(installed, Assert.Single(store.Installed));

        using var reloaded = CreateStore();
        reloaded.Load();
        var again = Assert.Single(reloaded.Installed);
        Assert.Equal((Lumen, "1.0.0", ExtensionOrigin.File, "lumen.vsix"), (again.Id, again.Version, again.Source.Origin, again.Source.FileName));
    }

    [Fact]
    public async Task Updating_replaces_the_old_version_and_keeps_it_disabled()
    {
        using var store = CreateStore();
        await store.InstallAsync(LumenExtension.Builder("1.0.0").WriteTo(_temp.Path, "old.vsix"), new ExtensionSource(ExtensionOrigin.File));
        var oldFolder = store.Installed[0].Folder;
        store.SetEnabled(Lumen, enabled: false);

        var updated = await store.InstallAsync(LumenExtension.Builder("1.1.0").WriteTo(_temp.Path, "new.vsix"), new ExtensionSource(ExtensionOrigin.Registry, new Uri("https://open-vsx.org/")));

        Assert.Equal("1.1.0", updated.Version);
        Assert.False(updated.IsEnabled);
        Assert.False(Directory.Exists(oldFolder));
        Assert.Equal("1.1.0", Assert.Single(store.Installed).Version);
        Assert.Equal(new Uri("https://open-vsx.org/"), store.Installed[0].Source.Registry);
    }

    [Fact]
    public async Task Disabling_enabling_and_uninstalling_are_remembered()
    {
        using var store = CreateStore();
        await store.InstallAsync(LumenExtension.Builder().WriteTo(_temp.Path), new ExtensionSource(ExtensionOrigin.File));
        var changes = 0;
        store.Changed += (_, _) => changes++;

        Assert.False(store.SetEnabled(Lumen, false)!.IsEnabled);
        using (var reloaded = CreateStore())
        {
            reloaded.Load();
            Assert.False(Assert.Single(reloaded.Installed).IsEnabled);
        }

        Assert.True(store.SetEnabled(Lumen, true)!.IsEnabled);
        var folder = store.Installed[0].Folder;
        Assert.True(store.Uninstall(Lumen));

        Assert.Empty(store.Installed);
        Assert.False(Directory.Exists(folder));
        Assert.False(store.Uninstall(Lumen));
        Assert.Null(store.SetEnabled(Lumen, true));
        Assert.Equal(3, changes);
        using var empty = CreateStore();
        empty.Load();
        Assert.Empty(empty.Installed);
    }

    [Fact]
    public async Task Installing_from_a_stream_that_cannot_seek_works()
    {
        using var store = CreateStore();
        await using var stream = new ForwardOnlyStream(LumenExtension.Build());

        var installed = await store.InstallAsync(stream, new ExtensionSource(ExtensionOrigin.File));

        Assert.Equal(Lumen, installed.Id);
        Assert.Empty(Directory.GetFiles(Path.Combine(store.Folder, ".downloads")));
    }

    [Fact]
    public async Task A_file_that_is_not_an_extension_is_refused_and_changes_nothing()
    {
        using var store = CreateStore();
        var notZip = _temp.Write("not.vsix", "hello");
        var noManifest = new VsixBuilder().Add("README.md", "# nothing").WriteTo(_temp.Path, "empty.vsix");

        var notZipError = await Assert.ThrowsAsync<ExtensionPackageException>(() => store.InstallAsync(notZip, new ExtensionSource(ExtensionOrigin.File)));
        var noManifestError = await Assert.ThrowsAsync<ExtensionPackageException>(() => store.InstallAsync(noManifest, new ExtensionSource(ExtensionOrigin.File)));

        Assert.Contains("not a zip archive", notZipError.Message, StringComparison.Ordinal);
        Assert.Contains("package.json", noManifestError.Message, StringComparison.Ordinal);
        Assert.Empty(store.Installed);
        Assert.Empty(Directory.GetDirectories(store.Folder).Where(d => !Path.GetFileName(d).StartsWith('.')));
    }

    [Fact]
    public async Task A_package_that_writes_outside_its_folder_is_refused()
    {
        using var store = CreateStore();
        var evil = LumenExtension.Builder().Add("../../escaped.txt", "gotcha").WriteTo(_temp.Path, "evil.vsix");

        await Assert.ThrowsAsync<ExtensionPackageException>(() => store.InstallAsync(evil, new ExtensionSource(ExtensionOrigin.File)));

        Assert.Empty(store.Installed);
        Assert.False(File.Exists(Path.Combine(_temp.Path, "escaped.txt")));
        Assert.False(File.Exists(Path.Combine(store.Folder, "escaped.txt")));
    }

    [Fact]
    public async Task Files_outside_the_extension_folder_of_the_archive_are_not_extracted()
    {
        using var store = CreateStore();
        var vsix = LumenExtension.Builder().WriteTo(_temp.Path);
        using (var package = VsixPackage.Open(vsix))
        {
            Assert.Contains("package.json", package.Files);
            Assert.DoesNotContain("extension.vsixmanifest", package.Files);
            Assert.Equal(LumenExtension.Readme, package.ReadText("./README.md")!.ReplaceLineEndings("\n"));
        }

        var installed = await store.InstallAsync(vsix, new ExtensionSource(ExtensionOrigin.File));

        Assert.False(File.Exists(Path.Combine(installed.Folder, "[Content_Types].xml")));
    }

    [Fact]
    public async Task Leftovers_of_an_interrupted_install_are_removed_when_the_folder_is_loaded()
    {
        using (var store = CreateStore())
        {
            await store.InstallAsync(LumenExtension.Builder().WriteTo(_temp.Path), new ExtensionSource(ExtensionOrigin.File));
            Directory.CreateDirectory(Path.Combine(store.Folder, ".installing-abc", "syntaxes"));
            Directory.CreateDirectory(Path.Combine(store.Folder, "orphan-1.0.0"));
        }

        using var reloaded = CreateStore();
        reloaded.Load();

        Assert.Single(reloaded.Installed);
        Assert.False(Directory.Exists(Path.Combine(reloaded.Folder, ".installing-abc")));
        Assert.False(Directory.Exists(Path.Combine(reloaded.Folder, "orphan-1.0.0")));
    }

    [Fact]
    public async Task A_damaged_list_is_rebuilt_from_the_extension_folders()
    {
        using (var store = CreateStore())
        {
            await store.InstallAsync(LumenExtension.Builder().WriteTo(_temp.Path), new ExtensionSource(ExtensionOrigin.File));
            File.WriteAllText(Path.Combine(store.Folder, "extensions.json"), "{ damaged");
        }

        using var reloaded = CreateStore();
        reloaded.Load();

        var recovered = Assert.Single(reloaded.Installed);
        Assert.Equal(Lumen, recovered.Id);
        Assert.True(recovered.IsEnabled);
    }

    [Fact]
    public void Relative_paths_cannot_leave_the_extension_folder()
    {
        var folder = _temp.Resolve("ext");

        Assert.Equal(Path.Combine(folder, "syntaxes", "a.json"), InstalledExtension.ResolveInFolder(folder, "./syntaxes/a.json"));
        Assert.Null(InstalledExtension.ResolveInFolder(folder, "../outside.json"));
        Assert.Null(InstalledExtension.ResolveInFolder(folder, "/etc/passwd"));
        Assert.Null(InstalledExtension.ResolveInFolder(folder, null));
    }

    public void Dispose() => _temp.Dispose();

    private ExtensionStore CreateStore() => new(_temp.Resolve("extensions"));

    /// <summary>A stream that can only be read forward, like a file picked through Android's storage access.</summary>
    private sealed class ForwardOnlyStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;

        public override long Seek(long offset, SeekOrigin loc) => throw new NotSupportedException();

        public override long Position
        {
            get => base.Position;
            set => throw new NotSupportedException();
        }
    }
}
