using System.Diagnostics;
using System.Globalization;
using TabStudio.App.ViewModels;
using TabStudio.Forms.Hosting;

namespace TabStudio.App.Services;

/// <summary>
/// The self test's Windows Forms part: a Windows Forms app built against the Forms layer and run in the app preview,
/// and its form opened in the Forms designer.
/// </summary>
public static partial class SelfTest
{
    /// <summary>The Windows Forms project the self test creates.</summary>
    public const string WindowsFormsAppName = "SelfTestForms";

    /// <summary>
    /// Creates a Windows Forms app from Visual Studio's template, presses Ctrl+F5, waits for its form in the app
    /// preview and stops it; then opens Form1.cs in the designer and adds a button, which Form1.Designer.cs gets.
    /// </summary>
    /// <returns>Whether the form ran and the designer wrote the button, with a summary.</returns>
    private static async Task<SelfTestResult> ShowWindowsFormsAppAsync(MainViewModel main, ProjectTemplates templates, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        await main.OpenWorkspaceAsync(templates.Create(ProjectTemplates.WindowsFormsId, WindowsFormsAppName));
        if (main.Workspace is not { } workspace || !workspace.RunCommand.CanExecute(null))
        {
            return new SelfTestResult(false, "Windows Forms: the app did not open.", clock.Elapsed);
        }

        var run = workspace.RunCommand.ExecuteAsync(null);
        TimeSpan shown;
        try
        {
            while (!(workspace.AppPreview.IsRunning && workspace.AppPreview.View is FormsDesktop { Windows.Count: > 0 }) && !run.IsCompleted)
            {
                await Task.Delay(PollInterval, cancellationToken);
            }

            if (workspace.AppPreview.View is not FormsDesktop { Windows: [var form, ..] })
            {
                return new SelfTestResult(false, $"Windows Forms: the form was not shown: {LastLines(workspace)}", clock.Elapsed);
            }

            shown = clock.Elapsed;
            if (form.Title != "Form1")
            {
                return new SelfTestResult(false, $"Windows Forms: the form is not the template's: '{form.Title}'", clock.Elapsed);
            }

            if (await CopyAndPasteImageAsync((FormsDesktop)workspace.AppPreview.View, cancellationToken) is { } problem)
            {
                return new SelfTestResult(false, $"Windows Forms: a picture copied to the clipboard did not come back: {problem}", clock.Elapsed);
            }
        }
        finally
        {
            workspace.StopCommand.Execute(null);
            await run.WaitAsync(cancellationToken);
        }

        var ran = string.Create(CultureInfo.InvariantCulture, $"Windows Forms: Form1 shown in {shown.TotalSeconds:0.0} s");
        var designed = await DesignWindowsFormAsync(workspace);
        return new SelfTestResult(designed.Success, $"{ran} | {designed.Message}", clock.Elapsed);
    }

    /// <summary>
    /// Draws a picture with <c>Graphics.FromImage</c> in the running app, copies it with <c>Clipboard.SetImage</c> and
    /// pastes it with <c>Clipboard.GetImage</c>, through the device's clipboard.
    /// </summary>
    /// <returns>What went wrong, or <see langword="null"/> when the pasted picture has the drawn pixels.</returns>
    private static async Task<string?> CopyAndPasteImageAsync(FormsDesktop desktop, CancellationToken cancellationToken)
    {
        if (desktop.Runtime is not { } runtime)
        {
            return "the app ended";
        }

        var done = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        runtime.Post(() =>
        {
            try
            {
                using var picture = new System.Drawing.Bitmap(24, 16);
                using (var g = System.Drawing.Graphics.FromImage(picture))
                {
                    g.Clear(System.Drawing.Color.White);
                    g.FillRectangle(System.Drawing.Brushes.Red, 4, 4, 12, 8);
                }

                System.Windows.Forms.Clipboard.SetImage(picture);
                using var pasted = System.Windows.Forms.Clipboard.GetImage() as System.Drawing.Bitmap;
                done.TrySetResult(
                    pasted is null ? "Clipboard.GetImage returned nothing"
                    : pasted.Size != picture.Size ? $"it is {pasted.Width} by {pasted.Height}"
                    : pasted.GetPixel(8, 8).ToArgb() != System.Drawing.Color.Red.ToArgb() ? $"its red pixel is {pasted.GetPixel(8, 8)}"
                    : null);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                done.TrySetResult($"{ex.GetType().Name}: {ex.Message}");
            }
        });

        try
        {
            return await done.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        }
        catch (TimeoutException)
        {
            return "the clipboard did not answer in 30 s";
        }
    }

    /// <summary>Opens Form1.cs in the Forms designer and adds a button, as the Toolbox does.</summary>
    /// <returns>Whether the designer showed the form and wrote the button, with a one-line summary.</returns>
    private static async Task<SelfTestResult> DesignWindowsFormAsync(WorkspaceViewModel workspace)
    {
        var clock = Stopwatch.StartNew();
        var folder = Path.GetDirectoryName(workspace.StartupProject!.FilePath)!;
        if (await workspace.OpenDocumentAsync(Path.Combine(folder, "Form1.cs")) is not { FormsDesigner: { } designer })
        {
            return new SelfTestResult(false, "Forms designer: Form1.cs did not open in the designer.", clock.Elapsed);
        }

        if (designer.Surface.Root is not System.Windows.Forms.Form { Text: "Form1" })
        {
            return new SelfTestResult(false, $"Forms designer: the form was not built: {designer.LoadError}", clock.Elapsed);
        }

        designer.Surface.Add(typeof(System.Windows.Forms.Button), new System.Drawing.Point(16, 16));
        var written = await File.ReadAllTextAsync(designer.DesignerPath);
        return written.Contains("button1 = new Button();", StringComparison.Ordinal) && written.Contains("Controls.Add(button1);", StringComparison.Ordinal)
            ? new SelfTestResult(true, string.Create(CultureInfo.InvariantCulture, $"Forms designer: added a button to Form1 in {clock.Elapsed.TotalSeconds:0.0} s"), clock.Elapsed)
            : new SelfTestResult(false, "Forms designer: the button was not written to Form1.Designer.cs.", clock.Elapsed);
    }
}
