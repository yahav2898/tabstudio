using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Accessibility;
using TabStudio.App.Services;
using TabStudio.App.Tests.Theming;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Accessibility;

/// <summary>
/// The IDE at Very large, the largest text size, on a landscape tablet, a portrait tablet and a phone, as a person who
/// chose it sees it: the screenshots of docs/design/README.md "Text size", each audited like every other screenshot
/// (<see cref="AccessibilityReport"/>), clipping included. Also the views no other screenshot shows.
/// </summary>
[Collection(AppearanceTestGroup.Name)]
public sealed class AccessibilityTourTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [AvaloniaFact]
    public async Task The_shell_at_the_largest_text_size_on_every_screen()
    {
        using var fixture = new WorkspaceFixture();
        var solution = await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        var program = Directory.EnumerateFiles(Path.GetDirectoryName(solution)!, "Program.cs", SearchOption.AllDirectories).First();
        await workspace.OpenDocumentAsync(program);
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = workspace } };
        window.Show();
        TypeScale.Apply(TextSizes.Largest);
        try
        {
            foreach (var (name, width, height) in new[] { ("landscape", 1280.0, 800.0), ("portrait", 800.0, 1280.0), ("phone", 412.0, 915.0) })
            {
                window.Width = width;
                window.Height = height;
                await UiTestHelpers.WaitUntilAsync(() => true);
                UiTestHelpers.SaveScreenshot(window, $"a11y-very-large-{name}");

                workspace.SelectPanelCommand.Execute(BottomPanel.Watch);
                await UiTestHelpers.WaitUntilAsync(() => true);
                UiTestHelpers.SaveScreenshot(window, $"a11y-very-large-{name}-watch");
                workspace.SelectPanelCommand.Execute(BottomPanel.ErrorList);
            }
        }
        finally
        {
            TypeScale.Apply(1);
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Joining_a_Live_session()
    {
        using var services = AppServices.Create(new AppPaths(_temp.Path));
        var main = services.GetRequiredService<MainViewModel>();
        var window = new MainWindow { DataContext = main, Width = 1280, Height = 800 };
        window.Show();
        try
        {
            Assert.NotNull(main.LiveJoin);
            main.LiveJoin!.Open();
            await UiTestHelpers.WaitUntilAsync(() => true);
            UiTestHelpers.SaveScreenshot(window, "a11y-live-join");
            window.Width = 412;
            window.Height = 915;
            await UiTestHelpers.WaitUntilAsync(() => true);
            UiTestHelpers.SaveScreenshot(window, "a11y-live-join-phone");
        }
        finally
        {
            window.Close();
        }
    }

    public void Dispose() => _temp.Dispose();
}
