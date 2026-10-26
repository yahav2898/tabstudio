using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using System.Text.Json;
using TabStudio.App.Services;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.Views;
using TabStudio.Toolchains.Embedded.Analysis;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Toolchains;

/// <summary>Embedded analysis is reachable through real IDE commands and never fabricates source locations.</summary>
public sealed class EmbeddedAnalysisTests
{
    [Fact]
    public void Memory_report_has_proportional_treemaps_and_escapes_firmware_names()
    {
        var report = new FirmwareMemoryReport(100, 25,
            [new(".text<script>", 0x8000000, 0x8000000, 75, 75, false, "FLASH"), new(".data", 0x20000000, 0x800004b, 25, 25, true, "RAM")],
            [new("<img src=x>", 0x8000000, 4, ".text", "a&b.c")], false);
        var html = EmbeddedMemoryReport.Create(report, new(12, -4),
            [new(".text.worker", 0x8000000, 12, "lib&foo.a(worker.o)")]);

        Assert.Contains("width=\"750.000\"", html, StringComparison.Ordinal);
        Assert.Contains("width=\"250.000\"", html, StringComparison.Ordinal);
        Assert.Contains(".text&lt;script&gt;", html, StringComparison.Ordinal);
        Assert.Contains("&lt;img src=x&gt;", html, StringComparison.Ordinal);
        Assert.Contains("a&amp;b.c", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.Contains("Since previous build", html, StringComparison.Ordinal);
        Assert.Contains("role=img", html, StringComparison.Ordinal);
        Assert.Contains("Object files from GNU linker map", html, StringComparison.Ordinal);
        Assert.Contains("lib&amp;foo.a(worker.o)", html, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Crash_command_reports_real_flags_and_keeps_unresolved_source_locations_empty()
    {
        using var fixture = new WorkspaceFixture();
        using var project = new TempDirectory();
        project.Write("platformio.ini", "[env:demo]\nplatform = ststm32\nboard = nucleo_f401re\nframework = arduino\n");
        var crash = project.Write("fault.json", """{ "Cfsr": 16777216, "Hfsr": 1073741824, "Mmfar": 0, "Bfar": 0 }""");
        var workspace = fixture.Workspace;
        workspace.SetUpProjectsWhenTheyOpen = false;
        await workspace.LoadAsync(project.Path);
        await workspace.OpenDocumentAsync(crash);

        Assert.True(workspace.DecodeEmbeddedCrashCommand.CanExecute(null));
        await workspace.DecodeEmbeddedCrashCommand.ExecuteAsync(null);

        var fault = Assert.Single(workspace.ErrorList.Items, item => item.Code == "EMBFAULT017");
        Assert.Contains("Unaligned", fault.Description, StringComparison.Ordinal);
        Assert.False(fault.HasLocation);
        Assert.Contains(workspace.ErrorList.Items, item => item.Code == "EMBFAULT020");
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = workspace } };
        window.Show();
        try { UiTestHelpers.SaveScreenshot(window, "embedded-01-crash-decoder"); }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Stack_command_checks_a_real_su_file_and_memory_command_refuses_to_guess_an_elf()
    {
        using var fixture = new WorkspaceFixture();
        using var project = new TempDirectory();
        project.Write("platformio.ini", "[env:demo]\nplatform = ststm32\nboard = nucleo_f401re\n");
        project.Write("build/main.su", "main.c:1:1:task\t32\tstatic\n");
        project.Write(".tabstudio/embedded-analysis.json", """
            { "StackUsageFiles": ["build/main.su"], "CallGraphComplete": true,
              "TaskBudgets": [{"Name":"Sensor", "EntryFunction":"task", "AllocatedBytes":16}] }
            """);
        var workspace = fixture.Workspace;
        workspace.SetUpProjectsWhenTheyOpen = false;
        await workspace.LoadAsync(project.Path);

        await workspace.AnalyzeEmbeddedStackCommand.ExecuteAsync(null);
        Assert.Contains(workspace.ErrorList.Items, item => item.Code == "EMBSTACK001" && item.Description.Contains("Sensor", StringComparison.Ordinal));
        Assert.Contains("32 B exceeds 16 B", workspace.Output.Text, StringComparison.Ordinal);
        await workspace.AnalyzeEmbeddedMemoryCommand.ExecuteAsync(null);
        Assert.Contains("Memory analysis needs one linked ELF", workspace.Output.Text, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(project.Path, ".tabstudio", "embedded-memory.json")));
    }

    [AvaloniaFact]
    public async Task Memory_command_reads_real_elf_and_serves_its_treemap_in_the_existing_preview()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var fixture = new WorkspaceFixture();
        using var project = new TempDirectory();
        project.Write("platformio.ini", "[env:demo]\nplatform = ststm32\nboard = nucleo_f401re\n");
        // The test host supplies real ELF tables; this is not an MCU execution or hardware proof.
        project.Write(".tabstudio/embedded-analysis.json", JsonSerializer.Serialize(new { Firmware = Environment.ProcessPath }));
        var workspace = fixture.Workspace;
        workspace.SetUpProjectsWhenTheyOpen = false;
        await workspace.LoadAsync(project.Path);

        await workspace.AnalyzeEmbeddedMemoryCommand.ExecuteAsync(null);
        Assert.True(workspace.IsWebPreviewSelected);
        Assert.True(workspace.WebPreview.IsRunning, workspace.Output.Text);
        Assert.True(File.Exists(Path.Combine(project.Path, ".tabstudio", "embedded-memory.json")));
        using var http = new HttpClient();
        var html = await http.GetStringAsync(workspace.WebPreview.AppAddress);
        Assert.Contains("Flash load image", html, StringComparison.Ordinal);
        Assert.Contains("RAM at runtime", html, StringComparison.Ordinal);
        Assert.Contains("<svg", html, StringComparison.Ordinal);
        await workspace.ReportEmbeddedMemoryAsync(project.Path, CancellationToken.None);
        Assert.Contains("Δ flash 0 B, RAM 0 B", workspace.Output.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Live_fault_command_refuses_an_unpaused_session_without_creating_capture_evidence()
    {
        using var fixture = new WorkspaceFixture();
        using var project = new TempDirectory();
        project.Write("platformio.ini", "[env:demo]\nplatform = ststm32\nboard = nucleo_f401re\n");
        var workspace = fixture.Workspace;
        workspace.SetUpProjectsWhenTheyOpen = false;
        await workspace.LoadAsync(project.Path);
        await workspace.CaptureEmbeddedFaultCommand.ExecuteAsync(null);
        Assert.Contains("Pause the matching on-chip GDB session", workspace.Output.Text, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(project.Path, ".tabstudio", "faults")));
        Assert.False(workspace.Prompt.IsOpen);
    }

    [AvaloniaFact]
    public async Task Missing_addr2line_keeps_captured_crash_evidence_in_error_list()
    {
        using var fixture = new WorkspaceFixture();
        using var project = new TempDirectory();
        project.Write("platformio.ini", "[env:demo]\nplatform = ststm32\nboard = nucleo_f401re\n");
        project.Write(".tabstudio/embedded-analysis.json", JsonSerializer.Serialize(new
        {
            Firmware = Environment.ProcessPath, Addr2Line = "tabstudio-no-such-addr2line",
        }));
        var fault = project.Write("fault.json", JsonSerializer.Serialize(new CortexFaultSnapshot(1u << 24, 0, 0, 0,
            [0, 0, 0, 0, 0, 0x08001001, 0x08000011, 1u << 24])));
        var workspace = fixture.Workspace;
        workspace.SetUpProjectsWhenTheyOpen = false;
        await workspace.LoadAsync(project.Path);
        await workspace.OpenDocumentAsync(fault);
        await workspace.DecodeEmbeddedCrashCommand.ExecuteAsync(null);
        Assert.Contains(workspace.ErrorList.Items, item => item.Code == "EMBFAULT017");
        Assert.Contains("Captured crash findings remain available", workspace.Output.Text, StringComparison.Ordinal);
        Assert.All(workspace.ErrorList.Items.Where(item => item.Code.StartsWith("EMBFAULT", StringComparison.Ordinal)), item => Assert.False(item.HasLocation));
    }
}
