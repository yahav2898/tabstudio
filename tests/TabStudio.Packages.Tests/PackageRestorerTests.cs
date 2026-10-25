using TabStudio.Packages.Installation;

namespace TabStudio.Packages.Tests;

public sealed class PackageRestorerTests : IDisposable
{
    private readonly RestoreFixture _fixture = new();

    [Fact]
    public async Task Installs_into_lower_case_id_and_version_folders_with_a_completion_marker()
    {
        _fixture.Add(new TestPackage("My.Package", "1.0.0-Beta").WithAssembly("lib/net8.0", "My.Package"));

        var result = await _fixture.RestoreAsync(("My.Package", "1.0.0-Beta"));

        Assert.True(result.Success, Describe(result));
        var folder = Path.Combine(_fixture.PackagesDirectory, "my.package", "1.0.0-beta");
        Assert.Equal(folder, Assert.Single(result.Packages).DirectoryPath);
        Assert.True(File.Exists(Path.Combine(folder, GlobalPackagesFolder.CompletionMarkerName)));
        Assert.True(File.Exists(Path.Combine(folder, "My.Package.nuspec")));
        Assert.Equal(new[] { Path.Combine(folder, "lib", "net8.0", "My.Package.dll") }, result.CompileAssemblies);
        Assert.Equal(result.CompileAssemblies, result.RuntimeAssemblies);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(_fixture.PackagesDirectory, "my.package"), ".*"));
    }

    [Fact]
    public async Task A_package_folder_without_a_completion_marker_is_installed_again()
    {
        _fixture.Add(new TestPackage("A", "1.0.0").WithAssembly("lib/net8.0"));
        var folder = Path.Combine(_fixture.PackagesDirectory, "a", "1.0.0");
        Directory.CreateDirectory(Path.Combine(folder, "lib", "net8.0"));
        await File.WriteAllTextAsync(Path.Combine(folder, "lib", "net8.0", "A.dll"), "half a download");

        var result = await _fixture.CreateRestorer(_fixture.Options() with { UseRestoreCache = false }).RestoreAsync(_fixture.Project(("A", "1.0.0")));

        Assert.True(result.Success, Describe(result));
        Assert.True(File.Exists(Path.Combine(folder, GlobalPackagesFolder.CompletionMarkerName)));
        Assert.NotEqual("half a download", await File.ReadAllTextAsync(Path.Combine(folder, "lib", "net8.0", "A.dll")));
    }

    [Fact]
    public async Task Combines_the_assemblies_of_every_package_in_the_graph()
    {
        _fixture.Add(
            new TestPackage("App.Core", "1.0.0").DependsOn("App.Util", "1.0.0")
                .WithAssembly("ref/netstandard2.0", "App.Core")
                .WithAssembly("lib/net8.0", "App.Core"),
            new TestPackage("App.Util", "1.0.0").WithAssembly("lib/netstandard2.0", "App.Util"));

        var result = await _fixture.RestoreAsync(("App.Core", "1.0.0"));

        Assert.True(result.Success, Describe(result));
        Assert.Equal(new[] { "App.Core.dll", "App.Util.dll" }, result.CompileAssemblies.Select(Path.GetFileName));
        Assert.Contains($"{Path.DirectorySeparatorChar}ref{Path.DirectorySeparatorChar}", result.CompileAssemblies[0], StringComparison.Ordinal);
        Assert.Contains($"{Path.DirectorySeparatorChar}lib{Path.DirectorySeparatorChar}net8.0", result.RuntimeAssemblies[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_package_without_assemblies_for_the_framework_reports_NU1202()
    {
        _fixture.Add(new TestPackage("Future", "1.0.0").WithAssembly("lib/net11.0"));

        var result = await _fixture.RestoreAsync(("Future", "1.0.0"));

        Assert.False(result.Success);
        var error = Assert.Single(result.Diagnostics);
        Assert.Equal(PackageDiagnosticIds.IncompatibleFramework, error.Id);
        Assert.Contains("net11.0", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Net_framework_only_packages_are_used_with_NU1701()
    {
        _fixture.Add(new TestPackage("Legacy", "1.0.0").WithAssembly("lib/net45"));

        var result = await _fixture.RestoreAsync(("Legacy", "1.0.0"));

        Assert.True(result.Success, Describe(result));
        Assert.Single(result.CompileAssemblies);
        Assert.Equal(PackageDiagnosticIds.FallbackFramework, Assert.Single(result.Diagnostics).Id);
    }

    [Fact]
    public async Task Assemblies_the_framework_already_has_are_left_out()
    {
        // Only the file names matter: assemblies named after framework assemblies are never used.
        _fixture.Add(new TestPackage("Polyfills", "1.0.0")
            .WithFile("lib/netstandard2.0/System.Memory.dll", "not loaded")
            .WithFile("lib/netstandard2.0/System.Buffers.dll", "not loaded"));

        var result = await _fixture.RestoreAsync(("Polyfills", "1.0.0"));

        Assert.True(result.Success, Describe(result));
        Assert.True(Assert.Single(result.Packages).ProvidedByFramework);
        Assert.Empty(result.CompileAssemblies);
        Assert.Empty(result.RuntimeAssemblies);
    }

    [Fact]
    public async Task A_direct_reference_to_a_framework_package_warns_NU1510()
    {
        _fixture.Add(new TestPackage("System.Text.Json", "8.0.5").WithFile("lib/net8.0/System.Text.Json.dll", "not loaded"));

        var result = await _fixture.RestoreAsync(("System.Text.Json", "8.0.5"));

        Assert.True(result.Success, Describe(result));
        Assert.Contains(result.Diagnostics, d => d.Id == PackageDiagnosticIds.RedundantFrameworkPackage);
        Assert.Empty(result.CompileAssemblies);
    }

    [Fact]
    public async Task A_project_without_package_references_restores_to_nothing()
    {
        var result = await _fixture.RestoreAsync();

        Assert.True(result.Success);
        Assert.Empty(result.Packages);
    }

    [Fact]
    public async Task Reports_progress_until_the_restore_completes()
    {
        _fixture.Add(new TestPackage("A", "1.0.0").WithAssembly("lib/net8.0"));
        var reports = new List<RestoreProgress>();
        var progress = new SynchronousProgress(reports.Add);

        await _fixture.CreateRestorer().RestoreAsync(_fixture.Project(("A", "1.0.0")), progress);

        Assert.Contains(reports, r => r.Stage == RestoreStage.Resolving);
        Assert.Contains(reports, r => r.Stage == RestoreStage.Downloading && r.Completed == 1 && r.Total == 1);
        Assert.Equal(RestoreStage.Completed, reports[^1].Stage);
    }

    public void Dispose() => _fixture.Dispose();

    private static string Describe(RestoreResult result) => string.Join('\n', result.Diagnostics);

    /// <summary>Reports on the calling thread, unlike <see cref="Progress{T}"/>, so the list is complete when the restore returns.</summary>
    private sealed class SynchronousProgress(Action<RestoreProgress> report) : IProgress<RestoreProgress>
    {
        private readonly Lock _gate = new();

        public void Report(RestoreProgress value)
        {
            lock (_gate)
            {
                report(value);
            }
        }
    }
}
