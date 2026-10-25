using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Testing;
using TabStudio.Web.Templates;

namespace TabStudio.App.Tests.Views;

/// <summary>The Blazor, Razor Pages and MVC templates on the start screen, with the Blazor Web App's render modes.</summary>
public sealed class WebTemplateScreenshotTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly ServiceProvider _services;
    private readonly MainViewModel _main;

    public WebTemplateScreenshotTests()
    {
        _services = AppServices.Create(new AppPaths(_temp.Path));
        _main = _services.GetRequiredService<MainViewModel>();
    }

    [AvaloniaFact]
    public void The_start_screen_offers_the_web_templates_named_as_in_Visual_Studio()
    {
        var window = new MainWindow { DataContext = _main, Width = 1280, Height = 800 };
        window.Show();
        var start = _main.Start;
        start.SelectCommand.Execute(StartAction.NewProject);
        start.SelectedTemplate = start.Templates.Single(t => t.Id == WebTemplates.BlazorWebAppId);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var templates = window.GetVisualDescendants().OfType<ListBox>().Single(l => ReferenceEquals(l.ItemsSource, start.Templates));
        templates.ScrollIntoView(start.SelectedTemplate);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        UiTestHelpers.SaveScreenshot(window, "blazor-03-templates");

        Assert.Equal(
            new[] { "Blazor Web App", "Blazor WebAssembly Standalone App", "ASP.NET Core Web App (Razor Pages)", "ASP.NET Core Web App (Model-View-Controller)" },
            start.Templates.Where(t => WebTemplates.IsTemplate(t.Id)).Select(t => t.Name));
        Assert.True(start.IsBlazorWebAppSelected);
        var renderModes = window.GetVisualDescendants().OfType<ComboBox>().Single(c => ReferenceEquals(c.ItemsSource, start.BlazorRenderModes));
        Assert.True(renderModes.IsEffectivelyVisible);
        Assert.Equal(BlazorInteractivity.Server, start.SelectedBlazorRenderMode.Interactivity);

        start.SelectedTemplate = start.Templates.Single(t => t.Id == WebTemplates.MvcId);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.False(renderModes.IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public async Task The_chosen_render_mode_creates_the_matching_projects()
    {
        var window = new MainWindow { DataContext = _main, Width = 1280, Height = 800 };
        window.Show();
        var start = _main.Start;
        start.SelectCommand.Execute(StartAction.NewProject);
        start.SelectedTemplate = start.Templates.Single(t => t.Id == WebTemplates.BlazorWebAppId);
        start.SelectedBlazorRenderMode = start.BlazorRenderModes.Single(m => m.Interactivity == BlazorInteractivity.WebAssembly);
        start.ProjectName = "Clicker";

        await start.CreateProjectCommand.ExecuteAsync(null);

        var folder = Path.GetDirectoryName(Directory.EnumerateFiles(_temp.Path, "Clicker.slnx", SearchOption.AllDirectories).Single())!;
        Assert.True(File.Exists(Path.Combine(folder, "Clicker", "Clicker.csproj")));
        Assert.True(File.Exists(Path.Combine(folder, "Clicker.Client", "Clicker.Client.csproj")));
        Assert.Contains("Clicker.Client/Clicker.Client.csproj", File.ReadAllText(Path.Combine(folder, "Clicker.slnx")), StringComparison.Ordinal);
        window.Close();
    }

    public void Dispose()
    {
        _main.Workspace?.Dispose();
        _services.Dispose();
        _temp.Dispose();
    }
}
