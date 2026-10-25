using TabStudio.Core.Compatibility;
using TabStudio.Core.Detection;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Core.Tests;

/// <summary>Python and Node.js projects, which run with toolchains from the Toolchain Store.</summary>
public sealed class ToolchainProjectTests
{
    [Theory]
    [InlineData(ProjectKind.Python, "Python toolchain")]
    [InlineData(ProjectKind.Node, "Node.js toolchain")]
    public void Toolchain_projects_need_their_toolchain(ProjectKind kind, string runsVia)
    {
        var report = CompatibilityDoctor.Diagnose(new ProjectInfo { Name = "app", FilePath = Path.Combine(Path.GetTempPath(), "app", "main"), Kind = kind });

        Assert.Equal(RunHost.Toolchain, report.Host);
        Assert.Equal(CompatibilityStatus.NeedsToolchain, report.Status);
        Assert.Equal(runsVia, report.RunsVia);
        Assert.False(report.CanRunToday);
        Assert.Contains("Toolchain Store", report.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void A_folder_of_Python_files_is_detected_as_Python()
    {
        using var temp = new TempDirectory();
        temp.Write("scripts/report.py", "print('report')\n");

        var stack = StackDetector.Detect(temp.Path);

        Assert.Equal("scripts" + Path.DirectorySeparatorChar + "report.py", stack.Get(StackKind.Python)?.Evidence);
    }
}
