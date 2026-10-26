using System.Text;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TabStudio.App.Editing;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.Services.LocalAI;
using TabStudio.Testing;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;

namespace TabStudio.App.Tests.Views;

public sealed class LocalAIEditorTests
{
    [AvaloniaTheory]
    [InlineData("code.cs")]
    [InlineData("code.py")]
    [InlineData("code.html")]
    [InlineData("code.unknown")]
    public async Task Ghost_is_visual_only_Tab_inserts_it_as_one_undo_step_and_typing_cancels_it(string file)
    {
        var profile = DeviceProfile.For(new DeviceInfo(8L * 1024 * 1024 * 1024, 4, false));
        using var document = new DocumentViewModel(Path.Combine(Path.GetTempPath(), file), new TextFileContent("result = ", new UTF8Encoding(false)), null, profile);
        var provider = new InlineCompletionController((_, _, _, _, _) => Task.FromResult<string?>("calculate(42)"), TimeSpan.Zero);
        using var view = new CodeEditorView(document, provider);
        var window = new Window { Content = view, Width = 900, Height = 400 };
        window.Show();
        try
        {
            view.Editor.TextArea.Focus();
            view.Editor.CaretOffset = document.Document.TextLength;
            await provider.RequestAsync(file, document.Document.Text, view.Editor.CaretOffset, []);
            Assert.Equal("result = ", document.Document.Text);
            Assert.Equal("calculate(42)", provider.Text);
            Assert.False(document.Document.UndoStack.CanUndo);
            Dispatcher.UIThread.RunJobs();
            UiTestHelpers.SaveScreenshot(window, "local-ai-ghost-" + Path.GetExtension(file).TrimStart('.'));
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Assert.Equal("result = calculate(42)", document.Document.Text);
            Assert.Empty(provider.Text);
            document.Document.UndoStack.Undo();
            Assert.Equal("result = ", document.Document.Text);
            await provider.RequestAsync(file, document.Document.Text, view.Editor.CaretOffset, []);
            view.Editor.TextArea.PerformTextInput("x");
            Assert.Empty(provider.Text);
            await provider.RequestAsync(file, document.Document.Text, view.Editor.CaretOffset, []);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Assert.Empty(provider.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Late_results_after_typing_or_disposal_never_reappear()
    {
        var response = new TaskCompletionSource<string?>();
        using var controller = new InlineCompletionController((_, _, _, _, _) => response.Task, TimeSpan.Zero);
        var request = controller.RequestAsync("a.py", "prefix", 6, []);
        controller.Cancel();
        response.SetResult("stale");
        await request;
        Assert.Empty(controller.Text);
        var late = new TaskCompletionSource<string?>();
        var disposed = new InlineCompletionController((_, _, _, _, _) => late.Task, TimeSpan.Zero);
        request = disposed.RequestAsync("a.py", "prefix", 6, []);
        disposed.Dispose();
        late.SetResult("stale");
        await request;
        Assert.Empty(disposed.Text);
    }

    [AvaloniaFact]
    public async Task Thermal_and_battery_events_clear_pending_ghosts()
    {
        using var temp = new TempDirectory();
        var device = new PowerDevice();
        using var service = new LocalAIService(new AppPaths(temp.Path), device);
        var profile = DeviceProfile.For(device.Device);
        using var document = new DocumentViewModel(temp.Resolve("a.txt"), new TextFileContent("prefix", new UTF8Encoding(false)), null, profile) { LocalAI = service };
        var response = new TaskCompletionSource<string?>();
        var provider = new InlineCompletionController((_, _, _, _, _) => response.Task, TimeSpan.Zero);
        using var view = new CodeEditorView(document, provider);
        var window = new Window { Content = view, Width = 500, Height = 200 };
        window.Show();
        try
        {
            var request = provider.RequestAsync("a.txt", "prefix", 6, []);
            device.Hot();
            Dispatcher.UIThread.RunJobs();
            response.SetResult("stale");
            await request;
            Assert.Empty(provider.Text);
            Assert.Contains("hot", service.PauseReason!, StringComparison.Ordinal);
            device.LowBattery();
            Assert.Contains("battery", service.PauseReason!, StringComparison.Ordinal);
        }
        finally { window.Close(); }
    }

    private sealed class PowerDevice : IDeviceStatus
    {
        public DeviceInfo Device { get; } = new(8L * 1024 * 1024 * 1024, 4, false);
        public ThermalState Thermal { get; private set; }
        public int BatteryPercent { get; private set; } = 80;
        public bool IsCharging => false;
        public event EventHandler? ThermalChanged;
        public event EventHandler? PowerChanged;
        public event EventHandler<MemoryPressure>? MemoryPressureRaised { add { } remove { } }
        public void Hot() { Thermal = ThermalState.Hot; ThermalChanged?.Invoke(this, EventArgs.Empty); }
        public void LowBattery() { Thermal = ThermalState.Normal; BatteryPercent = 10; PowerChanged?.Invoke(this, EventArgs.Empty); }
    }

    [AvaloniaFact]
    public void Local_mode_needs_no_API_key_and_shows_the_device_privacy_badge_and_model_manager()
    {
        using var fixture = new WorkspaceFixture();
        fixture.Workspace.LocalAI.Configure(fixture.Workspace.LocalAI.SelectedModelId, true);
        var agent = fixture.Workspace.Agent;
        Assert.True(fixture.Workspace.LocalAI.InlineEnabled);
        Assert.True(agent.LocalAI!.InlineEnabled);
        agent.UseLocalMode = true;
        agent.SaveSettingsCommand.Execute(null);
        Assert.True(agent.IsLocalMode);
        Assert.False(agent.IsSettingsOpen);
        Assert.Contains("stay on device", agent.PrivacyBadge, StringComparison.Ordinal);
        Assert.NotNull(agent.LocalAI);
        Assert.Contains("not yet measured", agent.LocalAI.Details, StringComparison.Ordinal);
        agent.OpenSettingsCommand.Execute(null);
        var window = new Window { Content = new AgentPanelView { DataContext = agent }, Width = 620, Height = 1000 };
        window.Show();
        try { UiTestHelpers.SaveScreenshot(window, "local-ai-model-manager"); }
        finally { window.Close(); }
    }
}
