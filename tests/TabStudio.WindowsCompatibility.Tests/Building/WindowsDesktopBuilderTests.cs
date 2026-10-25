using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Resources;
using TabStudio.Core.Diagnostics;
using TabStudio.Testing;
using TabStudio.WindowsCompatibility.Tests.Infrastructure;

namespace TabStudio.WindowsCompatibility.Tests.Building;

/// <summary>
/// Building Visual Studio's Windows Forms and WPF templates on the device: Roslyn against the Windows Desktop
/// reference pack from nuget.org, and WPF's markup compiled to BAML by the SDK's own markup compiler.
/// </summary>
public sealed class WindowsDesktopBuilderTests(WindowsBuildFixture fixture) : IClassFixture<WindowsBuildFixture>, IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task Builds_the_windows_forms_template_into_an_app_for_the_windows_desktop_runtime()
    {
        var file = VisualStudioTemplates.WriteWindowsForms(_temp, "WinFormsApp1");

        var (_, result) = await fixture.BuildAsync(file);

        Assert.True(result.Success, Describe(result.Build.Diagnostics));
        Assert.DoesNotContain(result.Build.Diagnostics, d => d.Severity == DiagnosticSeverityLevel.Warning);
        Assert.Equal(Path.Combine(_temp.Resolve("WinFormsApp1"), "bin", "Debug", "net10.0-windows", "WinFormsApp1.dll"), result.EntryAssembly);
        var assembly = Inspect(result.EntryAssembly!);
        Assert.Contains("System.Windows.Forms", assembly.References);
        Assert.Contains("WinFormsApp1.Form1", assembly.Types);
        Assert.Contains("WinFormsApp1.Program", assembly.Types);

        var runtimeConfig = await File.ReadAllTextAsync(Path.Combine(result.OutputFolder!, "WinFormsApp1.runtimeconfig.json"));
        Assert.Contains("\"Microsoft.WindowsDesktop.App\"", runtimeConfig, StringComparison.Ordinal);
        Assert.Contains("\"10.0.0\"", runtimeConfig, StringComparison.Ordinal);

        // The framework's assemblies come from the Windows Desktop runtime, not from the app's folder.
        Assert.DoesNotContain(Directory.GetFiles(result.OutputFolder!), f => Path.GetFileName(f).StartsWith("System.Windows.Forms", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_windows_forms_error_is_in_the_error_list_at_its_line()
    {
        var file = VisualStudioTemplates.WriteWindowsForms(_temp, "Broken");
        _temp.Write("Broken/Form1.cs", """
            namespace Broken
            {
                public partial class Form1 : Form
                {
                    public Form1()
                    {
                        InitializeComponent();
                        Text = 42;
                    }
                }
            }
            """);

        var (_, result) = await fixture.BuildAsync(file);

        Assert.False(result.Success);
        var error = Assert.Single(result.Build.Diagnostics, d => d.Severity == DiagnosticSeverityLevel.Error);
        Assert.Equal("CS0029", error.Id);
        Assert.Equal("Form1.cs", error.FileName);
        Assert.Equal(8, error.Location!.Line);
    }

    [Fact]
    public async Task Builds_the_wpf_template_with_its_markup_compiled_to_baml()
    {
        var file = VisualStudioTemplates.WriteWpf(_temp, "WpfApp1");

        var (_, result) = await fixture.BuildAsync(file);

        Assert.True(result.Success, Describe(result.Build.Diagnostics));
        var assembly = Inspect(result.EntryAssembly!);
        Assert.Contains("PresentationFramework", assembly.References);
        Assert.Contains("InitializeComponent", assembly.Methods["WpfApp1.MainWindow"]);
        Assert.Contains("InitializeComponent", assembly.Methods["WpfApp1.App"]);
        Assert.Contains("Main", assembly.Methods["WpfApp1.App"]);
        Assert.True(assembly.Resources.TryGetValue("WpfApp1.g.resources", out var resources), string.Join(", ", assembly.Resources.Keys));
        Assert.Contains("mainwindow.baml", ResourceNames(resources));

        // The markup compiler's code is in the intermediate folder, as the SDK leaves it.
        Assert.True(File.Exists(_temp.Resolve("WpfApp1/obj/Debug/net10.0-windows/MainWindow.g.cs")));
        Assert.True(File.Exists(_temp.Resolve("WpfApp1/obj/Debug/net10.0-windows/App.g.cs")));
    }

    [Fact]
    public async Task Named_elements_are_fields_of_the_code_behind()
    {
        var file = VisualStudioTemplates.WriteWpf(_temp, "Named");
        VisualStudioTemplates.WriteWpfWindow(_temp, "Named", """<TextBlock x:Name="Greeting" Text="Hello" />""");
        _temp.Write("Named/MainWindow.xaml.cs", """
            using System.Windows;

            namespace Named
            {
                public partial class MainWindow : Window
                {
                    public MainWindow()
                    {
                        InitializeComponent();
                        Greeting.Text = "Hello from the code";
                        Greeting.FontSize = "large";
                    }
                }
            }
            """);

        var (_, result) = await fixture.BuildAsync(file);

        var error = Assert.Single(result.Build.Diagnostics, d => d.Severity == DiagnosticSeverityLevel.Error);
        Assert.Equal("CS0029", error.Id);
        Assert.Equal("MainWindow.xaml.cs", error.FileName);
        Assert.Equal(11, error.Location!.Line);
    }

    [Fact]
    public async Task A_page_that_uses_the_projects_own_types_is_compiled_in_a_second_pass()
    {
        var file = VisualStudioTemplates.WriteWpf(_temp, "LocalTypes");
        VisualStudioTemplates.WriteWpfWindow(_temp, "LocalTypes", """<local:Greeting Text="Hello" />""");
        _temp.Write("LocalTypes/Greeting.cs", """
            using System.Windows.Controls;

            namespace LocalTypes;

            public class Greeting : TextBlock
            {
            }
            """);

        var (_, result) = await fixture.BuildAsync(file);

        Assert.True(result.Success, Describe(result.Build.Diagnostics));
        var assembly = Inspect(result.EntryAssembly!);
        Assert.True(assembly.Resources.TryGetValue("LocalTypes.g.resources", out var resources), string.Join(", ", assembly.Resources.Keys));
        Assert.Contains("mainwindow.baml", ResourceNames(resources));
        Assert.False(File.Exists(_temp.Resolve("LocalTypes/obj/Debug/net10.0-windows/LocalTypes_wpftmp.dll")));
    }

    [Fact]
    public async Task A_xaml_error_is_in_the_error_list_at_its_line_in_the_xaml()
    {
        var file = VisualStudioTemplates.WriteWpf(_temp, "BadXaml");
        VisualStudioTemplates.WriteWpfWindow(_temp, "BadXaml", """<Button Contnt="Hello" />""");

        var (_, result) = await fixture.BuildAsync(file);

        Assert.False(result.Success);
        var error = Assert.Single(result.Build.Diagnostics, d => d.Severity == DiagnosticSeverityLevel.Error);
        Assert.StartsWith("MC", error.Id, StringComparison.Ordinal);
        Assert.Contains("Contnt", error.Message, StringComparison.Ordinal);
        Assert.Equal("MainWindow.xaml", error.FileName);
        Assert.Equal(10, error.Location!.Line);
    }

    [Fact]
    public async Task A_missing_event_handler_is_reported_on_the_xaml_line_that_names_it()
    {
        var file = VisualStudioTemplates.WriteWpf(_temp, "Handlers");
        VisualStudioTemplates.WriteWpfWindow(_temp, "Handlers", """<Button Content="Hello" Click="OnClick" />""");

        var (_, result) = await fixture.BuildAsync(file);

        Assert.False(result.Success);
        var error = Assert.Single(result.Build.Diagnostics, d => d.Severity == DiagnosticSeverityLevel.Error);
        Assert.Equal("CS1061", error.Id);
        Assert.Contains("OnClick", error.Message, StringComparison.Ordinal);
        Assert.Equal("MainWindow.xaml", error.FileName);
        Assert.Equal(10, error.Location!.Line);
    }

    private static string Describe(IEnumerable<BuildDiagnostic> diagnostics) =>
        string.Join('\n', diagnostics.Select(d => $"{d.Severity} {d.Id} {d.FileName}({d.Location?.Line}): {d.Message}"));

    private static List<string> ResourceNames(byte[] resources)
    {
        using var reader = new ResourceReader(new MemoryStream(resources));
        var names = new List<string>();
        var entries = reader.GetEnumerator();
        while (entries.MoveNext())
        {
            names.Add((string)entries.Key);
        }

        return names;
    }

    /// <summary>What an assembly references, defines and embeds, read from its metadata without loading it.</summary>
    private static BuiltAssembly Inspect(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        var references = metadata.AssemblyReferences.Select(h => metadata.GetString(metadata.GetAssemblyReference(h).Name)).ToHashSet(StringComparer.Ordinal);
        var methods = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var handle in metadata.TypeDefinitions)
        {
            var type = metadata.GetTypeDefinition(handle);
            var name = $"{metadata.GetString(type.Namespace)}.{metadata.GetString(type.Name)}";
            methods[name] = [.. type.GetMethods().Select(m => metadata.GetString(metadata.GetMethodDefinition(m).Name))];
        }

        var resources = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var directory = pe.PEHeaders.CorHeader!.ResourcesDirectory.RelativeVirtualAddress;
        foreach (var handle in metadata.ManifestResources)
        {
            var resource = metadata.GetManifestResource(handle);
            if (!resource.Implementation.IsNil)
            {
                continue;
            }

            var reader = pe.GetSectionData(directory).GetReader();
            reader.Offset = (int)resource.Offset;
            var length = reader.ReadInt32();
            resources[metadata.GetString(resource.Name)] = reader.ReadBytes(length);
        }

        return new BuiltAssembly(references, methods.Keys.ToHashSet(StringComparer.Ordinal), methods, resources);
    }

    private sealed record BuiltAssembly(HashSet<string> References, HashSet<string> Types, Dictionary<string, List<string>> Methods, Dictionary<string, byte[]> Resources);
}
