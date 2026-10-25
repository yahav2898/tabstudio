using TabStudio.Core.Diagnostics;
using TabStudio.Testing;
using TabStudio.Uno.Templates;

namespace TabStudio.Uno.Tests;

/// <summary>
/// The Uno Platform App against nuget.org: Uno's XAML generator from the restored Uno.WinUI package compiles its XAML
/// in TabStudio's Roslyn host, configured as Uno.Sdk configures it, and its errors land in the XAML files.
/// </summary>
[Collection(NuGetTestGroup.Name)]
public sealed class UnoXamlBuildTests(NuGetFixture nuget) : IDisposable
{
    private const string Name = "HelloUno";

    private readonly TempDirectory _temp = new();

    [Fact]
    public async Task The_template_builds_with_Unos_XAML_generator()
    {
        using var app = await UnoAppWorkspace.CreateAsync(nuget, _temp, UnoTemplates.UnoAppId, Name);

        Assert.Contains(app.Restore.CompileAssemblies, path => path.EndsWith("Uno.UI.dll", StringComparison.Ordinal));
        Assert.Contains(app.Restore.Analyzers, path => path.EndsWith("Uno.UI.SourceGenerators.dll", StringComparison.Ordinal));
        Assert.True(app.Host.IsUnoXamlDocument(app.PathOf("MainPage.xaml")));
        Assert.True(app.Host.IsUnoXamlDocument(app.PathOf("App.xaml")));
        Assert.False(app.Host.IsUnoXamlDocument(app.PathOf("Platforms/Desktop/Program.cs")));

        var build = await app.BuildAsync();
        Assert.Equal(0, build.ErrorCount);
        Assert.Equal(Name, build.EntryAssembly!.Name);

        var compilation = await app.Host.GetCompilationAsync(app.Project.FilePath);
        var generated = compilation!.SyntaxTrees.Where(t => t.FilePath.Contains("XamlCodeGenerator", StringComparison.Ordinal)).Select(t => t.ToString()).ToList();
        Assert.Contains(generated, source => source.Contains("partial class MainPage", StringComparison.Ordinal) && source.Contains("InitializeComponent", StringComparison.Ordinal));
        Assert.Contains(generated, source => source.Contains("partial class App", StringComparison.Ordinal));

        // Uno's XAML generator sees PRIResource items and marks the assembly for localization.
        Assert.Contains(generated, source => source.Contains("AssemblyMetadata(\"UnoHasLocalizationResources\", \"True\")", StringComparison.Ordinal));

        // The other platforms' heads are not compiled, and Uno.Sdk's global usings are.
        Assert.DoesNotContain(compilation.SyntaxTrees, t => t.FilePath.Contains("Platforms", StringComparison.Ordinal));
        Assert.NotNull(compilation.GetTypeByMetadataName($"{Name}.MainPage"));
    }

    [Fact]
    public async Task An_x_Name_is_a_field_of_the_page_and_a_Click_handler_compiles()
    {
        using var app = await UnoAppWorkspace.CreateAsync(nuget, _temp, UnoTemplates.UnoAppId, Name);
        app.Save("MainPage.xaml", ClickingPage);
        app.Save("MainPage.xaml.cs", ClickingCodeBehind);

        var build = await app.BuildAsync();

        Assert.Equal(0, build.ErrorCount);
        var compilation = await app.Host.GetCompilationAsync(app.Project.FilePath);
        var page = compilation!.GetTypeByMetadataName($"{Name}.MainPage")!;
        Assert.Contains(page.GetMembers("Greeting"), m => m is Microsoft.CodeAnalysis.IFieldSymbol or Microsoft.CodeAnalysis.IPropertySymbol);
        Assert.Contains(page.GetMembers("SayHello"), m => m is Microsoft.CodeAnalysis.IFieldSymbol or Microsoft.CodeAnalysis.IPropertySymbol);
    }

    [Fact]
    public async Task A_XAML_error_is_on_its_line_of_the_XAML_file_live_and_in_the_build()
    {
        using var app = await UnoAppWorkspace.CreateAsync(nuget, _temp, UnoTemplates.UnoAppId, Name);
        var page = app.PathOf("MainPage.xaml");
        app.Save("MainPage.xaml", app.Read("MainPage.xaml").Replace("<TextBlock AutomationProperties", "<TextBlok AutomationProperties", StringComparison.Ordinal));

        var live = await app.Host.GetDocumentDiagnosticsAsync(page);
        var build = await new Compilation.Building.ProjectBuilder(app.Host).BuildAsync(app.Project);

        Assert.False(build.Success);
        var error = Assert.Single(live, d => d.Severity == DiagnosticSeverityLevel.Error);
        Assert.Equal(page, error.Location!.FilePath);
        Assert.Equal(11, error.Location.Line);
        Assert.Contains("TextBlok", error.Message, StringComparison.Ordinal);
        Assert.Contains(build.Diagnostics, d => d.Severity == DiagnosticSeverityLevel.Error && d.Location?.FilePath == page && d.Location.Line == 11);
    }

    [Fact]
    public async Task A_CSharp_error_in_the_generated_code_points_to_its_element_in_the_XAML()
    {
        using var app = await UnoAppWorkspace.CreateAsync(nuget, _temp, UnoTemplates.UnoAppId, Name);
        var page = app.PathOf("MainPage.xaml");
        app.Save("MainPage.xaml", ClickingPage.Replace("Click=\"OnSayHello\"", "Click=\"OnMissing\"", StringComparison.Ordinal));
        app.Save("MainPage.xaml.cs", ClickingCodeBehind);

        var live = await app.Host.GetUnoXamlDiagnosticsAsync(page);

        var error = Assert.Single(live, d => d.Severity == DiagnosticSeverityLevel.Error);
        Assert.Equal(page, error.Location!.FilePath);
        Assert.Equal(ClickingPage.ReplaceLineEndings("\n").Split('\n').ToList().FindIndex(l => l.Contains("<Button", StringComparison.Ordinal)) + 1, error.Location.Line);
        Assert.Contains("OnMissing", error.Message, StringComparison.Ordinal);
    }

    public void Dispose() => _temp.Dispose();

    /// <summary>The template's page with a named text and a button that changes it.</summary>
    internal const string ClickingPage = """
        <Page x:Class="HelloUno.MainPage"
              xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
              xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
              Background="{ThemeResource ApplicationPageBackgroundThemeBrush}">
          <StackPanel HorizontalAlignment="Center" VerticalAlignment="Center" Spacing="16">
            <TextBlock x:Name="Greeting" Text="Hello Uno Platform!" FontSize="28" HorizontalAlignment="Center" />
            <Button x:Name="SayHello" Content="Say hello" Click="OnSayHello" HorizontalAlignment="Center" />
          </StackPanel>
        </Page>
        """;

    /// <summary>The code-behind of <see cref="ClickingPage"/>.</summary>
    internal const string ClickingCodeBehind = """
        namespace HelloUno;

        public sealed partial class MainPage : Page
        {
            public MainPage()
            {
                this.InitializeComponent();
            }

            private void OnSayHello(object sender, RoutedEventArgs e) => Greeting.Text = "Hello from TabStudio!";
        }
        """;
}
