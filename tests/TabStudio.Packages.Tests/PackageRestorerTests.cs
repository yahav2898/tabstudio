using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Core.Workspaces;
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
    public async Task Restores_the_analyzers_of_packages_and_keeps_them_in_the_restore_cache()
    {
        _fixture.Add(new TestPackage("Generators", "1.0.0")
            .WithAssembly("lib/net8.0")
            .WithFile("analyzers/dotnet/roslyn4.12/cs/Generators.SourceGenerators.dll", TestPackage.CompileLibrary("Generators.SourceGenerators", "public static class G {}"))
            .WithFile("analyzers/dotnet/roslyn9.0/cs/Generators.SourceGenerators.dll", TestPackage.CompileLibrary("Generators.SourceGenerators", "public static class G {}"))
            .WithFile("analyzers/dotnet/vb/Generators.VisualBasic.dll", TestPackage.CompileLibrary("Generators.VisualBasic", "public static class V {}")));
        var project = _fixture.Project(("Generators", "1.0.0"));
        var restorer = _fixture.CreateRestorer();

        var result = await restorer.RestoreAsync(project);
        var cached = await restorer.RestoreAsync(project);

        var folder = Path.Combine(_fixture.PackagesDirectory, "generators", "1.0.0");
        var expected = new[] { Path.Combine(folder, "analyzers", "dotnet", "roslyn4.12", "cs", "Generators.SourceGenerators.dll") };
        Assert.True(result.Success, Describe(result));
        Assert.Equal(expected, result.Analyzers);
        Assert.Equal(expected, Assert.Single(result.Packages).Analyzers);
        Assert.True(cached.FromCache);
        Assert.Equal(expected, cached.Analyzers);
    }

    /// <summary>
    /// The props and targets a project imports from its packages, as NuGet 7 and SDK 10.0.113 wrote them into
    /// nuget.g.props and nuget.g.targets for the same packages: buildTransitive replaces build by file name, a
    /// dependency excluded with exclude="Build,Analyzers" (as packed projects write it) gives only its buildTransitive
    /// files, a dependency without exclude gives build files too, and the nearest framework folder is chosen.
    /// </summary>
    [Fact]
    public async Task Build_files_are_imported_as_NuGet_imports_them()
    {
        static TestPackage Leaf(string id, params string[] files)
        {
            var package = new TestPackage(id, "1.0.0").WithAssembly("lib/net8.0");
            foreach (var file in files)
            {
                package.WithFile(file, "<Project />");
            }

            return package;
        }

        _fixture.Add(
            Leaf("Both", "build/Both.props", "buildTransitive/Both.props"),
            Leaf("OnlyBuild", "build/OnlyBuild.props"),
            Leaf("OnlyTransitive", "buildTransitive/OnlyTransitive.props"),
            Leaf("TfmBuild", "build/net8.0/TfmBuild.props", "build/net462/TfmBuild.props", "buildTransitive/net8.0/TfmBuild.targets"),
            Leaf("Plain", "build/Plain.props", "build/Other.props"),
            new TestPackage("ParentDefault", "1.0.0").WithAssembly("lib/net8.0")
                .DependsOn("Both", "1.0.0", include: null, exclude: "Build,Analyzers")
                .DependsOn("OnlyBuild", "1.0.0", include: null, exclude: "Build,Analyzers")
                .DependsOn("OnlyTransitive", "1.0.0", include: null, exclude: "Build,Analyzers"),
            new TestPackage("ParentNone", "1.0.0").WithAssembly("lib/net8.0").DependsOn("Plain", "1.0.0").DependsOn("TfmBuild", "1.0.0"));
        var project = _fixture.Project(("ParentDefault", "1.0.0"), ("ParentNone", "1.0.0"));
        var restorer = _fixture.CreateRestorer();

        var result = await restorer.RestoreAsync(project);
        var cached = await restorer.RestoreAsync(project);

        Assert.True(result.Success, Describe(result));
        string Imported(string id, string path) => Path.Combine(_fixture.PackagesDirectory, id.ToLowerInvariant(), "1.0.0", path.Replace('/', Path.DirectorySeparatorChar));
        Assert.Equal(
            new[]
            {
                Imported("Both", "buildTransitive/Both.props"),
                Imported("OnlyTransitive", "buildTransitive/OnlyTransitive.props"),
                Imported("Plain", "build/Plain.props"),
                Imported("TfmBuild", "build/net8.0/TfmBuild.props"),
                Imported("TfmBuild", "buildTransitive/net8.0/TfmBuild.targets"),
            },
            result.BuildFiles);
        Assert.Empty(result.Packages.Single(p => p.Id == "OnlyBuild").BuildFiles);
        Assert.True(cached.FromCache);
        Assert.Equal(result.BuildFiles, cached.BuildFiles);
    }

    /// <summary>
    /// A source-only package compiles its contentFiles into the projects that reference it, but not into those that get
    /// it through another package, unless that package's dependency includes them: NuGet's default for a package's
    /// dependencies is everything but content files. WireMock.Net brings RamlToOpenApiConverter.SourceOnly this way,
    /// and its sources need a package WireMock.Net merges into itself.
    /// </summary>
    [Fact]
    public async Task Content_files_flow_only_from_packages_the_project_references_or_includes()
    {
        static TestPackage Source(string id) => new TestPackage(id, "1.0.0").WithAssembly("lib/net8.0").WithFile($"contentFiles/cs/net8.0/{id}.cs", "class C { }");
        _fixture.Add(
            Source("Direct"),
            Source("Transitive"),
            Source("Included"),
            new TestPackage("Parent", "1.0.0").WithAssembly("lib/net8.0")
                .DependsOn("Transitive", "1.0.0")
                .DependsOn("Included", "1.0.0", include: "all", exclude: null));
        var project = _fixture.Project(("Direct", "1.0.0"), ("Parent", "1.0.0"));

        var result = await _fixture.CreateRestorer().RestoreAsync(project);

        Assert.True(result.Success, Describe(result));
        Assert.Equal(
            new[] { "Direct.cs", "Included.cs" },
            result.ContentSources.Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_reference_s_ExcludeAssets_build_keeps_only_its_buildTransitive_files()
    {
        _fixture.Add(new TestPackage("Tool", "1.0.0").WithAssembly("lib/net8.0")
            .WithFile("build/Tool.props", "<Project />")
            .WithFile("buildTransitive/Tool.targets", "<Project />"));
        var path = _fixture.Write("Excluding/Excluding.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup><PackageReference Include="Tool" Version="1.0.0" ExcludeAssets="build" /></ItemGroup>
            </Project>
            """);

        var result = await _fixture.CreateRestorer().RestoreAsync(Core.Workspaces.ProjectParser.Load(path));

        Assert.True(result.Success, Describe(result));
        Assert.Equal(new[] { Path.Combine(_fixture.PackagesDirectory, "tool", "1.0.0", "buildTransitive", "Tool.targets") }, result.BuildFiles);
    }

    [Fact]
    public async Task A_package_whose_generators_all_need_a_newer_compiler_says_so_at_restore()
    {
        _fixture.Add(new TestPackage("Future", "1.0.0").WithAssembly("lib/net8.0")
            .WithFile("analyzers/dotnet/roslyn9.0/cs/Future.Generators.dll", TestPackage.CompileLibrary("Future.Generators", "public static class F {}")));

        var result = await _fixture.RestoreAsync(("Future", "1.0.0"));

        var warning = Assert.Single(result.Diagnostics, d => d.Id == PackageDiagnosticIds.AnalyzersNeedNewerCompiler);
        Assert.Contains("Roslyn 9.0", warning.Message, StringComparison.Ordinal);
        Assert.Empty(result.Analyzers);
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

    /// <summary>
    /// As in NuGet, one package that cannot be found does not take the others away: the restore fails with NU1101
    /// for it alone, and the project compiles against the rest, with TS2003 only for the missing one.
    /// </summary>
    [Fact]
    public async Task A_missing_package_reports_NU1101_and_the_project_still_compiles_against_the_others()
    {
        _fixture.Add(new TestPackage("Good", "1.0.0").WithAssembly("lib/net8.0"));
        var project = _fixture.Project(("Good", "1.0.0"), ("Missing", "1.0.0"));
        _fixture.Write("App/Program.cs", "Console.WriteLine(Good.Marker.Describe());");

        var result = await _fixture.CreateRestorer().RestoreAsync(project);

        Assert.False(result.Success);
        var error = Assert.Single(result.Diagnostics);
        Assert.Equal(PackageDiagnosticIds.PackageNotFound, error.Id);
        Assert.Contains("Unable to find package Missing", error.Message, StringComparison.Ordinal);
        Assert.Equal("Good", Assert.Single(result.Packages).Id);
        Assert.Equal(new[] { "Good.dll" }, result.CompileAssemblies.Select(Path.GetFileName));
        Assert.Equal(result.CompileAssemblies, result.RuntimeAssemblies);
        Assert.Equal(new[] { "Missing" }, result.UnrestoredReferences);

        var build = await BuildAsync(project, result);

        Assert.True(build.Success, string.Join('\n', build.Diagnostics));
        var unrestored = Assert.Single(build.Diagnostics, d => d.Id == ProjectBuilder.UnrestoredPackageId);
        Assert.Contains("'Missing'", unrestored.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A version conflict (NU1107) fails the restore, but, as in NuGet, the graph keeps the version that won and every
    /// other package, so nothing is missing from the compilation.
    /// </summary>
    [Fact]
    public async Task A_version_conflict_reports_NU1107_and_keeps_every_package()
    {
        _fixture.Add(
            new TestPackage("A", "1.0.0").WithAssembly("lib/net8.0").DependsOn("C", "[1.0.0]"),
            new TestPackage("B", "1.0.0").WithAssembly("lib/net8.0").DependsOn("C", "[2.0.0]"),
            new TestPackage("C", "1.0.0").WithAssembly("lib/net8.0"),
            new TestPackage("C", "2.0.0").WithAssembly("lib/net8.0"),
            new TestPackage("Good", "1.0.0").WithAssembly("lib/net8.0"));
        var project = _fixture.Project(("A", "1.0.0"), ("B", "1.0.0"), ("Good", "1.0.0"));
        _fixture.Write("App/Program.cs", "Console.WriteLine(A.Marker.Describe() + B.Marker.Describe() + C.Marker.Describe() + Good.Marker.Describe());");

        var result = await _fixture.CreateRestorer().RestoreAsync(project);

        Assert.False(result.Success);
        var conflict = Assert.Single(result.Diagnostics, d => d.Id == PackageDiagnosticIds.VersionConflict);
        Assert.Contains("Version conflict detected for C", conflict.Message, StringComparison.Ordinal);
        Assert.Equal(new[] { "A", "B", "C", "Good" }, result.Packages.Select(p => p.Id));
        Assert.Equal("2.0.0", result.Packages.Single(p => p.Id == "C").Version);
        Assert.Equal(new[] { "A.dll", "B.dll", "C.dll", "Good.dll" }, result.CompileAssemblies.Select(Path.GetFileName));
        Assert.Empty(result.UnrestoredReferences);

        var build = await BuildAsync(project, result);

        Assert.True(build.Success, string.Join('\n', build.Diagnostics));
        Assert.DoesNotContain(build.Diagnostics, d => d.Id == ProjectBuilder.UnrestoredPackageId);
    }

    /// <summary>A version that does not parse (TS3003) leaves out that reference only.</summary>
    [Fact]
    public async Task An_invalid_version_leaves_out_only_its_own_package()
    {
        _fixture.Add(new TestPackage("Good", "1.0.0").WithAssembly("lib/net8.0"));
        var project = _fixture.Project(("Good", "1.0.0"), ("Generated", "$(NoSuchVersionProperty)"));

        var result = await _fixture.CreateRestorer().RestoreAsync(project);

        Assert.False(result.Success);
        Assert.Equal(PackageDiagnosticIds.InvalidVersion, Assert.Single(result.Diagnostics).Id);
        Assert.Equal("Good", Assert.Single(result.Packages).Id);
        Assert.Equal(new[] { "Good.dll" }, result.CompileAssemblies.Select(Path.GetFileName));
        Assert.Equal(new[] { "Generated" }, result.UnrestoredReferences);
    }

    [Fact]
    public async Task A_restore_with_errors_is_not_cached_and_the_next_one_tries_the_sources_again()
    {
        _fixture.Add(new TestPackage("Good", "1.0.0").WithAssembly("lib/net8.0"));
        var project = _fixture.Project(("Good", "1.0.0"), ("Late", "1.0.0"));
        var restorer = _fixture.CreateRestorer();

        var first = await restorer.RestoreAsync(project);
        var second = await restorer.RestoreAsync(project);

        Assert.False(File.Exists(PackageRestorer.GetCacheFilePath(project)));
        Assert.False(second.FromCache);
        Assert.False(second.Success);
        Assert.Equal(PackageDiagnosticIds.PackageNotFound, Assert.Single(second.Diagnostics).Id);
        Assert.Equal(first.CompileAssemblies, second.CompileAssemblies);
        Assert.Equal(new[] { "Late" }, second.UnrestoredReferences);

        // Once the package is published, the restore succeeds, and from then on comes from the cache.
        _fixture.Add(new TestPackage("Late", "1.0.0").WithAssembly("lib/net8.0"));
        var third = await restorer.RestoreAsync(project);
        var fourth = await restorer.RestoreAsync(project);

        Assert.True(third.Success, Describe(third));
        Assert.False(third.FromCache);
        Assert.Empty(third.UnrestoredReferences);
        Assert.Equal(new[] { "Good.dll", "Late.dll" }, third.CompileAssemblies.Select(Path.GetFileName));
        Assert.True(fourth.FromCache);
        Assert.Empty(fourth.UnrestoredReferences);
        Assert.Equal(third.CompileAssemblies, fourth.CompileAssemblies);
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

    /// <summary>Builds a project against what a restore gave it, as the IDE's language service does.</summary>
    private static async Task<BuildResult> BuildAsync(ProjectInfo project, RestoreResult restore)
    {
        using var host = new RoslynWorkspaceHost();
        host.Load(WorkspaceLoader.Load(project.FilePath), FileSystemTextProvider.Instance);
        host.SetPackageReferences(project.FilePath, restore.CompileAssemblies, restore.UnrestoredReferences);
        return await new ProjectBuilder(host).BuildAsync(host.FindProject(project.FilePath)!);
    }

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
