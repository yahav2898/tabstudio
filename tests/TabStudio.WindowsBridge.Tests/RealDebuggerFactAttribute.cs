using TabStudio.WindowsBridge.Hosting;

namespace TabStudio.WindowsBridge.Tests;

/// <summary>
/// A test that downloads and runs netcoredbg. It runs where <c>TABSTUDIO_REAL_SERVERS=1</c>, as in CI, which has the
/// network; elsewhere it is skipped, since netcoredbg comes from GitHub.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RealDebuggerFactAttribute : FactAttribute
{
    /// <inheritdoc />
    public override string? Skip
    {
        get => base.Skip ?? (Environment.GetEnvironmentVariable("TABSTUDIO_REAL_SERVERS") == "1" && NetcoredbgInstaller.Download is not null
            ? null
            : "Downloads netcoredbg from GitHub; set TABSTUDIO_REAL_SERVERS=1 to run it.");
        set => base.Skip = value;
    }
}
