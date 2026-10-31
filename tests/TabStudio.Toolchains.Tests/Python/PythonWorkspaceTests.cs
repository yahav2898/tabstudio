using TabStudio.Testing;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Projects;
using TabStudio.Toolchains.Python;

namespace TabStudio.Toolchains.Tests.Python;

public sealed class PythonWorkspaceTests
{
    [Theory]
    [InlineData("backend", "backend", "", true)]
    [InlineData("packages/api", "packages/*", "", true)]
    [InlineData("packages/api", "packages/*", "packages/api", false)]
    [InlineData("unrelated", "backend", "", false)]
    public void F5_recognizes_transitive_server_dependencies_only_for_uv_workspace_members(string member, string pattern, string exclude, bool expected)
    {
        using var temp = new TempDirectory();
        var folder = Path.Combine(temp.Path, member);
        Directory.CreateDirectory(Path.Combine(folder, "app"));
        File.WriteAllText(Path.Combine(folder, "app/main.py"), "from fastapi import FastAPI\napp = FastAPI()\n");
        File.WriteAllText(Path.Combine(folder, "pyproject.toml"), "[project]\ndependencies = ['fastapi[standard]']\n");
        File.WriteAllText(Path.Combine(temp.Path, "pyproject.toml"), $"[tool.uv.workspace]\nmembers = ['{pattern}']\nexclude = ['{exclude}']\n");
        File.WriteAllText(Path.Combine(temp.Path, "uv.lock"), "[[package]]\nname = 'uvicorn'\nversion = '0.41.0'\nsource = { registry = 'https://pypi.org/simple' }\n");
        Assert.Equal(expected, PythonDependencies.DependsOn(folder, "uvicorn"));
        var project = new ToolchainProject(ToolchainCatalog.Default.Find("python")!, folder, null);
        Assert.Equal(expected, RunPlanner.PlanProject(project) is not null);
        File.WriteAllText(Path.Combine(folder, ".git"), "gitdir: elsewhere\n");
        Assert.False(PythonDependencies.DependsOn(folder, "uvicorn"));
    }
}
