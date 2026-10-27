using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;
using TabStudio.Compilation.Wpf;
using TabStudio.Core.Diagnostics;
using TabStudio.Testing;
using TabStudio.Wpf.Templates;

namespace TabStudio.Wpf.Tests;

/// <summary>
/// Visual Studio's WPF Application template and small changes to it, built by TabStudio against OpenSilver from
/// nuget.org: the first pass of the XAML compiler as the code is typed, OpenSilver's own second pass when it builds.
/// </summary>
[Collection(NuGetTestGroup.Name)]
public sealed class WpfTemplateTests(NuGetFixture nuget) : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public async Task The_Visual_Studio_template_builds_on_OpenSilver()
    {
        WpfTemplates.Create(_temp.Path, "WpfApp1");
        using var workspace = await WpfAppWorkspace.LoadAsync(nuget, _temp.Path);

        var build = await workspace.BuildAsync("WpfApp1");

        Assert.DoesNotContain(build.Diagnostics, d => d.Severity >= DiagnosticSeverityLevel.Warning);
        var project = workspace.Project("WpfApp1");
        Assert.True(File.Exists(Path.Combine(WpfProject.SecondPassDirectory(project), "MainWindow.xaml.g.cs")));
        Assert.True(File.Exists(Path.Combine(WpfProject.SecondPassDirectory(project), "App.xaml.g.cs")));
        var types = TypeNames(build.EntryAssembly!.Image);
        Assert.Contains("WpfApp1.MainWindow", types);
        Assert.Contains("WpfApp1.App", types);
        Assert.Contains(types, t => t.EndsWith("MainwindowǀǀXamlǀǀFactory", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Named_elements_are_fields_of_their_types_in_the_code_behind()
    {
        WpfTemplates.Create(_temp.Path, "WpfApp1");
        _temp.Write("WpfApp1/MainWindow.xaml", Window("""
            <StackPanel>
                <TextBlock x:Name="Greeting" Text="Hello" />
                <Button x:Name="Go" Content="Go" Click="Go_Click" />
            </StackPanel>
            """));
        var codeBehind = _temp.Write("WpfApp1/MainWindow.xaml.cs", CodeBehind("""
                    private void Go_Click(object sender, RoutedEventArgs e)
                    {
                        Greeting.Text = Go.Content as string ?? "Gone";
                    }
            """));
        using var workspace = await WpfAppWorkspace.LoadAsync(nuget, _temp.Path);

        var diagnostics = await workspace.Host.GetDocumentDiagnosticsAsync(codeBehind);
        var xaml = await workspace.Host.GetWpfXamlFileAsync(_temp.Resolve("WpfApp1/MainWindow.xaml"));

        Assert.DoesNotContain(diagnostics, d => d.Severity >= DiagnosticSeverityLevel.Warning);
        var window = xaml!.Compilation.GetTypeByMetadataName("WpfApp1.MainWindow")!;
        Assert.Equal("System.Windows.Controls.TextBlock", ((IFieldSymbol)window.GetMembers("Greeting").Single()).Type.ToDisplayString());
        Assert.Equal("System.Windows.Controls.Button", ((IFieldSymbol)window.GetMembers("Go").Single()).Type.ToDisplayString());
        Assert.Equal("System.Windows.Window", window.BaseType!.ToDisplayString());
        await workspace.BuildAsync("WpfApp1");
    }

    [Fact]
    public async Task A_xaml_error_is_reported_in_its_file()
    {
        WpfTemplates.Create(_temp.Path, "WpfApp1");
        var xamlPath = _temp.Write("WpfApp1/MainWindow.xaml", Window("""
            <StackPanel>
                <Buton Content="Typo" />
            </StackPanel>
            """));
        using var workspace = await WpfAppWorkspace.LoadAsync(nuget, _temp.Path);

        var build = await workspace.TryBuildAsync("WpfApp1");

        Assert.False(build.Success);
        Assert.Contains(build.Diagnostics, d => d.Severity == DiagnosticSeverityLevel.Error
            && d.Location?.FilePath == xamlPath
            && d.Message.Contains("Buton", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_event_handler_the_class_does_not_have_is_reported_at_its_attribute()
    {
        WpfTemplates.Create(_temp.Path, "WpfApp1");
        var xamlPath = _temp.Write("WpfApp1/MainWindow.xaml", Window("""<Button Content="Go" Click="Missing_Click" />"""));
        using var workspace = await WpfAppWorkspace.LoadAsync(nuget, _temp.Path);

        var build = await workspace.TryBuildAsync("WpfApp1");

        var error = Assert.Single(build.Diagnostics, d => d.Severity == DiagnosticSeverityLevel.Error);
        Assert.Equal(WpfDiagnosticIds.GeneratedCodeError, error.Id);
        Assert.Equal(xamlPath, error.Location!.FilePath);
        Assert.Equal("Missing_Click", File.ReadAllText(xamlPath).Substring(error.Location.Start, error.Location.Length));
        Assert.StartsWith("Click=\"Missing_Click\" names a method the class does not have", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Window_settings_without_effect_in_the_web_preview_are_reported_and_left_out()
    {
        WpfTemplates.Create(_temp.Path, "WpfApp1");
        var xamlPath = _temp.Write("WpfApp1/MainWindow.xaml", """
            <Window x:Class="WpfApp1.MainWindow"
                    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    Title="Settings" Height="450" Width="800"
                    WindowStartupLocation="CenterScreen" ResizeMode="NoResize" Background="Beige">
                <Grid />
            </Window>
            """);
        using var workspace = await WpfAppWorkspace.LoadAsync(nuget, _temp.Path);

        var build = await workspace.BuildAsync("WpfApp1");

        var notes = build.Diagnostics.Where(d => d.Id == WpfDiagnosticIds.WindowSettingIgnored).ToList();
        Assert.Equal(["Background", "ResizeMode", "WindowStartupLocation"], notes.Select(n => File.ReadAllText(xamlPath).Substring(n.Location!.Start, n.Location.Length)).Order(StringComparer.Ordinal));
        Assert.All(notes, n => Assert.Equal(DiagnosticSeverityLevel.Info, n.Severity));
        Assert.Contains(notes, n => n.Message == "WindowStartupLocation has no effect in the web preview: the web preview centers the window, or makes it fill the pane in Fill.");
    }

    [Fact]
    public async Task Code_that_uses_what_OpenSilver_does_not_implement_gets_a_warning()
    {
        WpfTemplates.Create(_temp.Path, "WpfApp1");
        var codeBehind = _temp.Write("WpfApp1/MainWindow.xaml.cs", CodeBehind("""
                    private void Again()
                    {
                        new MainWindow().Show();
                        Title = "Shown";
                    }
            """));
        using var workspace = await WpfAppWorkspace.LoadAsync(nuget, _temp.Path);

        var diagnostics = await workspace.Host.GetDocumentDiagnosticsAsync(codeBehind);

        var warning = Assert.Single(diagnostics, d => d.Id == WpfDiagnosticIds.NotImplementedApi);
        Assert.Equal(DiagnosticSeverityLevel.Warning, warning.Severity);
        Assert.StartsWith("Window.Show() is in OpenSilver's WPF API but not implemented", warning.Message, StringComparison.Ordinal);
        Assert.Contains("Windows Bridge", warning.Message, StringComparison.Ordinal);
    }

    public void Dispose() => _temp.Dispose();

    /// <summary>The template's window with other content.</summary>
    internal static string Window(string content) => $$"""
        <Window x:Class="WpfApp1.MainWindow"
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
                xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
                xmlns:local="clr-namespace:WpfApp1"
                mc:Ignorable="d"
                Title="MainWindow" Height="450" Width="800">
            {{content}}
        </Window>
        """;

    /// <summary>The template's code-behind with more members.</summary>
    internal static string CodeBehind(string members) => $$"""
        using System.Windows;
        using System.Windows.Controls;

        namespace WpfApp1
        {
            public partial class MainWindow : Window
            {
                public MainWindow()
                {
                    InitializeComponent();
                }

        {{members}}
            }
        }
        """;

    private static List<string> TypeNames(byte[] image)
    {
        using var reader = new PEReader(new MemoryStream(image));
        var metadata = reader.GetMetadataReader();
        return metadata.TypeDefinitions
            .Select(handle => metadata.GetTypeDefinition(handle))
            .Select(type => metadata.GetString(type.Namespace) is { Length: > 0 } ns ? $"{ns}.{metadata.GetString(type.Name)}" : metadata.GetString(type.Name))
            .ToList();
    }
}
