using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Services;
using TabStudio.App.Services.Appearance;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Theming;

/// <summary>Tools &gt; Theme: Dark, Light and Follow the system, checked as the theme in use (docs/design/stage-2.md, 2.8).</summary>
[Collection(AppearanceTestGroup.Name)]
public sealed class ThemeMenuTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly ServiceProvider _services;
    private readonly MainViewModel _main;

    public ThemeMenuTests()
    {
        _services = AppServices.Create(new AppPaths(_temp.Path));
        _main = _services.GetRequiredService<MainViewModel>();
    }

    [AvaloniaFact]
    public async Task Tools_Theme_switches_the_IDE_and_checks_the_theme_in_use()
    {
        var window = new MainWindow { DataContext = _main, Width = 1280, Height = 800 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var solution = await _services.GetRequiredService<SampleLibrary>().EnsureExtractedAsync();
        await _main.OpenWorkspaceAsync(solution);
        var workspace = _main.Workspace!;
        Dispatcher.UIThread.RunJobs();

        try
        {
            var menu = window.GetVisualDescendants().OfType<Menu>().Single(m => m.Name == "MainMenu");
            var theme = Item(Item(menu.Items, "T_ools").Items, "_Theme");
            var light = Item(theme.Items, "_Light");
            var dark = Item(theme.Items, "_Dark");

            light.Command!.Execute(light.CommandParameter);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(IdeTheme.Light, workspace.CurrentTheme);
            Assert.Equal(ThemeVariant.Light, Application.Current!.ActualThemeVariant);
            Assert.True(light.IsChecked);
            Assert.False(dark.IsChecked);

            dark.Command!.Execute(dark.CommandParameter);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(IdeTheme.Dark, workspace.CurrentTheme);
            Assert.True(dark.IsChecked);
            Assert.False(light.IsChecked);
        }
        finally
        {
            AppearanceService.Preview(TestTheme.Baseline);
            Dispatcher.UIThread.RunJobs();
        }

        static MenuItem Item(ItemCollection items, string header) =>
            items.OfType<MenuItem>().Single(item => item.Header as string == header);
    }

    public void Dispose()
    {
        _main.Workspace?.Dispose();
        _services.Dispose();
        _temp.Dispose();
    }
}
