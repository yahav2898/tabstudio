using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Services;
using TabStudio.App.Services.Toolchains;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Testing;
using TabStudio.Toolchains.Installation;
using TabStudio.Toolchains.Tests.Fakes;

namespace TabStudio.App.Tests.Toolchains;

/// <summary>
/// Opening a project on a fake tablet with a fake Alpine mirror: the toolchain it needs installs without a question,
/// with its progress in the status bar and the Output panel, and Cancel stops it.
/// </summary>
public sealed class ProjectSetupDeviceTests : IDisposable
{
    private readonly FakeDevice _device = FakeDevice.WithPythonAndNode();
    private readonly TempDirectory _temp = new();
    private readonly ToolchainServices _toolchains;
    private readonly ServiceProvider _services;
    private readonly MainViewModel _main;
    private readonly AppPaths _paths;

    public ProjectSetupDeviceTests()
    {
        _toolchains = new ToolchainServices(_device.Manager, _device.Platform, _device.Environment);
        _paths = new AppPaths(_temp.Path);
        _services = AppServices.Create(_paths, services => services.AddSingleton(_toolchains));
        _main = _services.GetRequiredService<MainViewModel>();
    }

    [AvaloniaFact]
    public async Task Opening_a_Python_app_installs_Python_without_asking_with_progress_in_the_status_bar()
    {
        var window = new MainWindow { DataContext = _main, Width = 1280, Height = 800 };
        window.Show();
        _device.Mirror.Server.Latency = TimeSpan.FromMilliseconds(300);

        await _main.OpenWorkspaceAsync(new ProjectTemplates(_paths).Create(ProjectTemplates.PythonId, "Weather"));
        var workspace = _main.Workspace!;
        await UiTestHelpers.WaitUntilAsync(() => workspace.ProjectSetupText?.StartsWith("Installing Python ", StringComparison.Ordinal) == true, because: "the download's progress");

        Assert.False(workspace.Prompt.IsOpen);
        Assert.False(workspace.ToolchainStore.IsOpen);
        Assert.Null(workspace.ToolchainNotice);
        Assert.Contains("------ Installing Python from the Toolchain Store ------", workspace.Output.Text, StringComparison.Ordinal);
        UiTestHelpers.SaveScreenshot(window, "open-any-repo-02b-installing-the-toolchain");

        _device.Mirror.Server.Latency = TimeSpan.Zero;
        await UiTestHelpers.WaitUntilAsync(() => workspace.ProjectSetupTask.IsCompleted, TimeSpan.FromMinutes(1), "the install");
        Assert.True(_device.Manager.IsReady("python"));
        Assert.Equal(ToolchainState.NotInstalled, _device.Manager.Status("node").State);
        Assert.Contains("========== Python 3.14.8: installed ==========", workspace.Output.Text, StringComparison.Ordinal);
        Assert.False(workspace.IsSettingUpProjects);
        Assert.Null(workspace.ToolchainNotice);
    }

    [AvaloniaFact]
    public async Task Cancel_in_the_status_bar_stops_the_install_and_leaves_the_offer()
    {
        _device.Mirror.Server.Latency = TimeSpan.FromMilliseconds(500);
        await _main.OpenWorkspaceAsync(new ProjectTemplates(_paths).Create(ProjectTemplates.PythonId, "Weather"));
        var workspace = _main.Workspace!;
        await UiTestHelpers.WaitUntilAsync(() => workspace.IsSettingUpProjects, because: "the install to start");

        workspace.CancelProjectSetupCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.ProjectSetupTask.IsCompleted, because: "the cancellation");
        await UiTestHelpers.WaitUntilAsync(() => !_device.Manager.Status("python").IsBusy, because: "the Store to stop");

        Assert.False(_device.Manager.IsReady("python"));
        Assert.Contains("The project setup was cancelled.", workspace.Output.Text, StringComparison.Ordinal);
        await UiTestHelpers.WaitUntilAsync(() => workspace.ToolchainNotice is not null, because: "the offer to install Python");
        Assert.Equal("Install Python", workspace.ToolchainNotice!.ActionText);
    }

    [AvaloniaFact]
    public async Task Turning_the_setting_off_cancels_setup_and_turning_it_on_retries()
    {
        _device.Mirror.Server.Latency = TimeSpan.FromMilliseconds(500);
        await _main.OpenWorkspaceAsync(new ProjectTemplates(_paths).Create(ProjectTemplates.PythonId, "Weather"));
        var workspace = _main.Workspace!;
        await UiTestHelpers.WaitUntilAsync(() => workspace.IsSettingUpProjects, because: "the install to start");
        workspace.SetUpProjectsWhenTheyOpen = false;
        await UiTestHelpers.WaitUntilAsync(() => workspace.ProjectSetupTask.IsCompleted, because: "setup to stop");
        Assert.False(_device.Manager.IsReady("python"));
        Assert.False(workspace.IsSettingUpProjects);

        _device.Mirror.Server.Latency = TimeSpan.Zero;
        workspace.SetUpProjectsWhenTheyOpen = true;
        await UiTestHelpers.WaitUntilAsync(() => workspace.ProjectSetupTask.IsCompleted, TimeSpan.FromMinutes(1), "setup to retry");
        Assert.True(_device.Manager.IsReady("python"), workspace.Output.Text);
    }

    public void Dispose()
    {
        _main.Workspace?.Dispose();
        _services.Dispose();
        _toolchains.Dispose();
        _device.Dispose();
        _temp.Dispose();
    }
}
