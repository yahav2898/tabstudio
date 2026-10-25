using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using TabStudio.App.Services.WindowsCompatibility;
using TabStudio.App.ViewModels;
using TabStudio.WindowsCompatibility.Input;

namespace TabStudio.App.Services;

/// <summary>
/// The self test's Windows Compatibility part: Windows Compatibility installed from the Toolchain Store, a Windows
/// Forms app built on the device and run as the real Windows app under Wine, its button tapped through the
/// preview's touch input, and the change seen in the next frame; then a WPF app shown the same way.
/// </summary>
public static partial class SelfTest
{
    /// <summary>The Windows Forms project the self test creates.</summary>
    public const string WindowsFormsAppName = "SelfTestForms";

    /// <summary>The WPF project the self test creates.</summary>
    public const string WpfAppName = "SelfTestWpf";

    /// <summary>Where the self test saves its pictures of the Windows apps, or <see langword="null"/> for none.</summary>
    public static string? ScreenshotFolder { get; set; }

    /// <summary>The Windows Forms app's form: the template's, with a button that says when it was tapped.</summary>
    internal const string TappableForm = """
        namespace SelfTestForms
        {
            public partial class Form1 : Form
            {
                private readonly Button _button = new() { Text = "Tap me", Location = new Point(24, 24), Size = new Size(220, 64) };

                public Form1()
                {
                    InitializeComponent();
                    Controls.Add(_button);
                    _button.Click += (_, _) =>
                    {
                        _button.Text = "Tapped";
                        _button.BackColor = Color.FromArgb(0x16, 0xA3, 0x4A);
                        Console.WriteLine("The button was tapped.");
                    };
                    Shown += (_, _) =>
                    {
                        var center = _button.PointToScreen(new Point(_button.Width / 2, _button.Height / 2));
                        Console.WriteLine($"Shown, button at {center.X},{center.Y}");
                    };
                }
            }
        }
        """;

    /// <summary>The WPF app's window code: the template's, saying when it shows.</summary>
    internal const string ReportingWindow = """
        using System.Windows;

        namespace SelfTestWpf
        {
            public partial class MainWindow : Window
            {
                public MainWindow()
                {
                    InitializeComponent();
                    ContentRendered += (_, _) => Console.WriteLine("WPF window shown");
                }
            }
        }
        """;

    /// <summary>The WPF app's window: a heading and a button, as on Windows.</summary>
    internal const string ReportingWindowXaml = """
        <Window x:Class="SelfTestWpf.MainWindow"
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                Title="SelfTestWpf" Height="300" Width="480">
            <StackPanel Margin="24">
                <TextBlock Text="Hello from WPF" FontSize="24" Foreground="SteelBlue" />
                <Button Content="A WPF button" Width="200" Height="44" Margin="0,16,0,0" HorizontalAlignment="Left" />
            </StackPanel>
        </Window>
        """;

    /// <summary>
    /// Installs Windows Compatibility, runs the Windows Forms template with a button under Wine, taps the button
    /// through the preview's touch input and checks the next frames show the change; then shows a WPF app.
    /// </summary>
    /// <returns>Whether both ran, with the timings.</returns>
    private static async Task<SelfTestResult> RunWindowsCompatibilityAsync(MainViewModel main, ProjectTemplates templates, string? screenshots, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        var project = templates.Create(ProjectTemplates.WindowsFormsId, WindowsFormsAppName);
        await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(project)!, WindowsFormsAppName, "Form1.cs"), TappableForm, cancellationToken);
        await main.OpenWorkspaceAsync(project);
        if (main.Workspace is not { } workspace)
        {
            return new SelfTestResult(false, "Windows: the Windows Forms app did not open.", clock.Elapsed);
        }

        var windows = workspace.WindowsCompatibility;
        if (windows.Problem is { } problem)
        {
            return new SelfTestResult(false, $"Windows: {problem}", clock.Elapsed);
        }

        if (!windows.IsInstalled)
        {
            await workspace.ToolchainStore.InstallAsync(WindowsCompatibilityService.ToolchainId).WaitAsync(cancellationToken);
            if (!windows.IsInstalled)
            {
                return new SelfTestResult(false, $"Windows: Windows Compatibility did not install: {windows.Status.Error}", clock.Elapsed);
            }
        }

        var installed = clock.Elapsed;
        var run = workspace.RunCommand.ExecuteAsync(null);
        var shown = await WaitForOutputAsync(workspace, ButtonLine(), run, TimeSpan.FromMinutes(6), cancellationToken);
        if (shown is null || workspace.WindowsDesktop is not { } view || windows.Session?.Display is not { } display)
        {
            return new SelfTestResult(false, $"Windows: the Windows Forms app did not show: {LastLines(workspace)}", clock.Elapsed);
        }

        var started = clock.Elapsed - installed;
        var x = int.Parse(shown.Groups["x"].Value, CultureInfo.InvariantCulture);
        var y = int.Parse(shown.Groups["y"].Value, CultureInfo.InvariantCulture);
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        var before = Pixel(view, x, y);

        // The tap goes through the same gestures as a finger on the preview.
        using (var gestures = new TouchGestures(display))
        {
            gestures.Down(1, x, y);
            gestures.Up(1, x, y);
        }

        var tapped = await WaitForOutputAsync(workspace, TappedLine(), run, TimeSpan.FromSeconds(60), cancellationToken);
        var changed = await WaitForAsync(() => Pixel(view, x, y) != before, TimeSpan.FromSeconds(30), cancellationToken);
        if (screenshots is not null)
        {
            await SavePicturesAsync(view, screenshots, "windows-forms");
        }

        workspace.StopCommand.Execute(null);
        await run.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        if (tapped is null || !changed)
        {
            return new SelfTestResult(false, $"Windows: tapping the button {(tapped is null ? "reached no handler" : "changed nothing on the screen")}: {LastLines(workspace)}", clock.Elapsed);
        }

        var forms = string.Create(CultureInfo.InvariantCulture, $"Windows: installed in {installed.TotalSeconds:0} s, the Windows Forms app shown in {started.TotalSeconds:0} s, the tap changed its button");
        var wpf = await RunWpfAppAsync(main, templates, screenshots, cancellationToken);
        return new SelfTestResult(wpf.Success, $"{forms} | {wpf.Message} | {clock.Elapsed.TotalSeconds:0} s in all", clock.Elapsed);
    }

    /// <summary>Runs the WPF template, with a heading and a button, until its window shows.</summary>
    private static async Task<SelfTestResult> RunWpfAppAsync(MainViewModel main, ProjectTemplates templates, string? screenshots, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        var project = templates.Create(ProjectTemplates.WpfId, WpfAppName);
        var folder = Path.Combine(Path.GetDirectoryName(project)!, WpfAppName);
        await File.WriteAllTextAsync(Path.Combine(folder, "MainWindow.xaml"), ReportingWindowXaml, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(folder, "MainWindow.xaml.cs"), ReportingWindow, cancellationToken);
        await main.OpenWorkspaceAsync(project);
        if (main.Workspace is not { } workspace)
        {
            return new SelfTestResult(false, "WPF: the app did not open.", clock.Elapsed);
        }

        var run = workspace.RunCommand.ExecuteAsync(null);
        var shown = await WaitForOutputAsync(workspace, WpfShownLine(), run, TimeSpan.FromMinutes(4), cancellationToken);
        if (shown is not null && workspace.WindowsDesktop is { } view && screenshots is not null)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            await SavePicturesAsync(view, screenshots, "windows-wpf");
        }

        workspace.StopCommand.Execute(null);
        await run.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        return shown is null
            ? new SelfTestResult(false, $"WPF: the window did not show: {LastLines(workspace)}", clock.Elapsed)
            : new SelfTestResult(true, string.Create(CultureInfo.InvariantCulture, $"WPF: built with its markup and shown in {clock.Elapsed.TotalSeconds:0} s"), clock.Elapsed);
    }

    /// <summary>Waits for a line of the Output panel, or for the run to end without it.</summary>
    private static async Task<Match?> WaitForOutputAsync(WorkspaceViewModel workspace, Regex pattern, Task run, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (pattern.Match(workspace.Output.Text) is { Success: true } match)
            {
                return match;
            }

            if (run.IsCompleted)
            {
                return null;
            }

            await Task.Delay(250, cancellationToken);
        }

        return null;
    }

    private static async Task<bool> WaitForAsync(Func<bool> condition, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(100, cancellationToken);
        }

        return false;
    }

    /// <summary>A pixel of the desktop the preview shows, as BGRA.</summary>
    private static uint Pixel(Views.WindowsCompatibility.WindowsDesktopView view, int x, int y)
    {
        var (width, height, pixels) = view.Snapshot();
        if (x < 0 || y < 0 || x >= width || y >= height)
        {
            return 0;
        }

        var offset = ((y * width) + x) * 4;
        return BitConverter.ToUInt32(pixels, offset) | 0xFF000000u;
    }

    /// <summary>
    /// Saves two pictures for CI: the IDE with the app beside its code (<c>name.png</c>) and the Windows desktop the
    /// preview shows, pixel for pixel (<c>name-desktop.png</c>).
    /// </summary>
    private static async Task SavePicturesAsync(Views.WindowsCompatibility.WindowsDesktopView view, string folder, string name)
    {
        Directory.CreateDirectory(folder);
        if (Avalonia.Controls.TopLevel.GetTopLevel(view) is { } root)
        {
            var scaling = root.RenderScaling;
            var size = new Avalonia.PixelSize(Math.Max(1, (int)(root.Bounds.Width * scaling)), Math.Max(1, (int)(root.Bounds.Height * scaling)));
            using var screen = new Avalonia.Media.Imaging.RenderTargetBitmap(size, new Avalonia.Vector(96 * scaling, 96 * scaling));
            screen.Render(root);
            screen.Save(Path.Combine(folder, name + ".png"));
        }

        var (width, height, pixels) = view.Snapshot();
        if (width == 0)
        {
            return;
        }

        using var bitmap = new Avalonia.Media.Imaging.WriteableBitmap(new Avalonia.PixelSize(width, height), new Avalonia.Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Opaque);
        using (var framebuffer = bitmap.Lock())
        {
            for (var row = 0; row < height; row++)
            {
                System.Runtime.InteropServices.Marshal.Copy(pixels, row * width * 4, framebuffer.Address + (row * framebuffer.RowBytes), width * 4);
            }
        }

        await using var file = File.Create(Path.Combine(folder, name + "-desktop.png"));
        bitmap.Save(file);
    }

    [GeneratedRegex(@"Shown, button at (?<x>\d+),(?<y>\d+)")]
    private static partial Regex ButtonLine();

    [GeneratedRegex("The button was tapped\\.")]
    private static partial Regex TappedLine();

    [GeneratedRegex("WPF window shown")]
    private static partial Regex WpfShownLine();
}
