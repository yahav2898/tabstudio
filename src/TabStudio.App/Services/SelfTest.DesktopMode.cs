using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.App.Views.DesktopMode;

namespace TabStudio.App.Services;

/// <summary>
/// The self test's desktop mode part: the IDE moved to an external display, a control put into the XAML from the
/// tablet's Toolbox and its content set in the tablet's Properties panel, text typed on the tablet's keyboard reaching
/// the editor on the display, a click on the display, and the IDE back on the tablet with nothing lost.
/// </summary>
public static partial class SelfTest
{
    /// <summary>The content the companion's Properties panel gives the button the Toolbox put in.</summary>
    public const string ContentFromTablet = "Made on a tablet";

    /// <summary>What the self test types on the tablet's keyboard, at the end of the button's content.</summary>
    public const string TypedOnKeyboard = " by keyboard";

    /// <summary>How long the outside world may take to type or click.</summary>
    private static readonly TimeSpan InputTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Runs desktop mode with the display that is connected, in the Avalonia app the main self test left open.
    /// </summary>
    /// <param name="main">The app's root view model, with the Avalonia app open.</param>
    /// <param name="tablet">The tablet's root view.</param>
    /// <param name="input">Types on the tablet's keyboard, clicks on the display and takes screenshots.</param>
    /// <param name="screenshots">A folder to save pictures of both displays in, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Gives up.</param>
    /// <returns>Whether every step worked, with a one-line summary.</returns>
    public static async Task<SelfTestResult> RunDesktopModeAsync(MainViewModel main, MainView tablet, IDesktopSelfTestInput input, string? screenshots, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(main);
        ArgumentNullException.ThrowIfNull(tablet);
        ArgumentNullException.ThrowIfNull(input);
        var clock = Stopwatch.StartNew();
        SelfTestResult Fail(string message) => new(false, $"Desktop: {message}", clock.Elapsed);

        if (main.DesktopMode is not { } desktop || main.Workspace is not { StartupProject: { } project } workspace)
        {
            return Fail("no workspace is open.");
        }

        if (!await WaitForAsync(() => desktop.HasDisplay, TimeSpan.FromSeconds(10), cancellationToken) || desktop.Display is not { } display)
        {
            return Fail("no presentation display is connected.");
        }

        var path = Path.Combine(Path.GetDirectoryName(project.FilePath)!, "Views", "MainView.axaml");
        if (await workspace.OpenDocumentAsync(path) is not { Designer: { } designer } document
            || tablet.GetVisualDescendants().OfType<WorkspaceView>().FirstOrDefault() is not { } ide)
        {
            return Fail("MainView.axaml did not open in the XAML designer.");
        }

        // With the caret in the StackPanel's start tag, the Toolbox puts the control into the panel.
        if (!await MoveCaretAsync(document, document.Document.Text.IndexOf("<StackPanel", StringComparison.Ordinal) + 2, cancellationToken))
        {
            return Fail("the caret did not move in MainView.axaml.");
        }

        var editor = ide.GetVisualDescendants().OfType<CodeEditorView>().FirstOrDefault(view => ReferenceEquals(view.DataContext, document));
        var tabletRoot = TopLevel.GetTopLevel(tablet);
        desktop.IsActive = true;
        if (!await WaitForAsync(() => tablet.DesktopScreen is { Bounds.Width: > 0 } shown && shown.IsVisualAncestorOf(ide) && CompanionShown(tablet), InputTimeout, cancellationToken)
            || tablet.DesktopScreen is not { } screen
            || TopLevel.GetTopLevel(screen) is not { } displayRoot
            || ReferenceEquals(displayRoot, tabletRoot))
        {
            return Fail($"the IDE did not appear on {display.Name}.");
        }

        var steps = new List<string>
        {
            string.Create(CultureInfo.InvariantCulture, $"Desktop: the IDE on {display.Name} ({display.Resolution} at {desktop.EffectiveScale * 100:0}%, {screen.Bounds.Width:0}×{screen.Bounds.Height:0})"),
        };

        // A tap on the tablet's Toolbox puts a button into the XAML on the display.
        var toolbox = desktop.Companion.Toolbox;
        if (toolbox.Categories.SelectMany(c => c.Items).FirstOrDefault(i => i.Name == "Button") is not { } button)
        {
            return Fail("the Toolbox has no Button.");
        }

        button.Insert.Execute(null);
        if (!document.Document.Text.Contains("<Button Content=\"Button\" />", StringComparison.Ordinal)
            || !await WaitForAsync(() => desktop.Companion is { ShowsElement: true, Element.ElementName: "Button" }, InputTimeout, cancellationToken))
        {
            return Fail($"the Toolbox did not put a Button at the caret: {toolbox.Status}");
        }

        steps.Add("the Toolbox put a Button into MainView.axaml");

        // The tablet's Properties panel sets its content.
        if (desktop.Companion.Element.Groups.SelectMany(g => g.Rows).FirstOrDefault(r => r.Name == "Content") is not { } content)
        {
            return Fail("the Properties panel has no Content for the Button.");
        }

        content.Value = ContentFromTablet;
        content.Commit();
        if (!document.Document.Text.Contains($"<Button Content=\"{ContentFromTablet}\" />", StringComparison.Ordinal))
        {
            return Fail($"the Properties panel did not set the content: {desktop.Companion.Element.Error}");
        }

        steps.Add("the Properties panel set its Content");

        // A keyboard connected to the tablet types into the editor on the display.
        var end = document.Document.Text.IndexOf(ContentFromTablet + "\"", StringComparison.Ordinal) + ContentFromTablet.Length;
        if (!await MoveCaretAsync(document, end, cancellationToken))
        {
            return Fail("the caret did not move to the Button's content.");
        }

        desktop.ReturnKeyboardToDisplay();
        input.Type(TypedOnKeyboard);
        if (!await WaitForAsync(() => document.Document.Text.Contains(ContentFromTablet + TypedOnKeyboard, StringComparison.Ordinal), InputTimeout, cancellationToken))
        {
            return Fail("keys typed on the tablet's keyboard did not reach the editor on the display.");
        }

        if (!await WaitForAsync(() => PreviewHasButton(designer, ContentFromTablet + TypedOnKeyboard), DesignerTimeout, cancellationToken))
        {
            return Fail($"the designer did not preview the new button: {designer.Error ?? designer.Status}");
        }

        steps.Add("the tablet's keyboard typed on the display and the designer showed it");

        // The tablet's mouse on the display: a click on the Output tab, and the wheel over the editor.
        if (await UseMouseOnDisplayAsync(new DesktopMouseCheck(workspace, desktop, screen, displayRoot, display, editor), input, steps, cancellationToken) is { } mouseProblem)
        {
            return Fail(mouseProblem);
        }

        if (screenshots is not null)
        {
            Directory.CreateDirectory(screenshots);
            Capture(displayRoot, Path.Combine(screenshots, "desktop-display.png"));
            if (tabletRoot is not null)
            {
                Capture(tabletRoot, Path.Combine(screenshots, "desktop-tablet.png"));
            }
        }

        await input.ScreenshotAsync(cancellationToken);

        // Back on the tablet: the same views, the same caret, the edits kept.
        var caret = document.CaretOffset;
        desktop.IsActive = false;
        if (!await WaitForAsync(() => tablet.DesktopScreen is null && ReferenceEquals(TopLevel.GetTopLevel(ide), tabletRoot), InputTimeout, cancellationToken))
        {
            return Fail("the IDE did not come back to the tablet.");
        }

        var sameEditor = editor is null || ide.GetVisualDescendants().Contains(editor);
        if (!sameEditor || document.CaretOffset != caret || !document.Document.Text.Contains(ContentFromTablet + TypedOnKeyboard, StringComparison.Ordinal))
        {
            return Fail($"the IDE came back to the tablet without its editor or caret (caret {document.CaretOffset}, was {caret}).");
        }

        steps.Add(string.Create(CultureInfo.InvariantCulture, $"back on the tablet with the caret at line {document.CaretLine}"));
        return new SelfTestResult(true, string.Join(" · ", steps), clock.Elapsed);
    }

    private static bool CompanionShown(MainView tablet) =>
        tablet.GetVisualDescendants().OfType<DesktopCompanionView>().Any(view => view.IsEffectivelyVisible);

    private static Button? FindTab(Control root, string text) =>
        root.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Content as string == text && b.IsEffectivelyVisible);

    private static bool PreviewHasButton(XamlDesignerViewModel designer, string content) =>
        designer.Preview?.GetLogicalDescendants().OfType<Button>().Any(b => b.Content as string == content) == true;

    private static async Task<bool> MoveCaretAsync(DocumentViewModel document, int offset, CancellationToken cancellationToken)
    {
        var location = document.Document.GetLocation(offset);
        document.NavigateTo(location.Line, location.Column);
        return await WaitForAsync(() => document.CaretOffset == offset, TimeSpan.FromSeconds(10), cancellationToken);
    }

    private static async Task<bool> WaitForAsync(Func<bool> condition, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        while (!condition())
        {
            if (clock.Elapsed > timeout)
            {
                return false;
            }

            await Task.Delay(PollInterval, cancellationToken);
        }

        return true;
    }

    /// <summary>Saves a picture of a window or view as it is drawn, at its own resolution.</summary>
    private static void Capture(TopLevel root, string path)
    {
        var scaling = root.RenderScaling;
        var size = new PixelSize(Math.Max(1, (int)(root.Bounds.Width * scaling)), Math.Max(1, (int)(root.Bounds.Height * scaling)));
        using var bitmap = new RenderTargetBitmap(size, new Vector(96 * scaling, 96 * scaling));
        bitmap.Render(root);
        bitmap.Save(path);
    }
}
