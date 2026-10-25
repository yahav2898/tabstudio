using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Core.Tests;

public sealed class SolutionFileEditorTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public void Projects_are_added_to_slnx_folders_and_removed()
    {
        var solution = _temp.Write("Shop.slnx", """
            <Solution>
              <Folder Name="/src/">
                <Project Path="src/App/App.csproj" />
              </Folder>
            </Solution>
            """);
        _temp.Write("src/App/App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        var lib = _temp.Write("src/Lib/Lib.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        var tests = _temp.Write("tests/Lib.Tests/Lib.Tests.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        Assert.True(SolutionFileEditor.AddProject(solution, lib, "src"));
        Assert.False(SolutionFileEditor.AddProject(solution, lib, "src"));
        Assert.True(SolutionFileEditor.AddProject(solution, tests, "tests"));

        var parsed = SolutionParser.Load(solution);
        Assert.Equal(new[] { "App", "Lib", "Lib.Tests" }, parsed.Projects.Select(p => p.Name));
        Assert.Equal("src", parsed.Projects.Single(p => p.Name == "Lib").FolderPath);
        Assert.Equal("tests", parsed.Projects.Single(p => p.Name == "Lib.Tests").FolderPath);
        Assert.Contains("    <Project Path=\"src/Lib/Lib.csproj\" />", File.ReadAllText(solution), StringComparison.Ordinal);

        Assert.True(SolutionFileEditor.RemoveProject(solution, lib));
        Assert.Equal(new[] { "App", "Lib.Tests" }, SolutionParser.Load(solution).Projects.Select(p => p.Name));
    }

    [Fact]
    public void Projects_are_added_to_sln_files_with_configurations_and_removed()
    {
        var solution = _temp.Write("Shop.sln", """
            Microsoft Visual Studio Solution File, Format Version 12.00
            # Visual Studio Version 17
            Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "App", "App\App.csproj", "{33333333-3333-3333-3333-333333333333}"
            EndProject
            Global
            	GlobalSection(SolutionConfigurationPlatforms) = preSolution
            		Debug|Any CPU = Debug|Any CPU
            		Release|Any CPU = Release|Any CPU
            	EndGlobalSection
            	GlobalSection(ProjectConfigurationPlatforms) = postSolution
            		{33333333-3333-3333-3333-333333333333}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
            		{33333333-3333-3333-3333-333333333333}.Debug|Any CPU.Build.0 = Debug|Any CPU
            	EndGlobalSection
            EndGlobal
            """);
        _temp.Write("App/App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        var lib = _temp.Write("Lib/Lib.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        Assert.True(SolutionFileEditor.AddProject(solution, lib));

        var text = File.ReadAllText(solution);
        Assert.Equal(new[] { "App", "Lib" }, SolutionParser.Load(solution).Projects.Select(p => p.Name));
        Assert.Contains("\"Lib\", \"Lib\\Lib.csproj\"", text, StringComparison.Ordinal);
        var libId = text.Split('\n').Single(l => l.Contains("Lib.csproj", StringComparison.Ordinal)).Split('"')[^2];
        Assert.Equal(4, text.Split('\n').Count(l => l.TrimStart().StartsWith(libId, StringComparison.Ordinal)));

        Assert.True(SolutionFileEditor.RemoveProject(solution, lib));
        var after = File.ReadAllText(solution);
        Assert.Equal(new[] { "App" }, SolutionParser.Load(solution).Projects.Select(p => p.Name));
        Assert.DoesNotContain("Lib", after, StringComparison.Ordinal);
        Assert.Contains("{33333333-3333-3333-3333-333333333333}.Debug|Any CPU.Build.0", after, StringComparison.Ordinal);
    }

    public void Dispose() => _temp.Dispose();
}
