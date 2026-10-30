using System.Diagnostics;
using System.Globalization;
using System.Text;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Compilation.Tests;

/// <summary>
/// Measures, on a solution of an app and a 300-class library seen as a skeleton (as on a 3 GB tablet), how long the
/// calling thread waits when a Rename edits the library and when Android reports memory pressure, and how long a
/// keystroke in the app waits meanwhile. Before 0.20.2 the caller, the UI thread, did the reload and the skeleton
/// compile itself, which is the time the reload takes now in the background. Writes the measurements to
/// <c>ci-out/skeleton-reloads.md</c> in CI.
/// </summary>
public sealed class SkeletonReloadMeasurementTests : IDisposable
{
    private const int Classes = 300;

    private readonly TempDirectory _temp = new();
    private readonly RoslynWorkspaceHost _host = new();

    public SkeletonReloadMeasurementTests()
    {
        _temp.Write("App/App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>
              <ItemGroup><ProjectReference Include="..\Lib\Lib.csproj" /></ItemGroup>
            </Project>
            """);
        _temp.Write("App/Program.cs", "Console.WriteLine(Lib.Model0.Describe(1));\n");
        _temp.Write("Lib/Lib.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup></Project>");
        for (var i = 0; i < Classes; i++)
        {
            _temp.Write($"Lib/Model{i}.cs", Model(i, "Describe"));
        }

        _host.Load(WorkspaceLoader.Load(_temp.Path), FileSystemTextProvider.Instance);
        _host.SetActiveProjects([_temp.Resolve("App/App.csproj")]);
        _host.UseSkeletonReferences(_temp.Resolve("skeletons"));
        _host.WhenReloadedAsync().WaitAsync(TimeSpan.FromMinutes(3)).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task A_cross_project_edit_and_memory_pressure_return_at_once_and_keystrokes_do_not_wait()
    {
        Assert.True(_host.IsSkeleton(_temp.Resolve("Lib/Lib.csproj")));
        var program = _temp.Resolve("App/Program.cs");
        await _host.GetDocumentDiagnosticsAsync(program);

        var rename = await MeasureAsync(() => _host.UpdateText(_temp.Resolve("Lib/Model0.cs"), Model(0, "Explain")), program);
        var trim = await MeasureAsync(_host.TrimMemory, program);

        var culture = CultureInfo.InvariantCulture;
        var report = new StringBuilder()
            .AppendLine("# Skeleton reloads on the UI thread").AppendLine()
            .AppendLine(culture, $"An app and a library of {Classes} classes referenced as a skeleton image, as on a 3 GB tablet.").AppendLine()
            .AppendLine("| | Caller waited, before (the reload and compile it did) | Caller waited, after | Longest keystroke wait, after |")
            .AppendLine("| --- | --- | --- | --- |")
            .AppendLine(culture, $"| Rename edits the library | {rename.Reload.TotalMilliseconds:0} ms | {rename.Call.TotalMilliseconds:0.0} ms | {rename.Keystroke.TotalMilliseconds:0.0} ms |")
            .AppendLine(culture, $"| Memory pressure | {trim.Reload.TotalMilliseconds:0} ms | {trim.Call.TotalMilliseconds:0.0} ms | {trim.Keystroke.TotalMilliseconds:0.0} ms |")
            .ToString();
        if (Environment.GetEnvironmentVariable("GITHUB_WORKSPACE") is { Length: > 0 } workspace)
        {
            Directory.CreateDirectory(Path.Combine(workspace, "ci-out"));
            await File.WriteAllTextAsync(Path.Combine(workspace, "ci-out", "skeleton-reloads.md"), report);
        }

        // The callers return long before the work they used to do: the reload is a background thread's.
        Assert.True(rename.Call < rename.Reload / 4, report);
        Assert.True(trim.Call < trim.Reload / 2 || trim.Call < TimeSpan.FromMilliseconds(5), report);
        Assert.True(_host.IsSkeleton(_temp.Resolve("Lib/Lib.csproj")));
    }

    /// <summary>
    /// Runs a call that starts a reload, then types into the open project every few milliseconds until the reload is
    /// done: how long the call took, how long the reload took, and the longest a keystroke waited.
    /// </summary>
    private async Task<(TimeSpan Call, TimeSpan Reload, TimeSpan Keystroke)> MeasureAsync(Action call, string typedFile)
    {
        var clock = Stopwatch.StartNew();
        await Task.Run(call);
        var returned = clock.Elapsed;
        var reloaded = _host.WhenReloadedAsync();
        var longest = TimeSpan.Zero;
        var typed = 0;
        while (!reloaded.IsCompleted)
        {
            var keystroke = Stopwatch.StartNew();
            await Task.Run(() => _host.UpdateText(typedFile, $"Console.WriteLine(Lib.Model1.Describe({typed++}));\n"));
            longest = keystroke.Elapsed > longest ? keystroke.Elapsed : longest;
            await Task.Delay(5);
        }

        await reloaded;
        return (returned, clock.Elapsed, longest);
    }

    private static string Model(int index, string method) => $$"""
        namespace Lib;

        public sealed record Model{{index}}(int Id, string Name, decimal Price)
        {
            public static string {{method}}(int id) => $"Model{{index}} {id}";

            public Model{{index}} WithDiscount(decimal percent) => this with { Price = Math.Round(Price * (1 - (percent / 100)), 2) };

            public IEnumerable<string> Tags() => Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(t => t.ToUpperInvariant());
        }
        """;

    public void Dispose()
    {
        _host.Dispose();
        _temp.Dispose();
    }
}
