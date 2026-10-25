using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.Services.WindowsBridge;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Link.Networking;
using TabStudio.Testing;

namespace TabStudio.App.Tests.WindowsBridge;

/// <summary>
/// A tablet and a Windows PC, each a TabStudio of its own in this process: the PC is a Windows Bridge on the loopback
/// address, with a desktop the test provides, and the tablet is the app under test, in a window, pairing with it.
/// </summary>
internal sealed class BridgeDevices : IAsyncDisposable
{
    private readonly List<IDisposable> _disposables = [];

    private BridgeDevices(TempDirectory folder)
    {
        Folder = folder;
        _disposables.Add(folder);
    }

    /// <summary>A folder for the tablet's workspaces.</summary>
    public TempDirectory Folder { get; }

    /// <summary>The PC's app.</summary>
    public ServiceProvider PcServices { get; private set; } = null!;

    /// <summary>The PC's Windows Bridge.</summary>
    public WindowsBridgeService Pc { get; private set; } = null!;

    /// <summary>The tablet's app.</summary>
    public ServiceProvider TabletServices { get; private set; } = null!;

    /// <summary>The tablet's main view model.</summary>
    public MainViewModel Main { get; private set; } = null!;

    /// <summary>The tablet's window.</summary>
    public MainWindow Window { get; private set; } = null!;

    /// <summary>The tablet's Windows Bridge.</summary>
    public WindowsBridgeService Tablet { get; private set; } = null!;

    /// <summary>The tablet's open workspace.</summary>
    public WorkspaceViewModel Workspace => Main.Workspace ?? throw new InvalidOperationException("No workspace is open.");

    /// <summary>Starts both devices; the PC's bridge is not started yet.</summary>
    /// <param name="desktop">The PC's desktop.</param>
    /// <param name="pcName">The PC's name.</param>
    /// <param name="tabletName">The tablet's name.</param>
    public static BridgeDevices Start(IBridgeDesktopFactory desktop, string pcName = "Test PC", string tabletName = "Galaxy Tab S9")
    {
        var devices = new BridgeDevices(new TempDirectory());
        devices.PcServices = devices.Track(AppServices.Create(
            new AppPaths(devices.Folder.Resolve("PcApp")),
            s =>
            {
                s.AddSingleton<IDeviceIdentity>(new TestDeviceIdentity(pcName, LinkDeviceKinds.Computer));
                s.AddSingleton(desktop);
            }));
        devices.Pc = devices.PcServices.GetRequiredService<WindowsBridgeService>();

        devices.TabletServices = devices.Track(AppServices.Create(
            new AppPaths(devices.Folder.Resolve("TabletApp")),
            s => s.AddSingleton<IDeviceIdentity>(new TestDeviceIdentity(tabletName, LinkDeviceKinds.Tablet))));
        devices.Main = devices.TabletServices.GetRequiredService<MainViewModel>();
        devices.Tablet = devices.TabletServices.GetRequiredService<WindowsBridgeService>();
        devices.Window = new MainWindow { DataContext = devices.Main, Width = 1280, Height = 800 };
        devices.Window.Show();
        Dispatcher.UIThread.RunJobs();
        return devices;
    }

    /// <summary>Starts the PC's bridge on the loopback address and pairs the tablet with it.</summary>
    public async Task PairAsync()
    {
        Assert.True(await Pc.StartBridgeAsync(port: 0, loopbackOnly: true), Pc.BridgeError);
        var invitation = Pc.BridgeServer!.CreateInvitation(["127.0.0.1"]) ?? throw new InvalidOperationException("The bridge made no invitation.");
        Assert.True(await Tablet.PairAsync(LinkTarget.FromInvitation(invitation)), Tablet.Error);
        await UiTestHelpers.WaitUntilAsync(() => Tablet.IsConnected && Tablet.Info is not null, because: "the PC");
    }

    /// <summary>Opens a folder of projects on the tablet.</summary>
    public async Task<WorkspaceViewModel> OpenAsync(string folder)
    {
        await Main.OpenWorkspaceAsync(folder);
        await UiTestHelpers.WaitUntilAsync(
            () => Main.Workspace is { Workspace: not null, Compatibility.Count: > 0, State: IdeState.Ready or IdeState.Failed },
            TimeSpan.FromMinutes(2),
            "the workspace");
        return Workspace;
    }

    /// <summary>Writes a .NET console program that prints a line and waits, as a program with a window would run.</summary>
    public string WriteConsoleProject(string name, string program = "Console.WriteLine(\"Hello from the PC\");\nConsole.ReadLine();\n")
    {
        Folder.Write($"Tablet/Solution/{name}/{name}.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);
        Folder.Write($"Tablet/Solution/{name}/Program.cs", program);
        return Folder.Resolve($"Tablet/Solution/{name}/{name}.csproj");
    }

    /// <summary>Writes a WPF app, which runs only on Windows.</summary>
    public string WriteWpfProject(string name)
    {
        Folder.Write($"Tablet/Solution/{name}/{name}.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>WinExe</OutputType>
                <TargetFramework>net10.0-windows</TargetFramework>
                <UseWPF>true</UseWPF>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);
        Folder.Write($"Tablet/Solution/{name}/MainWindow.xaml", $$"""
            <Window x:Class="{{name}}.MainWindow"
                    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    Title="{{name}}" Width="520" Height="360">
                <Grid x:Name="Root" Background="#F3F3F3">
                    <Button Content="Click me" Width="200" Height="64" Click="OnClick" />
                </Grid>
            </Window>
            """);
        Folder.Write($"Tablet/Solution/{name}/MainWindow.xaml.cs", $$"""
            using System.Windows;
            using System.Windows.Media;

            namespace {{name}};

            public partial class MainWindow : Window
            {
                public MainWindow() => InitializeComponent();

                private void OnClick(object sender, RoutedEventArgs e) =>
                    Root.Background = new SolidColorBrush(Color.FromRgb(0x20, 0xA0, 0x40));
            }
            """);
        return Folder.Resolve($"Tablet/Solution/{name}/{name}.csproj");
    }

    /// <summary>The folder the projects are written to.</summary>
    public string SolutionFolder => Folder.Resolve("Tablet/Solution");

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Main.Workspace?.Dispose();
        await Tablet.DisposeAsync();
        await Pc.DisposeAsync();
        Window.Close();
        for (var i = _disposables.Count - 1; i >= 0; i--)
        {
            _disposables[i].Dispose();
        }
    }

    private T Track<T>(T disposable)
        where T : IDisposable
    {
        _disposables.Add(disposable);
        return disposable;
    }
}
