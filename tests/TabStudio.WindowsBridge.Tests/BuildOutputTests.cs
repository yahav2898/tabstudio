using TabStudio.Core.Diagnostics;
using TabStudio.Link.Language;
using TabStudio.Testing;
using TabStudio.WindowsBridge.Builds;

namespace TabStudio.WindowsBridge.Tests;

/// <summary>MSBuild's errors and warnings are read from its output and land on the tablet's files with their codes.</summary>
public sealed class BuildOutputTests : IDisposable
{
    private const string TabletRoot = "/storage/emulated/0/TabStudio/Projects/Hello";

    private readonly TempDirectory _mirror = new();

    [Theory]
    [InlineData(@"C:\m\App\MainWindow.xaml.cs(12,17): error CS1002: ; expected [C:\m\App\App.csproj]", @"C:\m\App\MainWindow.xaml.cs", 12, 17, "CS1002", "; expected")]
    [InlineData(@"C:\m\Cpp\main.cpp(7,5): error C2065: 'count': undeclared identifier [C:\m\Cpp\Cpp.vcxproj]", @"C:\m\Cpp\main.cpp", 7, 5, "C2065", "'count': undeclared identifier")]
    [InlineData(@"C:\m\App\MainWindow.xaml(10,9): error MC3072: The property 'Fill' does not exist in XML namespace.", @"C:\m\App\MainWindow.xaml", 10, 9, "MC3072", "The property 'Fill' does not exist in XML namespace.")]
    [InlineData(@"LINK : fatal error LNK1104: cannot open file 'kernel32.lib' [C:\m\Cpp\Cpp.vcxproj]", "LINK", 0, 0, "LNK1104", "cannot open file 'kernel32.lib'")]
    [InlineData(@"MSBUILD : error MSB1009: Project file does not exist.", "MSBUILD", 0, 0, "MSB1009", "Project file does not exist.")]
    [InlineData(@"/home/m/App/Program.cs(3,1): error CS0103: The name 'x' does not exist in the current context [/home/m/App/App.csproj]", "/home/m/App/Program.cs", 3, 1, "CS0103", "The name 'x' does not exist in the current context")]
    public void Errors_are_read_with_their_file_position_code_and_text(string line, string origin, int lineNumber, int column, string code, string text)
    {
        var message = MsBuildMessage.Parse(line)!;

        Assert.Equal(origin, message.Origin);
        Assert.Equal((lineNumber, column), (message.Line, message.Column));
        Assert.Equal(DiagnosticSeverityLevel.Error, message.Severity);
        Assert.Equal(code, message.Code);
        Assert.Equal(text, message.Text);
    }

    [Fact]
    public void Warnings_are_warnings_and_name_their_project()
    {
        var message = MsBuildMessage.Parse(@"C:\m\App\App.csproj : warning NU1701: Package 'Old' was restored using '.NETFramework,Version=v4.6.1'. [C:\m\App\App.csproj]")!;

        Assert.Equal(DiagnosticSeverityLevel.Warning, message.Severity);
        Assert.Equal("NU1701", message.Code);
        Assert.Equal(@"C:\m\App\App.csproj", message.Project);
        Assert.True(message.HasFile);
    }

    [Theory]
    [InlineData("  HelloWpf -> C:\\m\\App\\bin\\Debug\\net10.0-windows\\HelloWpf.dll")]
    [InlineData("Build succeeded.")]
    [InlineData("    0 Warning(s)")]
    [InlineData("    1 Error(s)")]
    [InlineData("Restore complete (0.4s)")]
    public void Other_lines_are_not_errors(string line)
    {
        Assert.Null(MsBuildMessage.Parse(line));
    }

    [Fact]
    public void A_diagnostic_lands_on_the_tablets_file_at_its_position()
    {
        var file = _mirror.Write("App/Program.cs", "using System;\n\nConsole.WriteLine(x);\n");
        var paths = Mapper();

        var diagnostic = paths.ToDiagnostic(MsBuildMessage.Parse($"{file}(3,19): error CS0103: The name 'x' does not exist in the current context [{_mirror.Resolve("App/App.csproj")}]")!, _mirror.Resolve("App"), "App");

        Assert.Equal("CS0103", diagnostic.Id);
        Assert.Equal($"{TabletRoot}/App/Program.cs", diagnostic.Location!.FilePath);
        Assert.Equal((3, 19), (diagnostic.Location.Line, diagnostic.Location.Column));
        Assert.Equal("using System;\n\nConsole.WriteLine(".Length, diagnostic.Location.Start);
        Assert.Equal("App", diagnostic.ProjectName);
    }

    [Fact]
    public void Relative_paths_are_the_projects_and_errors_without_a_file_keep_their_tool()
    {
        _mirror.Write("App/Views/Main.xaml", "<Window />");
        var paths = Mapper();

        var relative = paths.ToDiagnostic(MsBuildMessage.Parse("Views/Main.xaml(1,2): error MC3000: Bad XAML.")!, _mirror.Resolve("App"), "App");
        var linker = paths.ToDiagnostic(MsBuildMessage.Parse("LINK : fatal error LNK1104: cannot open file 'x.lib'")!, _mirror.Resolve("App"), "App");

        Assert.Equal($"{TabletRoot}/App/Views/Main.xaml", relative.Location!.FilePath);
        Assert.Null(linker.Location);
        Assert.Equal("LNK1104", linker.Id);
    }

    [Fact]
    public void Paths_in_output_lines_become_the_tablets()
    {
        var paths = Mapper();
        var output = Path.Combine(_mirror.Path, "App", "bin", "Debug", "App.dll");

        var arrow = paths.ToClientText($"  App -> {output}");
        var quoted = paths.ToClientText($"Could not copy \"{output}\" to \"{output}.bak\": in use.");
        var outside = paths.ToClientText("Restored /usr/share/dotnet/sdk (in 1 s).");

        Assert.Equal($"  App -> {TabletRoot}/App/bin/Debug/App.dll", arrow);
        Assert.Equal($"Could not copy \"{TabletRoot}/App/bin/Debug/App.dll\" to \"{TabletRoot}/App/bin/Debug/App.dll.bak\": in use.", quoted);
        Assert.Equal("Restored /usr/share/dotnet/sdk (in 1 s).", outside);
    }

    [Fact]
    public void Paths_with_parentheses_and_apostrophes_in_folder_names_are_mapped_whole()
    {
        var paths = Mapper();
        var library = Path.Combine(_mirror.Path, "My App (x86)", "Yahav's", "lib.dll");
        var source = Path.Combine(_mirror.Path, "App", "Program.cs");

        Assert.Equal($"Could not resolve {TabletRoot}/My App (x86)/Yahav's/lib.dll.", paths.ToClientText($"Could not resolve {library}."));
        Assert.Equal($"{TabletRoot}/App/Program.cs(3,5): error CS1002", paths.ToClientText($"{source}(3,5): error CS1002"));
        Assert.Equal($"(see '{TabletRoot}/App/Program.cs')", paths.ToClientText($"(see '{source}')"));
    }

    [Fact]
    public void Paths_go_both_ways()
    {
        var paths = Mapper();
        var host = paths.ToHost($"{TabletRoot}/App/MainWindow.xaml.cs")!;

        Assert.Equal(Path.Combine(_mirror.Path, "App", "MainWindow.xaml.cs"), host);
        Assert.Equal($"{TabletRoot}/App/MainWindow.xaml.cs", paths.ToClient(host));
        Assert.Null(paths.ToHost("/storage/emulated/0/Other/File.cs"));
        Assert.Null(paths.ToHost($"{TabletRoot}/../Escape.cs"));
        Assert.Equal($"break at {host}", paths.ToHostText($"break at {TabletRoot}/App/MainWindow.xaml.cs"));
    }

    public void Dispose() => _mirror.Dispose();

    private BridgePathMapper Mapper() => new(new LinkPathMap(TabletRoot, '/', _mirror.Path), hostIgnoresCase: OperatingSystem.IsWindows());
}
