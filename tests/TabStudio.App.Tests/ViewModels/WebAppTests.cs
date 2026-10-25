using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>
/// Web apps in the IDE, against the real nuget.org: the web template restores ASP.NET Core, F5 serves it and
/// shows it in the web preview, saving a file restarts it, and Stop ends it.
/// </summary>
public sealed class WebAppTests
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(4);

    [AvaloniaFact]
    public async Task F5_serves_the_web_template_in_the_preview_restarts_it_on_save_and_stops_it()
    {
        using var fixture = new WorkspaceFixture();
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var solution = new ProjectTemplates(fixture.Paths).Create(ProjectTemplates.WebId, "HelloWeb");
        var workspace = fixture.Workspace;
        await workspace.LoadAsync(solution);
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = workspace } };
        window.Show();

        Assert.False(workspace.WebPreview.HasApp);
        var run = workspace.StartDebuggingCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.WebPreview.IsRunning || run.IsCompleted, StartTimeout, "the web app to start");

        Assert.True(workspace.WebPreview.IsRunning, workspace.Output.Text);
        Assert.True(workspace.IsWebPreviewSelected);
        Assert.True(workspace.IsRunning);
        var address = workspace.WebPreview.AppAddress!;
        Assert.Equal(address.ToString(), workspace.WebPreview.AddressText);
        Assert.Contains("<h1>HelloWeb</h1>", await http.GetStringAsync(address), StringComparison.Ordinal);
        Assert.Contains("\"message\":\"Hello, Tablet!\"", await http.GetStringAsync(new Uri(address, "api/greeting?name=Tablet")), StringComparison.Ordinal);
        Assert.Contains("Started without the debugger", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Contains($"HelloWeb is listening on {address}", workspace.Output.Text, StringComparison.Ordinal);
        UiTestHelpers.SaveScreenshot(window, "web-01-preview");

        // Saving a source file rebuilds the app and starts it again at the same address.
        var program = Path.Combine(Path.GetDirectoryName(solution)!, "HelloWeb", "Program.cs");
        var document = await workspace.OpenDocumentAsync(program);
        var text = document!.Document.Text;
        var greeting = text.IndexOf("Hello, ", StringComparison.Ordinal);
        document.Document.Replace(greeting, "Hello, ".Length, "Shalom, ");
        await workspace.SaveCommand.ExecuteAsync(null);
        await WaitForAsync(http, new Uri(address, "api/greeting?name=Tablet"), "Shalom, Tablet!");

        Assert.True(workspace.WebPreview.IsRunning);
        Assert.Equal(address, workspace.WebPreview.AppAddress);
        Assert.Contains("Restarting 'HelloWeb' with the saved changes.", workspace.Output.Text, StringComparison.Ordinal);

        workspace.StopDebuggingCommand.Execute(null);
        await run.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.False(workspace.WebPreview.IsRunning);
        Assert.False(workspace.IsRunning);
        Assert.True(workspace.WebPreview.HasApp);
        Assert.Contains("The program 'HelloWeb' has exited with code 0", workspace.Output.Text, StringComparison.Ordinal);
        await Assert.ThrowsAsync<HttpRequestException>(() => http.GetStringAsync(address));
        window.Close();
    }

    /// <summary>Waits until a page answers with some text, while the app restarts.</summary>
    private static async Task WaitForAsync(HttpClient http, Uri page, string expected)
    {
        string? last = null;
        await UiTestHelpers.WaitUntilAsync(
            () =>
            {
                if (last?.Contains(expected, StringComparison.Ordinal) == true)
                {
                    return true;
                }

                _ = Fetch();
                return false;
            },
            StartTimeout,
            $"{page} to answer {expected}");

        async Task Fetch()
        {
            try
            {
                last = await http.GetStringAsync(page);
            }
            catch (HttpRequestException)
            {
                // The app is restarting.
            }
            catch (TaskCanceledException)
            {
                // The app is restarting.
            }
        }
    }
}
