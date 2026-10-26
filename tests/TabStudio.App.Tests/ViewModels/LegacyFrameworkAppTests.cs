using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Compilation.Building;
using TabStudio.Core.Compatibility;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.Forms.Hosting;
using TabStudio.Testing;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>
/// .NET Framework projects in the legacy format, as most tutorials and student projects on GitHub are: a WPF calculator
/// and a Windows Forms app open with no errors and run on the tablet, like the SDK-style projects they would be
/// upgraded to; one that needs Windows has one diagnostic and a banner instead of the errors of the wrong framework,
/// and Upgrade project makes it an SDK-style project, reviewed as a diff.
/// </summary>
public sealed class LegacyFrameworkAppTests : IClassFixture<SharedToolchain>, IDisposable
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(6);

    private readonly SharedToolchain _toolchain;
    private readonly TempDirectory _temp = new();
    private readonly List<WorkspaceViewModel> _workspaces = [];
    private readonly List<HeadlessChrome> _browsers = [];

    public LegacyFrameworkAppTests(SharedToolchain toolchain)
    {
        _toolchain = toolchain;
    }

    [AvaloniaFact]
    public async Task A_legacy_wpf_calculator_opens_with_no_errors_and_runs_on_OpenSilver()
    {
        var (workspace, webViews) = await CreateWorkspaceAsync();
        var solution = LegacyFrameworkProjects.WriteWpfCalculator(_temp.Path, "WpfCalculator");
        await workspace.LoadAsync(solution);
        var window = Show(workspace);

        var row = Assert.Single(workspace.Compatibility);
        Assert.Equal(CompatibilityStatus.Ready, row.Report.Status);
        Assert.StartsWith(NetFrameworkCompatibility.RunsHereSummary, row.Explanation, StringComparison.Ordinal);
        Assert.Equal("WpfCalculator", workspace.StartupProject?.Name);
        await workspace.OpenDocumentAsync(Path.Combine(_temp.Path, "WpfCalculator", "MainWindow.xaml.cs"));
        Assert.False(workspace.HasNetFrameworkBanner);
        await AssertNoErrorsAsync(workspace);

        var run = workspace.StartDebuggingCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.WebPreview.IsRunning || run.IsCompleted, StartTimeout, "the WPF app to start");
        Assert.True(workspace.WebPreview.IsRunning, workspace.Output.Text);
        if (webViews?.WebView is { } webView)
        {
            var chrome = webView.Chrome;
            await WaitForAsync(webView.Rendering, "the preview's browser");
            await WaitForAsync(chrome.WaitUntilAsync("document.body.innerText.includes('WPF calculator from .NET Framework')", TimeSpan.FromSeconds(110)), "the calculator's window");
            await WaitForAsync(chrome.ClickTextAsync("Multiply"), "the click");
            await WaitForAsync(chrome.WaitUntilAsync("document.body.innerText.includes('Result: 42')", TimeSpan.FromSeconds(20)), "the product");
            await UiTestHelpers.WaitUntilAsync(
                () => workspace.Output.Text.Contains("Multiplied to 42, from its settings", StringComparison.Ordinal),
                TimeSpan.FromSeconds(30),
                "the click's Console output, with the setting's default value");
            await WaitForAsync(webView.RefreshAsync(), "the preview's browser");
        }

        UiTestHelpers.SaveScreenshot(window, "legacy-01-wpf-calculator");
        await AssertNoErrorsAsync(workspace);
        workspace.StopCommand.Execute(null);
        await WaitForAsync(run, "the run to stop");
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_legacy_windows_forms_app_opens_with_no_errors_and_runs_in_the_app_preview()
    {
        var (workspace, _) = await CreateWorkspaceAsync(withBrowser: false);
        var solution = LegacyFrameworkProjects.WriteWindowsFormsApp(_temp.Path, "HelloFramework");
        await workspace.LoadAsync(solution);
        var window = Show(workspace);

        var row = Assert.Single(workspace.Compatibility);
        Assert.Equal(CompatibilityStatus.Ready, row.Report.Status);
        Assert.True(row.CanUpgradeProject);
        await workspace.OpenDocumentAsync(Path.Combine(_temp.Path, "HelloFramework", "Form1.cs"));
        await AssertNoErrorsAsync(workspace);

        var run = workspace.StartDebuggingCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(
            () => (workspace.AppPreview.View is FormsDesktop { Windows.Count: > 0 } && workspace.AppPreview.IsRunning) || run.IsCompleted,
            StartTimeout,
            "the app's form");
        Assert.True(workspace.AppPreview.IsRunning, workspace.Output.Text);
        var desktop = (FormsDesktop)workspace.AppPreview.View!;
        await UiTestHelpers.WaitUntilAsync(() => desktop.Windows.Count > 0 && desktop.Windows[0].Content.Texts.Any(), because: "the form drawn on the desktop");
        Assert.Equal("Hello .NET Framework", desktop.Windows[0].Title);
        await UiTestHelpers.WaitUntilAsync(() => workspace.Output.Text.Contains("Form1 is ready.", StringComparison.Ordinal), because: "the form's Console output");

        // The button's handler reads the app's settings, which start with their default values.
        var runtime = desktop.Runtime ?? throw new InvalidOperationException("The app ended.");
        Assert.True(runtime.Send(() => ((System.Windows.Forms.Button)runtime.Windows[0].Form.Controls.Find("button1", true)[0]).PerformClick(), TimeSpan.FromSeconds(10)));
        string? label = null;
        await UiTestHelpers.WaitUntilAsync(
            () =>
            {
                runtime.Send(() => label = runtime.Windows[0].Form.Controls.Find("label1", true)[0].Text, TimeSpan.FromSeconds(10));
                return label == "Hello from .NET Framework, from its settings";
            },
            because: $"the click to change the label, which shows \"{label}\"");

        UiTestHelpers.SaveScreenshot(window, "legacy-02-windows-forms-app");
        await AssertNoErrorsAsync(workspace);
        workspace.StopCommand.Execute(null);
        await WaitForAsync(run, "the run to stop");
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_program_that_needs_windows_has_one_diagnostic_a_banner_and_upgrades_to_an_sdk_style_project()
    {
        var (workspace, _) = await CreateWorkspaceAsync(withBrowser: false);
        var solution = LegacyFrameworkProjects.WriteWindowsFormsApp(_temp.Path, "Beeper");
        var form = Path.Combine(_temp.Path, "Beeper", "Form1.cs");
        File.WriteAllText(form, File.ReadAllText(form).Replace(
            "public Form1()",
            "[System.Runtime.InteropServices.DllImport(\"user32.dll\")]\n        private static extern bool MessageBeep(uint type);\n\n        public Form1()",
            StringComparison.Ordinal));
        await workspace.LoadAsync(solution);
        var window = Show(workspace);

        var row = Assert.Single(workspace.Compatibility);
        Assert.Equal(CompatibilityStatus.NeedsWindows, row.Report.Status);
        Assert.Contains("calls Windows through P/Invoke in Form1.cs", row.Explanation, StringComparison.Ordinal);

        // Not compiled against .NET: one diagnostic says why, instead of the errors of the wrong framework.
        await workspace.OpenDocumentAsync(form);
        await Task.Delay(TimeSpan.FromSeconds(2));
        var item = Assert.Single(workspace.ErrorList.Items);
        Assert.Equal(ProjectBuilder.NeedsWindowsId, item.Code);
        Assert.Equal(DiagnosticSeverityLevel.Warning, item.Diagnostic.Severity);
        Assert.Contains("Beeper is a .NET Framework project that needs Windows: it calls Windows through P/Invoke in Form1.cs.", item.Description, StringComparison.Ordinal);

        // The editor says so too, with the two ways on.
        Assert.True(workspace.HasNetFrameworkBanner);
        Assert.Equal("Beeper is a .NET Framework project that needs Windows: it calls Windows through P/Invoke in Form1.cs.", workspace.NetFrameworkBannerText);
        Assert.True(workspace.RunNetFrameworkProjectOnWindowsBridgeCommand.CanExecute(null));
        UiTestHelpers.SaveScreenshot(window, "legacy-03-needs-windows-banner");

        // Run on Windows Bridge without a PC says where to pair one.
        await workspace.RunNetFrameworkProjectOnWindowsBridgeCommand.ExecuteAsync(null);
        Assert.Contains("Pair a Windows PC", workspace.Output.Text, StringComparison.Ordinal);

        // Building the solution skips it with the reason, and adds no errors.
        await workspace.BuildCommand.ExecuteAsync(null);
        Assert.Contains("Skipped: Project: Beeper: a .NET Framework project that needs Windows", workspace.Output.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(workspace.ErrorList.Items, i => i.IsError);

        // Upgrade project makes it an SDK-style project, and shows the change as a diff.
        var projectFile = Path.Combine(_temp.Path, "Beeper", "Beeper.csproj");
        await workspace.UpgradeProjectCommand.ExecuteAsync(null);
        var upgraded = ProjectParser.Load(projectFile);
        Assert.Equal("Microsoft.NET.Sdk", upgraded.Sdk);
        Assert.Equal(["net10.0-windows"], upgraded.TargetFrameworks);
        Assert.Equal(ProjectKind.WinForms, upgraded.Kind);
        var diff = workspace.ActiveDocument;
        Assert.NotNull(diff?.Comparison);
        Assert.Equal("Beeper.csproj upgrade", diff!.Comparison!.Title);
        Assert.Equal("Legacy project", diff.Comparison.BaseName);
        Assert.True(diff.Comparison.Diff.AddedCount > 0 && diff.Comparison.Diff.RemovedCount > 0);
        Assert.Contains("Upgraded Beeper to an SDK-style project for net10.0-windows.", workspace.Output.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(workspace.ErrorList.Items, i => i.Code == ProjectBuilder.NeedsWindowsId);
        Assert.False(Assert.Single(workspace.Compatibility).CanUpgradeProject);
        UiTestHelpers.SaveScreenshot(window, "legacy-04-upgrade-diff");

        // Back in the form's code, there is no banner: the project is an SDK-style one now.
        await workspace.OpenDocumentAsync(form);
        Assert.False(workspace.HasNetFrameworkBanner);
        window.Close();
    }

    public void Dispose()
    {
        foreach (var workspace in _workspaces)
        {
            workspace.Dispose();
        }

        // Off the UI thread, which this blocks: the browser's shutdown continues on the thread pool.
        foreach (var browser in _browsers)
        {
            Task.Run(() => browser.DisposeAsync().AsTask()).GetAwaiter().GetResult();
        }

        _temp.Dispose();
    }

    /// <summary>
    /// Asserts that the Error List settles at no errors: what analysis found before the packages were restored is
    /// replaced by what it finds after. The UI keeps running while it waits, as a test on the UI thread must.
    /// </summary>
    private static async Task AssertNoErrorsAsync(WorkspaceViewModel workspace)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (workspace.ErrorList.ErrorCount > 0 && DateTime.UtcNow < deadline)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Task.Delay(100);
        }

        Assert.True(
            workspace.ErrorList.ErrorCount == 0,
            string.Join('\n', workspace.ErrorList.AllDiagnostics
                .Where(d => d.Severity == DiagnosticSeverityLevel.Error)
                .Select(d => $"{d.Id} {d.Location?.FilePath}({d.Location?.Line}): {d.Message}")));
    }

    /// <summary>Waits for a task while the UI keeps running, as a test on the UI thread must.</summary>
    private static async Task WaitForAsync(Task task, string because)
    {
        await UiTestHelpers.WaitUntilAsync(() => task.IsCompleted, TimeSpan.FromMinutes(2), because);
        await task;
    }

    private static Window Show(WorkspaceViewModel workspace)
    {
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = workspace } };
        window.Show();
        return window;
    }

    private async Task<(WorkspaceViewModel Workspace, ChromeWebViewFactory? WebViews)> CreateWorkspaceAsync(bool withBrowser = true)
    {
        ChromeWebViewFactory? webViews = null;
        if (withBrowser && HeadlessChrome.ExecutablePath is not null)
        {
            var chrome = await HeadlessChrome.StartAsync();
            _browsers.Add(chrome);
            webViews = new ChromeWebViewFactory(chrome);
        }

        var device = new DefaultDeviceStatus(new DeviceInfo(8L * 1024 * 1024 * 1024, 8, IsLowRamDevice: false));
        var workspace = new WorkspaceViewModel(
            _toolchain.Compiler, device, DeviceProfile.For(device.Device), new AvaloniaUiDispatcher(), new SessionStore(_toolchain.Paths), _toolchain.Paths, webViews: webViews);
        _workspaces.Add(workspace);
        return (workspace, webViews);
    }
}
