using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Controls;
using TabStudio.App.Services.WindowsBridge;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.WindowsBridge.Hosting;
using TabStudio.WindowsBridge.Windows;

namespace TabStudio.App.Tests.WindowsBridge;

/// <summary>
/// The Windows Bridge on real Windows, as the Windows CI job runs it: the tablet's app and the PC's are both in this
/// process, and the PC builds a real WPF app from the .NET SDK's template with its own SDK, runs it on the desktop and
/// streams its window to the tablet, beside its code. A XAML error is MSBuild's, in the tablet's Error List.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsBridgeOnWindowsTests
{
    private const string Via = "Windows Bridge: Test PC";

    [WindowsAvaloniaFact]
    public async Task On_Windows_a_WPF_app_runs_on_the_PC_beside_its_code_and_the_bridge_window_shows_it()
    {
        await using var devices = BridgeDevices.Start(new RealDesktop());
        var project = await CreateWpfAsync(devices, xamlError: false);
        await devices.PairAsync();
        var workspace = await devices.OpenAsync(devices.SolutionFolder);
        Assert.Equal(Via, workspace.RunHostText);
        await workspace.OpenDocumentAsync(Path.Combine(Path.GetDirectoryName(project)!, "MainWindow.xaml.cs"));

        await workspace.RunCommand.ExecuteAsync(null);
        var pane = workspace.WindowsBridgePane!;
        await UiTestHelpers.WaitUntilAsync(() => pane.SelectedWindow is not null, TimeSpan.FromMinutes(4), "the WPF window on the PC");
        Widen(devices.Window, 600);
        await UiTestHelpers.WaitUntilAsync(() => pane.Stream is { FrameCount: > 0 } stream && Middle(stream) == 0xFFF3F3F3, TimeSpan.FromMinutes(1), "the window's light background");
        UiTestHelpers.SaveScreenshot(devices.Window, "windows-bridge-07-wpf-with-code");

        var view = devices.Window.GetVisualDescendants().OfType<BridgeFrameView>().Single(v => v.IsEffectivelyVisible);
        var map = view.Mapping;
        // The button sits a little below the middle of the window, under the greeting.
        var button = view.TranslatePoint(new Point(map.Left + (map.DrawnWidth / 2), map.Top + (map.DrawnHeight / 2) + (map.DrawnHeight / 20)), devices.Window)!.Value;
        devices.Window.MouseDown(button, MouseButton.Left);
        devices.Window.MouseUp(button, MouseButton.Left);
        await UiTestHelpers.WaitUntilAsync(() => Corner(pane.Stream!) == 0xFF20A040, TimeSpan.FromMinutes(1), "the green window after the click");
        UiTestHelpers.SaveScreenshot(devices.Window, "windows-bridge-08-wpf-clicked");

        var pc = devices.PcServices.GetRequiredService<MainViewModel>();
        var pcWindow = new MainWindow { DataContext = pc, Width = 1280, Height = 900 };
        pcWindow.Show();
        pc.Link.Open(LinkTab.Bridge);
        var bridge = pc.Link.Bridge!;
        await UiTestHelpers.WaitUntilAsync(() => bridge.IsServing && bridge.RunningApps.Count == 1 && bridge.Toolchains.Count > 0, because: "the bridge window");
        UiTestHelpers.SaveScreenshot(pcWindow, "windows-bridge-10-pc-window-on-windows");

        Assert.Contains(bridge.Toolchains, t => t.Toolchain.Kind == TabStudio.WindowsBridge.Protocol.BridgeToolchainKind.DotNetSdk);
        Assert.Contains(bridge.PcCapabilities, c => c.IsAvailable && c.Name.Contains("WPF", StringComparison.Ordinal));
        Assert.Equal("HelloWpf", bridge.RunningApps[0].Name);
        pcWindow.Close();
        workspace.StopDebuggingCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => pane.SelectedApp is null, because: "the WPF app to stop");
    }

    [WindowsAvaloniaFact]
    public async Task On_Windows_an_MSBuild_error_in_XAML_lands_in_the_Error_List_on_the_tablets_file()
    {
        await using var devices = BridgeDevices.Start(new RealDesktop());
        var project = await CreateWpfAsync(devices, xamlError: true);
        await devices.PairAsync();
        var workspace = await devices.OpenAsync(devices.SolutionFolder);

        await workspace.RunCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.ErrorList.Items.Any(i => i.IsFromBridge && i.Code.StartsWith("MC", StringComparison.Ordinal)), TimeSpan.FromMinutes(4), "MSBuild's XAML error");
        workspace.ErrorList.SearchText = "MC3072"; // The tablet's own squiggle errors would fill the short list above MSBuild's row.
        UiTestHelpers.SaveScreenshot(devices.Window, "windows-bridge-09-msbuild-error");

        var error = workspace.ErrorList.Items.First(i => i.IsFromBridge && i.Code.StartsWith("MC", StringComparison.Ordinal));
        Assert.Equal("MC3072", error.Code);
        Assert.Equal("Test PC", error.BridgePc);
        Assert.Equal(Path.Combine(Path.GetDirectoryName(project)!, "MainWindow.xaml"), error.Diagnostic.Location!.FilePath);
        Assert.Contains("0 succeeded, 1 failed, on Test PC", workspace.Output.Text, StringComparison.Ordinal);
    }

    /// <summary>The WPF template with a button that turns the window green, as the bridge's own Windows tests use it.</summary>
    private static async Task<string> CreateWpfAsync(BridgeDevices devices, bool xamlError)
    {
        var directory = Path.Combine(devices.SolutionFolder, "HelloWpf");
        Directory.CreateDirectory(directory);
        using (var process = BridgeProcess.Start(new BridgeProcessStart("dotnet", ["new", "wpf", "-n", "HelloWpf", "-o", directory, "--force"], directory)))
        {
            process.BeginReading();
            Assert.Equal(0, await process.Exited.WaitAsync(TimeSpan.FromMinutes(3)));
        }

        var grid = xamlError ? """<Grid x:Name="Root" Background="#F3F3F3" Fill="Red">""" : """<Grid x:Name="Root" Background="#F3F3F3">""";
        File.WriteAllText(Path.Combine(directory, "MainWindow.xaml"), $$"""
            <Window x:Class="HelloWpf.MainWindow"
                    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    Title="Hello WPF" Width="560" Height="400" WindowStartupLocation="CenterScreen">
                {{grid}}
                    <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center">
                        <TextBlock x:Name="Greeting" Text="Hello from WPF on the PC" FontSize="22" Margin="0,0,0,20" HorizontalAlignment="Center" />
                        <Button x:Name="Greet" Content="Click me" Width="200" Height="64" FontSize="18" Click="OnClick" />
                    </StackPanel>
                </Grid>
            </Window>
            """);
        File.WriteAllText(Path.Combine(directory, "MainWindow.xaml.cs"), """
            using System.Windows;
            using System.Windows.Media;

            namespace HelloWpf;

            public partial class MainWindow : Window
            {
                public MainWindow()
                {
                    InitializeComponent();
                }

                private void OnClick(object sender, RoutedEventArgs e)
                {
                    Root.Background = new SolidColorBrush(Color.FromRgb(0x20, 0xA0, 0x40));
                    Greeting.Foreground = Brushes.White;
                    Greet.Content = "Clicked";
                }
            }
            """);
        return Path.Combine(directory, "HelloWpf.csproj");
    }

    /// <summary>Makes the side bar wider, so the Bridge pane shows the window at a size people can read.</summary>
    private static void Widen(Window window, double width)
    {
        var main = window.GetVisualDescendants().OfType<Grid>().Single(g => g.Name == "MainArea");
        main.ColumnDefinitions[2].Width = new GridLength(width);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The window's color left of its content, in the middle of its height.</summary>
    private static uint Middle(TabStudio.WindowsBridge.Client.BridgeStreamReceiver stream) =>
        stream.Read(frame => frame.GetPixel(Math.Min(24, frame.Width - 1), frame.Height / 2));

    /// <summary>The window's color near its bottom left corner, inside its border.</summary>
    private static uint Corner(TabStudio.WindowsBridge.Client.BridgeStreamReceiver stream) =>
        stream.Read(frame => frame.GetPixel(Math.Min(24, frame.Width - 1), Math.Max(0, frame.Height - 24)));

    private sealed class RealDesktop : IBridgeDesktopFactory
    {
        public IBridgeDesktop Create() => new WindowsBridgeDesktop();
    }
}

/// <summary>A UI test on Avalonia's dispatcher that needs real Windows: skipped on every other system.</summary>
[AttributeUsage(AttributeTargets.Method)]
[Xunit.Sdk.XunitTestCaseDiscoverer("Avalonia.Headless.XUnit.AvaloniaUIFactDiscoverer", "Avalonia.Headless.XUnit")]
public sealed class WindowsAvaloniaFactAttribute : FactAttribute
{
    /// <inheritdoc />
    public override string? Skip
    {
        get => base.Skip ?? (OperatingSystem.IsWindows() ? null : "Needs Windows: the Windows CI job runs it.");
        set => base.Skip = value;
    }
}
