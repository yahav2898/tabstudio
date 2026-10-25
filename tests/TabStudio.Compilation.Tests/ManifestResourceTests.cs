using TabStudio.Compilation.Building;
using TabStudio.Compilation.Running;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Compilation.Tests;

/// <summary>
/// Files compiled into a project's assembly: <c>EmbeddedResource</c> items under the SDK's names, and an Avalonia
/// app's <c>AvaloniaResource</c> items in Avalonia's resource format.
/// </summary>
public sealed class ManifestResourceTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly RoslynWorkspaceHost _host = new();

    [Fact]
    public async Task Embedded_and_Avalonia_resources_are_compiled_into_the_program()
    {
        var projectPath = _temp.Write("App/App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>
              <ItemGroup>
                <EmbeddedResource Include="Data\**" />
                <AvaloniaResource Include="Assets\**" />
                <AvaloniaResource Remove="Assets\draft.txt" />
              </ItemGroup>
            </Project>
            """);
        _temp.Write("App/Data/notes.txt", "hello");
        _temp.Write("App/Assets/logo.txt", "logo");
        _temp.Write("App/Assets/fonts/font.txt", "font");
        _temp.Write("App/Assets/draft.txt", "draft");
        _temp.Write("App/Program.cs", """
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            using (var notes = new StreamReader(assembly.GetManifestResourceStream("App.Data.notes.txt")!))
            {
                Console.WriteLine(notes.ReadToEnd());
            }

            // Avalonia's format: the index's length, the index, then the files.
            using var blob = assembly.GetManifestResourceStream("!AvaloniaResources")!;
            var reader = new BinaryReader(blob);
            var indexLength = reader.ReadInt32();
            Console.WriteLine($"version {reader.ReadInt32()}");
            var entries = new List<(string Path, int Offset, int Size)>();
            for (var count = reader.ReadInt32(); count > 0; count--)
            {
                entries.Add((reader.ReadString(), reader.ReadInt32(), reader.ReadInt32()));
            }

            foreach (var (path, offset, size) in entries)
            {
                blob.Position = 4 + indexLength + offset;
                Console.WriteLine($"{path} = {System.Text.Encoding.UTF8.GetString(reader.ReadBytes(size))}");
            }
            """);
        _host.Load(WorkspaceLoader.Load(projectPath), FileSystemTextProvider.Instance);

        var build = await new ProjectBuilder(_host).BuildAsync(_host.FindProject(projectPath)!);
        Assert.True(build.Success, string.Join('\n', build.Diagnostics));
        var console = new CapturingConsole();
        var run = await new ProgramRunner().RunAsync(build.EntryAssembly!, build.Dependencies, [], [], console);

        Assert.True(run.Outcome == RunOutcome.Completed, console.Error);
        Assert.Equal("hello\nversion 2\n/Assets/fonts/font.txt = font\n/Assets/logo.txt = logo\n", console.Output);
    }

    [Fact]
    public void Embedded_resources_are_named_after_the_root_namespace_and_their_folders()
    {
        var project = new ProjectInfo
        {
            Name = "App",
            FilePath = Path.Combine(_temp.Path, "App", "App.csproj"),
            Properties = new Dictionary<string, string> { ["RootNamespace"] = "Company.App" },
        };

        var name = ManifestResources.EmbeddedResourceName(project, Path.Combine(_temp.Path, "App", "My Data", "1st", "read me.txt"));

        Assert.Equal("Company.App.My_Data._1st.read me.txt", name);
    }

    public void Dispose()
    {
        _host.Dispose();
        _temp.Dispose();
    }
}
