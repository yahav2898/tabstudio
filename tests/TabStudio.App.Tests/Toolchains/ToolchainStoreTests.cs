using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Services;
using TabStudio.App.Services.Toolchains;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Core.Compatibility;
using TabStudio.Testing;
using TabStudio.Toolchains.Installation;
using TabStudio.Toolchains.Tests.Fakes;

namespace TabStudio.App.Tests.Toolchains;

/// <summary>
/// The Toolchain Store in the IDE, on a fake tablet with a fake Alpine mirror: a Python project offers Python, the
/// Store installs it with progress, the Compatibility Doctor turns ready, and the Store removes it again.
/// </summary>
public sealed class ToolchainStoreTests : IDisposable
{
    private readonly FakeDevice _device = FakeDevice.WithPythonAndNode();
    private readonly TempDirectory _temp = new();
    private readonly ToolchainServices _toolchains;
    private readonly ServiceProvider _services;
    private readonly MainViewModel _main;
    private readonly AppPaths _paths;

    public ToolchainStoreTests()
    {
        _toolchains = new ToolchainServices(_device.Manager, _device.Platform, _device.Environment);
        _paths = new AppPaths(_temp.Path);

        // These tests are about the offers, which opening a project makes when it does not set itself up.
        new ProjectSetupStore(_paths).Save(ProjectSetupSettings.Default with { SetUpProjectsWhenTheyOpen = false });
        _services = AppServices.Create(_paths, services => services.AddSingleton(_toolchains));
        _main = _services.GetRequiredService<MainViewModel>();
    }

    [AvaloniaFact]
    public async Task Installs_and_removes_a_toolchain()
    {
        var window = Show(1280, 800);
        var workspace = await OpenPythonAppAsync();

        // The project needs Python: the Compatibility Doctor and an offer say so.
        await UiTestHelpers.WaitUntilAsync(() => workspace.ToolchainNotice is not null, because: "the offer to install Python");
        var row = Assert.Single(workspace.Compatibility, r => r.ToolchainId == "python");
        Assert.Equal(CompatibilityStatus.NeedsToolchain, row.Report.Status);
        Assert.Equal("Install Python", row.InstallToolchainText);
        Assert.Equal("Install Python", workspace.ToolchainNotice!.ActionText);

        workspace.ShowToolchainStoreCommand.Execute(null);
        var store = workspace.ToolchainStore;
        Assert.True(store.IsOpen);
        Assert.Equal(["Python", "Node.js"], store.Items.Select(i => i.Name));
        var python = store.Items[0];
        Assert.True(python.IsHighlighted);
        Assert.True(python.ShowsInstall);
        Assert.Equal("Downloads about 48 MB · 185 MB installed", python.SizeText);
        await UiTestHelpers.WaitUntilAsync(() => true);
        UiTestHelpers.SaveScreenshot(window, "30-toolchain-store");

        // Installing, slowly enough to watch.
        _device.Mirror.Server.Latency = TimeSpan.FromMilliseconds(500);
        var install = python.InstallCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => python.Status.Progress?.Stage == InstallStage.Downloading && python.Progress > 12, because: "the download");
        Assert.True(python.IsBusy);
        Assert.True(python.ShowsCancel);
        Assert.False(python.ShowsInstall);
        UiTestHelpers.SaveScreenshot(window, "31-toolchain-installing");

        _device.Mirror.Server.Latency = TimeSpan.Zero;
        await install;
        await UiTestHelpers.WaitUntilAsync(() => python.IsInstalled, because: "the install");
        Assert.Equal("3.14.8", python.VersionText);
        Assert.Equal("Uses 146 MB", python.SizeText);
        Assert.True(python.ShowsRemove);
        Assert.True(File.Exists(_device.Rootfs("/usr/bin/python3")));
        Assert.Contains(" in all · toolchains 146 MB · Linux ", store.DiskUsageText, StringComparison.Ordinal);
        await UiTestHelpers.WaitUntilAsync(() => workspace.Compatibility.Any(r => r.ToolchainId == "python" && r.IsReady), because: "the doctor");
        Assert.Contains("Python 3.14.8 from the Toolchain Store", workspace.Compatibility.Single(r => r.ToolchainId == "python").Explanation, StringComparison.Ordinal);
        Assert.Null(workspace.ToolchainNotice);

        // Removing asks first.
        var remove = python.RemoveCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.Prompt.IsOpen, because: "the question");
        Assert.Equal("Remove Python?", workspace.Prompt.Title);
        workspace.Prompt.ConfirmCommand.Execute(null);
        await remove;
        await UiTestHelpers.WaitUntilAsync(() => python.ShowsInstall, because: "the removal");
        Assert.False(File.Exists(_device.Rootfs("/usr/bin/python3")));
        Assert.Contains(_device.Apk.Calls, c => c.StartsWith("del ", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public async Task F5_asks_before_installing_the_toolchain_and_cancelling_installs_nothing()
    {
        Show(1280, 800);
        var workspace = await OpenPythonAppAsync();

        var run = workspace.RunCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.Prompt.IsOpen, because: "the question");
        Assert.Equal("Install Python?", workspace.Prompt.Title);
        Assert.Contains("It downloads about 48 MB", workspace.Prompt.Message, StringComparison.Ordinal);
        workspace.Prompt.CancelCommand.Execute(null);
        await run;

        Assert.Empty(_device.Mirror.Server.Requests);
        Assert.False(workspace.ToolchainStore.IsOpen);
        Assert.Equal(ToolchainState.NotInstalled, _device.Manager.Status("python").State);
    }

    [AvaloniaFact]
    public async Task The_doctor_installs_the_toolchain_a_project_needs()
    {
        Show(1280, 800);
        var workspace = await OpenPythonAppAsync();
        var row = workspace.Compatibility.Single(r => r.ToolchainId == "python");

        await workspace.InstallToolchainCommand.ExecuteAsync(row);

        Assert.True(workspace.ToolchainStore.IsOpen);
        Assert.True(_device.Manager.IsReady("python"));
        Assert.Equal(ToolchainState.NotInstalled, _device.Manager.Status("node").State);
    }

    public void Dispose()
    {
        _main.Workspace?.Dispose();
        _services.Dispose();
        _toolchains.Dispose();
        _device.Dispose();
        _temp.Dispose();
    }

    private MainWindow Show(double width, double height)
    {
        var window = new MainWindow { DataContext = _main, Width = width, Height = height };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return window;
    }

    private async Task<WorkspaceViewModel> OpenPythonAppAsync()
    {
        await _main.OpenWorkspaceAsync(new ProjectTemplates(_paths).Create(ProjectTemplates.PythonId, "Weather"));
        var workspace = _main.Workspace!;
        await UiTestHelpers.WaitUntilAsync(() => workspace.ActiveDocument is not null, because: "main.py");
        return workspace;
    }
}
