using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Swarm;

/// <summary>The Swarm part of the self test CI runs on an Android emulator, run here against the same app services.</summary>
[Collection(SwarmAppFixture.Collection)]
public sealed class SelfTestSwarmTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly ServiceProvider _services;
    private readonly MainViewModel _main;

    public SelfTestSwarmTests()
    {
        _services = AppServices.Create(new AppPaths(_temp.Path));
        _main = _services.GetRequiredService<MainViewModel>();
    }

    [AvaloniaFact]
    public async Task A_phone_joins_runs_the_Avalonia_template_and_stops_at_a_breakpoint_the_tablet_sees()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var result = await SelfTest.RunOnSwarmAsync(_main, _services.GetRequiredService<ProjectTemplates>(), timeout.Token);

        Assert.True(result.Success, result.Message);
        Assert.Contains($"Swarm: {SelfTest.SwarmDeviceName} joined over TLS 1.", result.Message, StringComparison.Ordinal);
        Assert.Contains("paused at the breakpoint in", result.Message, StringComparison.Ordinal);
        Assert.Contains("with next = 1", result.Message, StringComparison.Ordinal);
        Assert.False(_services.GetRequiredService<TabStudio.App.Swarm.SwarmCoordinatorService>().IsOn);
    }

    public void Dispose()
    {
        _main.Workspace?.Dispose();
        _services.Dispose();
        _temp.Dispose();
    }
}
