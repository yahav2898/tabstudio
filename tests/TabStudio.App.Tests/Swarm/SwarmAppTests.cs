using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TabStudio.App.Services;
using TabStudio.App.Services.Agent;
using TabStudio.App.Swarm;
using TabStudio.App.Swarm.LayoutChecks;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.App.ViewModels.Agent;
using TabStudio.App.Views;

namespace TabStudio.App.Tests.Swarm;

/// <summary>
/// Swarm's screens in the IDE, against the real nuget.org: the Avalonia template on the app preview and on virtual
/// devices in the Device Matrix, layout checks that land in the Error List and open the XAML, the preview answering
/// the screen contract, and Fix with Agent sending Claude a screenshot.
/// </summary>
public sealed class SwarmAppTests
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(4);
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// A shop's view: a status line that only fits a wide screen, Hebrew text, a help button too small to tap, and the
    /// template's counter.
    /// </summary>
    private const string ShopView = """
        <UserControl xmlns="https://github.com/avaloniaui"
                     xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                     xmlns:vm="using:Shop.ViewModels"
                     x:Class="Shop.Views.MainView"
                     x:DataType="vm:MainViewModel">
          <StackPanel Margin="24" Spacing="16">
            <Grid ColumnDefinitions="*,Auto">
              <TextBlock Text="{Binding Greeting}" FontSize="24" FontWeight="SemiBold" />
              <Button x:Name="Help" Grid.Column="1" Content="?" Width="28" Height="28" MinWidth="0" MinHeight="0" Padding="0"
                      HorizontalContentAlignment="Center" VerticalContentAlignment="Center" />
            </Grid>
            <Grid ColumnDefinitions="140,*">
              <TextBlock Text="Status" Opacity="0.7" />
              <TextBlock x:Name="Summary" Grid.Column="1" Text="All orders shipped on time this quarter" />
            </Grid>
            <TextBlock x:Name="Hebrew" Text="שלום! ההזמנה שלך בדרך אליך" FontSize="18" TextWrapping="Wrap" />
            <Button x:Name="CountButton" Content="Count" Command="{Binding CountCommand}" MinWidth="120" MinHeight="48"
                    HorizontalContentAlignment="Center" VerticalContentAlignment="Center" />
            <TextBlock Text="{Binding Clicks, StringFormat='Clicked {0} times'}" />
          </StackPanel>
        </UserControl>

        """;

    [AvaloniaFact]
    public async Task The_template_runs_on_three_virtual_devices_and_a_status_cut_off_on_the_small_phone_opens_its_XAML()
    {
        using var fixture = new WorkspaceFixture();
        var (workspace, window, run, viewPath) = await StartShopAsync(fixture);
        workspace.IsSideBarVisible = false;
        workspace.IsBottomPanelVisible = false;

        // A small phone, a foldable and a tablet, each with its own instance of the app.
        workspace.ShowDeviceMatrixCommand.Execute(null);
        var matrix = workspace.DeviceMatrix!;
        Assert.True(workspace.ActiveDocument!.IsDeviceMatrix);
        foreach (var tile in matrix.Tiles.Where(t => t.IsVirtual).ToList())
        {
            matrix.RemoveDevice(tile);
        }

        matrix.AddDeviceCommand.Execute(DevicePresets.SmallPhone);
        matrix.AddDeviceCommand.Execute(DevicePresets.Foldable);
        matrix.AddDeviceCommand.Execute(DevicePresets.Tablet);
        var devices = matrix.Tiles.Where(t => t.IsVirtual).Select(t => t.Virtual!).ToList();
        Assert.Equal(new[] { "Small phone", "Foldable", "Tablet" }, devices.Select(d => d.Profile.Name));
        await UiTestHelpers.WaitUntilAsync(() => devices.All(d => d.IsRunning && Texts(d.AppRoot!).Contains("Clicked 0 times")), StartTimeout, "the app on every virtual device");
        Assert.Equal(4, workspace.Screens.Screens.Count);
        Assert.Equal(AppScreenKind.Preview, workspace.Screens.Screens[0].Kind);
        var viewModels = devices.Select(d => d.AppRoot!.DataContext).Append(workspace.PreviewScreen.AppRoot!.DataContext).ToList();
        Assert.Equal(4, viewModels.Distinct(ReferenceEqualityComparer.Instance).Count());

        // A tap replayed on the small phone counts there, through its own button, and nowhere else.
        var phone = devices[0];
        var phoneButton = phone.AppRoot!.GetLogicalDescendants().OfType<Button>().Single(b => b.Name == "CountButton");
        var tap = new AppInput(AppInputKind.Tap, ElementPaths.Describe(phone.AppRoot!, phoneButton)!);
        Assert.True(await SwarmTestHelpers.WithFramesAsync(phone.SendInputAsync(tap, CancellationToken.None)), phone.LastReplayMiss);
        await UiTestHelpers.WaitUntilAsync(() => Texts(phone.AppRoot!).Contains("Clicked 1 times"), because: "the tap on the phone");
        Assert.Contains("Clicked 0 times", Texts(devices[2].AppRoot!));
        Assert.Contains("Clicked 0 times", Texts(workspace.PreviewScreen.AppRoot!));

        // The status line is cut off on the small phone, and fits on the foldable and the tablet.
        await UiTestHelpers.WaitUntilAsync(() => Row(workspace, "SWRM001", "TextBlock#Summary") is not null && workspace.LayoutChecks.Checking == 0, CheckTimeout, "the layout checks");
        var clipped = Row(workspace, "SWRM001", "TextBlock#Summary")!;
        var screens = clipped.Layout!.Finding.Screens.Select(s => s.Profile.Name).ToList();
        Assert.Contains("Small phone", screens);
        Assert.DoesNotContain("Tablet", screens);
        Assert.DoesNotContain("Foldable", screens);
        Assert.Equal(LayoutRules.HelpLink(LayoutIssueKind.ClippedText), clipped.HelpLink);
        Assert.True(clipped.IsError);
        var location = clipped.Diagnostic.Location!;
        Assert.Equal(viewPath, location.FilePath);
        Assert.Equal(LineOf(viewPath, "x:Name=\"Summary\""), location.Line);

        // The help button is too small to tap on every touch screen.
        var help = Row(workspace, "SWRM004", "Button#Help")!;
        Assert.Equal(4, help.Layout!.Finding.Screens.Count);

        matrix.Zoom = 0.33;
        UiTestHelpers.SaveScreenshot(window, "swarm-01-device-matrix");

        // The foldable in dark, right to left, with its Hebrew text, at full size.
        var foldable = matrix.Tiles.Single(t => t.Virtual == devices[1]);
        foldable.ToggleThemeCommand.Execute(null);
        foldable.ToggleDirectionCommand.Execute(null);
        Assert.True(devices[1].Profile.IsDark);
        Assert.Equal(TextDirection.RightToLeft, devices[1].Profile.Direction);
        await UiTestHelpers.WaitUntilAsync(() => foldable.Profile == devices[1].Profile, because: "the tile's header");
        Assert.Equal(Avalonia.Media.FlowDirection.RightToLeft, devices[1].AppRoot!.FlowDirection);
        matrix.ShowFullSize(foldable);
        await UiTestHelpers.WaitUntilAsync(() => workspace.LayoutChecks.Checking == 0, CheckTimeout, "the foldable's new checks");
        UiTestHelpers.SaveScreenshot(window, "swarm-02-rtl-dark-hebrew");
        matrix.CloseFullSizeCommand.Execute(null);

        // The screens button of the row outlines the element in the tiles that have it; the row opens its XAML.
        workspace.IsBottomPanelVisible = true;
        workspace.SelectPanelCommand.Execute(BottomPanel.ErrorList);
        workspace.ErrorList.ShowLayoutScreensCommand.Execute(clipped);
        Assert.Same(clipped.Layout.Finding, matrix.ShownFinding);
        Assert.True(matrix.Tiles.Single(t => t.Virtual == devices[0]).HasHighlights);
        Assert.False(matrix.Tiles.Single(t => t.Virtual == devices[2]).HasHighlights);
        UiTestHelpers.SaveScreenshot(window, "swarm-03-error-list-highlight");

        workspace.ErrorList.NavigateCommand.Execute(clipped);
        await UiTestHelpers.WaitUntilAsync(() => workspace.ActiveDocument?.FilePath == viewPath && workspace.ActiveDocument.CaretLine == location.Line, because: "the XAML element in the editor");
        Assert.Same(clipped.Layout.Finding, matrix.ShownFinding);

        workspace.StopCommand.Execute(null);
        await run.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.All(devices, d => Assert.False(d.IsRunning));
        Assert.DoesNotContain(workspace.PreviewScreen, workspace.Screens.Screens);
        window.Close();
    }

    [AvaloniaFact]
    public async Task The_app_preview_is_a_screen_that_replays_input_takes_pictures_and_checks_its_layout()
    {
        using var fixture = new WorkspaceFixture();
        var solution = new ProjectTemplates(fixture.Paths).Create(ProjectTemplates.AvaloniaId, "Counter");
        var workspace = fixture.Workspace;
        await workspace.LoadAsync(solution);
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = workspace } };
        window.Show();
        var run = workspace.RunCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.AppPreview.IsRunning || run.IsCompleted, StartTimeout, "the app to start");
        var preview = workspace.PreviewScreen;
        Assert.True(preview.IsRunning, workspace.Output.Text);
        Assert.Contains(preview, workspace.Screens.Screens);
        Assert.Equal(AppScreenKind.Preview, preview.Kind);
        Assert.Equal(FormFactor.Tablet, preview.Profile.FormFactor);
        var captured = new List<AppInput>();
        preview.InputCaptured += (_, input) => captured.Add(input);

        // A replayed tap counts, through the button's own input handling, and is not captured as a person's.
        var button = preview.AppRoot!.GetLogicalDescendants().OfType<Button>().Single(b => b.Name == "CountButton");
        var path = ElementPaths.Describe(preview.AppRoot!, button)!;
        Assert.Equal("MainView/StackPanel[0]/Button#CountButton", path);
        Assert.True(await SwarmTestHelpers.WithFramesAsync(preview.SendInputAsync(new AppInput(AppInputKind.Tap, path), CancellationToken.None)));
        await UiTestHelpers.WaitUntilAsync(() => Texts(preview.AppRoot!).Contains("Clicked 1 times"), because: "the replayed tap");
        Assert.Empty(captured);

        // A person's tap on the preview is captured, by its element.
        var center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        await UiTestHelpers.WaitUntilAsync(() => Texts(preview.AppRoot!).Contains("Clicked 2 times"), because: "the person's tap");
        Assert.Equal(path, Assert.Single(captured).ElementPath);

        // A picture of the screen, at its density.
        var png = await preview.CaptureAsync(CancellationToken.None);
        Assert.NotNull(png);
        using (var picture = new Bitmap(new MemoryStream(png)))
        {
            Assert.Equal(Math.Ceiling(preview.Profile.Width * preview.Profile.Scale), picture.PixelSize.Width, 0);
        }

        // The template's button is 32 high: too small for a finger on a tablet.
        var issues = await preview.InspectLayoutAsync(CancellationToken.None);
        Assert.Contains(issues, i => i.Kind == LayoutIssueKind.SmallTouchTarget && i.ElementPath == path);

        workspace.StopCommand.Execute(null);
        await run.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.False(preview.IsRunning);
        Assert.DoesNotContain(preview, workspace.Screens.Screens);
        Assert.False(await preview.SendInputAsync(new AppInput(AppInputKind.Tap, path), CancellationToken.None));
        Assert.Null(await preview.CaptureAsync(CancellationToken.None));
        Assert.Empty(await preview.InspectLayoutAsync(CancellationToken.None));
        window.Close();
    }

    [AvaloniaFact]
    public async Task Fix_with_Agent_on_a_layout_row_sends_Claude_the_problem_its_XAML_and_a_screenshot()
    {
        var claude = new FakeClaude().Text("The status now wraps on narrow screens.");
        using var http = new HttpClient(claude);
        using var fixture = new WorkspaceFixture(agentHttp: http);
        new AgentSettingsStore(fixture.Paths).Save(AgentSettings.Default with { ApiKey = "sk-ant-test" });
        var (workspace, window, run, _) = await StartShopAsync(fixture);
        workspace.ShowDeviceMatrixCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => Row(workspace, "SWRM001", "TextBlock#Summary") is not null, CheckTimeout, "the clipped status");
        var clipped = Row(workspace, "SWRM001", "TextBlock#Summary")!;

        await workspace.FixWithAgentAsync(clipped.Diagnostic);

        var request = claude.Requests.Single().ToJsonString();
        Assert.Contains("image/png", request, StringComparison.Ordinal);
        Assert.Contains("SWRM001 Clipped text", request, StringComparison.Ordinal);
        Assert.Contains("TextBlock#Summary", request, StringComparison.Ordinal);
        Assert.Contains("x:Name=\\u0022Summary\\u0022", request, StringComparison.Ordinal);
        Assert.Contains("Small phone", request, StringComparison.Ordinal);
        var asked = workspace.Agent.Items.OfType<UserMessageViewModel>().Single();
        Assert.True(asked.HasImage);
        Assert.False(workspace.Agent.HasAttachment);
        workspace.IsBottomPanelVisible = false;

        // The request's top, with the picture Claude got.
        var conversation = window.GetVisualDescendants().OfType<AgentPanelView>().Single().Messages;
        Dispatcher.UIThread.RunJobs();
        conversation.UpdateLayout();
        conversation.Offset = default;
        UiTestHelpers.SaveScreenshot(window, "swarm-04-fix-with-agent");

        workspace.StopCommand.Execute(null);
        await run.WaitAsync(TimeSpan.FromSeconds(30));
        window.Close();
    }

    /// <summary>Creates the shop app, opens it in the IDE and runs it.</summary>
    private static async Task<(WorkspaceViewModel Workspace, Window Window, Task Run, string ViewPath)> StartShopAsync(WorkspaceFixture fixture)
    {
        var solution = new ProjectTemplates(fixture.Paths).Create(ProjectTemplates.AvaloniaId, "Shop");
        var viewPath = Path.Combine(Path.GetDirectoryName(solution)!, "Shop", "Views", "MainView.axaml");
        await File.WriteAllTextAsync(viewPath, ShopView);
        var workspace = fixture.Workspace;
        await workspace.LoadAsync(solution);
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = workspace } };
        window.Show();
        var run = workspace.RunCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.AppPreview.IsRunning || run.IsCompleted, StartTimeout, "the shop to start");
        Assert.True(workspace.AppPreview.IsRunning, workspace.Output.Text);
        return (workspace, window, run, viewPath);
    }

    private static ErrorItemViewModel? Row(WorkspaceViewModel workspace, string code, string pathEnd) =>
        workspace.ErrorList.Items.FirstOrDefault(i => i.Code == code && i.Layout?.Finding.ElementPath.EndsWith(pathEnd, StringComparison.Ordinal) == true);

    private static int LineOf(string file, string text) =>
        File.ReadAllLines(file).Select((line, index) => (line, index)).First(l => l.line.Contains(text, StringComparison.Ordinal)).index + 1;

    private static List<string> Texts(Control view) =>
        [.. view.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty)];
}
