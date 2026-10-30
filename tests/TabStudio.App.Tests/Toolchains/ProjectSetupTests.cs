using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TabStudio.App.Services;
using TabStudio.App.Services.Toolchains;
using TabStudio.App.Services.Search;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.App.ViewModels.Search;
using TabStudio.App.Views;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Toolchains;

/// <summary>
/// Opening a repository sets its projects up by itself, with the computer's own Python, node and npm as the
/// toolchains (CI's runners have them): the packages install once, again when the manifest changes, and F5 runs.
/// </summary>
public sealed class ProjectSetupTests
{
    private static readonly TimeSpan SetupTimeout = TimeSpan.FromMinutes(3);

    [AvaloniaFact]
    public async Task Opening_a_Python_package_in_src_installs_it_into_its_venv_and_F5_runs_it()
    {
        using var fixture = new WorkspaceFixture();
        using var repo = new TempDirectory();
        repo.Write("pyproject.toml", """
            [build-system]
            requires = ["setuptools>=61"]
            build-backend = "setuptools.build_meta"

            [project]
            name = "greeter"
            version = "1.2.0"
            """);
        repo.Write("src/greeter/__init__.py", "def greet(name):\n    return f'Hello, {name}, from an installed package!'\n");
        repo.Write("src/greeter/__main__.py", "import importlib.metadata\n\nfrom greeter import greet\n\nprint(greet('Ada'))\nprint('greeter', importlib.metadata.version('greeter'))\n");
        repo.Write("tests/test_greeter.py", "from greeter import greet\n\n\ndef test_greet():\n    assert 'Ada' in greet('Ada')\n");
        var workspace = fixture.Workspace;
        Assert.True(workspace.SetUpProjectsWhenTheyOpen);
        var window = Show(workspace);

        await workspace.LoadAsync(repo.Path);
        await UiTestHelpers.WaitUntilAsync(() => workspace.ProjectSetupText?.StartsWith("Setting up", StringComparison.Ordinal) == true || workspace.ProjectSetupTask.IsCompleted, because: "the setup to start");
        Assert.True(workspace.IsSettingUpProjects, workspace.Output.Text);
        Assert.Equal($"Setting up {Path.GetFileName(repo.Path)}: pip install -e .", workspace.ProjectSetupText);
        Assert.Null(workspace.ToolchainNotice);
        await UiTestHelpers.WaitUntilAsync(() => workspace.Output.Text.Contains("pip install", StringComparison.Ordinal), because: "pip in the Output");
        UiTestHelpers.SaveScreenshot(window, "open-any-repo-02-setup-progress");
        await UiTestHelpers.WaitUntilAsync(() => workspace.ProjectSetupTask.IsCompleted, SetupTimeout, "pip install -e .");

        var output = workspace.Output.Text;
        Assert.Contains("venv .venv", output, StringComparison.Ordinal);
        Assert.Contains("pip install -e .: succeeded", output, StringComparison.Ordinal);
        Assert.True(Directory.Exists(repo.Resolve(".venv")));
        Assert.False(workspace.IsSettingUpProjects);
        Assert.Null(workspace.ToolchainNotice);

        await workspace.RunCommand.ExecuteAsync(null);

        output = workspace.Output.Text;
        Assert.Contains("Running src/greeter/__main__.py with Python", output, StringComparison.Ordinal);
        Assert.Contains("Hello, Ada, from an installed package!", output, StringComparison.Ordinal);
        Assert.Contains("greeter 1.2.0", output, StringComparison.Ordinal);
        Assert.Contains("has exited with code 0", output, StringComparison.Ordinal);
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_Node_repo_installs_its_packages_once_and_again_when_package_json_changes()
    {
        using var fixture = new WorkspaceFixture();
        using var repo = new TempDirectory();
        repo.Write("package.json", """{ "name": "local", "main": "index.js", "dependencies": { "greeting": "file:./greeting" } }""");
        repo.Write("greeting/package.json", """{ "name": "greeting", "version": "1.0.0", "main": "index.js" }""");
        repo.Write("greeting/index.js", "module.exports = (name) => `Hi, ${name}!`;\n");
        repo.Write("shout/package.json", """{ "name": "shout", "version": "1.0.0", "main": "index.js" }""");
        repo.Write("shout/index.js", "module.exports = (text) => text.toUpperCase();\n");
        repo.Write("index.js", "console.log(require('greeting')('npm'));\n");
        var workspace = fixture.Workspace;

        await workspace.LoadAsync(repo.Path);
        // The person looks at the Error List while the packages install in the background: setup leaves it there.
        workspace.SelectPanelCommand.Execute(BottomPanel.ErrorList);
        await UiTestHelpers.WaitUntilAsync(() => workspace.ProjectSetupTask.IsCompleted, SetupTimeout, "npm install");
        Assert.Equal(BottomPanel.ErrorList, workspace.SelectedPanel);

        Assert.Contains($"------ Setting up {Path.GetFileName(repo.Path)}: npm install ------", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Contains("npm install: succeeded", workspace.Output.Text, StringComparison.Ordinal);
        Assert.True(Directory.Exists(repo.Resolve("node_modules/greeting")));
        Assert.Null(workspace.ToolchainNotice);
        await workspace.RunCommand.ExecuteAsync(null);
        Assert.Contains("Hi, npm!", workspace.Output.Text, StringComparison.Ordinal);

        // Opening it again does not install again: the manifest has not changed.
        using (var again = new WorkspaceViewModel(fixture.Compiler, fixture.DeviceStatus, fixture.Profile, new AvaloniaUiDispatcher(), fixture.Sessions, fixture.Paths))
        {
            await again.LoadAsync(repo.Path);
            await UiTestHelpers.WaitUntilAsync(() => again.ProjectSetupTask.IsCompleted, SetupTimeout, "the setup to find nothing to do");
            Assert.DoesNotContain("Setting up", again.Output.Text, StringComparison.Ordinal);
        }

        // Saving a changed package.json sets the project up again.
        var manifest = (await workspace.OpenDocumentAsync(repo.Resolve("package.json")))!;
        var text = manifest.Document.Text;
        var at = text.IndexOf("\"file:./greeting\"", StringComparison.Ordinal);
        manifest.Document.Insert(at + "\"file:./greeting\"".Length, ", \"shout\": \"file:./shout\"");
        await workspace.SaveCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.ProjectSetupTask.IsCompleted && Directory.Exists(repo.Resolve("node_modules/shout")), SetupTimeout, "npm install after the save");
        Assert.Equal(1, Occurrences(workspace.Output.Text, $"------ Setting up {Path.GetFileName(repo.Path)}: npm install ------"));
        Assert.Equal(1, Occurrences(workspace.Output.Text, "npm install: succeeded"));
    }

    [AvaloniaFact]
    public async Task An_offline_SSH_cache_never_installs_or_runs_its_projects_locally()
    {
        using var fixture = new WorkspaceFixture();
        using var repo = new TempDirectory();
        repo.Write(".tabstudio/remote-workspace.txt", "development");
        repo.Write("package.json", """
            {
              "name": "remote-cache",
              "main": "index.js",
              "dependencies": { "local-only": "file:./dependency" },
              "scripts": { "postinstall": "node postinstall.js" }
            }
            """);
        repo.Write("dependency/package.json", """{ "name": "local-only", "version": "1.0.0", "main": "index.js" }""");
        repo.Write("dependency/index.js", "module.exports = 'local dependency';");
        repo.Write("postinstall.js", "require('node:fs').writeFileSync('install-marker.txt', 'installed locally');");
        repo.Write("index.js", "require('node:fs').writeFileSync('run-marker.txt', 'ran locally');");
        repo.Write("python/requirements.txt", "-e ./local-package\n");
        repo.Write("python/local-package/setup.py", "from setuptools import setup\nsetup(name='remote-cache-only', version='1.0.0')\n");
        repo.Write("python/main.py", "from pathlib import Path\nPath('python-run-marker.txt').write_text('ran locally')\n");
        repo.Write("capacitor.config.json", "{}");
        var workspace = fixture.Workspace;
        Assert.True(workspace.SetUpProjectsWhenTheyOpen);

        await workspace.LoadAsync(repo.Path);
        await workspace.ProjectSetupTask.WaitAsync(SetupTimeout);
        Assert.True(workspace.IsRemoteWorkspace);
        Assert.Contains(workspace.ToolchainProjects, item => item.Project.Directory == repo.Path);
        Assert.Contains(workspace.ToolchainProjects, item => item.Project.Directory == repo.Resolve("python"));
        Assert.False(workspace.IsSettingUpProjects);
        Assert.Null(workspace.ToolchainNotice);

        // Changing the preference must not schedule a second, local setup of the cached manifests.
        workspace.SetUpProjectsWhenTheyOpen = false;
        workspace.SetUpProjectsWhenTheyOpen = true;
        await workspace.ProjectSetupTask.WaitAsync(SetupTimeout);
        await workspace.OpenDocumentAsync(repo.Resolve("index.js"));
        Assert.False(workspace.RunCurrentFileCommand.CanExecute(null));
        Assert.False(workspace.InstallProjectPackagesCommand.CanExecute(null));

        // ExecuteAsync also exercises callers that bypass the command's disabled UI state.
        await workspace.RunCurrentFileCommand.ExecuteAsync(null);
        Assert.Contains("remote run profile", workspace.StatusText, StringComparison.Ordinal);
        await workspace.InstallProjectPackagesCommand.ExecuteAsync(null);
        Assert.Contains("remote run profile", workspace.StatusText, StringComparison.Ordinal);
        await workspace.BuildAndroidCommand.ExecuteAsync(null);
        Assert.Contains("remote run profile", workspace.StatusText, StringComparison.Ordinal);
        await workspace.OpenDocumentAsync(repo.Resolve("python/main.py"));
        await workspace.RunCurrentFileCommand.ExecuteAsync(null);
        Assert.Contains("remote run profile", workspace.StatusText, StringComparison.Ordinal);

        Assert.False(Directory.Exists(repo.Resolve(".venv")));
        Assert.False(Directory.Exists(repo.Resolve("python/.venv")));
        Assert.Empty(Directory.EnumerateDirectories(repo.Path, "node_modules", SearchOption.AllDirectories));
        Assert.False(File.Exists(repo.Resolve("install-marker.txt")));
        Assert.False(File.Exists(repo.Resolve("run-marker.txt")));
        Assert.False(File.Exists(repo.Resolve("python/python-run-marker.txt")));
        Assert.DoesNotContain("------ Setting up", workspace.Output.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("> ", workspace.Output.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task The_setting_is_in_the_Tools_menu_and_is_kept_for_the_next_workspace()
    {
        using var fixture = new WorkspaceFixture();
        var workspace = fixture.Workspace;
        await workspace.LoadAsync(new ProjectTemplates(fixture.Paths).Create(ProjectTemplates.NodeId, "Greeter"));
        var window = Show(workspace);
        var tools = window.GetVisualDescendants().OfType<MenuItem>().Single(m => m.Header as string == "T_ools");
        tools.IsSubMenuOpen = true;
        Dispatcher.UIThread.RunJobs();
        var setting = tools.Items.OfType<MenuItem>().Single(m => m.Header as string == "Set Projects Up When They _Open");

        Assert.True(setting.IsChecked);
        SaveWithPopups(window, "open-any-repo-01-setting", setting);
        setting.IsChecked = false;
        tools.Close();

        Assert.False(workspace.SetUpProjectsWhenTheyOpen);
        Assert.False(new ProjectSetupStore(fixture.Paths).Load().SetUpProjectsWhenTheyOpen);
        using var next = new WorkspaceViewModel(fixture.Compiler, fixture.DeviceStatus, fixture.Profile, new AvaloniaUiDispatcher(), fixture.Sessions, fixture.Paths);
        Assert.False(next.SetUpProjectsWhenTheyOpen);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Ctrl_Q_finds_both_setup_settings_and_changes_the_persisted_value()
    {
        using var fixture = new WorkspaceFixture();
        var workspace = fixture.Workspace;
        workspace.Search.OpenCommand.Execute(null);
        workspace.Search.Query = "install packages";
        workspace.Search.ChooseScopeCommand.Execute(workspace.Search.Scopes.Single(s => s.Kind == SearchKind.Setting));
        await workspace.Search.Completion;
        var off = workspace.Search.Rows.OfType<SearchResultRow>().Single(r => r.Title == "Set projects up when they open: Off");
        await workspace.Search.AcceptCommand.ExecuteAsync(off);
        Assert.False(workspace.SetUpProjectsWhenTheyOpen);
        Assert.False(new ProjectSetupStore(fixture.Paths).Load().SetUpProjectsWhenTheyOpen);
        workspace.Search.OpenCommand.Execute(null);
        workspace.Search.Query = "setup";
        await workspace.Search.Completion;
        var on = workspace.Search.Rows.OfType<SearchResultRow>().Single(r => r.Title == "Set projects up when they open: On");
        await workspace.Search.AcceptCommand.ExecuteAsync(on);
        Assert.True(workspace.SetUpProjectsWhenTheyOpen);
    }

    [AvaloniaFact]
    public async Task Workspace_members_share_one_install_and_a_manifest_saved_during_it_is_not_lost()
    {
        using var fixture = new WorkspaceFixture();
        using var repo = new TempDirectory();
        repo.Write("package.json", """{ "name": "mono", "private": true, "workspaces": ["packages/*"], "scripts": { "preinstall": "node setup.js" } }""");
        repo.Write("packages/app/package.json", """{ "name": "app", "version": "1.0.0", "main": "index.js" }""");
        repo.Write("packages/app/index.js", "console.log('workspace app ran');");
        repo.Write("setup.js", """
            const fs = require('node:fs');
            fs.appendFileSync('installs.txt', 'run\n');
            if (!fs.existsSync('release.txt')) {
              const timer = setInterval(() => {
                if (fs.existsSync('release.txt')) clearInterval(timer);
              }, 25);
            }
            """);
        var workspace = fixture.Workspace;
        await workspace.LoadAsync(repo.Path);
        try
        {
            Assert.Contains(workspace.Workspace!.Tree.DescendantsAndSelf(), node => node.FullPath == repo.Resolve("packages/app/package.json"));
            await UiTestHelpers.WaitUntilAsync(() => File.Exists(repo.Resolve("installs.txt")), SetupTimeout, "npm's preinstall to start");
            var manifest = (await workspace.OpenDocumentAsync(repo.Resolve("packages/app/package.json")))!;
            var at = manifest.Document.Text.IndexOf("1.0.0", StringComparison.Ordinal);
            manifest.Document.Replace(at, 5, "1.0.1");
            await workspace.SaveCommand.ExecuteAsync(null);
        }
        finally
        {
            repo.Write("release.txt");
        }

        await UiTestHelpers.WaitUntilAsync(() => workspace.ProjectSetupTask.IsCompleted, SetupTimeout, "both setup passes");
        Assert.Equal(2, File.ReadAllLines(repo.Resolve("installs.txt")).Length);
        Assert.Equal(2, Occurrences(workspace.Output.Text, $"------ Setting up {Path.GetFileName(repo.Path)}:"));
        Assert.DoesNotContain("------ Setting up app:", workspace.Output.Text, StringComparison.Ordinal);
        var member = workspace.StartupProjects.Single(p => p.Project.Name == "app");
        workspace.SetStartupProject(member.Project);
        await workspace.RunCommand.ExecuteAsync(null);
        Assert.Contains("workspace app ran", workspace.Output.Text, StringComparison.Ordinal);

        using var again = new WorkspaceViewModel(fixture.Compiler, fixture.DeviceStatus, fixture.Profile, new AvaloniaUiDispatcher(), fixture.Sessions, fixture.Paths);
        await again.LoadAsync(repo.Path);
        await UiTestHelpers.WaitUntilAsync(() => again.ProjectSetupTask.IsCompleted, SetupTimeout, "unchanged workspace setup");
        Assert.Equal(2, File.ReadAllLines(repo.Resolve("installs.txt")).Length);
    }

    private static Window Show(WorkspaceViewModel workspace)
    {
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = workspace } };
        window.Show();
        return window;
    }

    private static int Occurrences(string text, string value)
    {
        var count = 0;
        for (var at = text.IndexOf(value, StringComparison.Ordinal); at >= 0; at = text.IndexOf(value, at + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    /// <summary>Saves the window with the menus that are open over it, which are popups of their own.</summary>
    private static void SaveWithPopups(Window window, string name, params Control[] insidePopups)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("Nothing was rendered.");
        using var target = new RenderTargetBitmap(frame.PixelSize);
        using (var context = target.CreateDrawingContext())
        {
            context.DrawImage(frame, new Rect(frame.Size));
            var origin = window.PointToScreen(default);
            foreach (var control in insidePopups)
            {
                if (TopLevel.GetTopLevel(control) is { } popup && !ReferenceEquals(popup, window) && popup.CaptureRenderedFrame() is { } image)
                {
                    using (image)
                    {
                        var corner = popup.PointToScreen(default);
                        context.DrawImage(image, new Rect(new Point(corner.X - origin.X, corner.Y - origin.Y), image.Size));
                    }
                }
            }
        }

        var folder = Environment.GetEnvironmentVariable("TABSTUDIO_SCREENSHOTS") ?? Path.Combine(AppContext.BaseDirectory, "screenshots");
        Directory.CreateDirectory(folder);
        target.Save(Path.Combine(folder, name + ".png"));
    }
}
