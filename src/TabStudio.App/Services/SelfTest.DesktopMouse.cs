using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TabStudio.App.Platform;
using TabStudio.App.ViewModels;
using TabStudio.App.ViewModels.DesktopMode;
using TabStudio.App.Views;
using TabStudio.App.Views.DesktopMode;

namespace TabStudio.App.Services;

/// <summary>The self test's mouse on the external display: a click on a tab of the IDE there, and its wheel.</summary>
public static partial class SelfTest
{
    /// <summary>
    /// Clicks the Output tab of the IDE on the display with the tablet's mouse, then turns the wheel over the editor,
    /// and checks TabStudio's own pointer went there when TabStudio has the mouse.
    /// </summary>
    /// <returns>What went wrong, or <see langword="null"/> when it all worked.</returns>
    private static async Task<string?> UseMouseOnDisplayAsync(DesktopMouseCheck check, IDesktopSelfTestInput input, List<string> steps, CancellationToken cancellationToken)
    {
        var (workspace, desktop, screen, root, display, editor) = check;
        workspace.SelectPanelCommand.Execute(BottomPanel.ErrorList);
        await WaitForAsync(() => FindTab(screen, "Output") is { Bounds.Width: > 0 }, TimeSpan.FromSeconds(5), cancellationToken);
        if (FindTab(screen, "Output") is not { } outputTab || PixelCenter(outputTab, root) is not { } target)
        {
            return "the Output tab is not on the display.";
        }

        await input.ClickAsync(display.Id, target, cancellationToken);
        if (!await WaitForAsync(() => workspace.SelectedPanel == BottomPanel.Output, InputTimeout, cancellationToken))
        {
            return "a click of the mouse on the display did not reach the IDE.";
        }

        var cursor = desktop.Displays.Cursor;
        if (cursor.IsVisible && (Math.Abs(cursor.PixelPosition.X - target.X) > 2 || Math.Abs(cursor.PixelPosition.Y - target.Y) > 2))
        {
            return string.Create(CultureInfo.InvariantCulture, $"TabStudio's pointer is at {cursor.PixelPosition}, not on the Output tab at {target}.");
        }

        steps.Add(cursor.IsVisible
            ? "the tablet's mouse moved TabStudio's pointer to the Output tab and clicked it"
            : "the mouse clicked the Output tab");

        // The wheel over the editor reaches the editor.
        if (editor?.Editor.TextArea is not { IsEffectivelyVisible: true } text || PixelCenter(text, root) is not { } over)
        {
            return "the editor is not on the display to scroll.";
        }

        var wheeled = false;
        void OnWheel(object? sender, PointerWheelEventArgs e) =>
            wheeled |= e.Source is Visual source && (ReferenceEquals(source, text) || text.IsVisualAncestorOf(source));

        root.AddHandler(InputElement.PointerWheelChangedEvent, OnWheel, RoutingStrategies.Bubble, handledEventsToo: true);
        try
        {
            await input.ScrollAsync(display.Id, over, -3, cancellationToken);
            if (!await WaitForAsync(() => wheeled, InputTimeout, cancellationToken))
            {
                return "the mouse wheel on the display did not reach the editor.";
            }
        }
        finally
        {
            root.RemoveHandler(InputElement.PointerWheelChangedEvent, OnWheel);
        }

        steps.Add("its wheel reached the editor");
        return null;
    }

    /// <summary>The middle of a control, in the pixels of the display it is on.</summary>
    private static PixelPoint? PixelCenter(Visual visual, TopLevel root) =>
        visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), root) is { } center
            ? new PixelPoint((int)Math.Round(center.X * root.RenderScaling), (int)Math.Round(center.Y * root.RenderScaling))
            : null;

    /// <summary>What the mouse part of the self test works with.</summary>
    /// <param name="Workspace">The open workspace.</param>
    /// <param name="Desktop">Desktop mode, on.</param>
    /// <param name="Screen">The display's view, with the IDE.</param>
    /// <param name="Root">The display's window.</param>
    /// <param name="Display">The display.</param>
    /// <param name="Editor">The editor of the XAML file, on the display.</param>
    private sealed record DesktopMouseCheck(
        WorkspaceViewModel Workspace,
        DesktopModeViewModel Desktop,
        DesktopScreenView Screen,
        TopLevel Root,
        ExternalDisplay Display,
        CodeEditorView? Editor);
}
