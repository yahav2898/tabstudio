using System.Collections.Concurrent;
using System.Net;
using TabStudio.Compilation;
using TabStudio.Testing;
using TabStudio.Web.Serving;
using TabStudio.Wpf.Browser;
using TabStudio.Wpf.Templates;

namespace TabStudio.Wpf.Tests;

/// <summary>
/// A WPF app built by TabStudio on OpenSilver, served by the WebAssembly host with the real .NET for WebAssembly, and
/// run by a real browser: its window fills the page under its title, clicks reach its handlers, and its console
/// output and exceptions are posted back.
/// </summary>
[Collection(NuGetTestGroup.Name)]
public sealed class WpfBrowserTests(NuGetFixture nuget) : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public async Task Serves_the_app_with_OpenSilver_and_Blazor()
    {
        WriteApp();
        using var workspace = await WpfAppWorkspace.LoadAsync(nuget, _temp.Path);
        await using var host = await workspace.StartAsync("WpfApp1");

        var page = await nuget.Http.GetStringAsync(host.Address);
        Assert.Contains("libs/opensilver.js", page, StringComparison.Ordinal);
        Assert.Contains("_framework/blazor.webassembly.js", page, StringComparison.Ordinal);
        Assert.Contains("<div id=\"opensilver-root\"", page, StringComparison.Ordinal);
        Assert.Contains("<link href=\"_framework/dotnet.js\" rel=\"preload\"", page, StringComparison.Ordinal);

        var loader = await nuget.Http.GetStringAsync(new Uri(host.Address, "_framework/dotnet.js"));
        Assert.Contains("\"mainAssemblyName\": \"WpfApp1.Browser\"", loader, StringComparison.Ordinal);
        foreach (var route in new[]
        {
            "libs/opensilver.js", "libs/cshtml5.js", "libs/cshtml5.css", "libs/quill.min.js", "_tabstudio/wpf.js", "_tabstudio/wpf.css",
            "_framework/blazor.webassembly.js", "_framework/WpfApp1.dll", "_framework/WpfApp1.pdb", "_framework/WpfApp1.Browser.dll",
            "_framework/OpenSilver.dll", "_framework/OpenSilver.WebAssembly.dll", "_framework/Microsoft.AspNetCore.Components.WebAssembly.dll",
        })
        {
            var response = await nuget.Http.GetAsync(new Uri(host.Address, route));
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{route}: {response.StatusCode}");
        }
    }

    [ChromeFact]
    public async Task Shows_the_window_full_size_under_its_title_and_handles_clicks()
    {
        WriteApp();
        using var workspace = await WpfAppWorkspace.LoadAsync(nuget, _temp.Path);
        await using var host = await workspace.StartAsync("WpfApp1");
        var messages = new ConcurrentQueue<PostedMessage>();
        host.MessageReceived += (_, message) => messages.Enqueue(message);
        await using var chrome = await HeadlessChrome.StartAsync(1024, 700);

        await chrome.NavigateAsync(host.Address);
        await chrome.WaitUntilAsync("document.body.innerText.includes('Hello from WPF')", TimeSpan.FromSeconds(120));
        await chrome.WaitUntilAsync("document.getElementById('tabstudio-wpf-title').textContent === 'Greetings'", TimeSpan.FromSeconds(10));

        // The window fills the page under the title bar, whatever its Width and Height say.
        var size = await chrome.EvaluateAsync("""
            (() => {
                const client = document.getElementById('tabstudio-wpf-client').getBoundingClientRect();
                const window = [...document.getElementById('tabstudio-wpf-client').children].find(c => c.id !== 'tabstudio-wpf-starting').getBoundingClientRect();
                return [Math.round(client.width), Math.round(client.height), Math.round(window.width), Math.round(window.height)];
            })()
            """);
        Assert.Equal(1024, size![0]!.GetValue<int>());
        Assert.Equal(size[0]!.GetValue<int>(), size[2]!.GetValue<int>());
        Assert.Equal(size[1]!.GetValue<int>(), size[3]!.GetValue<int>());
        Assert.Equal("Greetings", (await chrome.EvaluateAsync("document.title"))?.GetValue<string>());

        await chrome.ClickTextAsync("Click me");
        await chrome.WaitUntilAsync("document.body.innerText.includes('Clicked 1 time')", TimeSpan.FromSeconds(10));
        await chrome.ClickTextAsync("Click me");
        await chrome.WaitUntilAsync("document.body.innerText.includes('Clicked 2 times')", TimeSpan.FromSeconds(10));
        await SaveScreenshotAsync(chrome, "wpf-app-in-browser.png");

        await WaitForAsync(() => messages.Any(m => m.Route == WpfBrowserApps.ConsoleRoute && m.Body.Contains("The window is up", StringComparison.Ordinal)), messages);
        await WaitForAsync(() => messages.Any(m => m.Route == WpfBrowserApps.ConsoleRoute && m.Body.Contains("Clicked 2 times", StringComparison.Ordinal)), messages);
        Assert.Contains(messages, m => m.Route == WpfBrowserApps.TitleRoute && m.Body == "Greetings");
    }

    [ChromeFact]
    public async Task Reports_an_exception_in_a_handler_with_its_stack()
    {
        WriteApp();
        using var workspace = await WpfAppWorkspace.LoadAsync(nuget, _temp.Path);
        await using var host = await workspace.StartAsync("WpfApp1");
        var messages = new ConcurrentQueue<PostedMessage>();
        host.MessageReceived += (_, message) => messages.Enqueue(message);
        await using var chrome = await HeadlessChrome.StartAsync();

        await chrome.NavigateAsync(host.Address);
        await chrome.WaitUntilAsync("document.body.innerText.includes('Hello from WPF')", TimeSpan.FromSeconds(120));
        await chrome.ClickTextAsync("Fail");

        await WaitForAsync(() => messages.Any(m => m.Body.Contains("Boom", StringComparison.Ordinal) && m.Body.Contains("WpfApp1.MainWindow.Fail_Click", StringComparison.Ordinal)), messages);
        var report = messages.First(m => m.Body.Contains("Boom", StringComparison.Ordinal));
        Assert.Contains("\"level\":\"error\"", report.Body, StringComparison.Ordinal);
        Assert.Contains("Unhandled exception in WpfApp1", report.Body, StringComparison.Ordinal);

        // The app keeps running after it.
        await chrome.ClickTextAsync("Click me");
        await chrome.WaitUntilAsync("document.body.innerText.includes('Clicked 1 time')", TimeSpan.FromSeconds(10));
    }

    [ChromeFact]
    public async Task A_new_build_is_served_at_the_same_address()
    {
        WriteApp();
        using var workspace = await WpfAppWorkspace.LoadAsync(nuget, _temp.Path);
        await using var host = await workspace.StartAsync("WpfApp1");
        await using var chrome = await HeadlessChrome.StartAsync();
        await chrome.NavigateAsync(host.Address);
        await chrome.WaitUntilAsync("document.body.innerText.includes('Hello from WPF')", TimeSpan.FromSeconds(120));
        var address = host.Address;

        File.WriteAllText(_temp.Resolve("WpfApp1/MainWindow.xaml"), File.ReadAllText(_temp.Resolve("WpfApp1/MainWindow.xaml")).Replace("Hello from WPF", "Hello again", StringComparison.Ordinal));
        workspace.Host.UpdateText(_temp.Resolve("WpfApp1/MainWindow.xaml"), File.ReadAllText(_temp.Resolve("WpfApp1/MainWindow.xaml")));
        var build = await workspace.BuildAsync("WpfApp1");
        var prepared = await new WpfAppPreparer(nuget.Restorer).PrepareAsync(workspace.Project("WpfApp1"), build, ReferenceAssemblies.Net100);
        await host.UpdateAsync(prepared.App!);
        await chrome.NavigateAsync(host.Address);

        await chrome.WaitUntilAsync("document.body.innerText.includes('Hello again')", TimeSpan.FromSeconds(120));
        Assert.Equal(address, host.Address);
    }

    public void Dispose() => _temp.Dispose();

    /// <summary>The template with a greeting, a button that counts clicks and one that throws.</summary>
    private void WriteApp()
    {
        WpfTemplates.Create(_temp.Path, "WpfApp1");
        _temp.Write("WpfApp1/MainWindow.xaml", """
            <Window x:Class="WpfApp1.MainWindow"
                    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    Title="Greetings" Height="450" Width="800">
                <StackPanel Margin="24">
                    <TextBlock x:Name="Greeting" Text="Hello from WPF" FontSize="24" />
                    <Button Content="Click me" Click="ClickMe_Click" Margin="0,12,0,0" Padding="12,6" HorizontalAlignment="Left" />
                    <Button Content="Fail" Click="Fail_Click" Margin="0,12,0,0" Padding="12,6" HorizontalAlignment="Left" />
                </StackPanel>
            </Window>
            """);
        _temp.Write("WpfApp1/MainWindow.xaml.cs", """
            using System.Windows;

            namespace WpfApp1
            {
                public partial class MainWindow : Window
                {
                    private int _clicks;

                    public MainWindow()
                    {
                        InitializeComponent();
                        Console.WriteLine("The window is up");
                    }

                    private void ClickMe_Click(object sender, RoutedEventArgs e)
                    {
                        _clicks++;
                        Greeting.Text = _clicks == 1 ? "Clicked 1 time" : $"Clicked {_clicks} times";
                        Console.WriteLine(Greeting.Text);
                    }

                    private void Fail_Click(object sender, RoutedEventArgs e) => throw new InvalidOperationException("Boom");
                }
            }
            """);
    }

    private static async Task SaveScreenshotAsync(HeadlessChrome chrome, string name)
    {
        var directory = Path.Combine(Environment.GetEnvironmentVariable("GITHUB_WORKSPACE") ?? AppContext.BaseDirectory, "ci-out", "wpf-screenshots");
        Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(Path.Combine(directory, name), await chrome.ScreenshotAsync());
    }

    private static async Task WaitForAsync(Func<bool> condition, IEnumerable<PostedMessage> messages)
    {
        var waited = System.Diagnostics.Stopwatch.StartNew();
        while (!condition())
        {
            if (waited.Elapsed > TimeSpan.FromSeconds(20))
            {
                Assert.Fail("The page did not post what was expected. It posted:\n" + string.Join('\n', messages.Select(m => $"{m.Route}: {m.Body}")));
            }

            await Task.Delay(100);
        }
    }
}
