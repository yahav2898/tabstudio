using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using TabStudio.App.Swarm;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Services;

/// <summary>
/// The self test's Swarm part: an Avalonia app on virtual devices in the Device Matrix, a tap replayed on one of them
/// through real input, its count going up there and nowhere else, and the layout checks run on every screen.
/// </summary>
public static partial class SelfTest
{
    /// <summary>The Avalonia project the Swarm part creates.</summary>
    public const string SwarmAppName = "SelfTestSwarm";

    /// <summary>How long the virtual devices and the layout checks may take.</summary>
    private static readonly TimeSpan SwarmTimeout = TimeSpan.FromSeconds(90);

    /// <summary>
    /// Runs the Avalonia template, opens the Device Matrix with a small phone and a tablet, taps the count button on
    /// the phone through <see cref="IAppScreen.SendInputAsync"/>, and checks that the phone counted and the tablet,
    /// with its own view model, did not; then waits for the layout checks of every screen.
    /// </summary>
    /// <returns>Whether the tap counted on the phone alone, with a one-line summary.</returns>
    private static async Task<SelfTestResult> CheckSwarmScreensAsync(MainViewModel main, ProjectTemplates templates, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        await main.OpenWorkspaceAsync(templates.Create(ProjectTemplates.AvaloniaId, SwarmAppName));
        if (main.Workspace is not { } workspace || !workspace.RunCommand.CanExecute(null))
        {
            return new SelfTestResult(false, "Swarm: the app did not open.", clock.Elapsed);
        }

        var run = workspace.RunCommand.ExecuteAsync(null);
        try
        {
            while (!workspace.AppPreview.IsRunning && !run.IsCompleted)
            {
                await Task.Delay(PollInterval, cancellationToken);
            }

            if (!workspace.AppPreview.IsRunning)
            {
                return new SelfTestResult(false, $"Swarm: the app did not start: {LastLines(workspace)}", clock.Elapsed);
            }

            // Exactly two virtual devices: a small phone and a tablet.
            workspace.ShowDeviceMatrixCommand.Execute(null);
            var matrix = workspace.DeviceMatrix!;
            foreach (var tile in matrix.Tiles.Where(t => t.IsVirtual).ToList())
            {
                matrix.RemoveDevice(tile);
            }

            matrix.AddDeviceCommand.Execute(DevicePresets.SmallPhone);
            matrix.AddDeviceCommand.Execute(DevicePresets.Tablet);
            var devices = matrix.Tiles.Where(t => t.IsVirtual).Select(t => t.Virtual!).ToList();
            var deadline = clock.Elapsed + SwarmTimeout;
            while (!devices.All(d => d.IsRunning && Texts(d.AppRoot!).Contains("Clicked 0 times")) && clock.Elapsed < deadline)
            {
                await Task.Delay(PollInterval, cancellationToken);
            }

            if (devices.Count != 2 || !devices.All(d => d.IsRunning))
            {
                return new SelfTestResult(false, $"Swarm: the virtual devices did not show the app: {string.Join(", ", devices.Select(d => d.Error ?? d.Profile.Name))}", clock.Elapsed);
            }

            // A tap on the phone's count button, as a person's finger, through Avalonia's input.
            var (phone, tablet) = (devices[0], devices[1]);
            var (phoneApp, tabletApp) = (phone.AppRoot!, tablet.AppRoot!);
            var button = phoneApp.GetLogicalDescendants().OfType<Button>().Single(b => b.Name == "CountButton");
            var path = ElementPaths.Describe(phoneApp, button)!;
            var tapped = await phone.SendInputAsync(new AppInput(AppInputKind.Tap, path), cancellationToken);
            while (!Texts(phoneApp).Contains("Clicked 1 times") && clock.Elapsed < deadline)
            {
                await Task.Delay(PollInterval, cancellationToken);
            }

            if (!tapped || !Texts(phoneApp).Contains("Clicked 1 times") || !Texts(tabletApp).Contains("Clicked 0 times"))
            {
                return new SelfTestResult(false, $"Swarm: the tap on {path} did not count on the phone alone: {DescribeTap(button, tapped)}, {phone.LastReplayMiss ?? "found its element"}; phone {string.Join(", ", Texts(phoneApp))}", clock.Elapsed);
            }

            // Every screen is checked a moment after the app starts on it.
            var screens = workspace.Screens.Screens.Count;
            while (workspace.LayoutChecks.Checking > 0 && clock.Elapsed < deadline)
            {
                await Task.Delay(PollInterval, cancellationToken);
            }

            var picture = await phone.CaptureAsync(cancellationToken);
            return new SelfTestResult(
                picture is { Length: > 0 },
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Swarm: tapped {path} on a small phone through real input, counted there alone, {screens} screens, {workspace.LayoutChecks.Findings.Count} layout findings, in {clock.Elapsed.TotalSeconds:0.0} s"),
                clock.Elapsed);
        }
        finally
        {
            workspace.StopCommand.Execute(null);
            await run.WaitAsync(cancellationToken);

            // The IDE is left as the Avalonia part left it, for the parts that follow.
            if (workspace.Documents.FirstOrDefault(d => d.IsDeviceMatrix) is { } matrixTab)
            {
                await workspace.CloseDocumentCommand.ExecuteAsync(matrixTab);
            }
        }
    }

    /// <summary>Where a replayed tap went, for a failure's message: whether it was sent, drawn, and where the frame was.</summary>
    private static string DescribeTap(Button button, bool tapped)
    {
        var center = new Point(button.Bounds.Width / 2, button.Bounds.Height / 2);
        var drawn = button.GetVisualsAt(center).Count();
        var host = button.FindAncestorOfType<VirtualDeviceFrame>()?.Parent?.GetType().Name ?? "no frame";
        var window = TopLevel.GetTopLevel(button) is { } top ? button.TranslatePoint(center, top) : null;
        return string.Create(CultureInfo.InvariantCulture, $"sent {tapped}, {drawn} drawn visuals at its center, frame in {host}, at {window} in the window, pressed {button.IsPressed}");
    }
}
