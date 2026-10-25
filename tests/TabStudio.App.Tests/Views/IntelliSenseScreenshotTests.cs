using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Testing;
using static TabStudio.App.Tests.Views.EditorHarness;

namespace TabStudio.App.Tests.Views;

/// <summary>Screenshots of IntelliSense in the IDE: the completion list, Quick Info, Parameter Info and references.</summary>
public sealed class IntelliSenseScreenshotTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly ServiceProvider _services;
    private readonly MainViewModel _main;

    public IntelliSenseScreenshotTests()
    {
        _services = AppServices.Create(new AppPaths(_temp.Path));
        _main = _services.GetRequiredService<MainViewModel>();
    }

    [AvaloniaFact]
    public async Task Completion_list_after_Console_dot()
    {
        var window = Show();
        await OpenSampleAsync();
        var view = ActiveEditor(window);
        var completion = view.Completion!;

        StartLineAfter(view.Editor, "Console.WriteLine();");
        Type(view.Editor, "Console.Wri");
        await UiTestHelpers.WaitUntilAsync(() => completion.IsOpen && completion.HasDescription, because: "the list and its description");

        UiTestHelpers.SaveScreenshot(window, "07-completion");

        Assert.StartsWith("Wri", completion.SelectedItem?.Entry.DisplayText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Quick_info_on_a_type()
    {
        var window = Show();
        var workspace = await OpenSampleAsync();
        var view = ActiveEditor(window);
        view.Editor.CaretOffset = OffsetOf(view.Editor, "new Greeter(") + "new Gree".Length;

        workspace.QuickInfoCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => view.QuickInfo is not null, because: "Quick Info");

        UiTestHelpers.SaveScreenshot(window, "08-quick-info");

        Assert.True(view.QuickInfo!.HasSignature);
    }

    [AvaloniaFact]
    public async Task Parameter_info_while_typing_arguments()
    {
        var window = Show();
        await OpenSampleAsync();
        var view = ActiveEditor(window);

        StartLineAfter(view.Editor, "Console.WriteLine();");
        Type(view.Editor, "Console.WriteLine(");
        await UiTestHelpers.WaitUntilAsync(() => view.SignatureHelp!.IsOpen, because: "Parameter Info");
        Type(view.Editor, "\"Hi\", ");
        await UiTestHelpers.WaitUntilAsync(() => view.SignatureHelp!.Result?.ArgumentIndex == 1, because: "the second argument");

        UiTestHelpers.SaveScreenshot(window, "09-parameter-info");

        Assert.True(view.SignatureHelp!.HasOverloads);
    }

    [AvaloniaFact]
    public async Task Find_all_references_panel()
    {
        var window = Show();
        var workspace = await OpenSampleAsync();
        var view = ActiveEditor(window);
        view.Editor.CaretOffset = OffsetOf(view.Editor, "greeter.Greet()") + "greeter.Gr".Length;

        await workspace.FindAllReferencesCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.References.Rows.Count > 0, because: "the references");

        UiTestHelpers.SaveScreenshot(window, "10-references");

        Assert.Equal(BottomPanel.References, workspace.SelectedPanel);
    }

    [AvaloniaFact]
    public async Task Light_bulb_and_breadcrumb_in_a_class()
    {
        var window = Show();
        var workspace = await OpenSampleAsync();
        var greeterPath = Path.Combine(workspace.Workspace!.Projects.Single(p => p.Name == "Greetings").DirectoryPath, "Greeter.cs");
        var greeter = (await workspace.OpenDocumentAsync(greeterPath))!;
        var view = ActiveEditor(window);
        view.Editor.CaretOffset = OffsetOf(view.Editor, "var partOfDay") + 1;

        await UiTestHelpers.WaitUntilAsync(() => view.IsLightBulbVisible && greeter.SymbolPath.Count >= 2, because: "the light bulb and the breadcrumb");

        UiTestHelpers.SaveScreenshot(window, "11-light-bulb");

        Assert.StartsWith("Greet(", greeter.SymbolPath[^1].Name, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        _main.Workspace?.Dispose();
        _services.Dispose();
        _temp.Dispose();
    }

    private MainWindow Show()
    {
        var window = new MainWindow { DataContext = _main, Width = 1280, Height = 800 };
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
