using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Services;
using TabStudio.App.Services.Toolchains;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.LanguageServers.Hosting;
using TabStudio.LanguageServers.Loopback;
using TabStudio.Testing;
using TabStudio.Toolchains.Runtimes;
using TabStudio.Toolchains.Tests.Fakes;
using static TabStudio.App.Tests.Views.EditorHarness;

namespace TabStudio.App.Tests.Toolchains;

/// <summary>
/// A missing language server or debug adapter on a fake tablet with a fake Alpine mirror: the status bar, the
/// IntelliSense commands and F5 offer to install the toolchain that brings it, the Store installs it, and Pyright
/// starts by itself for the open file once Python is installed.
/// </summary>
public sealed class ToolchainServerInstallTests : IDisposable
{
    private readonly FakeDevice _device = FakeDevice.WithPythonAndNode();
    private readonly TempDirectory _temp = new();
    private readonly ToolchainServices _toolchains;
    private readonly TabletLauncher _launcher;
    private readonly ServiceProvider _services;
    private readonly MainViewModel _main;

    public ToolchainServerInstallTests()
    {
        // The fake mirror's Python has no setup steps; on a tablet, one installs Pyright with Python.
        _device.Manager.StatusChanged += (_, status) =>
        {
            if (status.Toolchain.Id == "python" && status.IsReady)
            {
                var server = _device.Rootfs("/usr/local/bin/pyright-langserver");
                Directory.CreateDirectory(Path.GetDirectoryName(server)!);
                File.WriteAllText(server, "#!/bin/sh\n");
            }
        };
        _toolchains = new ToolchainServices(_device.Manager, _device.Platform, _device.Environment);
        _launcher = new TabletLauncher(_toolchains.Runtime);
        // These tests are about the offers, which opening a project makes when it does not set itself up.
        new ProjectSetupStore(new AppPaths(_temp.Path)).Save(ProjectSetupSettings.Default with { SetUpProjectsWhenTheyOpen = false });
        _services = AppServices.Create(new AppPaths(_temp.Path), services =>
        {
            services.AddSingleton(_toolchains);
            services.AddSingleton<IServerProcessLauncher>(_launcher);
        });
        _main = _services.GetRequiredService<MainViewModel>();
    }

    [AvaloniaFact]
    public async Task A_missing_server_offers_its_toolchain_and_starts_by_itself_once_it_is_installed()
    {
        var window = Show();
        var (workspace, document) = await OpenPythonAppAsync(CreatePythonApp("Greeter"));
        var before = ActiveEditor(window);

        await UiTestHelpers.WaitUntilAsync(() => workspace.ToolchainNotice?.Title == "Greeter needs Python", because: "the project's offer");
        await UiTestHelpers.WaitUntilAsync(() => workspace.ServerInstallOffer is not null, because: "the status bar's offer");
        Assert.False(document.HasLanguageServer);
        Assert.Equal("Pyright not installed", workspace.LanguageServerText);
        Assert.Equal("Install Python", workspace.ServerInstallOffer!.ActionText);
        Assert.Equal("Install Pyright", workspace.ServerInstallOffer.StatusBarText);
        Assert.False(workspace.ShowsLanguageServerText);
        Assert.StartsWith("Pyright comes with Python from the Toolchain Store (about ", workspace.ServerInstallOffer.ToolTip, StringComparison.Ordinal);

        // A command that needs IntelliSense says why it cannot work, and offers the install over the editor.
        await workspace.GoToDefinitionCommand.ExecuteAsync(null);
        Assert.Equal("This needs Pyright, which comes with Python from the Toolchain Store.", workspace.StatusText);
        Assert.True(workspace.HasToolchainNotice);
        Assert.Equal("Python IntelliSense needs Pyright", workspace.ToolchainNotice!.Title);
        Assert.Equal("Install Python", workspace.ToolchainNotice.ActionText);
        await UiTestHelpers.WaitUntilAsync(() => true);
        UiTestHelpers.SaveScreenshot(window, "toolchains-lsp-04-install-offer");

        await workspace.InstallServerToolchainCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => document.LanguageServer?.ServerState == LanguageServerState.Running, because: "Pyright to start by itself");

        Assert.True(_device.Manager.IsReady("python"));
        Assert.False(workspace.ToolchainStore.IsOpen);
        Assert.Equal("Pyright ready", workspace.LanguageServerText);
        Assert.Null(workspace.ServerInstallOffer);
        Assert.True(workspace.ShowsLanguageServerText);
        var started = Assert.Single(_launcher.Started);
        Assert.Equal(("/usr/local/bin/pyright-langserver", "--stdio"), (started.FileName, started.Arguments[0]));
        var after = ActiveEditor(window);
        Assert.NotSame(before, after);
        Assert.Null(before.InlayHints);
        Assert.NotNull(after.InlayHints);
    }

    [AvaloniaFact]
    public async Task Installing_from_the_Store_clears_the_status_bar_message_that_the_server_is_missing()
    {
        // The message stayed after Python was installed from Tools > Toolchain Store, next to "Pyright ready".
        Show();
        var (workspace, document) = await OpenPythonAppAsync(CreatePythonApp("StoreGreeter"));
        await UiTestHelpers.WaitUntilAsync(() => workspace.ServerInstallOffer is not null, because: "the status bar's offer");
        await UiTestHelpers.WaitUntilAsync(() => workspace.StatusText.Contains("which is not installed", StringComparison.Ordinal), because: "the status bar to say Pyright is missing");

        await workspace.ToolchainStore.InstallAsync("python");
        await UiTestHelpers.WaitUntilAsync(() => document.LanguageServer?.ServerState == LanguageServerState.Running, because: "Pyright to start by itself");

        Assert.DoesNotContain("not installed", workspace.StatusText, StringComparison.Ordinal);
        Assert.Equal("Pyright ready", workspace.LanguageServerText);
    }

    [AvaloniaFact]
    public async Task F5_offers_to_install_the_toolchain_of_a_missing_debug_adapter_and_then_starts_it()
    {
        var window = Show();
        var folder = CreatePythonApp("Scripts");
        Directory.CreateDirectory(Path.Combine(folder, ".tabstudio"));
        File.WriteAllText(Path.Combine(folder, ".tabstudio", "launch.json"), """
            { "configurations": [ { "name": "Python: main", "type": "debugpy", "request": "launch", "program": "${workspaceFolder}/main.py" } ] }
            """);
        var (workspace, _) = await OpenPythonAppAsync(folder);
        var configuration = Assert.Single(workspace.LanguageServers!.Configuration.LaunchConfigurations);
        workspace.SelectLaunchTarget(new LaunchTarget(configuration.Name, configuration, IsCurrentFile: false));
        Assert.Equal("Python: main", workspace.RunLabel);

        var debugging = workspace.StartDebuggingCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.Prompt.IsOpen, because: "the offer to install Python");
        Assert.Equal("Install Python?", workspace.Prompt.Title);
        Assert.StartsWith("Debugging needs debugpy, which comes with Python from the Toolchain Store. Install it now? It downloads about ", workspace.Prompt.Message, StringComparison.Ordinal);
        UiTestHelpers.SaveScreenshot(window, "toolchains-lsp-05-debug-adapter-install");

        workspace.Prompt.ConfirmCommand.Execute(null);
        await debugging;

        Assert.True(_device.Manager.IsReady("python"));
        var adapter = Assert.Single(_launcher.Started, s => s.Arguments.Contains("debugpy.adapter"));
        Assert.Equal(("/usr/bin/python3", "-m"), (adapter.FileName, adapter.Arguments[0]));
        Assert.Contains("Python (debugpy) did not start: The fake tablet runs no programs.", workspace.Output.Text, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        _main.Workspace?.Dispose();
        _services.Dispose();
        _temp.Dispose();
        _device.Dispose();
    }

    private MainWindow Show()
    {
        var window = new MainWindow { DataContext = _main, Width = 1280, Height = 800 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return window;
    }

    private string CreatePythonApp(string name) => _services.GetRequiredService<ProjectTemplates>().Create(ProjectTemplates.PythonId, name);

    private async Task<(WorkspaceViewModel Workspace, DocumentViewModel Document)> OpenPythonAppAsync(string folder)
    {
        await _main.OpenWorkspaceAsync(folder);
        var workspace = _main.Workspace!;
        await UiTestHelpers.WaitUntilAsync(() => workspace.ActiveDocument?.FileName == "main.py", because: "main.py");
        return (workspace, workspace.ActiveDocument!);
    }

    /// <summary>
    /// The tablet's launcher, finding programs in the fake Linux environment; its PRoot is a stand-in that runs
    /// nothing, so the loopback server stands in for Pyright, and other programs fail to start.
    /// </summary>
    private sealed class TabletLauncher(IToolchainRuntime runtime) : IServerProcessLauncher
    {
        private readonly ToolchainServerLauncher _store = new(runtime);
        private readonly LoopbackLauncher _loopback = new(ProcessServerLauncher.Instance);

        public List<ServerStartInfo> Started { get; } = [];

        public string? Resolve(string command) => _store.Resolve(command);

        public IServerProcess Start(ServerStartInfo startInfo)
        {
            Started.Add(startInfo);
            return startInfo.FileName.EndsWith("/pyright-langserver", StringComparison.Ordinal)
                ? _loopback.Start(startInfo with { FileName = LoopbackLauncher.LanguageServerCommand })
                : throw new ServerStartException("The fake tablet runs no programs.");
        }

        public string? ToLocalPath(string programPath) => _store.ToLocalPath(programPath);
    }
}
