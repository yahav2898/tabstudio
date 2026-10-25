using TabStudio.Compilation.Building;
using TabStudio.Compilation.Running;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Compilation.Tests;

/// <summary>
/// TabStudio's XAML generator for Avalonia projects, against a small stand-in for Avalonia: the XAML goes into the
/// assembly, InitializeComponent and AvaloniaXamlLoader.Load(this) hand it to the host with the file's path in the
/// app, and x:Name fields are typed.
/// </summary>
public sealed class AvaloniaXamlGeneratorTests : IDisposable
{
    private const string AvaloniaStandIn = """
        [assembly: Avalonia.Metadata.XmlnsDefinition("https://github.com/avaloniaui", "Avalonia.Controls")]

        namespace Avalonia.Metadata
        {
            [System.AttributeUsage(System.AttributeTargets.Assembly, AllowMultiple = true)]
            public sealed class XmlnsDefinitionAttribute : System.Attribute
            {
                public XmlnsDefinitionAttribute(string xmlNamespace, string clrNamespace) { }
            }
        }

        namespace Avalonia
        {
            public class StyledElement { public Avalonia.Controls.INameScope? Scope { get; set; } }
            public class Application { public virtual void Initialize() { } }
        }

        namespace Avalonia.Controls
        {
            public interface INameScope { object? Find(string name); }
            public static class NameScope { public static INameScope? GetNameScope(Avalonia.StyledElement element) => element.Scope; }
            public class Control : Avalonia.StyledElement { }
            public class UserControl : Control { }
            public class TextBox : Control { public string? Text { get; set; } }
        }

        namespace Avalonia.Markup.Xaml
        {
            public static class AvaloniaXamlLoader
            {
                public static void Load(object obj) => throw new System.InvalidOperationException("Not intercepted.");
            }
        }
        """;

    private readonly TempDirectory _temp = new();
    private readonly RoslynWorkspaceHost _host = new();
    private readonly string _projectPath;
    private readonly string _avalonia;

    public AvaloniaXamlGeneratorTests()
    {
        _projectPath = _temp.Write("Tablet/Tablet.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Avalonia" Version="11.3.0" />
              </ItemGroup>
            </Project>
            """);
        _temp.Write("Tablet/App.axaml", """
            <Application xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" x:Class="Tablet.App" />
            """);
        _temp.Write("Tablet/App.axaml.cs", """
            namespace Tablet;

            public partial class App : Avalonia.Application
            {
                public override void Initialize() => Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
            }
            """);
        _temp.Write("Tablet/Views/MainView.axaml", """
            <UserControl xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" x:Class="Tablet.Views.MainView">
              <TextBox x:Name="NameBox" />
            </UserControl>
            """);
        _temp.Write("Tablet/Views/MainView.axaml.cs", """
            namespace Tablet.Views;

            public partial class MainView : Avalonia.Controls.UserControl
            {
                public MainView() => InitializeComponent();
            }
            """);
        _temp.Write("Tablet/Program.cs", """
            using Tablet;
            using Tablet.Views;

            var loaded = new List<string>();
            AppContext.SetData("TabStudio.Avalonia.LoadXaml", new Action<object, string, string>((root, xaml, path) =>
            {
                loaded.Add($"{root.GetType().Name} {xaml.Contains("x:Class", StringComparison.Ordinal)} {path}");
                if (root is Avalonia.StyledElement element)
                {
                    element.Scope = new Scope(new() { ["NameBox"] = new Avalonia.Controls.TextBox { Text = "found" } });
                }
            }));

            new App().Initialize();
            var view = new MainView();
            Console.WriteLine(string.Join(", ", loaded));
            Console.WriteLine(view.NameBox?.Text);

            sealed class Scope(Dictionary<string, object> names) : Avalonia.Controls.INameScope
            {
                public object? Find(string name) => names.GetValueOrDefault(name);
            }
            """);

        var library = new CSharpCompiler().Compile(new CompilationRequest
        {
            AssemblyName = "Avalonia",
            Kind = AssemblyKind.Library,
            Documents = [new SourceDocument("Avalonia.cs", AvaloniaStandIn)],
        });
        Assert.True(library.Success, string.Join('\n', library.Diagnostics));
        _avalonia = _temp.Resolve("packages/avalonia/11.3.0/lib/net8.0/Avalonia.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(_avalonia)!);
        File.WriteAllBytes(_avalonia, library.Assembly!.Image);

        _host.Load(WorkspaceLoader.Load(_projectPath), FileSystemTextProvider.Instance);
        _host.SetPackageReferences(_projectPath, [_avalonia]);
    }

    private ProjectInfo Project => _host.FindProject(_projectPath)!;

    [Fact]
    public async Task InitializeComponent_and_AvaloniaXamlLoader_hand_the_XAML_to_the_host_and_named_elements_are_found()
    {
        var build = await new ProjectBuilder(_host).BuildAsync(Project);
        Assert.True(build.Success, string.Join('\n', build.Diagnostics));

        var console = new CapturingConsole();
        var run = await new ProgramRunner().RunAsync(build.EntryAssembly!, build.Dependencies, [_avalonia], [], console);

        Assert.True(run.Outcome == RunOutcome.Completed, console.Error);
        Assert.Equal("App True /App.axaml, MainView True /Views/MainView.axaml\nfound\n", console.Output);
    }

    [Fact]
    public async Task Editing_the_XAML_changes_what_is_compiled_into_the_program()
    {
        var path = _temp.Resolve("Tablet/Views/MainView.axaml");
        Assert.True(_host.UpdateText(path, """
            <UserControl xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" x:Class="Tablet.Views.MainView">
              <TextBox x:Name="RenamedBox" />
            </UserControl>
            """));

        var build = await new ProjectBuilder(_host).BuildAsync(Project);

        // The code still uses NameBox, which the XAML no longer names.
        Assert.Contains(build.Diagnostics, d => d.Id == "CS1061" && d.Message.Contains("NameBox", StringComparison.Ordinal));
    }

    [Fact]
    public async Task XAML_that_is_not_well_formed_is_an_error_naming_the_file()
    {
        _host.UpdateText(_temp.Resolve("Tablet/Views/MainView.axaml"), "<UserControl");

        var build = await new ProjectBuilder(_host).BuildAsync(Project);

        Assert.Contains(build.Diagnostics, d => d.Id == "CS1029" && d.Message.Contains("MainView.axaml is not valid XAML", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        _host.Dispose();
        _temp.Dispose();
    }
}
