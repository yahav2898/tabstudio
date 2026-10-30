using TabStudio.Core.Workspaces;

namespace TabStudio.Packages.Tests;

public sealed class ResolutionTests : IDisposable
{
    private readonly RestoreFixture _fixture = new();

    [Fact]
    public async Task Picks_the_lowest_version_in_range()
    {
        _fixture.Add(Package("A", "1.0.0"), Package("A", "1.1.0"), Package("A", "1.2.0"), Package("A", "2.0.0"));

        var result = await _fixture.RestoreAsync(("A", "[1.1.0, 2.0.0)"));

        Assert.True(result.Success, Describe(result));
        Assert.Equal("1.1.0", Version(result, "A"));
    }

    [Fact]
    public async Task A_plain_version_means_that_version_or_higher()
    {
        _fixture.Add(Package("A", "1.0.0"), Package("A", "2.0.0"));

        var result = await _fixture.RestoreAsync(("A", "1.0.0"));

        Assert.Equal("1.0.0", Version(result, "A"));
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task Floating_versions_pick_the_highest_match()
    {
        _fixture.Add(Package("A", "1.0.0"), Package("A", "1.5.0"), Package("A", "2.0.0"));

        var result = await _fixture.RestoreAsync(("A", "1.*"));

        Assert.Equal("1.5.0", Version(result, "A"));
    }

    [Fact]
    public async Task Prerelease_versions_are_skipped_unless_the_range_asks_for_one()
    {
        _fixture.Add(Package("A", "1.0.0-beta"), Package("A", "1.1.0"), Package("B", "2.0.0-rc.1"), Package("B", "2.0.0"));

        var result = await _fixture.RestoreAsync(("A", "1.0.0"), ("B", "2.0.0-rc.1"));

        Assert.Equal("1.1.0", Version(result, "A"));
        Assert.Equal("2.0.0-rc.1", Version(result, "B"));
    }

    [Fact]
    public async Task Uses_the_next_version_with_NU1603_when_the_lower_bound_does_not_exist()
    {
        _fixture.Add(Package("A", "1.0.0"), Package("A", "1.2.0"));

        var result = await _fixture.RestoreAsync(("A", "1.1.0"));

        Assert.True(result.Success);
        Assert.Equal("1.2.0", Version(result, "A"));
        Assert.Contains(result.Diagnostics, d => d.Id == PackageDiagnosticIds.ApproximateMatch);
    }

    [Fact]
    public async Task Restores_transitive_dependencies_at_their_lowest_applicable_version()
    {
        _fixture.Add(
            Package("A", "1.0.0").DependsOn("B", "1.0.0"),
            Package("B", "1.0.0").DependsOn("C", "[2.0.0, 3.0.0)"),
            Package("B", "2.0.0"),
            Package("C", "2.0.0"),
            Package("C", "2.5.0"));

        var result = await _fixture.RestoreAsync(("A", "1.0.0"));

        Assert.True(result.Success, Describe(result));
        Assert.Equal(new[] { "A", "B", "C" }, result.Packages.Select(p => p.Id));
        Assert.Equal("1.0.0", Version(result, "B"));
        Assert.Equal("2.0.0", Version(result, "C"));
        Assert.True(result.Packages.Single(p => p.Id == "A").IsDirect);
        Assert.False(result.Packages.Single(p => p.Id == "C").IsDirect);
    }

    [Fact]
    public async Task A_direct_reference_wins_over_a_transitive_one()
    {
        _fixture.Add(Package("A", "1.0.0").DependsOn("B", "1.0.0"), Package("B", "1.0.0"), Package("B", "3.0.0"));

        var result = await _fixture.RestoreAsync(("A", "1.0.0"), ("B", "3.0.0"));

        Assert.Equal("3.0.0", Version(result, "B"));
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task A_direct_reference_that_downgrades_a_transitive_one_wins_with_NU1605()
    {
        _fixture.Add(Package("A", "1.0.0").DependsOn("B", "2.0.0"), Package("B", "1.0.0"), Package("B", "2.0.0"));

        var result = await _fixture.RestoreAsync(("A", "1.0.0"), ("B", "1.0.0"));

        Assert.Equal("1.0.0", Version(result, "B"));
        var downgrade = Assert.Single(result.Diagnostics, d => d.Id == PackageDiagnosticIds.Downgrade);
        Assert.Contains("B from 2.0.0 to 1.0.0", downgrade.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cousins_settle_on_the_lowest_version_that_satisfies_both()
    {
        _fixture.Add(
            Package("A", "1.0.0").DependsOn("C", "1.0.0"),
            Package("B", "1.0.0").DependsOn("C", "2.0.0"),
            Package("C", "1.0.0"),
            Package("C", "2.0.0"),
            Package("C", "3.0.0"));

        var result = await _fixture.RestoreAsync(("A", "1.0.0"), ("B", "1.0.0"));

        Assert.True(result.Success, Describe(result));
        Assert.Equal("2.0.0", Version(result, "C"));
    }

    [Fact]
    public async Task Cousins_whose_ranges_do_not_overlap_report_NU1107()
    {
        _fixture.Add(
            Package("A", "1.0.0").DependsOn("C", "[1.0.0]"),
            Package("B", "1.0.0").DependsOn("C", "[2.0.0]"),
            Package("C", "1.0.0"),
            Package("C", "2.0.0"));

        var result = await _fixture.RestoreAsync(("A", "1.0.0"), ("B", "1.0.0"));

        Assert.False(result.Success);
        var conflict = Assert.Single(result.Diagnostics, d => d.Id == PackageDiagnosticIds.VersionConflict);
        Assert.Contains("Version conflict detected for C", conflict.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Nearest_wins_only_against_dependencies_on_the_same_path()
    {
        // X 1.0 under A is not on the path to X 2.0 under B -> C, so they are cousins and the higher version wins.
        _fixture.Add(
            Package("A", "1.0.0").DependsOn("X", "1.0.0"),
            Package("B", "1.0.0").DependsOn("C", "1.0.0"),
            Package("C", "1.0.0").DependsOn("X", "2.0.0"),
            Package("X", "1.0.0"),
            Package("X", "2.0.0"));

        var result = await _fixture.RestoreAsync(("A", "1.0.0"), ("B", "1.0.0"));

        Assert.Equal("2.0.0", Version(result, "X"));
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == PackageDiagnosticIds.Downgrade);
    }

    [Fact]
    public async Task A_rejected_version_does_not_bring_its_own_dependencies()
    {
        _fixture.Add(
            Package("A", "1.0.0").DependsOn("C", "1.0.0"),
            Package("B", "1.0.0").DependsOn("C", "2.0.0"),
            Package("C", "1.0.0").DependsOn("Old", "1.0.0"),
            Package("C", "2.0.0"),
            Package("Old", "1.0.0"));

        var result = await _fixture.RestoreAsync(("A", "1.0.0"), ("B", "1.0.0"));

        Assert.Equal("2.0.0", Version(result, "C"));
        Assert.DoesNotContain(result.Packages, p => p.Id == "Old");
    }

    [Fact]
    public async Task Selects_the_nearest_dependency_group_for_the_target_framework()
    {
        _fixture.Add(
            Package("A", "1.0.0")
                .Group(".NETStandard2.0", ("FromNetStandard", "1.0.0"))
                .Group("net8.0", ("FromNet8", "1.0.0"))
                .Group("net11.0", ("FromNet11", "1.0.0")),
            Package("FromNetStandard", "1.0.0"),
            Package("FromNet8", "1.0.0"),
            Package("FromNet11", "1.0.0"));

        var result = await _fixture.RestoreAsync(("A", "1.0.0"));

        Assert.Equal(new[] { "A", "FromNet8" }, result.Packages.Select(p => p.Id));
    }

    [Fact]
    public async Task Dependencies_without_a_group_apply_to_every_framework()
    {
        _fixture.Add(Package("A", "1.0.0").DependsOn("B", "1.0.0"), Package("B", "1.0.0"));

        var result = await _fixture.RestoreAsync(("A", "1.0.0"));

        Assert.Contains(result.Packages, p => p.Id == "B");
    }

    [Fact]
    public async Task Prunes_transitive_dependencies_on_packages_the_framework_provides()
    {
        // None of these exist in the feed: they must not even be looked up.
        _fixture.Add(Package("A", "1.0.0")
            .DependsOn("System.Runtime", "4.3.0")
            .DependsOn("System.Text.Json", "8.0.5")
            .DependsOn("NETStandard.Library", "1.6.1"));

        var result = await _fixture.RestoreAsync(("A", "1.0.0"));

        Assert.True(result.Success, Describe(result));
        Assert.Equal(new[] { "A" }, result.Packages.Select(p => p.Id));
    }

    [Fact]
    public async Task An_unknown_package_reports_NU1101()
    {
        _fixture.Add(Package("A", "1.0.0"));

        var result = await _fixture.RestoreAsync(("Missing", "1.0.0"));

        Assert.False(result.Success);
        var error = Assert.Single(result.Diagnostics);
        Assert.Equal(PackageDiagnosticIds.PackageNotFound, error.Id);
        Assert.Contains("Unable to find package Missing", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_version_outside_every_range_reports_NU1102()
    {
        _fixture.Add(Package("A", "1.0.0"), Package("A", "1.5.0"));

        var result = await _fixture.RestoreAsync(("A", "[2.0.0, 3.0.0)"));

        var error = Assert.Single(result.Diagnostics);
        Assert.Equal(PackageDiagnosticIds.VersionNotFound, error.Id);
        Assert.Contains("from 1.0.0 to 1.5.0", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Only_prerelease_versions_in_range_report_NU1103()
    {
        _fixture.Add(Package("A", "2.0.0-preview.1"));

        var result = await _fixture.RestoreAsync(("A", "1.0.0"));

        Assert.Equal(PackageDiagnosticIds.OnlyPrereleaseVersions, Assert.Single(result.Diagnostics).Id);
    }

    [Fact]
    public async Task A_dependency_cycle_reports_NU1108()
    {
        _fixture.Add(Package("A", "1.0.0").DependsOn("B", "1.0.0"), Package("B", "1.0.0").DependsOn("A", "1.0.0"));

        var result = await _fixture.RestoreAsync(("A", "1.0.0"));

        Assert.Contains(result.Diagnostics, d => d.Id == PackageDiagnosticIds.DependencyCycle);
    }

    [Fact]
    public async Task A_reference_without_a_version_uses_the_lowest_with_NU1015()
    {
        _fixture.Add(Package("A", "1.0.0"), Package("A", "2.0.0"));

        var result = await _fixture.RestoreAsync(("A", null));

        Assert.Equal("1.0.0", Version(result, "A"));
        Assert.Contains(result.Diagnostics, d => d.Id == PackageDiagnosticIds.MissingVersion);
    }

    [Fact]
    public async Task Versions_come_from_Directory_Packages_props_with_central_package_management()
    {
        _fixture.Add(Package("A", "1.0.0"), Package("A", "2.0.0"));
        _fixture.Write("Directory.Packages.props", """
            <Project>
              <PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup>
              <ItemGroup><PackageVersion Include="A" Version="2.0.0" /></ItemGroup>
            </Project>
            """);

        var result = await _fixture.RestoreAsync(("A", null));

        Assert.True(result.Success, Describe(result));
        Assert.Equal("2.0.0", Version(result, "A"));
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task An_invalid_version_reports_TS3003()
    {
        var result = await _fixture.RestoreAsync(("A", "not-a-version"));

        Assert.Equal(PackageDiagnosticIds.InvalidVersion, Assert.Single(result.Diagnostics).Id);
    }

    [Fact]
    public async Task Packages_of_referenced_projects_flow_to_the_project_one_level_further_away()
    {
        _fixture.Add(
            Package("A", "1.0.0").DependsOn("B", "1.0.0"),
            Package("B", "1.0.0"),
            Package("X", "1.0.0"),
            Package("X", "2.0.0"));
        WriteProject("Lib", references: ["<PackageReference Include=\"A\" Version=\"1.0.0\" />", "<PackageReference Include=\"X\" Version=\"1.0.0\" />"]);
        var app = WriteProject("Main", references: ["<ProjectReference Include=\"..\\Lib\\Lib.csproj\" />", "<PackageReference Include=\"X\" Version=\"2.0.0\" />"]);

        var result = await _fixture.CreateRestorer().RestoreAsync(ProjectParser.Load(app));

        Assert.True(result.Success, Describe(result));
        Assert.Equal(new[] { "A", "B", "X" }, result.Packages.Select(p => p.Id));
        Assert.Equal("2.0.0", Version(result, "X"));
        Assert.False(result.Packages.Single(p => p.Id == "A").IsDirect);
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == PackageDiagnosticIds.Downgrade);
    }

    /// <summary>
    /// MudBlazor references its analyzer projects with PrivateAssets="all": their Roslyn packages stay with MudBlazor, so a
    /// project that references MudBlazor and a newer Roslyn has no version conflict (NU1107).
    /// </summary>
    [Fact]
    public async Task A_project_referenced_with_PrivateAssets_all_keeps_its_packages_from_further_projects()
    {
        _fixture.Add(Package("X", "1.0.0"), Package("X", "2.0.0"));
        WriteProject("Analyzer", references: ["<PackageReference Include=\"X\" Version=\"[1.0.0]\" />"]);
        WriteProject("Lib", references: ["<ProjectReference Include=\"..\\Analyzer\\Analyzer.csproj\" PrivateAssets=\"all\" ReferenceOutputAssembly=\"false\" OutputItemType=\"Analyzer\" />"]);
        var app = WriteProject("Main", references: ["<ProjectReference Include=\"..\\Lib\\Lib.csproj\" />", "<PackageReference Include=\"X\" Version=\"[2.0.0]\" />"]);

        var result = await _fixture.CreateRestorer().RestoreAsync(ProjectParser.Load(app));

        Assert.True(result.Success, Describe(result));
        Assert.Equal("2.0.0", Version(result, "X"));
        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == Core.Diagnostics.DiagnosticSeverityLevel.Error);
    }

    [Fact]
    public async Task Changing_the_packages_of_a_referenced_project_restores_again()
    {
        _fixture.Add(Package("A", "1.0.0"), Package("A", "2.0.0"));
        WriteProject("Lib", references: ["<PackageReference Include=\"A\" Version=\"1.0.0\" />"]);
        var app = WriteProject("Main", references: ["<ProjectReference Include=\"..\\Lib\\Lib.csproj\" />"]);
        var restorer = _fixture.CreateRestorer();

        var first = await restorer.RestoreAsync(ProjectParser.Load(app));
        WriteProject("Lib", references: ["<PackageReference Include=\"A\" Version=\"2.0.0\" />"]);
        var second = await restorer.RestoreAsync(ProjectParser.Load(app));

        Assert.Equal("1.0.0", Version(first, "A"));
        Assert.False(second.FromCache);
        Assert.Equal("2.0.0", Version(second, "A"));
    }

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task Analyzers_stay_with_the_project_that_references_their_package_and_PrivateAssets_all_packages_do_not_flow()
    {
        // ardalis/CleanArchitecture: Web references Mediator.SourceGenerator with PrivateAssets="all", and the tests
        // that reference Web must not run its generator a second time.
        _fixture.Add(
            Package("Generator", "1.0.0").WithFile("analyzers/dotnet/cs/Generator.dll"),
            Package("Library", "1.0.0").DependsOn("Helper", "1.0.0"),
            Package("Helper", "1.0.0").WithFile("analyzers/dotnet/cs/Helper.Generator.dll"),
            Package("Private", "1.0.0"));
        WriteProject("Web", references:
        [
            "<PackageReference Include=\"Generator\" Version=\"1.0.0\" />",
            "<PackageReference Include=\"Library\" Version=\"1.0.0\" />",
            "<PackageReference Include=\"Private\" Version=\"1.0.0\" PrivateAssets=\"all\" />",
        ]);
        var tests = WriteProject("Tests", references: ["<ProjectReference Include=\"..\\Web\\Web.csproj\" />"]);

        var web = await _fixture.CreateRestorer().RestoreAsync(ProjectParser.Load(_fixture.Resolve("Web/Web.csproj")));
        var result = await _fixture.CreateRestorer().RestoreAsync(ProjectParser.Load(tests));

        Assert.True(result.Success, Describe(result));
        Assert.Equal(new[] { "Generator.dll", "Helper.Generator.dll" }, web.Analyzers.Select(a => Path.GetFileName(a)).Order(StringComparer.Ordinal));
        Assert.Empty(result.Analyzers);
        Assert.Equal(new[] { "Generator", "Helper", "Library" }, result.Packages.Select(p => p.Id).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Versions_given_as_properties_of_the_project_or_of_Directory_Packages_props_are_expanded()
    {
        // Humanizer and eShopOnWeb: Version="$(BaselinePackageVersion)" and <PackageVersion Version="$(AspNetVersion)" />.
        _fixture.Add(Package("A", "1.0.0"), Package("A", "2.0.0"), Package("B", "3.0.0"), Package("B", "4.0.0"));
        _fixture.Write("Directory.Packages.props", """
            <Project>
              <PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally><BVersion>3.0.0</BVersion></PropertyGroup>
              <ItemGroup><PackageVersion Include="B" Version="$(BVersion)" /></ItemGroup>
            </Project>
            """);
        var app = _fixture.Write("Main/Main.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework><AVersion Condition="'$(AVersion)' == ''">2.0.0</AVersion></PropertyGroup>
              <ItemGroup><PackageReference Include="A" Version="$(AVersion)" /><PackageReference Include="B" /></ItemGroup>
            </Project>
            """);

        var result = await _fixture.CreateRestorer().RestoreAsync(ProjectParser.Load(app));

        Assert.True(result.Success, Describe(result));
        Assert.Equal("2.0.0", Version(result, "A"));
        Assert.Equal("3.0.0", Version(result, "B"));
    }

    private string WriteProject(string name, string[] references) =>
        _fixture.Write($"{name}/{name}.csproj", $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup>
                {string.Join('\n', references)}
              </ItemGroup>
            </Project>
            """);

    private static TestPackage Package(string id, string version) => new TestPackage(id, version).WithAssembly("lib/netstandard2.0");

    private static string? Version(RestoreResult result, string id) =>
        result.Packages.SingleOrDefault(p => p.Id == id)?.Version;

    private static string Describe(RestoreResult result) => string.Join('\n', result.Diagnostics);
}
