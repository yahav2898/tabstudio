using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TabStudio.App.Accessibility;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.Views;

namespace TabStudio.App.Tests.Accessibility;

/// <summary>
/// Everything works without touch (docs/design/README.md, "Keyboard"): Tab goes through the shell in reading order, to
/// controls a person can see, each showing that it has the focus.
/// </summary>
public sealed class KeyboardTests
{
    /// <summary>The most Tab presses the walk takes before it must be back where it started.</summary>
    private const int MaxStops = 400;

    [AvaloniaTheory]
    [InlineData(1280, 800)]
    [InlineData(800, 1280)]
    [InlineData(412, 915)]
    public async Task Tab_goes_through_the_shell_in_order_to_visible_controls_that_show_their_focus(double width, double height)
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var window = new Window { Width = width, Height = height, Content = new WorkspaceView { DataContext = fixture.Workspace } };
        window.Show();
        try
        {
            await UiTestHelpers.WaitUntilAsync(() => true);
            // On a phone the menus fold into the Menu button.
            var menu = window.GetVisualDescendants().OfType<Menu>().FirstOrDefault(m => m.IsEffectivelyVisible);
            var first = KeyboardNavigationHandler.GetNext(window, NavigationDirection.Next) as Control;
            Assert.NotNull(first);
            first!.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();

            var stops = new List<Control>();
            var problems = new List<string>();
            for (var i = 0; i < MaxStops; i++)
            {
                if (window.FocusManager?.GetFocusedElement() is not Control focused)
                {
                    problems.Add($"Tab {i}: nothing has the focus.");
                    break;
                }

                if (stops.Count > 0 && ReferenceEquals(focused, stops[0]))
                {
                    break;
                }

                stops.Add(focused);
                if (Problem(window, focused) is { } problem)
                {
                    problems.Add($"Tab {i}: {Describe(focused)} {problem}");
                }

                window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
            }

            Assert.True(problems.Count == 0, "Keyboard focus problems:\n" + string.Join("\n", problems.Take(60)));
            Assert.InRange(stops.Count, 5, MaxStops - 1);

            // Reading order: the bars at the top come before the editor, and the status bar last.
            var toolbar = window.GetVisualDescendants().OfType<Control>().First(c => c.Name == "Toolbar");
            var status = window.GetVisualDescendants().OfType<Control>().First(c => c.Name == "StatusBar");
            int IndexIn(Control region) => stops.FindIndex(s => ReferenceEquals(s, region) || region.IsVisualAncestorOf(s));
            var inMenu = menu is null ? -1 : IndexIn(menu);
            var inToolbar = IndexIn(toolbar);
            var inStatus = IndexIn(status);
            var inEditor = stops.FindIndex(s => s.GetVisualAncestors().OfType<CodeEditorView>().Any() || s is AvaloniaEdit.Editing.TextArea);
            Assert.True(inToolbar >= 0, "Tab never reaches the toolbar.");
            Assert.True(inStatus >= 0, "Tab never reaches the status bar.");
            Assert.True(inMenu < 0 || inMenu < inToolbar, "The menus come after the toolbar.");
            Assert.True(inEditor < 0 || inToolbar < inEditor, "The editor comes before the toolbar.");
            Assert.True(inStatus > inToolbar, "The status bar comes before the toolbar.");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>What is wrong with a control that has the keyboard focus, if anything.</summary>
    private static string? Problem(Window window, Control focused)
    {
        if (!focused.IsEffectivelyVisible)
        {
            return "has the focus while hidden.";
        }

        if (focused.TranslatePoint(default, window) is not { } origin)
        {
            return "is not in the window.";
        }

        var bounds = new Rect(origin, focused.Bounds.Size);
        var screen = new Rect(window.Bounds.Size);
        if (!bounds.Intersects(screen) || bounds.Width < 1 || bounds.Height < 1)
        {
            return $"has the focus off screen, at {bounds}.";
        }

        // A text box draws its own focus; every other control gets an adorner when the keyboard moved the focus.
        if (focused is not TextBox and not AvaloniaEdit.Editing.TextArea && !HasFocusAdorner(focused))
        {
            return "shows no focus.";
        }

        return AccessibilityAudit.NameProblem(focused) is { } name ? name : null;
    }

    private static bool HasFocusAdorner(Control control)
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        return AdornerLayer.GetAdornerLayer(control) is { } layer
            && layer.Children.Any(adorner => adorner.IsVisible && ReferenceEquals(AdornerLayer.GetAdornedElement(adorner), control));
    }

    private static string Describe(Control control)
    {
        var name = string.IsNullOrEmpty(control.Name) ? control.GetType().Name : control.GetType().Name + "#" + control.Name;
        var label = AccessibleNames.NameOf(control);
        return label is null ? name : $"{name} \"{label}\"";
    }
}
