using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Services;

/// <summary>The self test CI runs on an Android emulator, run here against the same app services.</summary>
public sealed class SelfTestTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly ServiceProvider _services;
    private readonly MainViewModel _main;

    public SelfTestTests()
    {
        _services = AppServices.Create(new AppPaths(_temp.Path));
        _main = _services.GetRequiredService<MainViewModel>();
    }

    [AvaloniaFact]
    public async Task Opens_the_sample_runs_it_and_answers_its_question()
    {
        var result = await SelfTest.RunAsync(_main, _services.GetRequiredService<SampleLibrary>());

        Assert.True(result.Success, result.Message);
        Assert.Contains($", {SelfTest.Name}!", result.Message, StringComparison.Ordinal);
        Assert.Contains("has exited with code 0", result.Message, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        _main.Workspace?.Dispose();
        _services.Dispose();
        _temp.Dispose();
    }
}
