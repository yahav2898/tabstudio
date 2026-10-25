using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using TabStudio.App.Services;
using TabStudio.App.Views;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>
/// Avalonia apps in the IDE, against the real nuget.org: the Avalonia template restores, F5 shows its view in the app
/// preview with its view model and commands working, saving a XAML file restarts it, and Stop ends it.
/// </summary>
public sealed class AvaloniaAppTests
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(4);

    [AvaloniaFact]
    public async Task F5_shows_the_Avalonia_template_in_the_app_preview_and_saving_its_XAML_restarts_it()
    {
        using var fixture = new WorkspaceFixture();
        var solution = new ProjectTemplates(fixture.Paths).Create(ProjectTemplates.AvaloniaId, "HelloAvalonia");
        var workspace = fixture.Workspace;
        await workspace.LoadAsync(solution);
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = workspace } };
        window.Show();

        var run = workspace.StartDebuggingCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.AppPreview.IsRunning || run.IsCompleted, StartTimeout, "the app to start");

        Assert.True(workspace.AppPreview.IsRunning, workspace.Output.Text);
        Assert.True(workspace.IsAppPreviewSelected);
        Assert.Null(workspace.AppPreview.Note);
        Assert.Contains("HelloAvalonia is running in the app preview.", workspace.Output.Text, StringComparison.Ordinal);
        await UiTestHelpers.WaitUntilAsync(() => Texts(workspace).Contains("Hello from Avalonia!"), because: "the greeting from the view model");

        var count = workspace.AppPreview.View!.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "CountButton");
        count.Command!.Execute(null);
        count.Command.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => Texts(workspace).Contains("Clicked 2 times"), because: "the command to count");
        UiTestHelpers.SaveScreenshot(window, "avalonia-01-app-preview");

        // Saving the view's XAML builds the app again and shows the change.
        var xamlPath = Path.Combine(Path.GetDirectoryName(solution)!, "HelloAvalonia", "Views", "MainView.axaml");
        var document = await workspace.OpenDocumentAsync(xamlPath);
        var text = document!.Document.Text;
        var hint = text.IndexOf("Edit Views/MainView.axaml", StringComparison.Ordinal);
        document.Document.Replace(hint, "Edit Views/MainView.axaml".Length, "Changed on a tablet");
        await workspace.SaveCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(
            () => workspace.AppPreview.IsRunning && Texts(workspace).Any(t => t.StartsWith("Changed on a tablet", StringComparison.Ordinal)),
            StartTimeout,
            "the app to restart with the change");
        Assert.Contains("Restarting 'HelloAvalonia' with the saved changes.", workspace.Output.Text, StringComparison.Ordinal);

        workspace.StopDebuggingCommand.Execute(null);
        await run.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.False(workspace.AppPreview.IsRunning);
        Assert.Null(workspace.AppPreview.View);
        Assert.Contains("The program 'HelloAvalonia' has exited with code 0", workspace.Output.Text, StringComparison.Ordinal);
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_restarted_app_gets_the_types_and_files_of_its_new_build()
    {
        using var fixture = new WorkspaceFixture();
        var solution = new ProjectTemplates(fixture.Paths).Create(ProjectTemplates.AvaloniaId, "Items");
        var project = Path.Combine(Path.GetDirectoryName(solution)!, "Items");

        // The template packs Assets/ as avares:// files.
        var assetPath = Path.Combine(project, "Assets", "name.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath)!);
        File.WriteAllText(assetPath, "Item from build 1");
        File.WriteAllText(Path.Combine(project, "ViewModels", "ItemViewModel.cs"), """
            using Avalonia.Platform;

            namespace Items.ViewModels;

            public class ItemViewModel
            {
                public string Name { get; } = new StreamReader(AssetLoader.Open(new Uri("avares://Items/Assets/name.txt"))).ReadToEnd();
            }
            """);
        File.WriteAllText(Path.Combine(project, "ViewModels", "MainViewModel.cs"), """
            namespace Items.ViewModels;

            public class MainViewModel
            {
                public ItemViewModel Item { get; } = new();
            }
            """);

        // A data template matches the item by its type, which must be this build's, not the one before it.
        File.WriteAllText(Path.Combine(project, "Views", "MainView.axaml"), """
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         xmlns:vm="using:Items.ViewModels"
                         x:Class="Items.Views.MainView"
                         x:DataType="vm:MainViewModel">
              <ContentControl Content="{Binding Item}">
                <ContentControl.DataTemplates>
                  <DataTemplate DataType="vm:ItemViewModel">
                    <TextBlock Text="{Binding Name}" />
                  </DataTemplate>
                </ContentControl.DataTemplates>
              </ContentControl>
            </UserControl>
            """);

        var workspace = fixture.Workspace;
        await workspace.LoadAsync(solution);
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = workspace } };
        window.Show();

        var run = workspace.RunCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => Texts(workspace).Contains("Item from build 1") || run.IsCompleted, StartTimeout, "the first build's item");
        Assert.Contains("Item from build 1", Texts(workspace));

        // Saving any of the app's files restarts it, its assets included.
        var document = await workspace.OpenDocumentAsync(assetPath);
        var text = document!.Document.Text;
        document.Document.Replace(text.IndexOf("build 1", StringComparison.Ordinal), "build 1".Length, "build 2");
        await workspace.SaveCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => Texts(workspace).Contains("Item from build 2") || run.IsCompleted, StartTimeout, "the second build's item");
        Assert.Contains("Item from build 2", Texts(workspace));

        workspace.StopCommand.Execute(null);
        await run.WaitAsync(TimeSpan.FromSeconds(30));
        window.Close();
    }

    [AvaloniaFact]
    public void Avalonia_s_XAML_compiler_and_asset_loader_can_be_kept_in_step_with_the_app()
    {
        Assert.True(Previews.RuntimeXamlTypes.IsSupported, "Avalonia's run-time XAML compiler changed; see RuntimeXamlTypes.");
        Assert.True(Previews.RuntimeAssets.IsSupported, "Avalonia's asset loader changed; see RuntimeAssets.");
    }

    [AvaloniaFact]
    public void A_window_shows_as_a_content_control_with_its_content_namespaces_and_resources()
    {
        var content = Previews.AvaloniaAppHost.ContentOf("""
            <Window xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:vm="using:App.ViewModels" x:Class="App.MainWindow" x:DataType="vm:MainWindowViewModel"
                    Title="App" Width="800" Height="450" Icon="/Assets/logo.ico" Background="Black">
              <Window.Resources><x:String x:Key="Hello">Hello</x:String></Window.Resources>
              <Window.SystemDecorations>Full</Window.SystemDecorations>
              <TextBlock Text="{Binding Greeting}" />
            </Window>
            """);

        Assert.StartsWith("<ContentControl", content, StringComparison.Ordinal);
        Assert.Contains("xmlns:vm=\"using:App.ViewModels\"", content, StringComparison.Ordinal);
        Assert.Contains("x:DataType=\"vm:MainWindowViewModel\"", content, StringComparison.Ordinal);
        Assert.Contains("Background=\"Black\"", content, StringComparison.Ordinal);
        Assert.Contains("<ContentControl.Resources>", content, StringComparison.Ordinal);
        Assert.Contains("<TextBlock Text=\"{Binding Greeting}\" />", content, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Class", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Title", content, StringComparison.Ordinal);
        Assert.DoesNotContain("SystemDecorations", content, StringComparison.Ordinal);
    }

    private static List<string> Texts(TabStudio.App.ViewModels.WorkspaceViewModel workspace) =>
        workspace.AppPreview.View?.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty).ToList() ?? [];
}
