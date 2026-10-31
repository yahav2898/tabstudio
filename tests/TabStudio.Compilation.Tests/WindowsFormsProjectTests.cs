using TabStudio.Compilation.WindowsForms;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Compilation.Tests;

/// <summary>
/// What the Windows Desktop SDK gives a Windows Forms project, which TabStudio gives it itself: the global usings of
/// System.Drawing and System.Windows.Forms, and the ApplicationConfiguration class Main calls.
/// </summary>
public sealed class WindowsFormsProjectTests : IDisposable
{
    private const string VisualStudioProject = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>WinExe</OutputType>
            <TargetFramework>net10.0-windows</TargetFramework>
            <Nullable>enable</Nullable>
            <UseWindowsForms>true</UseWindowsForms>
            <ImplicitUsings>enable</ImplicitUsings>
          </PropertyGroup>
        </Project>
        """;

    private readonly TempDirectory _temp = new();

    [Fact]
    public void A_Windows_Forms_project_imports_System_Drawing_and_System_Windows_Forms()
    {
        var project = ProjectParser.Parse(_temp.Resolve("App/App.csproj"), VisualStudioProject);

        var source = CSharpCompiler.CreateGlobalUsingsSource(project);

        Assert.NotNull(source);
        Assert.Contains("global using global::System.Drawing;\n", source, StringComparison.Ordinal);
        Assert.Contains("global using global::System.Windows.Forms;\n", source, StringComparison.Ordinal);
        Assert.Contains("global using global::System.Linq;\n", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Without_implicit_usings_or_Windows_Forms_nothing_is_imported_for_it()
    {
        var noImplicit = ProjectParser.Parse(_temp.Resolve("A/A.csproj"), VisualStudioProject.Replace("<ImplicitUsings>enable</ImplicitUsings>", string.Empty, StringComparison.Ordinal));
        var console = ProjectParser.Parse(_temp.Resolve("B/B.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>
            </Project>
            """);

        Assert.Null(CSharpCompiler.CreateGlobalUsingsSource(noImplicit));
        Assert.DoesNotContain("System.Windows.Forms", CSharpCompiler.CreateGlobalUsingsSource(console), StringComparison.Ordinal);
    }

    [Fact]
    public void A_Windows_Forms_app_gets_ApplicationConfiguration_with_the_SDK_defaults()
    {
        var project = ProjectParser.Parse(_temp.Resolve("App/App.csproj"), VisualStudioProject);

        var source = WindowsFormsProject.CreateApplicationConfigurationSource(project);

        Assert.NotNull(source);
        Assert.Contains("internal static partial class ApplicationConfiguration", source, StringComparison.Ordinal);
        Assert.Contains("global::System.Windows.Forms.Application.EnableVisualStyles();", source, StringComparison.Ordinal);
        Assert.Contains("global::System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);", source, StringComparison.Ordinal);
        Assert.Contains("SetHighDpiMode(global::System.Windows.Forms.HighDpiMode.SystemAware);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SetDefaultFont", source, StringComparison.Ordinal);
    }

    [Fact]
    public void The_projects_application_properties_configure_the_app()
    {
        var project = ProjectParser.Parse(_temp.Resolve("App/App.csproj"), VisualStudioProject.Replace(
            "<ImplicitUsings>enable</ImplicitUsings>",
            """
            <ImplicitUsings>enable</ImplicitUsings>
            <ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>
            <ApplicationVisualStyles>false</ApplicationVisualStyles>
            <ApplicationDefaultFont>Segoe UI, 10.5pt, style=Bold Italic</ApplicationDefaultFont>
            """,
            StringComparison.Ordinal));

        var source = WindowsFormsProject.CreateApplicationConfigurationSource(project);

        Assert.NotNull(source);
        Assert.DoesNotContain("EnableVisualStyles", source, StringComparison.Ordinal);
        Assert.Contains("HighDpiMode.PerMonitorV2", source, StringComparison.Ordinal);
        Assert.Contains(
            "SetDefaultFont(new global::System.Drawing.Font(\"Segoe UI\", 10.5f, global::System.Drawing.FontStyle.Bold | global::System.Drawing.FontStyle.Italic, global::System.Drawing.GraphicsUnit.Point));",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_Windows_Forms_library_gets_no_ApplicationConfiguration()
    {
        var project = ProjectParser.Parse(_temp.Resolve("Lib/Lib.csproj"), VisualStudioProject.Replace("<OutputType>WinExe</OutputType>", "<OutputType>Library</OutputType>", StringComparison.Ordinal));

        Assert.Null(WindowsFormsProject.CreateApplicationConfigurationSource(project));
    }

    /// <summary>
    /// VBDev2Dev's Dots: its game library, a .NET Framework project, references System.Drawing to draw for its Windows
    /// Forms app. It found no Graphics, Pen or Font (16 errors), as only Windows Forms projects got the Forms layer.
    /// </summary>
    [Fact]
    public void A_NET_Framework_library_that_references_System_Drawing_compiles_against_the_Forms_layer()
    {
        const string Library = """
            <Project ToolsVersion="12.0" DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup><OutputType>Library</OutputType><TargetFrameworkVersion>v4.6.2</TargetFrameworkVersion></PropertyGroup>
              <ItemGroup><Reference Include="System" /><Reference Include="System.Drawing" /></ItemGroup>
              <ItemGroup><Import Include="System.Drawing" /></ItemGroup>
            </Project>
            """;
        var drawing = ProjectParser.Parse(_temp.Resolve("DotsGame/DotsGame.vbproj"), Library);
        var plain = ProjectParser.Parse(_temp.Resolve("Data/Data.vbproj"), Library.Replace("<Reference Include=\"System.Drawing\" />", string.Empty, StringComparison.Ordinal));

        Assert.False(WindowsFormsProject.UsesWindowsForms(drawing));
        Assert.True(WindowsFormsProject.UsesFormsLayer(drawing));
        Assert.False(WindowsFormsProject.UsesFormsLayer(plain));
    }

    public void Dispose() => _temp.Dispose();
}
