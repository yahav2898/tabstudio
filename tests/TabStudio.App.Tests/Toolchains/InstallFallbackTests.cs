using TabStudio.App.ViewModels;
using TabStudio.Testing;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Projects;

namespace TabStudio.App.Tests.Toolchains;

/// <summary>
/// The second try a person makes when a package install fails for a reason that is not the packages': npm install
/// after npm ci, and a normal pip install after an editable one the project's build backend cannot do.
/// </summary>
public sealed class InstallFallbackTests
{
    [Fact]
    public void A_failed_npm_ci_is_followed_by_npm_install_with_the_same_options()
    {
        var install = WorkspaceViewModel.NpmInstallInstead(new ProcessStartSpec { Program = "npm", Arguments = ["ci", "--no-fund", "--no-audit"], WorkingDirectory = "/work/app" });

        Assert.NotNull(install);
        Assert.Equal(["install", "--no-fund", "--no-audit"], install.Arguments);
        Assert.Equal("/work/app", install.WorkingDirectory);
        Assert.Null(WorkspaceViewModel.NpmInstallInstead(new ProcessStartSpec { Program = "npm", Arguments = ["install"] }));
        Assert.Null(WorkspaceViewModel.NpmInstallInstead(new ProcessStartSpec { Program = "cargo", Arguments = ["ci"] }));
    }

    [Fact]
    public void An_editable_install_the_build_backend_cannot_do_is_followed_by_a_normal_one()
    {
        var pip = new ProcessStartSpec { Program = "/work/app/.venv/bin/python", Arguments = ["-m", "pip", "install", "--disable-pip-version-check", "-e", "."] };
        var failed = new OutputProcessResult(new ProcessExit(1, null, false),
            ["ERROR: Project file:///work/app uses a build backend that is missing the 'build_editable' hook, so it cannot be installed in editable mode."]);

        var plain = WorkspaceViewModel.NonEditable(pip, failed);

        Assert.NotNull(plain);
        Assert.Equal(["-m", "pip", "install", "--disable-pip-version-check", "."], plain.Arguments);
        Assert.Null(WorkspaceViewModel.NonEditable(pip, new OutputProcessResult(new ProcessExit(1, null, false), ["ERROR: No matching distribution found for asyncpg"])));
    }

    [Fact]
    public void An_application_its_build_backend_cannot_build_installs_its_lock_file_s_packages()
    {
        // fastapi-realworld: a Poetry project with no package of its own name to build.
        using var folder = new TempDirectory();
        folder.Write("pyproject.toml", "[tool.poetry]\nname = \"fastapi-realworld-example-app\"\n");
        folder.Write("poetry.lock", "[[package]]\nname = \"fastapi\"\nversion = \"0.79.0\"\n\n[[package]]\nname = \"asyncpg\"\nversion = \"0.26.0\"\n");
        Directory.CreateDirectory(folder.Resolve(".venv"));
        var project = new ToolchainProject(ToolchainCatalog.Default.Find("python")!, folder.Path, "pyproject.toml");
        var pip = new ProcessStartSpec { Program = folder.Resolve(".venv/bin/python"), Arguments = ["-m", "pip", "install", "--disable-pip-version-check", "."] };
        var failed = new OutputProcessResult(new ProcessExit(1, null, false),
            ["      poetry.core.masonry.utils.module.ModuleOrPackageNotFoundError: No file/folder found for package fastapi-realworld-example-app"]);

        var locked = WorkspaceViewModel.LockedInstead(project, pip, failed);

        Assert.NotNull(locked);
        Assert.Equal(["-m", "pip", "install", "--disable-pip-version-check", "-r", folder.Resolve(".venv/tabstudio-locked-requirements.txt")], locked.Arguments);
        var requirements = File.ReadAllLines(folder.Resolve(".venv/tabstudio-locked-requirements.txt"));
        Assert.Contains("fastapi==0.79.0", requirements);
        Assert.Contains("asyncpg==0.26.0", requirements);
        Assert.Null(WorkspaceViewModel.LockedInstead(project, pip, new OutputProcessResult(new ProcessExit(1, null, false), ["ERROR: No matching distribution found for asyncpg"])));
    }
}
