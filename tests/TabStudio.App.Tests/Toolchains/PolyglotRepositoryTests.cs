using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.Theming;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Services;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Toolchains;

/// <summary>
/// Repositories like the ones people open from GitHub, with the computer's own toolchains: a Python project whose
/// dependency must be compiled, a Compose file with a missing bind source, an invalid service and an optional one,
/// and a Cargo project with a Makefile that only wraps Cargo.
/// </summary>
public sealed class PolyglotRepositoryTests
{
    private static readonly TimeSpan SetupTimeout = TimeSpan.FromMinutes(4);

    [RealServerAvaloniaFact("python3", "cc")]
    public async Task A_Python_repo_whose_dependency_fails_to_compile_says_why_in_one_message_and_installs_once_fixed()
    {
        using var fixture = new WorkspaceFixture();
        using var repo = new TempDirectory();
        repo.Write("requirements.txt", "./native\n");
        repo.Write("native/pyproject.toml", """
            [build-system]
            requires = ["setuptools>=61"]
            build-backend = "setuptools.build_meta"

            [project]
            name = "tabstudio-native"
            version = "1.0.0"
            """);
        repo.Write("native/setup.py", """
            from setuptools import Extension, setup

            setup(ext_modules=[Extension("tabstudio_native", ["native.c"], include_dirs=["include"])])
            """);
        repo.Write("native/native.c", """
            #include <Python.h>
            #include <tabstudio_answer.h>

            static PyObject *answer(PyObject *self, PyObject *args) { return PyLong_FromLong(TABSTUDIO_ANSWER); }
            static PyMethodDef methods[] = { {"answer", answer, METH_NOARGS, "The answer."}, {NULL, NULL, 0, NULL} };
            static struct PyModuleDef module = { PyModuleDef_HEAD_INIT, "tabstudio_native", NULL, -1, methods };
            PyMODINIT_FUNC PyInit_tabstudio_native(void) { return PyModule_Create(&module); }
            """);
        repo.Write("main.py", "import tabstudio_native\n\nprint('compiled answer', tabstudio_native.answer())\n");
        var workspace = fixture.Workspace;
        var window = Show(workspace);

        // The header the extension includes is missing: pip compiles it, and the build fails.
        await workspace.LoadAsync(repo.Path);
        await UiTestHelpers.WaitUntilAsync(() => workspace.ProjectSetupTask.IsCompleted && !workspace.IsSettingUpProjects, SetupTimeout, "pip install -r requirements.txt");

        var output = workspace.Output.Text;
        Assert.Contains("tabstudio-native could not be installed: it has no prebuilt wheel for Python 3.", output, StringComparison.Ordinal);
        Assert.Contains("so pip compiled it, and the C header tabstudio_answer.h was missing.", output, StringComparison.Ordinal);
        Assert.Contains("The full log is in Output > Project Setup.", output, StringComparison.Ordinal);
        Assert.Contains("pip install -r requirements.txt: failed", output, StringComparison.Ordinal);

        // pip's own report, dozens of lines from the build backend, is one tap away rather than in front.
        Assert.DoesNotContain("subprocess-exited-with-error", output, StringComparison.Ordinal);
        Assert.Contains("tabstudio_answer.h: No such file or directory", workspace.ProjectSetupOutput.Text, StringComparison.Ordinal);
        var notice = workspace.ToolchainNotice;
        Assert.NotNull(notice);
        Assert.Equal("tabstudio-native could not be installed", notice.Title);
        Assert.Equal("Show Full Log", notice.ActionText);
        UiTestHelpers.SaveScreenshot(window, "polyglot-01-pip-failure");
        await notice.Action.ExecuteAsync(null);
        Assert.Same(workspace.ProjectSetupOutput, workspace.ShownOutput);
        UiTestHelpers.SaveScreenshot(window, "polyglot-02-pip-full-log");

        // With the header there, the same install compiles it, and F5 runs the program that imports it.
        repo.Write("native/include/tabstudio_answer.h", "#define TABSTUDIO_ANSWER 42\n");
        workspace.SelectedOutputSource = workspace.OutputSources[0];
        await workspace.InstallProjectPackagesCommand.ExecuteAsync(null);
        output = workspace.Output.Text;
        Assert.Contains("pip install -r requirements.txt: succeeded", output, StringComparison.Ordinal);
        Assert.Contains("Installed 1 package into .venv", output, StringComparison.Ordinal);
        Assert.Null(workspace.ToolchainNotice);

        await workspace.RunCommand.ExecuteAsync(null);
        Assert.Contains("compiled answer 42", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Contains("The program 'main.py' has exited with code 0", workspace.Output.Text, StringComparison.Ordinal);
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_Compose_file_with_a_missing_bind_source_an_invalid_and_an_optional_service_starts_nothing_by_itself()
    {
        using var fixture = new WorkspaceFixture();
        using var repo = new TempDirectory();
        repo.Write("docker-compose.yml", """
            services:
              mysql:
                image: mysql:9.7
                ports: ["3306:3306"]
                environment: [MYSQL_ROOT_PASSWORD=, MYSQL_ALLOW_EMPTY_PASSWORD=true, MYSQL_DATABASE=petclinic]
                volumes: ["./conf.d:/etc/mysql/conf.d:ro"]
              postgres:
                image: postgres:18
                ports: ["5432:5432"]
                environment: [POSTGRES_PASSWORD=petclinic]
              search:
                image: redis:7
                volumes: [{type: tmpfs, target: /data}]
              cache:
                image: redis:7
                profiles: [cache]
            """);
        repo.Write("main.py", "print('PetClinic on H2')\n");
        var workspace = fixture.Workspace;
        var window = Show(workspace);

        await workspace.LoadAsync(repo.Path);
        var services = workspace.Services;
        Assert.Equal(["mysql", "postgres", "search", "cache"], services.Items.Select(i => i.Name));

        // One service's unsupported setting is that service's error, not the file's.
        Assert.False(services.HasError);
        var search = services.Items.Single(i => i.Name == "search");
        Assert.True(search.IsUnsupported);
        Assert.Contains("tmpfs", search.Detail, StringComparison.Ordinal);
        Assert.All(services.Items.Where(i => i.Name != "search"), i => Assert.True(i.CanStart));

        // Nothing is downloaded or started on open, nor by F5 of a program that points at no service.
        Assert.True(services.HasOffer);
        Assert.All(services.Items, i => Assert.False(i.Status.IsRunning));
        await workspace.RunCommand.ExecuteAsync(null);
        Assert.Contains("PetClinic on H2", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Empty(services.LastNeeded);
        Assert.All(services.Items, i => Assert.True(i.Status.State is ServiceState.Stopped or ServiceState.Unsupported, $"{i.Name}: {i.Status.State}"));
        Assert.False(Directory.Exists(repo.Resolve("conf.d")));
        Assert.False(services.HasOperationError);
        workspace.SelectSideBarPanelCommand.Execute(SideBarPanel.Services);
        UiTestHelpers.SaveScreenshot(window, "polyglot-03-services-offered");

        // A start that fails stays on its row: the file is still read, and the other services are untouched.
        var mysql = services.Items.Single(i => i.Name == "mysql");
        await mysql.StartCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => mysql.Status.State is not (ServiceState.Stopped or ServiceState.Installing or ServiceState.Starting), because: "mysql's start to end");
        Assert.False(services.HasError);
        Assert.True(services.HasOperationError);
        Assert.StartsWith("mysql:", services.OperationError, StringComparison.Ordinal);
        Assert.NotEqual(ServiceState.Stopped, mysql.Status.State);
        Assert.True(mysql.HasDetail);
        Assert.Equal(ServiceState.Stopped, services.Items.Single(i => i.Name == "postgres").Status.State);
        Assert.True(services.HasServices);
        UiTestHelpers.SaveScreenshot(window, "polyglot-04-service-error-on-its-row");
        window.Close();
    }

    [RealServerAvaloniaFact("cargo")]
    public async Task A_Cargo_repo_with_a_wrapper_Makefile_runs_its_binary_with_Cargo_progress_in_ordinary_colors()
    {
        using var fixture = new WorkspaceFixture();
        using var repo = new TempDirectory();
        repo.Write("Cargo.toml", "[package]\nname = \"finder-cli\"\nversion = \"0.1.0\"\nedition = \"2021\"\n\n[[bin]]\nname = \"finder\"\npath = \"src/main.rs\"\n");
        repo.Write("src/main.rs", "fn main() {\n    let unused = 1;\n    println!(\"finder ran\");\n}\n");
        repo.Write("Makefile", "PROFILE?=release\nEXE=target/$(PROFILE)/finder\n\n$(EXE): Cargo.toml src/**/*.rs\n\tcargo build --profile $(PROFILE) --locked\n\ninstall: $(EXE)\n\tinstall -Dm755 $(EXE) $(DESTDIR)/usr/local/bin/finder\n");
        var workspace = fixture.Workspace;
        var window = Show(workspace);

        await workspace.LoadAsync(repo.Path);

        // The Makefile only wraps Cargo: the repository is one Rust project, and it is the startup project.
        var project = Assert.Single(workspace.ToolchainProjects);
        Assert.Equal("rust", project.Toolchain.Id);
        Assert.Equal(project.Info.FilePath, workspace.StartupProject?.FilePath);

        await workspace.RunCommand.ExecuteAsync(null);

        var output = workspace.Output;
        var text = output.Text;
        Assert.Contains("finder ran", text, StringComparison.Ordinal);
        Assert.Contains("The program 'finder' has exited with code 0", text, StringComparison.Ordinal);
        Assert.DoesNotContain("'Makefile'", text, StringComparison.Ordinal);
        Assert.DoesNotContain("no unique executable", text, StringComparison.Ordinal);

        // Cargo writes "Compiling" to standard error; it is ordinary output. Its warning is a warning.
        var compiling = text.IndexOf("Compiling finder-cli", StringComparison.Ordinal);
        Assert.True(compiling >= 0, text);
        Assert.DoesNotContain(output.Segments, s => s.Start <= compiling && compiling < s.End && ReferenceEquals(s.Value, ThemeBrushes.Get("Brush.Output.Error")));
        var warning = text.IndexOf("warning: unused variable", StringComparison.Ordinal);
        Assert.True(warning >= 0, text);
        Assert.Contains(output.Segments, s => s.Start <= warning && warning < s.End && ReferenceEquals(s.Value, ThemeBrushes.Get("Brush.Output.Warning")));
        UiTestHelpers.SaveScreenshot(window, "polyglot-05-cargo-output");
        window.Close();
    }

    private static Window Show(WorkspaceViewModel workspace)
    {
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = workspace } };
        window.Show();
        return window;
    }
}
