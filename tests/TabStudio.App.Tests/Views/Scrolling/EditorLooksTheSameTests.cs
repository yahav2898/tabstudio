using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using TabStudio.App.Editing;
using TabStudio.App.Services;
using TabStudio.App.Tests.Extensions;
using static TabStudio.App.Tests.Views.EditorHarness;

namespace TabStudio.App.Tests.Views.Scrolling;

/// <summary>
/// The editor's faster parts draw exactly what AvaloniaEdit's own draw: the same window, scrolled to the same place,
/// rendered once with the editor's text layer, line numbers and folding margin and once with AvaloniaEdit's, is the same
/// picture, also with the mouse on a fold marker.
/// </summary>
[Collection(ExtensionsTestGroup.Name)]
public sealed class EditorLooksTheSameTests
{
    [AvaloniaFact]
    public async Task The_text_line_numbers_and_folds_look_as_AvaloniaEdit_draws_them()
    {
        using var fixture = new ExtensionsFixture();
        using (var package = new MemoryStream(LanguageConfigurationExtension.CSharp.Package))
        {
            await fixture.Service.InstallFileAsync(package, LanguageConfigurationExtension.CSharp.FileName);
        }

        var solution = await new SampleLibrary(fixture.Paths).EnsureExtractedAsync();
        var path = Path.Combine(Path.GetDirectoryName(solution)!, "src", "Greetings", "Operations.cs");
        await File.WriteAllTextAsync(path, LongSampleCode.CSharp());
        await fixture.Workspace.LoadAsync(solution);
        // Tall enough that the fold marker the pointer goes to is clear of the floating touch bar.
        var window = fixture.Show(height: 960, bottomPanel: false);
        var document = await fixture.Workspace.OpenDocumentAsync(path) ?? throw new InvalidOperationException("The file did not open.");
        var view = ActiveEditor(window);
        await UiTestHelpers.WaitUntilAsync(
            () => document.Diagnostics.Any(d => d.Id == "CS0219") && view.Folding?.AllFoldings.Any() == true,
            TimeSpan.FromMinutes(2),
            "the colors and the folds");
        await AvaloniaEditParts.UntilSettledAsync(document, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));
        Render();
        var editor = view.Editor;
        var scroll = editor.GetVisualDescendants().OfType<ScrollViewer>().First();
        scroll.Offset = new Vector(37, 1234.5);
        Render();
        var folding = editor.TextArea.LeftMargins.OfType<DrawnFoldingMargin>().Single();
        var marker = folding.MarkerBounds[2].Bounds.Center;
        var markerInWindow = folding.TranslatePoint(marker, window)!.Value;
        var area = AreaOf(window, editor);

        var ours = Capture(window, area, "perf-03-editor-parts");
        window.MouseMove(markerInWindow);
        Render();
        var oursHovered = Capture(window, area, "perf-04-editor-parts-fold-hovered");
        window.MouseMove(new Point(1, 1));
        Render();

        // AvaloniaEdit's own parts back in place.
        Assert.Equal("text layer, line numbers, folding margin", AvaloniaEditParts.PutBack(view));
        Render();

        var theirs = Capture(window, area, "perf-05-avaloniaedit-parts");
        window.MouseMove(markerInWindow);
        Render();
        var theirsHovered = Capture(window, area, "perf-06-avaloniaedit-parts-fold-hovered");

        AssertSamePicture(theirs, ours, "the editor");
        AssertSamePicture(theirsHovered, oursHovered, "the editor with the mouse on a fold marker");
        window.Close();
    }

    private static PixelRect AreaOf(Window window, TextEditor editor)
    {
        var topLeft = editor.TranslatePoint(default, window)!.Value;
        return new PixelRect(PixelPoint.FromPoint(topLeft, window.RenderScaling), PixelSize.FromSize(editor.Bounds.Size, window.RenderScaling));
    }

    /// <summary>The pixels of an area of the window, as BGRA rows; the whole window is saved as a screenshot too.</summary>
    private static (byte[] Pixels, int Width, int Height) Capture(Window window, PixelRect area, string screenshot)
    {
        // The pixels first: the screenshot's accessibility check lays the window out again at another text size.
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("Nothing was rendered.");
        using var locked = frame.Lock();
        var area2 = area.Intersect(new PixelRect(locked.Size));
        var pixels = new byte[area2.Width * area2.Height * 4];
        for (var y = 0; y < area2.Height; y++)
        {
            Marshal.Copy(locked.Address + ((area2.Y + y) * locked.RowBytes) + (area2.X * 4), pixels, y * area2.Width * 4, area2.Width * 4);
        }

        UiTestHelpers.SaveScreenshot(window, screenshot);
        return (pixels, area2.Width, area2.Height);
    }

    private static void AssertSamePicture((byte[] Pixels, int Width, int Height) expected, (byte[] Pixels, int Width, int Height) actual, string what)
    {
        Assert.Equal((expected.Width, expected.Height), (actual.Width, actual.Height));
        var different = 0;
        var largest = 0;
        for (var i = 0; i < expected.Pixels.Length; i += 4)
        {
            var difference = 0;
            for (var channel = 0; channel < 4; channel++)
            {
                difference = Math.Max(difference, Math.Abs(expected.Pixels[i + channel] - actual.Pixels[i + channel]));
            }

            if (difference > 0)
            {
                different++;
                largest = Math.Max(largest, difference);
            }
        }

        Assert.True(different == 0, $"{what}: {different} of {expected.Pixels.Length / 4} pixels differ, by up to {largest} (see the perf-0* screenshots)");
    }

    private static void Render()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }

        Dispatcher.UIThread.RunJobs();
    }
}
