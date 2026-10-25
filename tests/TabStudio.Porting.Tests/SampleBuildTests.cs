using System.Diagnostics;
using System.Text;
using TabStudio.Testing;

namespace TabStudio.Porting.Tests;

/// <summary>
/// The proof: every sample, ported by the converter alone, builds with zero errors with the real .NET SDK and
/// Avalonia's own XAML compiler, which checks every element, property, binding path syntax and style selector.
/// The build log goes to ci-out/porting-build. Runs where the .NET SDK is, as on CI.
/// </summary>
public sealed class SampleBuildTests
{
    private static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(8);

    [Theory]
    [MemberData(nameof(SampleProjects.All), MemberType = typeof(SampleProjects))]
    public async Task The_ported_sample_builds_with_zero_errors(string sample)
    {
        if (Dotnet() is not { } dotnet)
        {
            return;
        }

        using var folder = new TempDirectory();
        var source = SampleProjects.Load(sample);
        var name = source.Name + ".Avalonia";
        var result = AvaloniaPorter.Port(source, new PortOptions(name, Path.Combine(folder.Path, name), SampleProjects.AvaloniaVersion));
        result.Write();

        var (exitCode, output) = await RunAsync(dotnet, $"build \"{result.ProjectFilePath}\" -c Debug -nologo -clp:NoSummary -m:1 -nr:false -p:UseSharedCompilation=false -m:1 -p:UseSharedCompilation=false", folder.Path);
        var log = SampleProjects.CiOut("porting-build", sample + ".log");
        Directory.CreateDirectory(Path.GetDirectoryName(log)!);
        await File.WriteAllTextAsync(log, output);

        var errors = output.Split('\n').Where(l => l.Contains(": error ", StringComparison.Ordinal)).Distinct().ToList();
        Assert.True(exitCode == 0 && errors.Count == 0, $"dotnet build of the ported {sample} failed ({exitCode}):\n{string.Join('\n', errors.Take(40))}\n{(errors.Count == 0 ? Tail(output) : string.Empty)}");
    }

    /// <summary>The .NET SDK's dotnet, or <see langword="null"/> where there is none.</summary>
    private static string? Dotnet()
    {
        var host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (host is not null && File.Exists(host))
        {
            return host;
        }

        var paths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator);
        return paths.Select(p => Path.Combine(p, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet")).FirstOrDefault(File.Exists);
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(string program, string arguments, string directory)
    {
        var start = new ProcessStartInfo(program, arguments)
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        // A build of its own: not part of the test host's build, which sets MSBuild variables of its own.
        foreach (var variable in (string[])["MSBuildExtensionsPath", "MSBuildSDKsPath", "MSBUILD_EXE_PATH", "MSBuildLoadMicrosoftTargetsReadOnly"])
        {
            start.Environment.Remove(variable);
        }

        using var process = Process.Start(start)!;
        var output = new StringBuilder();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(BuildTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            return (-1, "The build did not finish in time.\n" + await stdout);
        }

        output.Append(await stdout).Append(await stderr);
        return (process.ExitCode, output.ToString());
    }

    private static string Tail(string output) => string.Join('\n', output.Split('\n').TakeLast(30));
}
