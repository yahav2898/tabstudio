using TabStudio.Core.Workspaces;

namespace TabStudio.Core.Tests;

public sealed class SolutionParserTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "Shop");

    private const string Sln = """
        Microsoft Visual Studio Solution File, Format Version 12.00
        # Visual Studio Version 17
        Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "src", "src", "{11111111-1111-1111-1111-111111111111}"
        EndProject
        Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "Libraries", "Libraries", "{22222222-2222-2222-2222-222222222222}"
        EndProject
        Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "App", "src\App\App.csproj", "{33333333-3333-3333-3333-333333333333}"
        EndProject
        Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "Lib", "src\Lib\Lib.csproj", "{44444444-4444-4444-4444-444444444444}"
        EndProject
        Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "Solution Items", "Solution Items", "{55555555-5555-5555-5555-555555555555}"
        	ProjectSection(SolutionItems) = preProject
        		README.md = README.md
        	EndProjectSection
        EndProject
        Project("{E24C65DC-7377-472B-9ABA-BC803B73C61A}") = "Website", "http://localhost/site", "{66666666-6666-6666-6666-666666666666}"
        EndProject
        Global
        	GlobalSection(NestedProjects) = preSolution
        		{33333333-3333-3333-3333-333333333333} = {11111111-1111-1111-1111-111111111111}
        		{22222222-2222-2222-2222-222222222222} = {11111111-1111-1111-1111-111111111111}
        		{44444444-4444-4444-4444-444444444444} = {22222222-2222-2222-2222-222222222222}
        	EndGlobalSection
        EndGlobal
        """;

    [Fact]
    public void Reads_projects_and_nested_solution_folders_from_an_sln()
    {
        var solution = SolutionParser.Parse(Path.Combine(Root, "Shop.sln"), Sln);

        Assert.Equal(SolutionFormat.Sln, solution.Format);
        Assert.Equal("Shop", solution.Name);
        Assert.Collection(
            solution.Projects,
            app =>
            {
                Assert.Equal("App", app.Name);
                Assert.Equal("src", app.FolderPath);
                Assert.Equal(Path.Combine(Root, "src", "App", "App.csproj"), app.AbsolutePath);
            },
            lib =>
            {
                Assert.Equal("Lib", lib.Name);
                Assert.Equal("src/Libraries", lib.FolderPath);
            });
    }

    [Fact]
    public void Handles_windows_line_endings()
    {
        var solution = SolutionParser.Parse(Path.Combine(Root, "Shop.sln"), Sln.Replace("\n", "\r\n", StringComparison.Ordinal));

        Assert.Equal(2, solution.Projects.Count);
    }

    [Fact]
    public void Reads_projects_and_folders_from_an_slnx()
    {
        const string slnx = """
            <Solution>
              <Folder Name="/src/">
                <Project Path="src/App/App.csproj" />
              </Folder>
              <Folder Name="/tests/unit/">
                <Project Path="tests\App.Tests\App.Tests.csproj" />
              </Folder>
              <Project Path="tools/Tool.fsproj" />
              <File Path="README.md" />
            </Solution>
            """;

        var solution = SolutionParser.Parse(Path.Combine(Root, "Shop.slnx"), slnx);

        Assert.Equal(SolutionFormat.Slnx, solution.Format);
        Assert.Equal(new[] { "App", "App.Tests", "Tool" }, solution.Projects.Select(p => p.Name));
        Assert.Equal(new[] { "src", "tests/unit", null }, solution.Projects.Select(p => p.FolderPath));
        Assert.Equal(Path.Combine(Root, "tests", "App.Tests", "App.Tests.csproj"), solution.Projects[1].AbsolutePath);
    }

    /// <summary>
    /// StronglyTypedId's NuGet integration tests: mapped to Debug|Any CPU without Build.0, they are in the solution but
    /// Visual Studio and dotnet build leave them out; a .slnx says it with &lt;Build Project="false" /&gt;.
    /// </summary>
    [Fact]
    public void Projects_the_Debug_configuration_does_not_build_are_marked()
    {
        var sln = """
            Microsoft Visual Studio Solution File, Format Version 12.00
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "App", "App\App.csproj", "{33333333-3333-3333-3333-333333333333}"
            EndProject
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Nuget.Tests", "Nuget.Tests\Nuget.Tests.csproj", "{44444444-4444-4444-4444-444444444444}"
            EndProject
            Global
            	GlobalSection(SolutionConfigurationPlatforms) = preSolution
            		Debug|Any CPU = Debug|Any CPU
            		Release|Any CPU = Release|Any CPU
            	EndGlobalSection
            	GlobalSection(ProjectConfigurationPlatforms) = postSolution
            		{33333333-3333-3333-3333-333333333333}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
            		{33333333-3333-3333-3333-333333333333}.Debug|Any CPU.Build.0 = Debug|Any CPU
            		{44444444-4444-4444-4444-444444444444}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
            		{44444444-4444-4444-4444-444444444444}.Release|Any CPU.ActiveCfg = Release|Any CPU
            		{44444444-4444-4444-4444-444444444444}.Release|Any CPU.Build.0 = Release|Any CPU
            	EndGlobalSection
            EndGlobal
            """;
        var slnx = """
            <Solution>
              <Project Path="App/App.csproj" />
              <Project Path="Nuget.Tests/Nuget.Tests.csproj"><Build Project="false" /></Project>
            </Solution>
            """;

        foreach (var solution in new[] { SolutionParser.Parse(Path.Combine(Root, "Shop.sln"), sln), SolutionParser.Parse(Path.Combine(Root, "Shop.slnx"), slnx) })
        {
            Assert.Equal(new[] { ("App", true), ("Nuget.Tests", false) }, solution.Projects.Select(p => (p.Name, p.IsBuilt)));
        }
    }

    [Fact]
    public void Invalid_slnx_throws_a_load_exception()
    {
        Assert.Throws<ProjectLoadException>(() => SolutionParser.Parse(Path.Combine(Root, "Broken.slnx"), "<Solution>"));
    }

    [Theory]
    [InlineData("App.sln", true)]
    [InlineData("App.SLNX", true)]
    [InlineData("App.slnf", false)]
    public void Recognizes_solution_file_extensions(string fileName, bool expected) =>
        Assert.Equal(expected, SolutionParser.IsSolutionFile(fileName));
}
