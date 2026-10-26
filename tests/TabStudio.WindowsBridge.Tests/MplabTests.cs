using TabStudio.Link.Language;
using TabStudio.Testing;
using TabStudio.WindowsBridge.Builds;
using TabStudio.WindowsBridge.Hosting;
using TabStudio.WindowsBridge.Protocol;

namespace TabStudio.WindowsBridge.Tests;

public sealed class MplabTests
{
    [Fact]
    public async Task Windows_batch_generator_keeps_a_project_path_with_spaces_as_one_argument()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var folder = new TempDirectory();
        var batch = folder.Write("Vendor Tools/generator.bat", "@echo off\r\necho PROJECT=[%~1]\r\n");
        var project = folder.Resolve("Project with spaces") + "@production";
        var lines = new List<string>();
        using var process = BridgeProcess.Start(new BridgeProcessStart("cmd.exe", [], folder.Path)
        {
            WindowsCommandLine = $"/d /s /c \"\"{batch}\" \"{project}\"\"",
        });
        process.OutputLine += (_, line) => lines.Add(line);
        process.BeginReading();
        Assert.Equal(0, await process.Exited.WaitAsync(TimeSpan.FromSeconds(15)));
        Assert.Contains("PROJECT=[" + project + "]", lines);
    }

    [Fact]
    public void Finds_the_newest_vendor_tools_including_bundled_java()
    {
        using var folder = new TempDirectory();
        foreach (var version in new[] { "v6.20", "v6.30" })
        {
            folder.Write($"Microchip/MPLABX/{version}/gnuBins/GnuWin32/bin/make.exe", "placeholder");
            folder.Write($"Microchip/MPLABX/{version}/mplab_platform/bin/prjMakefilesGenerator.bat", "placeholder");
            folder.Write($"Microchip/MPLABX/{version}/mplab_platform/mplab_ipe/ipecmd.jar", "placeholder");
            folder.Write($"Microchip/MPLABX/{version}/sysjava/jre/bin/java.exe", "placeholder");
        }
        var tools = MplabTools.Find([folder.Path])!;
        Assert.EndsWith("v6.30", tools.Root, StringComparison.Ordinal);
        Assert.EndsWith("ipecmd.jar", tools.Ipe, StringComparison.Ordinal);
        Assert.NotNull(tools.Java);
    }

    [Fact]
    public void Ipe_programs_and_verifies_the_explicit_tool_without_changing_target_power()
    {
        var tools = new MplabTools("vendor", "make.exe", "generator.bat", "ipe.jar", null, "java.exe");
        var arguments = tools.ProgrammingArguments("PIC18F45K22", "C:/project with spaces/blink.hex", "PK5", "ABC123");
        Assert.Equal(["-jar", "ipe.jar", "-P18F45K22", "-TPPK5", "-FC:/project with spaces/blink.hex", "-M", "-Y", "-TSABC123"], arguments);
        Assert.DoesNotContain(arguments, a => a.StartsWith("-W", StringComparison.Ordinal));
        Assert.Throws<BridgeBuildException>(() => tools.ProgrammingArguments("PIC18F45K22", "x.hex", "anything", null));
        Assert.Throws<BridgeBuildException>(() => tools.ProgrammingArguments("PIC18F45K22", "x.hex", "PK4", "abc\n-Pother"));
    }

    [Fact]
    public async Task Mplab_projects_report_missing_vendor_tools_instead_of_being_parsed_as_msbuild()
    {
        using var folder = new TempDirectory();
        folder.Write("Blink.X/nbproject/configurations.xml", """
            <configurationDescriptor><confs><conf name="default"><toolsSet>
            <targetDevice>PIC18F45K22</targetDevice><languageToolchain>XC8</languageToolchain>
            </toolsSet></conf></confs></configurationDescriptor>
            """);
        var paths = new BridgePathMapper(new LinkPathMap("/tablet", '/', folder.Path), OperatingSystem.IsWindows());
        var output = new List<string>();
        var outcome = await BridgeBuilder.BuildAsync(new BuildRequest(1, "/tablet/Blink.X/nbproject/configurations.xml", "Debug", false), paths, BridgeToolset.None, output.Add, CancellationToken.None);
        Assert.False(outcome.Success);
        Assert.Contains(outcome.Diagnostics, d => d.Id == "TS3003" && d.Message.Contains("MPLAB X", StringComparison.Ordinal));
        Assert.Contains(output, line => line.Contains("Install MPLAB", StringComparison.Ordinal));
    }
}
