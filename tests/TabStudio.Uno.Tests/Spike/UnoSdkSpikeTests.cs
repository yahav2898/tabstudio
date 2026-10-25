using System.Diagnostics;
using System.Globalization;
using System.Text;
using TabStudio.Compilation;
using TabStudio.Core.Workspaces;
using TabStudio.Packages;
using TabStudio.Testing;

namespace TabStudio.Uno.Tests.Spike;

/// <summary>
/// Collects facts on CI, where the .NET SDK and the internet are available: what Uno.Sdk does for an app, and how
/// TabStudio's own restore handles Uno's packages. The results go to the CI logs.
/// </summary>
public sealed class UnoSdkSpikeTests
{
    private static string? Output => Environment.GetEnvironmentVariable("GITHUB_WORKSPACE") is { Length: > 0 } workspace
        ? Path.Combine(workspace, "ci-out", "uno-spike")
        : null;

    [Fact]
    public async Task CollectUnoSdkFacts()
    {
        if (Output is not { } output)
        {
            return;
        }

        var script = Path.Combine(AppContext.BaseDirectory, "Spike", "uno-sdk-facts.sh");
        using var process = Process.Start(new ProcessStartInfo("bash", [script, output]) { RedirectStandardOutput = true, RedirectStandardError = true })!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(20));
        await File.WriteAllTextAsync(Path.Combine(output, "script-output.txt"), await stdout + await stderr);
    }

    [Fact]
    public async Task MeasureTabStudioRestoreOfUno()
    {
        if (Output is not { } output)
        {
            return;
        }

        Directory.CreateDirectory(output);
        using var temp = new TempDirectory();
        var projectPath = temp.Write("UnoProbe/UnoProbe.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <OutputType>Exe</OutputType>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Uno.WinUI" Version="6.7.135" />
                <PackageReference Include="Uno.Fonts.OpenSans" Version="2.9.4" />
                <PackageReference Include="SkiaSharp" Version="3.119.0" />
              </ItemGroup>
            </Project>
            """);
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        var restorer = new PackageRestorer(
            new PackageRestoreOptions { GlobalPackagesFolder = temp.Resolve("packages"), FrameworkAssemblyNames = ReferenceAssemblyNames.Net100 },
            http);

        var clock = Stopwatch.StartNew();
        var result = await restorer.RestoreAsync(ProjectParser.Load(projectPath));
        var report = new StringBuilder();
        report.AppendLine(CultureInfo.InvariantCulture, $"Restore took {clock.Elapsed.TotalSeconds:0.0} s, success {result.Success}");
        foreach (var diagnostic in result.Diagnostics)
        {
            report.AppendLine(CultureInfo.InvariantCulture, $"diagnostic {diagnostic.Id} {diagnostic.Severity}: {diagnostic.Message}");
        }

        foreach (var package in result.Packages)
        {
            var size = Directory.EnumerateFiles(package.DirectoryPath, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);
            report.AppendLine(CultureInfo.InvariantCulture, $"package {package.Id} {package.Version} direct={package.IsDirect} size={size / 1024 / 1024} MB");
        }

        foreach (var (kind, paths) in new[] { ("compile", result.CompileAssemblies), ("runtime", result.RuntimeAssemblies), ("analyzer", result.Analyzers) })
        {
            foreach (var path in paths)
            {
                report.AppendLine(CultureInfo.InvariantCulture, $"{kind} {Path.GetRelativePath(temp.Resolve("packages"), path)}");
            }
        }

        await File.WriteAllTextAsync(Path.Combine(output, "tabstudio-restore.txt"), report.ToString());
    }
}
