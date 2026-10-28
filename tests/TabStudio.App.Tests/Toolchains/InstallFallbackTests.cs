using TabStudio.App.ViewModels;
using TabStudio.Toolchains.Processes;

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
}
