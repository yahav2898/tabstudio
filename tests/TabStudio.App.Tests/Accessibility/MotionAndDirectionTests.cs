using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TabStudio.App.Accessibility;
using TabStudio.App.Controls;
using TabStudio.App.Swarm;
using TabStudio.App.Swarm.LayoutChecks;
using TabStudio.App.Tests.Theming;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.Theming;
using TabStudio.App.Views;

namespace TabStudio.App.Tests.Accessibility;

/// <summary>
/// The device's own settings (docs/design/README.md, "Motion" and "Right to left"): no animations when Android's
/// Remove animations is on, and a mirrored layout, with code still left to right, when its language is written right
/// to left.
/// </summary>
[Collection(AppearanceTestGroup.Name)]
public sealed class MotionAndDirectionTests
{
    [AvaloniaFact]
    public void Spinners_stand_still_and_transitions_are_skipped_while_animations_are_off()
    {
        var spinner = new StrokeIcon { Data = Geometry.Parse("M0,0 L24,24") };
        spinner.Classes.Add("spinning");
        var fading = new Border { Transitions = [new DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromMilliseconds(150) }] };
        var window = new Window { Content = new StackPanel { Children = { spinner, fading } } };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            Assert.DoesNotContain(HiddenAnimations.StillClass, spinner.Classes);

            HiddenAnimations.ReduceMotion = true;
            Assert.Contains(HiddenAnimations.StillClass, spinner.Classes);
            Assert.Null(fading.Transitions);

            HiddenAnimations.ReduceMotion = false;
            Assert.DoesNotContain(HiddenAnimations.StillClass, spinner.Classes);
            Assert.Single(fading.Transitions!);

            // The device's setting drives it.
            var system = new SystemAccessibility();
            system.Update(1, reduceMotion: true, isRightToLeft: false);
            Assert.True(system.ReduceMotion);
        }
        finally
        {
            HiddenAnimations.ReduceMotion = false;
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(1280, 800)]
    [InlineData(800, 1280)]
    [InlineData(412, 915)]
    public async Task A_right_to_left_layout_mirrors_the_shell_and_keeps_code_left_to_right(double width, double height)
    {
        using var fixture = new WorkspaceFixture();
        var solution = await fixture.OpenSampleAsync();
        var program = Directory.EnumerateFiles(Path.GetDirectoryName(solution)!, "Program.cs", SearchOption.AllDirectories).First();
        await fixture.Workspace.OpenDocumentAsync(program);
        var window = new Window { Width = width, Height = height, FlowDirection = FlowDirection.RightToLeft, Content = new WorkspaceView { DataContext = fixture.Workspace } };
        window.Show();
        try
        {
            await UiTestHelpers.WaitUntilAsync(() => window.GetVisualDescendants().OfType<CodeEditorView>().Any(e => e.IsEffectivelyVisible), because: "the editor");
            await UiTestHelpers.WaitUntilAsync(() => true);
            UiTestHelpers.SaveScreenshot(window, $"rtl-{width}x{height}");

            var editor = window.GetVisualDescendants().OfType<CodeEditorView>().First(e => e.IsEffectivelyVisible).Editor;
            Assert.Equal(FlowDirection.LeftToRight, editor.FlowDirection);

            // The activity bar is at the left edge, where the right edge is in a left-to-right layout.
            var activity = window.GetVisualDescendants().OfType<ActivityBar>().First(a => a.IsEffectivelyVisible);
            var left = activity.TranslatePoint(default, window)!.Value.X;
            Assert.True(left < width / 2, $"The activity bar is at {left}, not at the left edge.");

            // An icon that points a way mirrors; Run's triangle does not.
            var icons = window.GetVisualDescendants().OfType<StrokeIcon>().Where(i => i.IsEffectivelyVisible).ToList();
            Assert.All(icons.Where(i => !i.IsDirectional), icon => Assert.Equal(FlowDirection.LeftToRight, icon.FlowDirection));
            Assert.All(icons.Where(i => i.IsDirectional), icon => Assert.Equal(((Visual)icon.GetVisualParent()!).FlowDirection, icon.FlowDirection));

            // Nothing is off screen or lies over another control.
            var scan = LayoutScan.Run(window, new ScreenProfile("rtl", FormFactor.Tablet, width, height));
            var offScreen = new HashSet<Visual>(ReferenceEqualityComparer.Instance);
            var problems = OffScreenRule.Find(scan, offScreen)
                .Concat(OverlapRule.Find(scan, offScreen))
                .Where(r => r.Severity == LayoutIssueSeverity.Error && !AccessibilityAudit.IsContent(r.Element.Visual) && !IsParked(r.Element.Visual))
                .Select(r => r.Message)
                .ToList();
            Assert.True(problems.Count == 0, "The mirrored layout is broken:\n" + string.Join("\n", problems.Take(40)));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Whether an element is an adaptive bar's item parked for its More menu, which is off screen by design.</summary>
    private static bool IsParked(Visual visual) =>
        visual.GetVisualAncestors().Prepend(visual).OfType<AdaptiveBarItem>().Any(item => item.Bounds.X <= AdaptiveLayout.Parked.X + 1)
        || visual.GetVisualAncestors().Prepend(visual).OfType<Control>().Any(c => c.Bounds.X <= AdaptiveLayout.Parked.X + 1);
}
