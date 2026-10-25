using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TabStudio.App.Previews;
using TabStudio.App.Services;
using TabStudio.App.Views;
using TabStudio.Core.Diagnostics;
using TabStudio.Uno.Templates;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>The actual templates, F5, saved XAML and C#, and XAML errors in the IDE, with Uno and Avalonia drawing together.</summary>
public sealed class UnoAppTests
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(4);

    [AvaloniaTheory]
    [InlineData(UnoTemplates.WinUIAppId, "HelloWinUI")]
    [InlineData(UnoTemplates.UnoAppId, "HelloUno")]
    public async Task F5_shows_the_template_and_saving_XAML_and_CSharp_restarts_it(string template, string name)
    {
        using var fixture = new WorkspaceFixture();
        Assert.Contains(ProjectTemplates.All, t => t.Id == template);
        var solution = new ProjectTemplates(fixture.Paths).Create(template, name);
        var page = Path.Combine(Path.GetDirectoryName(solution)!, name, "MainPage.xaml");
        // Keep the template's App and Window; give its content an identifiable greeting.
        File.WriteAllText(page, $$"""
            <Page x:Class="{{name}}.MainPage"
                  xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Grid Background="#18253D" Padding="24">
                    <TextBlock x:Name="Greeting" Text="Hello from WinUI" Foreground="White" FontSize="28" />
                </Grid>
            </Page>
            """);
        var workspace = fixture.Workspace;
        await workspace.LoadAsync(solution);
        var document = await workspace.OpenDocumentAsync(page);
        var window = new Window { Width = 1440, Height = 900, Content = new WorkspaceView { DataContext = workspace } };
        window.Show();
        var run = workspace.StartDebuggingCommand.ExecuteAsync(null);
        try
        {
            await UiTestHelpers.WaitUntilAsync(() => workspace.AppPreview.IsRunning || run.IsCompleted, StartTimeout, "Uno's preview");
            var preview = Assert.IsType<UnoPreviewControl>(workspace.AppPreview.View);
            Assert.True(workspace.IsAppPreviewSelected);
            Assert.NotNull(preview.App);
            Assert.True(Core.Compatibility.CompatibilityDoctor.Diagnose(workspace.StartupProject!).CanRunToday);
            Assert.Contains("Hello from WinUI", await preview.App.GetTextsAsync());
            Assert.Contains("without the debugger", workspace.Output.Text, StringComparison.Ordinal);
            UiTestHelpers.SaveScreenshot(window, "winui-01-" + template + "-preview");

            var previous = preview.App.Assembly;
            var text = document!.Document.Text;
            document.Document.Replace(text.IndexOf("Hello from WinUI", StringComparison.Ordinal), "Hello from WinUI".Length, "Saved on a tablet");
            await workspace.SaveCommand.ExecuteAsync(null);
            await UiTestHelpers.WaitUntilAsync(
                () => workspace.AppPreview.View is UnoPreviewControl { App: { } app } && !ReferenceEquals(app.Assembly, previous) || run.IsCompleted,
                StartTimeout, "the saved XAML to restart");
            preview = Assert.IsType<UnoPreviewControl>(workspace.AppPreview.View);
            Assert.Contains("Saved on a tablet", await preview.App!.GetTextsAsync());

            previous = preview.App.Assembly;
            var code = await workspace.OpenDocumentAsync(page + ".cs");
            var initialize = code!.Document.Text.IndexOf("InitializeComponent();", StringComparison.Ordinal) + "InitializeComponent();".Length;
            code.Document.Insert(initialize, "\n        Greeting.Text = \"Saved CSharp\";");
            await workspace.SaveCommand.ExecuteAsync(null);
            await UiTestHelpers.WaitUntilAsync(
                () => workspace.AppPreview.View is UnoPreviewControl { App: { } app } && !ReferenceEquals(app.Assembly, previous) || run.IsCompleted,
                StartTimeout, "the saved C# to restart");
            preview = Assert.IsType<UnoPreviewControl>(workspace.AppPreview.View);
            Assert.Contains("Saved CSharp", await preview.App!.GetTextsAsync());
        }
        finally
        {
            workspace.StopCommand.Execute(null);
            await run.WaitAsync(TimeSpan.FromSeconds(30));
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_XAML_error_is_in_the_Error_List_on_its_XAML_line()
    {
        using var fixture = new WorkspaceFixture();
        var solution = new ProjectTemplates(fixture.Paths).Create(UnoTemplates.UnoAppId, "BrokenUno");
        var page = Path.Combine(Path.GetDirectoryName(solution)!, "BrokenUno", "MainPage.xaml");
        var workspace = fixture.Workspace;
        await workspace.LoadAsync(solution);
        var document = await workspace.OpenDocumentAsync(page);
        var window = new Window { Width = 1440, Height = 900, Content = new WorkspaceView { DataContext = workspace } };
        window.Show();
        try
        {
            var source = document!.Document.Text;
            var offset = source.IndexOf("<TextBlock AutomationProperties", StringComparison.Ordinal) + 1;
            document.Document.Replace(offset, "TextBlock".Length, "TextBlok");
            await workspace.BuildCommand.ExecuteAsync(null);
            var line = source[..offset].Count(c => c == '\n') + 1;
            Assert.Contains(workspace.ErrorList.Items, item => item.Diagnostic.Severity == DiagnosticSeverityLevel.Error
                && item.Diagnostic.Location?.FilePath == page && item.Diagnostic.Location.Line == line);
            UiTestHelpers.SaveScreenshot(window, "winui-02-xaml-error-list");
            Assert.False(workspace.AppPreview.IsRunning);
        }
        finally
        {
            window.Close();
        }
    }
}
