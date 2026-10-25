using System.Collections.Concurrent;
using System.Diagnostics;
using TabStudio.LanguageServers.Hosting;
using Xunit.Sdk;

namespace TabStudio.App.Tests;

/// <summary>
/// A UI test, on Avalonia's dispatcher, that needs a real language server or debug adapter. It is skipped where the
/// programs are not installed, but never when <c>TABSTUDIO_REAL_SERVERS=1</c>, as the CI job that installs them sets.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
[XunitTestCaseDiscoverer("Avalonia.Headless.XUnit.AvaloniaUIFactDiscoverer", "Avalonia.Headless.XUnit")]
public sealed class RealServerAvaloniaFactAttribute : FactAttribute
{
    private static readonly ConcurrentDictionary<string, bool> Modules = new(StringComparer.Ordinal);

    /// <summary>Creates the attribute.</summary>
    /// <param name="programs">The programs the test runs, which must be on the PATH.</param>
    public RealServerAvaloniaFactAttribute(params string[] programs)
    {
        Programs = programs;
    }

    /// <summary>The programs the test runs.</summary>
    public IReadOnlyList<string> Programs { get; }

    /// <summary>A Python module the test needs, such as <c>debugpy</c>, if any.</summary>
    public string? PythonModule { get; set; }

    /// <inheritdoc/>
    public override string? Skip
    {
        get => base.Skip ?? FindMissing();
        set => base.Skip = value;
    }

    private string? FindMissing()
    {
        if (Environment.GetEnvironmentVariable("TABSTUDIO_REAL_SERVERS") == "1")
        {
            return null;
        }

        var missing = Programs.Where(p => ProcessServerLauncher.Instance.Resolve(p) is null).ToList();
        if (missing.Count == 0 && PythonModule is { } module && !Modules.GetOrAdd(module, HasPythonModule))
        {
            missing.Add($"the Python module {module}");
        }

        return missing.Count == 0 ? null : $"Needs {string.Join(", ", missing)}; set TABSTUDIO_REAL_SERVERS=1 to require it.";
    }

    private static bool HasPythonModule(string module)
    {
        if (ProcessServerLauncher.Instance.Resolve("python3") is not { } python)
        {
            return false;
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo(python, ["-c", "import " + module]) { RedirectStandardError = true, RedirectStandardOutput = true });
            if (process is null || !process.WaitForExit(TimeSpan.FromSeconds(20)))
            {
                process?.Kill();
                return false;
            }

            return process.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
