using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TabStudio.App.Controls;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Core.Progress;

namespace TabStudio.App.Tests.Views;

/// <summary>
/// An idle IDE draws no frames: a running animation keeps Avalonia rendering, which costs battery on a tablet, and
/// Avalonia runs a style's animation even while its control is hidden. Animations are found by asking every visual,
/// and the transforms animations move, whether any property is animating, not by looking at TabStudio's own classes.
/// </summary>
[Collection(IdleAnimationTestGroup.Name)]
public sealed class IdleAnimationTests
{
    [AvaloniaFact]
    public async Task A_progress_bar_or_spinner_animates_only_while_it_is_shown()
    {
        var bar = new ProgressBar { IsIndeterminate = true, Width = 120 };
        var icon = new StrokeIcon { Data = Geometry.Parse("M12 3 A9 9 0 1 1 3 12"), Size = 16 };
        icon.Classes.Add("spinning");
        var panel = new StackPanel { Spacing = 8, Children = { bar, icon } };
        var window = new Window { Width = 400, Height = 200, Content = new Border { Child = panel } };
        window.Show();
        try
        {
            await SettleAsync();
            var shown = RunningAnimations(window);
            Assert.Contains(shown, a => a.StartsWith("StrokeIcon", StringComparison.Ordinal));
            Assert.Contains(shown, a => a.Contains("IndeterminateProgressBarIndicator", StringComparison.Ordinal));

            // Hidden by the panel around them, as most of the IDE's spinners are.
            panel.IsVisible = false;
            await SettleAsync();
            Assert.Empty(RunningAnimations(window));

            panel.IsVisible = true;
            await SettleAsync();
            Assert.NotEmpty(RunningAnimations(window));

            // Hidden themselves.
            bar.IsVisible = false;
            icon.IsVisible = false;
            await SettleAsync();
            Assert.Empty(RunningAnimations(window));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task An_idle_IDE_runs_no_animation_even_after_a_long_operation_showed_its_progress()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = workspace } };
        window.Show();
        try
        {
            await UiTestHelpers.WaitUntilAsync(() => !workspace.Operations.HasOperations && workspace.State != IdeState.Busy,
                TimeSpan.FromMinutes(2), "the workspace's own operations to end");

            // An operation without numbers: the status bar's spinner turns and its bar moves while it runs.
            var operation = workspace.Operations.Begin("test:idle", "Restoring packages", new OperationProgress("Restoring packages"));
            await SettleAsync();
            Assert.NotEmpty(RunningAnimations(window));

            // Once it ends, the status bar item is hidden, and nothing in the IDE moves.
            operation.Complete();
            await SettleAsync();
            Assert.Empty(RunningAnimations(window));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Gives finite transitions a second of real time to finish, ticking the render clock as a screen would.</summary>
    private static async Task SettleAsync()
    {
        for (var i = 0; i < 40; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            await Task.Delay(25);
        }

        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Every visual with an animation running on it or on its render transform, with where it is.</summary>
    private static List<string> RunningAnimations(Visual root)
    {
        var running = new List<string>();
        foreach (var visual in root.GetSelfAndVisualDescendants())
        {
            AvaloniaObject[] transforms = visual.RenderTransform switch
            {
                TransformGroup group => [group, .. group.Children],
                Transform transform => [transform],
                _ => [],
            };
            if (IsAnimating(visual) || transforms.Any(IsAnimating))
            {
                var around = visual.GetVisualAncestors().OfType<StyledElement>().Select(a => a.Name).OfType<string>().Where(n => n.Length > 0).Take(3);
                running.Add($"{visual.GetType().Name} {(visual as StyledElement)?.Name} in {string.Join(" < ", around)}");
            }
        }

        return running;
    }

    private static bool IsAnimating(AvaloniaObject target) =>
        AvaloniaPropertyRegistry.Instance.GetRegistered(target).Any(target.IsAnimating);
}

/// <summary>
/// The idle-animation tests tick the render clock and drain the UI thread for a second at a time, which would change
/// the timing of tests running beside them, and they measure animations that other tests' windows also drive.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class IdleAnimationTestGroup
{
    public const string Name = "Idle animations";
}
