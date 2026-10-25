using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Services;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Views;

/// <summary>
/// Renders the real views with Skia and saves screenshots, so every CI run shows what the IDE looks like.
/// The assertions check the visual tree; the PNGs are for people.
/// </summary>
public sealed class ScreenshotTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly ServiceProvider _services;
    private readonly MainViewModel _main;

    public ScreenshotTests()
    {
        _services = AppServices.Create(new AppPaths(_temp.Path));
        _main = _services.GetRequiredService<MainViewModel>();
    }

    [AvaloniaFact]
    public void Start_screen()
    {
        var window = Show(1280, 800);

        UiTestHelpers.SaveScreenshot(window, "01-start");

        Assert.IsType<StartViewModel>(_main.CurrentPage);
    }

    [AvaloniaFact]
    public async Task Ide_with_the_sample_open()
    {
        var window = Show(1280, 800);
        await OpenSampleAsync();

        UiTestHelpers.SaveScreenshot(window, "02-ide");

        Assert.NotNull(_main.Workspace?.ActiveDocument);
    }

    [AvaloniaFact]
    public async Task Program_output_after_F5()
    {
        var window = Show(1280, 800);
        var workspace = await OpenSampleAsync();
        workspace.Output.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(OutputViewModel.IsWaitingForInput) && workspace.Output.IsWaitingForInput)
            {
                workspace.Output.InputText = "Ada";
                workspace.Output.SubmitInputCommand.Execute(null);
            }
        };

        await workspace.RunCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => true);

        UiTestHelpers.SaveScreenshot(window, "03-run");

        Assert.Contains("Ada", workspace.Output.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Live_errors_while_typing()
    {
        var window = Show(1280, 800);
        var workspace = await OpenSampleAsync();
        var document = workspace.ActiveDocument!;
        var offset = document.Document.Text.IndexOf("greeter.Greet()", StringComparison.Ordinal);
        document.Document.Replace(offset, "greeter".Length, "greeterr");
        document.NavigateTo(document.Document.GetLocation(offset).Line, 1);
        await document.AnalyzeNowAsync();
        await UiTestHelpers.WaitUntilAsync(() => workspace.ErrorList.ErrorCount > 0, because: "the error");

        UiTestHelpers.SaveScreenshot(window, "04-errors");

        Assert.Contains(workspace.ErrorList.Items, i => i.Code == "CS0103");
    }

    [AvaloniaFact]
    public async Task Compatibility_doctor_panel()
    {
        var window = Show(1280, 800);
        var workspace = await OpenSampleAsync();
        workspace.SelectPanelCommand.Execute(BottomPanel.Compatibility);
        await UiTestHelpers.WaitUntilAsync(() => true);

        UiTestHelpers.SaveScreenshot(window, "05-doctor");

        Assert.True(workspace.IsCompatibilitySelected);
    }

    [AvaloniaFact]
    public async Task Portrait_tablet_hides_the_side_bar()
    {
        var window = Show(800, 1280);
        var workspace = await OpenSampleAsync();

        UiTestHelpers.SaveScreenshot(window, "06-portrait");

        Assert.False(workspace.IsSideBarVisible);
    }

    [AvaloniaFact]
    public async Task Test_Explorer_with_passed_failed_and_skipped_tests()
    {
        var window = Show(1280, 800);
        await _main.OpenWorkspaceAsync(CalculatorSolution.Write(_temp));
        var workspace = _main.Workspace!;
        var tests = workspace.Tests;
        await UiTestHelpers.WaitUntilAsync(() => tests.TotalCount == 5, because: "the tests found in source");

        await tests.RunAllCommand.ExecuteAsync(null);
        tests.SelectedNode = tests.Rows.Single(r => r.Name == "Subtracts_with_a_mistake");
        await workspace.NavigateToAsync(_temp.Resolve(CalculatorSolution.TestFile), CalculatorSolution.FailingLine, 1);
        var document = workspace.ActiveDocument!;
        await document.AnalyzeNowAsync();
        await UiTestHelpers.WaitUntilAsync(() => document.Classifications.Count > 0, because: "syntax colors");

        UiTestHelpers.SaveScreenshot(window, "07-test-explorer");

        Assert.True(workspace.IsTestExplorerSelected);
        Assert.Equal((3, 1, 1), (tests.PassedCount, tests.FailedCount, tests.SkippedCount));
        var explorer = window.GetVisualDescendants().OfType<TestExplorerView>().Single();
        Assert.True(explorer.IsEffectivelyVisible);
        Assert.Contains(explorer.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Stack Trace" && t.IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public async Task Test_Explorer_grouped_by_outcome()
    {
        var window = Show(1280, 800);
        await _main.OpenWorkspaceAsync(CalculatorSolution.Write(_temp));
        var workspace = _main.Workspace!;
        var tests = workspace.Tests;
        await UiTestHelpers.WaitUntilAsync(() => tests.TotalCount == 5, because: "the tests found in source");
        await tests.RunAllCommand.ExecuteAsync(null);

        tests.SetGroupByCommand.Execute(TestGroupBy.Outcome);
        tests.SelectedNode = tests.Rows.Single(r => r.Name == "Passed Tests");
        workspace.SelectPanelCommand.Execute(BottomPanel.ErrorList);
        await UiTestHelpers.WaitUntilAsync(() => true);

        UiTestHelpers.SaveScreenshot(window, "08-test-explorer-outcome");

        Assert.Equal("3 Passed", Assert.Single(tests.Details!.Outcomes).Text);
    }

    public void Dispose()
    {
        _main.Workspace?.Dispose();
        _services.Dispose();
        _temp.Dispose();
    }

    private MainWindow Show(double width, double height)
    {
        var window = new MainWindow { DataContext = _main, Width = width, Height = height };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return window;
    }

    private async Task<WorkspaceViewModel> OpenSampleAsync()
    {
        var solution = await _services.GetRequiredService<SampleLibrary>().EnsureExtractedAsync();
        await _main.OpenWorkspaceAsync(solution);
        var workspace = _main.Workspace!;
        var document = workspace.ActiveDocument!;
        await document.AnalyzeNowAsync();
        await UiTestHelpers.WaitUntilAsync(() => document.Classifications.Count > 0, because: "syntax colors");
        return workspace;
    }
}
