using TabStudio.Testing;
using TabStudio.Uno.Hosting;
using TabStudio.Uno.Templates;

namespace TabStudio.Uno.Tests;

/// <summary>
/// Uno Platform running in this process as it runs in the IDE: TabStudio's host compiled against the restored Uno
/// assemblies, Uno's Skia renderer drawing the app preview's frames, each build of an app launched as an Uno
/// secondary app, and taps and keys going to it.
/// </summary>
[Collection(NuGetTestGroup.Name)]
public sealed class UnoRuntimeTests(NuGetFixture nuget) : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public async Task The_Uno_Platform_App_shows_its_text_in_the_preview()
    {
        using var app = await UnoAppWorkspace.CreateAsync(nuget, _temp, UnoTemplates.UnoAppId, "HelloUno");
        var build = await app.BuildAsync();
        var runtime = await StartAsync(app);

        await using var running = await runtime.LaunchAsync(app.Project, build.EntryAssembly!, build.Dependencies, app.Assets);

        await PreviewFrames.WaitForTextAsync(running, "Hello Uno Platform!");
        using var frame = await PreviewFrames.SaveAsync(runtime, "uno-app-template");
        Assert.Equal((960, 600), (frame.Width, frame.Height));
        Assert.True(PreviewFrames.CountDrawnPixels(frame) > 100, "The frame shows no text.");
        Assert.True(File.Exists(Path.Combine(runtime.ContentDirectory, "Uno.Fonts.OpenSans", "Fonts", "OpenSans.ttf")));
    }

    [Fact]
    public async Task A_click_changes_the_text_and_a_saved_change_shows_after_a_restart()
    {
        using var app = await UnoAppWorkspace.CreateAsync(nuget, _temp, UnoTemplates.UnoAppId, "HelloUno");
        app.Save("MainPage.xaml", UnoXamlBuildTests.ClickingPage);
        app.Save("MainPage.xaml.cs", UnoXamlBuildTests.ClickingCodeBehind);
        var runtime = await StartAsync(app);

        var build = await app.BuildAsync();
        System.Reflection.Assembly first;
        await using (var running = await runtime.LaunchAsync(app.Project, build.EntryAssembly!, build.Dependencies, app.Assets))
        {
            first = running.Assembly;
            await PreviewFrames.WaitForTextAsync(running, "Hello Uno Platform!");
            await ClickAsync(runtime, running, "SayHello");
            await PreviewFrames.WaitForTextAsync(running, "Hello from TabStudio!");
            (await PreviewFrames.SaveAsync(runtime, "uno-app-clicked")).Dispose();
        }

        // Saving restarts the app with the new build, in a new load context, as the live preview does.
        app.Save("MainPage.xaml", UnoXamlBuildTests.ClickingPage.Replace("Hello Uno Platform!", "Saved and restarted", StringComparison.Ordinal));
        build = await app.BuildAsync();
        await using var restarted = await runtime.LaunchAsync(app.Project, build.EntryAssembly!, build.Dependencies, app.Assets);

        await PreviewFrames.WaitForTextAsync(restarted, "Saved and restarted");
        var texts = await restarted.GetTextsAsync();
        Assert.DoesNotContain("Hello from TabStudio!", texts);
        Assert.NotSame(first, restarted.Assembly);
        Assert.NotEqual(System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(first)!.Name, System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(restarted.Assembly)!.Name);
    }

    [Fact]
    public async Task An_exception_in_the_apps_code_reaches_TabStudio_with_its_stack_and_the_app_keeps_running()
    {
        using var app = await UnoAppWorkspace.CreateAsync(nuget, _temp, UnoTemplates.UnoAppId, "HelloUno");
        app.Save("MainPage.xaml", UnoXamlBuildTests.ClickingPage);
        app.Save("MainPage.xaml.cs", UnoXamlBuildTests.ClickingCodeBehind.Replace(
            "=> Greeting.Text = \"Hello from TabStudio!\";",
            "=> throw new InvalidOperationException(\"The button broke.\");",
            StringComparison.Ordinal));
        var runtime = await StartAsync(app);
        var build = await app.BuildAsync();
        await using var running = await runtime.LaunchAsync(app.Project, build.EntryAssembly!, build.Dependencies, app.Assets);
        var thrown = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        running.UnhandledException += (_, e) => thrown.TrySetResult((Exception)e.ExceptionObject);

        await PreviewFrames.WaitForTextAsync(running, "Hello Uno Platform!");
        await ClickAsync(runtime, running, "SayHello");

        var exception = await thrown.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal("The button broke.", exception.Message);
        Assert.Contains("OnSayHello", exception.StackTrace, StringComparison.Ordinal);
        Assert.Contains("Hello Uno Platform!", await running.GetTextsAsync());
    }

    [Fact]
    public async Task A_WinUI_3_app_runs_with_its_window_title_and_page()
    {
        using var app = await UnoAppWorkspace.CreateAsync(nuget, _temp, UnoTemplates.WinUIAppId, "Notes");
        app.Save("MainPage.xaml", app.Read("MainPage.xaml").Replace(
            "<Grid />",
            "<Grid Padding=\"24\">\n        <TextBlock Text=\"Hello from WinUI 3 on Uno Platform\" FontSize=\"24\" />\n    </Grid>",
            StringComparison.Ordinal));
        var build = await app.BuildAsync();
        var runtime = await StartAsync(app);

        await using var running = await runtime.LaunchAsync(app.Project, build.EntryAssembly!, build.Dependencies, app.Assets);

        await PreviewFrames.WaitForTextAsync(running, "Hello from WinUI 3 on Uno Platform");
        Assert.Equal("Notes", await running.GetTitleAsync());
        using var frame = await PreviewFrames.SaveAsync(runtime, "winui-app-template");
        Assert.True(PreviewFrames.CountDrawnPixels(frame) > 100, "The frame shows no text.");
    }

    [Fact]
    public async Task Typing_goes_to_the_focused_text_box()
    {
        using var app = await UnoAppWorkspace.CreateAsync(nuget, _temp, UnoTemplates.UnoAppId, "HelloUno");
        app.Save("MainPage.xaml", """
            <Page x:Class="HelloUno.MainPage"
                  xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center" Width="300">
                <TextBox x:Name="Input" />
              </StackPanel>
            </Page>
            """);
        var build = await app.BuildAsync();
        var runtime = await StartAsync(app);
        await using var running = await runtime.LaunchAsync(app.Project, build.EntryAssembly!, build.Dependencies, app.Assets);
        await WaitForElementAsync(running, "Input");

        await ClickAsync(runtime, running, "Input");
        runtime.SendText("Uno", UnoKeyModifiers.None);

        await PreviewFrames.WaitForTextAsync(running, "Uno");
    }

    public void Dispose() => _temp.Dispose();

    private Task<UnoRuntime> StartAsync(UnoAppWorkspace app) =>
        UnoRuntime.StartAsync(app.Assets, new UnoRuntimeOptions { CacheDirectory = nuget.CacheDirectory, Width = 960, Height = 600, Scale = 1, DarkTheme = false });

    private static async Task<UnoElementBounds> WaitForElementAsync(UnoApp app, string name)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (true)
        {
            if (await app.FindElementAsync(name) is { Width: > 0 } bounds)
            {
                return bounds;
            }

            Assert.True(DateTime.UtcNow < deadline, $"The app shows no element named {name}.");
            await Task.Delay(100);
        }
    }

    /// <summary>Taps an element with the mouse, as a tap in the app preview does.</summary>
    private static async Task ClickAsync(UnoRuntime runtime, UnoApp app, string name)
    {
        var (x, y) = (await WaitForElementAsync(app, name)).Center;
        // Native pointer frame ids and timestamps increase across app restarts, as Avalonia's do.
        var timestamp = (ulong)(System.Diagnostics.Stopwatch.GetElapsedTime(0).Ticks / 10);
        runtime.SendPointer(new UnoPointerEvent { Action = UnoPointerAction.Entered, Device = UnoPointerDevice.Mouse, X = x, Y = y, Timestamp = timestamp });
        runtime.SendPointer(new UnoPointerEvent { Action = UnoPointerAction.Moved, Device = UnoPointerDevice.Mouse, X = x, Y = y, Timestamp = timestamp + 1 });
        runtime.SendPointer(new UnoPointerEvent { Action = UnoPointerAction.Pressed, Device = UnoPointerDevice.Mouse, X = x, Y = y, IsLeftPressed = true, Timestamp = timestamp + 2 });
        await Task.Delay(50);
        runtime.SendPointer(new UnoPointerEvent { Action = UnoPointerAction.Released, Device = UnoPointerDevice.Mouse, X = x, Y = y, Timestamp = timestamp + 50_000 });
    }
}
