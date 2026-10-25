using TabStudio.Core.Workspaces;
using TabStudio.Testing;
using TabStudio.Uno.Templates;

namespace TabStudio.Uno.Tests;

/// <summary>The Uno Platform App and the WinUI 3 Blank App, written as their own tools write them.</summary>
public sealed class UnoTemplateTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public void The_Uno_Platform_App_is_the_blank_preset_of_dotnet_new_unoapp()
    {
        var project = UnoTemplates.Create(UnoTemplates.UnoAppId, _temp.Path, "Weather");

        Assert.Equal(_temp.Resolve("Weather/Weather.csproj"), project);
        foreach (var file in new[]
        {
            "global.json", "Directory.Build.props", "Directory.Packages.props", ".gitignore", ".editorconfig",
            "Weather/App.xaml", "Weather/App.xaml.cs", "Weather/MainPage.xaml", "Weather/MainPage.xaml.cs", "Weather/GlobalUsings.cs",
            "Weather/Strings/en/Resources.resw", "Weather/Assets/Icons/icon.svg", "Weather/Platforms/Desktop/Program.cs",
            "Weather/Platforms/Android/MainActivity.Android.cs", "Weather/Platforms/WebAssembly/Program.cs", "Weather/Package.appxmanifest",
        })
        {
            Assert.True(File.Exists(_temp.Resolve(file)), $"{file} is missing.");
        }

        var csproj = File.ReadAllText(project);
        Assert.StartsWith("<Project Sdk=\"Uno.Sdk\">", csproj, StringComparison.Ordinal);
        Assert.Contains("<ApplicationId>com.companyname.weather</ApplicationId>", csproj, StringComparison.Ordinal);
        Assert.Contains("SkiaRenderer;", csproj, StringComparison.Ordinal);
        Assert.Contains("\"Uno.Sdk\": \"6.7.30\"", File.ReadAllText(_temp.Resolve("global.json")), StringComparison.Ordinal);
        Assert.Contains("x:Class=\"Weather.MainPage\"", File.ReadAllText(_temp.Resolve("Weather/MainPage.xaml")), StringComparison.Ordinal);
        Assert.Contains("Text=\"Hello Uno Platform!\"", File.ReadAllText(_temp.Resolve("Weather/MainPage.xaml")), StringComparison.Ordinal);
        Assert.DoesNotContain(Directory.EnumerateFiles(_temp.Path, "*", SearchOption.AllDirectories), path => File.ReadAllText(path).Contains("UnoApp1", StringComparison.OrdinalIgnoreCase));

        var loaded = ProjectParser.Load(project);
        Assert.Equal(ProjectKind.Uno, loaded.Kind);
        Assert.True(UnoProjects.IsApp(loaded));
        Assert.Equal(("6.7.30", _temp.Resolve("global.json")), UnoProjects.FindSdkVersion(loaded));
    }

    [Fact]
    public void The_WinUI_3_Blank_App_is_dotnet_new_winui_with_its_packages()
    {
        var project = UnoTemplates.Create(UnoTemplates.WinUIAppId, _temp.Path, "Notes");

        Assert.Equal(_temp.Resolve("Notes/Notes.csproj"), project);
        foreach (var file in new[]
        {
            "App.xaml", "App.xaml.cs", "MainWindow.xaml", "MainWindow.xaml.cs", "MainPage.xaml", "MainPage.xaml.cs", "Package.appxmanifest",
            "app.manifest", "Assets/AppIcon.ico", "Assets/StoreLogo.png", "Properties/launchSettings.json",
        })
        {
            Assert.True(File.Exists(_temp.Resolve("Notes/" + file)), $"{file} is missing.");
        }

        var csproj = File.ReadAllText(project);
        Assert.Contains("<TargetFramework>net10.0-windows10.0.26100.0</TargetFramework>", csproj, StringComparison.Ordinal);
        Assert.Contains("<UseWinUI>true</UseWinUI>", csproj, StringComparison.Ordinal);
        Assert.Contains("<PackageReference Include=\"Microsoft.WindowsAppSDK\" Version=\"2.5.1\" />", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("<!--#", csproj, StringComparison.Ordinal);

        var window = File.ReadAllText(_temp.Resolve("Notes/MainWindow.xaml"));
        Assert.Contains("x:Class=\"Notes.MainWindow\"", window, StringComparison.Ordinal);
        Assert.Contains("Title=\"Notes\"", window, StringComparison.Ordinal);
        Assert.Contains("<MicaBackdrop />", window, StringComparison.Ordinal);
        Assert.Contains("EntryPoint=\"Notes.App\"", File.ReadAllText(_temp.Resolve("Notes/Package.appxmanifest")), StringComparison.Ordinal);
        Assert.DoesNotContain(
            Directory.EnumerateFiles(_temp.Path, "*", SearchOption.AllDirectories).Where(p => !p.EndsWith(".png", StringComparison.Ordinal) && !p.EndsWith(".ico", StringComparison.Ordinal)),
            path => File.ReadAllText(path).Contains('$'));
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, File.ReadAllBytes(_temp.Resolve("Notes/Assets/StoreLogo.png"))[..4]);

        var loaded = ProjectParser.Load(project);
        Assert.Equal(ProjectKind.WinUI, loaded.Kind);
        Assert.True(UnoProjects.IsWinUI(loaded));
        Assert.True(UnoProjects.UsesUno(loaded));
    }

    [Fact]
    public void Names_that_are_not_identifiers_become_safe_namespaces()
    {
        UnoTemplates.Create(UnoTemplates.WinUIAppId, _temp.Path, "My.App2");

        Assert.Contains("namespace My.App2;", File.ReadAllText(_temp.Resolve("My.App2/App.xaml.cs")), StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => UnoTemplates.Create("wpf", _temp.Path, "Other"));
        Assert.True(UnoTemplates.IsTemplate(UnoTemplates.UnoAppId));
        Assert.False(UnoTemplates.IsTemplate("console"));
    }

    public void Dispose() => _temp.Dispose();
}
