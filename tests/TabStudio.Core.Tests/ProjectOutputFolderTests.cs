using TabStudio.Core.Workspaces;

namespace TabStudio.Core.Tests;

public sealed class ProjectOutputFolderTests
{
    private static readonly string ProjectPath = Path.Combine(Path.GetTempPath(), "Sample", "Lib.Tests", "Lib.Tests.csproj");
    private static readonly string ProjectDirectory = Path.GetDirectoryName(ProjectPath)!;

    [Theory]
    [InlineData("<TargetFramework>net8.0</TargetFramework>", "bin/Debug/net8.0")]
    [InlineData("<TargetFrameworks>net10.0;net9.0;net8.0</TargetFrameworks>", "bin/Debug/net10.0")]
    [InlineData("<TargetFramework>net8.0</TargetFramework><BaseOutputPath>out</BaseOutputPath>", "out/Debug/net8.0")]
    [InlineData("<TargetFramework>net8.0</TargetFramework><OutputPath>build/</OutputPath>", "build/net8.0")]
    [InlineData("<TargetFramework>net8.0</TargetFramework><AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>", "bin/Debug")]
    public void The_output_folder_is_where_the_SDK_puts_the_Debug_build(string properties, string expected)
    {
        var project = ProjectParser.Parse(ProjectPath, $"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>{properties}</PropertyGroup></Project>");

        Assert.Equal(Path.Combine(ProjectDirectory, expected.Replace('/', Path.DirectorySeparatorChar)), ProjectOutputFolder.For(project));
    }
}
