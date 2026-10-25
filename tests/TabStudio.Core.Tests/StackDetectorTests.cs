using TabStudio.Core.Detection;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Core.Tests;

public sealed class StackDetectorTests
{
    [Fact]
    public void Detects_dotnet_with_the_sdk_version_from_global_json()
    {
        using var temp = new TempDirectory();
        temp.Write("global.json", """
            {
              // comments are allowed in global.json
              "sdk": { "version": "10.0.100", "rollForward": "latestFeature" },
            }
            """);

        var dotnet = StackDetector.Detect(temp.Path).Get(StackKind.DotNet);

        Assert.NotNull(dotnet);
        Assert.Equal("10.0.100", dotnet.Version);
        Assert.Equal("global.json", dotnet.Evidence);
    }

    [Fact]
    public void Detects_node_frameworks_from_package_json()
    {
        using var temp = new TempDirectory();
        temp.Write("web/package.json", """
            {
              "dependencies": { "react": "^19.1.0", "react-dom": "^19.1.0" },
              "devDependencies": { "vite": "~7.0.0", "typescript": "5.9.2" }
            }
            """);

        var stack = StackDetector.Detect(temp.Path);

        Assert.True(stack.Has(StackKind.Node));
        Assert.Equal("19.1.0", stack.Get(StackKind.React)?.Version);
        Assert.Equal("7.0.0", stack.Get(StackKind.Vite)?.Version);
        Assert.True(stack.Has(StackKind.TypeScript));
        Assert.Equal(Path.Combine("web", "package.json"), stack.Get(StackKind.React)?.Evidence);
    }

    [Fact]
    public void Detects_other_ecosystems_from_their_marker_files()
    {
        using var temp = new TempDirectory();
        temp.Write("engine/Cargo.toml", "[package]\nname = \"engine\"");
        temp.Write("api/go.mod", "module example.com/api\n\ngo 1.24\n");
        temp.Write("ml/pyproject.toml", "[project]\nname = \"ml\"");
        temp.Write("native/CMakeLists.txt", "cmake_minimum_required(VERSION 3.28)");
        temp.Write("contracts/foundry.toml", "[profile.default]");
        temp.Write("move/Move.toml", "[package]\nname = \"vault\"");
        temp.Write(".devcontainer/devcontainer.json", "{}");

        var stack = StackDetector.Detect(temp.Path);

        Assert.True(stack.Has(StackKind.Rust));
        Assert.Equal("1.24", stack.Get(StackKind.Go)?.Version);
        Assert.True(stack.Has(StackKind.Python));
        Assert.True(stack.Has(StackKind.Cpp));
        Assert.True(stack.Has(StackKind.Solidity));
        Assert.True(stack.Has(StackKind.Move));
        Assert.True(stack.Has(StackKind.DevContainer));
    }

    [Fact]
    public void Ignores_dependencies_folders()
    {
        using var temp = new TempDirectory();
        temp.Write("node_modules/some-lib/Cargo.toml", "[package]");

        Assert.False(StackDetector.Detect(temp.Path).Has(StackKind.Rust));
    }

    [Fact]
    public void Adds_ui_frameworks_from_loaded_projects()
    {
        using var temp = new TempDirectory();
        var projectPath = temp.Write("Admin/Admin.csproj");
        var wpf = new ProjectInfo { Name = "Admin", FilePath = projectPath, Kind = ProjectKind.Wpf };

        var stack = StackDetector.Detect(temp.Path, [wpf]);

        Assert.Equal(Path.Combine("Admin", "Admin.csproj"), stack.Get(StackKind.Wpf)?.Evidence);
        Assert.True(stack.Has(StackKind.DotNet));
    }

    [Fact]
    public void A_missing_directory_yields_an_empty_result()
    {
        var stack = StackDetector.Detect(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        Assert.Empty(stack.Components);
    }
}
