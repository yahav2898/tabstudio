using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Services;
using TabStudio.App.Services.Search;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Design;

/// <summary>Gathering the IDE's commands for the search from the menus and key bindings, without moving them.</summary>
public sealed class CommandRegistryTests
{
    [Theory]
    [InlineData("_Save", "Save", null)]
    [InlineData("Save A_ll", "Save All", null)]
    [InlineData("Run _All Tests (Ctrl+R, A)", "Run All Tests", "Ctrl+R, A")]
    [InlineData("_Rename... (Ctrl+R, Ctrl+R)", "Rename", "Ctrl+R, Ctrl+R")]
    [InlineData("TabStudio _Link...", "TabStudio Link", null)]
    [InlineData("Show _Next Statement (Alt+Num *)", "Show Next Statement", "Alt+Num *")]
    [InlineData("Use __init__.py", "Use _init_.py", null)]
    public void Menu_headers_become_titles_and_gestures(string header, string title, string? gesture)
    {
        Assert.Equal((title, gesture), CommandRegistry.CleanHeader(header));
    }

    [AvaloniaFact]
    public void Commands_come_with_their_menu_path_and_gesture()
    {
        var registry = new CommandRegistry();

        registry.AddMenu(DesignSamples.MenuBar());

        var save = registry.Commands.Single(command => command.Title == "Save All");
        Assert.Equal(("File", "Ctrl+Shift+S"), (save.Path, save.Gesture));
        var info = registry.Commands.Single(command => command.Title == "Quick Info");
        Assert.Equal(("Edit > IntelliSense", "Ctrl+K, Ctrl+I"), (info.Path, info.Gesture));
        Assert.False(registry.Commands.Single(command => command.Title == "Rebuild Solution").IsAvailable());
        Assert.True(registry.Commands.Single(command => command.Title == "Build Solution").IsAvailable());
    }

    [AvaloniaFact]
    public void Key_bindings_give_gestures_to_commands_whose_menu_shows_none()
    {
        var close = new RelayCommand(() => { });
        var menu = new Menu();
        var file = new MenuItem { Header = "_File" };
        file.Items.Add(new MenuItem { Header = "_Close Document", Command = close });
        menu.Items.Add(file);
        var registry = new CommandRegistry();
        registry.AddMenu(menu);

        registry.AddKeyBindings([new KeyBinding { Gesture = KeyGesture.Parse("Ctrl+W"), Command = close }]);

        Assert.Equal("Ctrl+W", registry.Commands.Single().Gesture);
    }

    [AvaloniaFact]
    public async Task The_IDEs_own_menus_and_key_bindings_give_the_search_its_commands()
    {
        using var temp = new TempDirectory();
        using var services = AppServices.Create(new AppPaths(temp.Path));
        var main = services.GetRequiredService<MainViewModel>();
        var window = new MainWindow { DataContext = main, Width = 1280, Height = 800 };
        window.Show();
        await main.OpenWorkspaceAsync(await services.GetRequiredService<SampleLibrary>().EnsureExtractedAsync());
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var view = window.GetVisualDescendants().OfType<WorkspaceView>().Single();
        var registry = new CommandRegistry();

        registry.AddMenu(view.GetVisualDescendants().OfType<Menu>().First());
        registry.AddKeyBindings(view.KeyBindings);

        Assert.True(registry.Commands.Count > 60, $"Only {registry.Commands.Count} commands.");
        Assert.Contains(registry.Commands, command => command is { Title: "Build Solution", Path: "Build", Gesture: "Ctrl+Shift+B" });
        Assert.Contains(registry.Commands, command => command is { Title: "Toggle Line Comment", Gesture: "Ctrl+/" });
        Assert.Contains(registry.Commands, command => command is { Title: "List Members", Path: "Edit > IntelliSense" });
        Assert.All(registry.Commands, command => Assert.DoesNotContain("_", command.Title.Replace("__", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal));
        main.Workspace?.Dispose();
        window.Close();
    }
}
