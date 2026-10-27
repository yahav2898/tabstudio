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

    [Theory]
    [InlineData(null, SelfTestShard.All)]
    [InlineData("", SelfTestShard.All)]
    [InlineData("all", SelfTestShard.All)]
    [InlineData("managed", SelfTestShard.Managed)]
    [InlineData("UI", SelfTestShard.Ui)]
    [InlineData("native", SelfTestShard.Native)]
    [InlineData("performance", SelfTestShard.Performance)]
    public void Recognizes_only_named_device_test_shards(string? value, SelfTestShard expected)
    {
        Assert.True(SelfTest.TryParseShard(value, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("3")]
    [InlineData("4")]
    [InlineData("unknown")]
    [InlineData("managed,ui")]
    public void Rejects_invalid_device_test_shards(string value) =>
        Assert.False(SelfTest.TryParseShard(value, out _));

    [Fact]
    public void Shard_installation_plans_cover_the_full_run_without_installing_other_shards_tools()
    {
        var managed = SelfTest.SelfTestToolchainIdsForShard(SelfTestShard.Managed);
        var ui = SelfTest.SelfTestToolchainIdsForShard(SelfTestShard.Ui);
        var native = SelfTest.SelfTestToolchainIdsForShard(SelfTestShard.Native);
        var all = SelfTest.SelfTestToolchainIdsForShard(SelfTestShard.All);

        Assert.Equal(["go", "rust"], managed);
        Assert.Empty(ui);
        Assert.Equal(["python", "node", "cpp", "java"], native);
        Assert.Empty(managed.Intersect(ui, StringComparer.Ordinal));
        Assert.Empty(ui.Intersect(native, StringComparer.Ordinal));
        Assert.Empty(managed.Intersect(native, StringComparer.Ordinal));
        Assert.Equal(all.Order(StringComparer.Ordinal), managed.Concat(ui).Concat(native).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void An_unknown_shard_cannot_silently_skip_toolchain_preparation() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SelfTest.SelfTestToolchainIdsForShard((SelfTestShard)(-1)));

    [AvaloniaFact]
    public async Task Opens_the_sample_runs_it_and_answers_its_question()
    {
        var result = await SelfTest.RunAsync(_main, _services.GetRequiredService<SampleLibrary>());

        Assert.True(result.Success, result.Message);
        Assert.Contains($", {SelfTest.Name}!", result.Message, StringComparison.Ordinal);
        Assert.Contains("has exited with code 0", result.Message, StringComparison.Ordinal);
        Assert.Contains($"Link: debugged on {SelfTest.LinkHostName} over TLS 1.", result.Message, StringComparison.Ordinal);
        Assert.Contains($"Live: shared the sample, {SelfTest.LiveGuestName} joined over TLS (2 participants)", result.Message, StringComparison.Ordinal);
        Assert.Contains("Language servers: Loopback Server ready", result.Message, StringComparison.Ordinal);
        Assert.Contains("Debug adapters: Paused at a breakpoint", result.Message, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        _main.Workspace?.Dispose();
        _services.Dispose();
        _temp.Dispose();
    }
}
