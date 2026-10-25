using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TabStudio.Compilation;
using TabStudio.Compilation.Wpf;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;
using TabStudio.Wpf.Compiler;

namespace TabStudio.Wpf.Tests;

/// <summary>
/// A record of how OpenSilver's packages and XAML compiler behave inside TabStudio, written to the CI logs
/// (<c>ci-out/wpf-experiment.md</c>) so the choices of TabStudio.Wpf rest on what the real packages do.
/// </summary>
[Collection(NuGetTestGroup.Name)]
public sealed class OpenSilverExperiment(NuGetFixture nuget) : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly StringBuilder _report = new();

    [Fact]
    public async Task Records_how_OpenSilver_builds_a_WPF_template()
    {
        try
        {
            await RunAsync();
        }
#pragma warning disable CA1031 // The experiment records whatever goes wrong, so the report shows how far it got.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Section("Unexpected exception", ex.ToString());
        }
        finally
        {
            var directory = Path.Combine(Environment.GetEnvironmentVariable("GITHUB_WORKSPACE") ?? AppContext.BaseDirectory, "ci-out");
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, "wpf-experiment.md"), _report.ToString());
        }
    }

    public void Dispose() => _temp.Dispose();

    private async Task RunAsync()
    {
        var projectPath = WriteTemplate();
        var project = ProjectParser.Load(projectPath);
        Section("Project", $"Kind: {project.Kind}; UseWPF: {WpfProject.UsesWpf(project)}; items: {string.Join(", ", WpfProject.XamlItems(project))}");

        var clock = Stopwatch.StartNew();
        var restore = await nuget.Restorer.RestoreAsync(OpenSilverPackages.WithOpenSilver(project));
        Section($"Restore with OpenSilver ({clock.ElapsedMilliseconds} ms, success {restore.Success})", string.Join('\n', restore.Diagnostics.Select(d => d.ToString())));
        Section("Packages", string.Join('\n', restore.Packages.Select(p => $"{p.Id} {p.Version} {p.DirectoryPath}")));
        Section("Compile assemblies", string.Join('\n', restore.CompileAssemblies));
        Section("Runtime assemblies", string.Join('\n', restore.RuntimeAssemblies));
        Section("Analyzers", string.Join('\n', restore.Analyzers));

        var openSilver = restore.Packages.FirstOrDefault(p => string.Equals(p.Id, OpenSilverPackages.OpenSilver, StringComparison.OrdinalIgnoreCase));
        if (openSilver is null)
        {
            Section("Stop", "OpenSilver was not restored.");
            return;
        }

        Section("OpenSilver package files", ListFiles(openSilver.DirectoryPath));
        var references = Directory.EnumerateFiles(nuget.FrameworkReferenceDirectory, "*.dll").Concat(restore.CompileAssemblies).ToList();

        clock.Restart();
        var compiler = OpenSilverXamlCompiler.Load(openSilver.DirectoryPath);
        Section($"Compiler loaded ({clock.ElapsedMilliseconds} ms)", compiler.AssemblyPath);

        var items = WpfProject.XamlItems(project);
        var obj = Path.Combine(project.DirectoryPath, "obj", "experiment");
        clock.Restart();
        var pass1 = compiler.Compile(new XamlCompilerRequest
        {
            ProjectFilePath = project.FilePath,
            AssemblyName = project.Name,
            Files = [.. items.Select(i => new XamlCompilerInput(i))],
            References = references,
            IntermediateDirectory = Path.Combine(obj, "pass1"),
        }, project.Name);
        Section($"Pass 1 ({clock.ElapsedMilliseconds} ms, succeeded {pass1.Succeeded})", string.Join('\n', pass1.Diagnostics));
        foreach (var (source, generated) in pass1.GeneratedFiles)
        {
            Section($"Pass 1 of {Path.GetFileName(source)}", await File.ReadAllTextAsync(generated));
        }

        var codeBehind = Directory.EnumerateFiles(project.DirectoryPath, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();
        var metadata = ReferenceAssemblies.Net100.Concat(restore.CompileAssemblies.Select(p => MetadataReference.CreateFromFile(p))).ToList();
        var firstCompile = Compile(project.Name, [.. codeBehind, .. pass1.GeneratedFiles.Values], metadata);
        var firstEmit = Path.Combine(obj, "bin", $"{project.Name}.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(firstEmit)!);
        var emitted = firstCompile.Emit(firstEmit);
        Section($"Compile of pass 1 (success {emitted.Success})", string.Join('\n', emitted.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning)));

        foreach (var stripStartupUri in new[] { false, true })
        {
            var inputs = items.Select(i => i.Kind == XamlItemKind.ApplicationDefinition && stripStartupUri
                ? new XamlCompilerInput(i, WithoutStartupUri(i.FilePath, obj))
                : new XamlCompilerInput(i)).ToList();
            clock.Restart();
            var pass2 = compiler.Compile(new XamlCompilerRequest
            {
                ProjectFilePath = project.FilePath,
                AssemblyName = project.Name,
                Files = inputs,
                References = references,
                IntermediateDirectory = Path.Combine(obj, stripStartupUri ? "pass2-stripped" : "pass2"),
                IsSecondPass = true,
                FirstPassAssemblyPath = firstEmit,
            }, project.Name);
            Section($"Pass 2, StartupUri stripped: {stripStartupUri} ({clock.ElapsedMilliseconds} ms, succeeded {pass2.Succeeded})", string.Join('\n', pass2.Diagnostics));
            foreach (var (source, generated) in pass2.GeneratedFiles)
            {
                Section($"Pass 2 of {Path.GetFileName(source)}", await File.ReadAllTextAsync(generated));
            }

            var second = Compile(project.Name, [.. codeBehind, .. pass2.GeneratedFiles.Values], metadata);
            using var image = new MemoryStream();
            var result = second.Emit(image);
            Section($"Compile of pass 2 (success {result.Success})", string.Join('\n', result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning)));
        }

        clock.Restart();
        var host = await nuget.Restorer.RestoreAsync(OpenSilverPackages.BrowserHost(project));
        Section($"Restore of the browser host ({clock.ElapsedMilliseconds} ms, success {host.Success})", string.Join('\n', host.Diagnostics.Select(d => d.ToString())));
        Section("Browser host packages", string.Join('\n', host.Packages.Select(p => $"{p.Id} {p.Version}")));
        Section("Browser host compile assemblies", string.Join('\n', host.CompileAssemblies.Select(Path.GetFileName)));
        Section("Browser host runtime assemblies", string.Join('\n', host.RuntimeAssemblies));
        if (host.Packages.FirstOrDefault(p => string.Equals(p.Id, OpenSilverPackages.WebAssembly, StringComparison.OrdinalIgnoreCase)) is { } webAssembly)
        {
            Section("OpenSilver.WebAssembly package files", ListFiles(webAssembly.DirectoryPath));
        }

        if (host.Packages.FirstOrDefault(p => string.Equals(p.Id, OpenSilverPackages.BlazorWebAssembly, StringComparison.OrdinalIgnoreCase)) is { } blazor)
        {
            Section("Blazor WebAssembly package files", ListFiles(blazor.DirectoryPath));
        }
    }

    private static CSharpCompilation Compile(string name, IEnumerable<string> files, IEnumerable<MetadataReference> references)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["OPENSILVER"]);
        var trees = files.Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), parse, f)).ToList();
        trees.Add(CSharpSyntaxTree.ParseText(Shim, parse, "Shim.cs"));
        trees.Add(CSharpSyntaxTree.ParseText(
            "global using global::System;\nglobal using global::System.Collections.Generic;\nglobal using global::System.Linq;\nglobal using global::System.Threading;\nglobal using global::System.Threading.Tasks;\n",
            parse,
            "GlobalUsings.cs"));
        return CSharpCompilation.Create(name, trees, references, new CSharpCompilationOptions(OutputKind.ConsoleApplication, nullableContextOptions: NullableContextOptions.Enable));
    }

    private const string Shim = """
        namespace System.Windows
        {
            [global::System.AttributeUsage(global::System.AttributeTargets.Assembly)]
            internal sealed class ThemeInfoAttribute : global::System.Attribute
            {
                public ThemeInfoAttribute(ResourceDictionaryLocation themeDictionaryLocation, ResourceDictionaryLocation genericDictionaryLocation)
                {
                }
            }

            internal enum ResourceDictionaryLocation
            {
                None,
                SourceAssembly,
                ExternalAssembly,
            }
        }

        namespace WpfApp1
        {
            public partial class App
            {
                [global::System.STAThread]
                public static void Main()
                {
                    var app = new App();
                    app.InitializeComponent();
                }
            }
        }
        """;

    private static string WithoutStartupUri(string path, string obj)
    {
        var text = File.ReadAllText(path);
        var start = text.IndexOf("StartupUri=", StringComparison.Ordinal);
        if (start >= 0)
        {
            var end = text.IndexOf('"', text.IndexOf('"', start) + 1) + 1;
            text = text[..start] + new string(' ', end - start) + text[end..];
        }

        var copy = Path.Combine(obj, "sanitized", Path.GetFileName(path));
        Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
        File.WriteAllText(copy, text);
        return copy;
    }

    private static string ListFiles(string directory) =>
        string.Join('\n', Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Select(f => $"{Path.GetRelativePath(directory, f)} ({new FileInfo(f).Length} bytes)")
            .Order(StringComparer.Ordinal));

    private void Section(string title, string body)
    {
        _report.Append("## ").AppendLine(title).AppendLine("```").AppendLine(body.Length > 60_000 ? body[..60_000] + "\n[cut]" : body).AppendLine("```").AppendLine();
    }

    private string WriteTemplate()
    {
        var project = _temp.Write("WpfApp1/WpfApp1.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <OutputType>WinExe</OutputType>
                <TargetFramework>net10.0-windows</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <UseWPF>true</UseWPF>
              </PropertyGroup>

            </Project>
            """);
        _temp.Write("WpfApp1/App.xaml", """
            <Application x:Class="WpfApp1.App"
                         xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         xmlns:local="clr-namespace:WpfApp1"
                         StartupUri="MainWindow.xaml">
                <Application.Resources>

                </Application.Resources>
            </Application>
            """);
        _temp.Write("WpfApp1/App.xaml.cs", """
            using System.Configuration;
            using System.Data;
            using System.Windows;

            namespace WpfApp1
            {
                /// <summary>
                /// Interaction logic for App.xaml
                /// </summary>
                public partial class App : Application
                {
                }

            }
            """);
        _temp.Write("WpfApp1/AssemblyInfo.cs", """
            using System.Windows;

            [assembly: ThemeInfo(
                ResourceDictionaryLocation.None,            //where theme specific resource dictionaries are located
                                                            //(used if a resource is not found in the page,
                                                            // or application resource dictionaries)
                ResourceDictionaryLocation.SourceAssembly   //where the generic resource dictionary is located
                                                            //(used if a resource is not found in the page,
                                                            // app, or any theme specific resource dictionaries)
            )]
            """);
        _temp.Write("WpfApp1/MainWindow.xaml", """
            <Window x:Class="WpfApp1.MainWindow"
                    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
                    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
                    xmlns:local="clr-namespace:WpfApp1"
                    mc:Ignorable="d"
                    Title="MainWindow" Height="450" Width="800">
                <Grid>
                    <StackPanel Margin="16">
                        <TextBlock x:Name="Greeting" Text="Hello from WPF" FontSize="20" />
                        <Button x:Name="ClickMe" Content="Click me" Click="ClickMe_Click" HorizontalAlignment="Left" />
                        <DataGrid x:Name="People" AutoGenerateColumns="True" />
                    </StackPanel>
                </Grid>
            </Window>
            """);
        _temp.Write("WpfApp1/MainWindow.xaml.cs", """
            using System.Text;
            using System.Windows;
            using System.Windows.Controls;
            using System.Windows.Data;
            using System.Windows.Documents;
            using System.Windows.Input;
            using System.Windows.Media;
            using System.Windows.Media.Imaging;
            using System.Windows.Navigation;
            using System.Windows.Shapes;

            namespace WpfApp1
            {
                /// <summary>
                /// Interaction logic for MainWindow.xaml
                /// </summary>
                public partial class MainWindow : Window
                {
                    public MainWindow()
                    {
                        InitializeComponent();
                    }

                    private void ClickMe_Click(object sender, RoutedEventArgs e)
                    {
                        Greeting.Text = "Clicked";
                    }
                }
            }
            """);
        return project;
    }
}
