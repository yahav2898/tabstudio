using Avalonia;
using Avalonia.Headless.XUnit;
using TabStudio.App.Services;
using TabStudio.App.Swarm;
using TabStudio.App.Swarm.LayoutChecks;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Tests.Swarm;

/// <summary>
/// Swarm's two halves together, against the real nuget.org: two phones join the tablet's swarm over TLS on the
/// loopback address and run the Avalonia template, each in its own app preview, which is the screen the tablet drives.
/// A person's tap on one phone reaches the other as real input through that screen, a tap recorded there replays by
/// its element's name, and the Device Matrix shows both phones with the layout checks they ran on themselves.
/// </summary>
[Collection(SwarmAppFixture.Collection)]
public sealed class SwarmScreensOnDevicesTests
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(4);
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(45);

    /// <summary>The template's count button, as every screen names it.</summary>
    private const string CountButton = "MainView/StackPanel[0]/Button#CountButton";

    /// <summary>The template's count, as every screen names it.</summary>
    private const string Count = "MainView/StackPanel[0]/TextBlock[2]";

    [AvaloniaFact]
    public async Task A_phone_takes_mirrored_and_recorded_taps_through_its_preview_screen_and_its_layout_checks_show_in_the_Device_Matrix()
    {
        await using var fixture = new SwarmAppFixture();
        var solution = new ProjectTemplates(fixture.Paths).Create(ProjectTemplates.AvaloniaId, "HelloAvalonia");
        var workspace = fixture.Workspace;
        await workspace.LoadAsync(solution);
        var window = fixture.ShowIde();
        await fixture.StartAsync();
        var pixel = await fixture.JoinAsync("Pixel 7");
        var galaxy = await fixture.JoinAsync("Galaxy S24");
        SwarmAppFixture.Progress("screens: joined");

        // Ctrl+F5 on the swarm: the app runs on both phones, each in its own app preview.
        workspace.SelectSwarmTarget();
        await workspace.RunCommand.ExecuteAsync(null);
        var run = workspace.SwarmRun;
        await UiTestHelpers.WaitUntilAsync(
            () => (pixel.Texts().Contains("Clicked 0 times") && galaxy.Texts().Contains("Clicked 0 times")) || run.IsCompleted,
            StartTimeout,
            "the app on both phones");
        Assert.True(galaxy.Texts().Contains("Clicked 0 times"), workspace.Output.Text + workspace.SwarmOutput.Text);
        SwarmAppFixture.Progress("screens: running on both");

        // On each phone, the app preview is the screen of the swarm: it shows the app the page shows, full screen.
        foreach (var phone in new[] { pixel, galaxy })
        {
            Assert.Equal(AppScreenKind.Preview, phone.Preview.Kind);
            Assert.Equal(SwarmDeviceHost.PreviewScreenId, phone.Preview.Id);
            Assert.True(phone.Preview.IsRunning);
            Assert.Same(phone.Host.Preview.View, phone.Preview.Surface);
            Assert.Equal(FormFactor.Phablet, phone.Preview.Profile.FormFactor);
        }

        // Mirrored input: a person's tap on the Pixel is replayed on the Galaxy as a real tap, through its preview screen.
        fixture.SwarmWindow.MirrorInput = true;
        pixel.PersonTaps(CountButton);
        await UiTestHelpers.WaitUntilAsync(() => galaxy.Texts().Contains("Clicked 1 times"), because: "the Pixel's tap mirrored to the Galaxy");
        Assert.Contains("Clicked 1 times", pixel.Texts());
        fixture.SwarmWindow.MirrorInput = false;
        SwarmAppFixture.Progress("screens: mirrored");

        // Recorded: a person's tap on the Galaxy, captured by its preview screen, becomes the recorder's step.
        var recorder = fixture.SwarmWindow.Recorder;
        recorder.Open(start: false);
        recorder.SelectedScreen = recorder.Screens.First(s => s.Screen.Profile.Name == "Galaxy S24");
        await recorder.StartCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(60));
        Assert.True(recorder.IsRecording, recorder.Error);
        await UiTestHelpers.WaitUntilAsync(() => galaxy.Texts().Contains("Clicked 0 times"), because: "the Galaxy to start the app again for the recording");
        galaxy.PersonTaps(CountButton);
        await UiTestHelpers.WaitUntilAsync(() => recorder.Steps.Count == 1, because: "the recorded tap");
        var step = recorder.RecordedSteps[0];
        Assert.Equal(CountButton, step.ElementPath);
        Assert.Equal("Tap CountButton", recorder.Steps[0].Text);
        recorder.DiscardCommand.Execute(null);
        SwarmAppFixture.Progress("screens: recorded");

        // The step replays on the Pixel by the element's name, as the recorded test writes it, and the Pixel reads its count.
        var pixelOnTablet = fixture.ScreenOf(pixel);
        var tap = new AppInput(AppInputKind.Tap, ElementPaths.ShortName(step.ElementPath), new Point(0.5, 0.5));
        Assert.True(await pixelOnTablet.SendInputAsync(tap, CancellationToken.None));
        await UiTestHelpers.WaitUntilAsync(() => pixel.Texts().Contains("Clicked 2 times"), because: "the replayed tap on the Pixel");
        Assert.Equal("Clicked 2 times", await pixelOnTablet.ReadTextAsync(Count, CancellationToken.None));
        Assert.False(await pixelOnTablet.SendInputAsync(new AppInput(AppInputKind.Tap, "NoSuchButton"), CancellationToken.None));
        SwarmAppFixture.Progress("screens: replayed by name");

        // Each phone checked its own layout: the template's button is too small for a finger there, as the Error List says.
        await UiTestHelpers.WaitUntilAsync(
            () => SmallButton(workspace) is { } found && found.Screens.Count == 2 && workspace.LayoutChecks.Checking == 0,
            CheckTimeout,
            "both phones' layout checks");
        var finding = SmallButton(workspace)!;
        Assert.Equal(["Pixel 7", "Galaxy S24"], finding.Screens.Select(s => s.Profile.Name));
        Assert.All(finding.Screens, s => Assert.NotNull(s.Screenshot));
        var row = workspace.ErrorList.Items.Single(i => ReferenceEquals(i.Layout?.Finding, finding));
        Assert.Equal(LayoutRules.Code(LayoutIssueKind.SmallTouchTarget), row.Code);
        Assert.Contains("Pixel 7", row.LayoutScreens, StringComparison.Ordinal);
        SwarmAppFixture.Progress("screens: checked");

        // The Device Matrix, from the Swarm window: both phones by their pictures, with their issues on their badges.
        fixture.SwarmWindow.OpenDeviceMatrixCommand!.Execute(null);
        var matrix = workspace.DeviceMatrix!;
        foreach (var tile in matrix.Tiles.Where(t => t.IsVirtual).ToList())
        {
            matrix.RemoveDevice(tile);
        }

        var phones = matrix.Tiles.Where(t => t.IsDevice).ToList();
        Assert.Equal(["Pixel 7", "Galaxy S24"], phones.Select(t => t.Title));
        await UiTestHelpers.WaitUntilAsync(() => phones.All(t => t.Snapshot is not null && t.IsRunning && !t.IsClean), CheckTimeout, "the phones' pictures and badges");

        // The row's screens button outlines the button in both phones' tiles.
        workspace.ErrorList.ShowLayoutScreensCommand.Execute(row);
        Assert.Same(finding, matrix.ShownFinding);
        Assert.All(phones, t => Assert.True(t.HasHighlights));
        workspace.IsSideBarVisible = false;
        workspace.IsBottomPanelVisible = false;
        matrix.Zoom = 0.4;
        await UiTestHelpers.WaitUntilAsync(() => phones.All(t => t.ScreenHeight == Math.Round(915 * 0.4)), because: "the tiles at the new zoom");
        UiTestHelpers.SaveScreenshot(window, "swarm-11-device-matrix-with-phones");
        SwarmAppFixture.Progress("screens: matrix");

        workspace.StopCommand.Execute(null);
        await run.WaitAsync(TimeSpan.FromSeconds(60));
        await UiTestHelpers.WaitUntilAsync(() => pixel.Page.ShowsWaiting && galaxy.Page.ShowsWaiting, because: "both phones to wait for the next app");
        Assert.False(pixel.Preview.IsRunning);
        Assert.Null(pixel.Preview.AppRoot);
        window.Close();
    }

    /// <summary>The finding that the template's count button is too small to tap.</summary>
    private static LayoutFinding? SmallButton(WorkspaceViewModel workspace) =>
        workspace.LayoutChecks.Findings.FirstOrDefault(f => f.Kind == LayoutIssueKind.SmallTouchTarget && f.ElementPath == CountButton);
}
