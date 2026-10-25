using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using TabStudio.App.ViewModels;
using TabStudio.App.ViewModels.DesktopMode;
using TabStudio.App.Views;
using TabStudio.App.Views.DesktopMode;

namespace TabStudio.App.Services;

/// <summary>
/// The self test's checks of what stays with the tablet in desktop mode: the floating touch buttons, which the big
/// screen does not show, and the companion's typing bar, which types on the big screen with the tablet's on-screen
/// keyboard when no keyboard is connected.
/// </summary>
public static partial class SelfTest
{
    /// <summary>What the self test types into the companion's typing bar, after what the keyboard typed.</summary>
    public const string TypedOnTablet = " and touch";

    /// <summary>Whether the editor's floating touch buttons show anywhere in a view.</summary>
    /// <param name="root">The view, such as the display's or the tablet's.</param>
    private static bool ShowsTouchBars(Visual root) =>
        root.GetVisualDescendants().OfType<Button>().Any(button => button.Name is "FoldTouchBarButton" or "UnfoldTouchBarButton" && button.IsEffectivelyVisible);

    /// <summary>
    /// Without a keyboard connected, types on the big screen through the companion's typing bar: the self test taps
    /// its field, which takes the on-screen keyboard's typing, and the CI script types into the field with Android's
    /// input, which must then appear at the caret of the editor on the display.
    /// </summary>
    /// <returns>What went wrong, or <see langword="null"/> when it worked or a keyboard is connected.</returns>
    private static async Task<string?> TypeWithTypingBarAsync(DesktopModeViewModel desktop, MainView tablet, DocumentViewModel document, IDesktopSelfTestInput input, List<string> steps, CancellationToken cancellationToken)
    {
        var bar = tablet.GetVisualDescendants().OfType<DesktopKeyboardView>().FirstOrDefault(view => view.IsEffectivelyVisible);
        if (desktop.HasKeyboard)
        {
            if (bar is not null)
            {
                return "the companion shows its typing bar while a keyboard is connected.";
            }

            steps.Add("a keyboard is connected, so the companion shows no typing bar");
            return null;
        }

        if (bar?.GetVisualDescendants().OfType<TextBox>().FirstOrDefault() is not { } field)
        {
            return "without a keyboard, the companion shows no typing bar.";
        }

        var expected = ContentFromTablet + TypedOnKeyboard + TypedOnTablet;
        field.Focus();
        input.Type(TypedOnTablet);
        var typed = await WaitForAsync(() => document.Document.Text.Contains(expected, StringComparison.Ordinal), InputTimeout, cancellationToken);
        desktop.ReturnKeyboardToDisplay();
        if (!typed)
        {
            return "typing into the companion's typing bar did not reach the editor on the display.";
        }

        steps.Add("without a keyboard, the companion's typing bar typed on the display");
        return null;
    }
}
