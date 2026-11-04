namespace TabStudio.Porting.Tests;

/// <summary>Visual Studio's WPF template, ported: the parts every WPF app has.</summary>
public sealed class WpfTemplateTests
{
    [Fact]
    public void The_window_becomes_an_AppWindow_that_keeps_its_title_and_size()
    {
        var result = SampleProjects.Port("WpfTemplate");
        var window = result.Files.Single(f => f.Path == "MainWindow.axaml").Text!;

        Assert.Contains("<app:AppWindow x:Class=\"WpfApp1.MainWindow\"", window, StringComparison.Ordinal);
        Assert.Contains("xmlns=\"https://github.com/avaloniaui\"", window, StringComparison.Ordinal);
        Assert.Contains("xmlns:local=\"using:WpfApp1\"", window, StringComparison.Ordinal);
        Assert.Contains("xmlns:app=\"using:WpfApp1.Porting\"", window, StringComparison.Ordinal);
        Assert.Contains("Title=\"MainWindow\" Height=\"450\" Width=\"800\"", window, StringComparison.Ordinal);
        Assert.DoesNotContain("winfx/2006/xaml/presentation", window, StringComparison.Ordinal);
    }

    [Fact]
    public void The_code_behind_derives_from_AppWindow_without_WPF_usings()
    {
        var code = SampleProjects.Port("WpfTemplate").Files.Single(f => f.Path == "MainWindow.axaml.cs").Text!;

        Assert.Contains("public partial class MainWindow : AppWindow", code, StringComparison.Ordinal);
        Assert.Contains("using WpfApp1.Porting;", code, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Windows", code, StringComparison.Ordinal);
        Assert.Contains("InitializeComponent();", code, StringComparison.Ordinal);
    }

    [Fact]
    public void The_application_opens_the_StartupUri_window_as_the_main_window()
    {
        var result = SampleProjects.Port("WpfTemplate");
        var xaml = result.Files.Single(f => f.Path == "App.axaml").Text!;
        var code = result.Files.Single(f => f.Path == "App.axaml.cs").Text!;

        Assert.DoesNotContain("StartupUri", xaml, StringComparison.Ordinal);
        Assert.Contains("RequestedThemeVariant=\"Light\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<FluentTheme", xaml, StringComparison.Ordinal);
        Assert.Contains("public override void Initialize() => AvaloniaXamlLoader.Load(this);", code, StringComparison.Ordinal);
        Assert.Contains("WindowView.ShowMain(ApplicationLifetime, new MainWindow());", code, StringComparison.Ordinal);
        Assert.Contains("public partial class App : Application", code, StringComparison.Ordinal);
    }

    [Fact]
    public void The_project_is_an_Avalonia_project_as_TabStudio_makes_one()
    {
        var result = SampleProjects.Port("WpfTemplate");
        var project = result.Files.Single(f => f.Path == "WpfApp1.Avalonia.csproj").Text!;

        Assert.Contains("<TargetFramework>net10.0</TargetFramework>", project, StringComparison.Ordinal);
        Assert.Contains("<RootNamespace>WpfApp1</RootNamespace>", project, StringComparison.Ordinal);
        Assert.Contains("<PackageReference Include=\"Avalonia\" Version=\"11.3.22\" />", project, StringComparison.Ordinal);
        Assert.Contains("<PackageReference Include=\"Avalonia.Themes.Fluent\" Version=\"11.3.22\" />", project, StringComparison.Ordinal);
        Assert.DoesNotContain("UseWPF", project, StringComparison.Ordinal);
        Assert.DoesNotContain("DataGrid", project, StringComparison.Ordinal);
        Assert.Contains(result.Files, f => f.Path == "Program.cs");
        Assert.DoesNotContain(result.Files, f => f.Path == "AssemblyInfo.cs");
        Assert.Contains(result.Files, f => f.Path == "Porting/WindowView.cs");
        Assert.Contains(result.Files, f => f.Path == "Porting/AppWindow.cs");
    }

    [Fact]
    public void The_template_ports_without_notes()
    {
        Assert.Empty(SampleProjects.Port("WpfTemplate").Notes);
    }

    [Fact]
    public void Resource_names_with_semicolons_and_percent_signs_are_escaped_for_MSBuild()
    {
        var sample = SampleProjects.Load("WpfTemplate");
        var source = new PortSource
        {
            Name = sample.Name,
            Kind = sample.Kind,
            Properties = sample.Properties,
            Packages = sample.Packages,
            Files = sample.Files,
            Resources = ["Images/a;b.png", "Images/100%.png", "Images/plain.png", "Images/what?*.png"],
        };

        var result = AvaloniaPorter.Port(source, new PortOptions("WpfApp1.Avalonia", Path.Combine(Path.GetTempPath(), "tabstudio-port", "escape"), SampleProjects.AvaloniaVersion));
        var project = result.Files.Single(f => f.Path == "WpfApp1.Avalonia.csproj").Text!;

        // "a;b.png" would be two items, a and b.png, and "100%.png" an escape MSBuild cannot read.
        Assert.Contains("<AvaloniaResource Include=\"Images\\a%3Bb.png\" />", project, StringComparison.Ordinal);
        Assert.Contains("<AvaloniaResource Include=\"Images\\100%25.png\" />", project, StringComparison.Ordinal);
        Assert.Contains("<AvaloniaResource Include=\"Images\\plain.png\" />", project, StringComparison.Ordinal);
        Assert.Contains("<AvaloniaResource Include=\"Images\\what%3F%2A.png\" />", project, StringComparison.Ordinal);
        Assert.DoesNotContain("a;b", project, StringComparison.Ordinal);
        Assert.Equal("%24(x)%40y%27", TabStudio.Porting.Output.ProjectFiles.MSBuildEscape("$(x)@y'"));
    }
}
