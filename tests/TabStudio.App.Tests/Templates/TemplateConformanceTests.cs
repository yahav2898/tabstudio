using System.Reflection;
using Avalonia.Headless.XUnit;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Templates;

/// <summary>
/// Every New Project template, from creating it to running, debugging and testing it: the .NET templates build with
/// no errors and no warnings, the programs run and stop at a breakpoint, and the test projects' tests run and debug.
/// Each template names the tests that check it, so a template without them fails here.
/// </summary>
/// <remarks>
/// The builds restore real packages from nuget.org and take minutes, so CI runs the tests whose names contain
/// <c>template_conformance</c> in a step of their own; the list of templates and their tests runs with the others.
/// </remarks>
public sealed class TemplateConformanceTests : IClassFixture<SharedToolchain>, IDisposable
{
    /// <summary>
    /// The tests that create each template and build, run and debug it, by template, as <c>Class.Method</c>. Embedded
    /// starters are checked by <c>EmbeddedTemplateTests</c> and the canonical embedded workflow; Android and Flutter
    /// apps by their own workflows, named here by their test classes.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string[]> Checks = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        [ProjectTemplates.ConsoleId] = [nameof(Dotnet_templates_template_conformance_build_with_no_errors_or_warnings), nameof(Console_templates_template_conformance_run_and_stop_at_a_breakpoint)],
        [ProjectTemplates.ConsoleWithLibraryId] = [nameof(Dotnet_templates_template_conformance_build_with_no_errors_or_warnings), nameof(Console_templates_template_conformance_run_and_stop_at_a_breakpoint)],
        [ProjectTemplates.LibraryId] = [nameof(Dotnet_templates_template_conformance_build_with_no_errors_or_warnings)],
        [ProjectTemplates.VisualBasicConsoleId] = [nameof(Dotnet_templates_template_conformance_build_with_no_errors_or_warnings), nameof(Console_templates_template_conformance_run_and_stop_at_a_breakpoint)],
        [ProjectTemplates.VisualBasicLibraryId] = [nameof(Dotnet_templates_template_conformance_build_with_no_errors_or_warnings)],
        [ProjectTemplates.VisualBasicWindowsFormsId] = [nameof(Dotnet_templates_template_conformance_build_with_no_errors_or_warnings), "VisualBasicScreenshotTests"],
        [ProjectTemplates.XunitId] = [nameof(Dotnet_templates_template_conformance_build_with_no_errors_or_warnings), nameof(Test_templates_template_conformance_run_and_debug_their_tests)],
        [ProjectTemplates.NunitId] = [nameof(Dotnet_templates_template_conformance_build_with_no_errors_or_warnings), nameof(Test_templates_template_conformance_run_and_debug_their_tests)],
        [ProjectTemplates.MstestId] = [nameof(Dotnet_templates_template_conformance_build_with_no_errors_or_warnings), nameof(Test_templates_template_conformance_run_and_debug_their_tests)],
        [ProjectTemplates.WebId] = [nameof(Dotnet_templates_template_conformance_build_with_no_errors_or_warnings), "WebAppTests.F5_debugs_the_web_template_a_request_stops_at_a_breakpoint_in_its_endpoint_and_Shift_F5_stops_it"],
        [ProjectTemplates.AvaloniaId] = [nameof(Dotnet_templates_template_conformance_build_with_no_errors_or_warnings), "AvaloniaAppTests"],
        [ProjectTemplates.WpfId] = [nameof(Dotnet_templates_template_conformance_build_with_no_errors_or_warnings), "WpfXamlEditorTests"],
        [ProjectTemplates.WindowsFormsId] = [nameof(Dotnet_templates_template_conformance_build_with_no_errors_or_warnings), "WindowsFormsAppTests"],
        [TabStudio.Uno.Templates.UnoTemplates.UnoAppId] = [nameof(Dotnet_templates_template_conformance_build_with_no_errors_or_warnings), "UnoAppTests"],
        [TabStudio.Uno.Templates.UnoTemplates.WinUIAppId] = [nameof(Dotnet_templates_template_conformance_build_with_no_errors_or_warnings), "UnoAppTests"],
        [TabStudio.Web.Templates.WebTemplates.BlazorWebAppId] = [nameof(Dotnet_templates_template_conformance_build_with_no_errors_or_warnings), "BlazorAppTests"],
        [TabStudio.Web.Templates.WebTemplates.BlazorWebAssemblyId] = [nameof(Dotnet_templates_template_conformance_build_with_no_errors_or_warnings), "BlazorAppTests"],
        [TabStudio.Web.Templates.WebTemplates.RazorPagesId] = [nameof(Dotnet_templates_template_conformance_build_with_no_errors_or_warnings), "BlazorAppTests"],
        [TabStudio.Web.Templates.WebTemplates.MvcId] = [nameof(Dotnet_templates_template_conformance_build_with_no_errors_or_warnings), "BlazorAppTests"],
        [ProjectTemplates.NotebookId] = ["NotebookDocumentTests"],
        [ProjectTemplates.PythonId] = ["ToolchainLanguageToolsTests.A_Python_template_passes_the_debugger_conformance_checks", "PythonRunTests"],
        [ProjectTemplates.NodeId] = ["ToolchainLanguageToolsTests.A_Node_template_passes_the_debugger_conformance_checks", "NodeRunTests"],
        [ProjectTemplates.GoId] = ["ToolchainLanguageToolsTests.A_Go_template_passes_the_debugger_conformance_checks", "ToolchainLanguageToolsTests.A_Go_template_debugger_conformance_for_go_tests"],
        [ProjectTemplates.RustId] = ["ToolchainLanguageToolsTests.A_Rust_template_passes_the_debugger_conformance_checks", "ToolchainLanguageToolsTests.A_Rust_template_debugger_conformance_for_cargo_tests"],
        [ProjectTemplates.CppId] = ["ToolchainLanguageToolsTests.A_Cpp_template_passes_the_debugger_conformance_checks"],
        [ProjectTemplates.JavaId] = ["ToolchainLanguageToolsTests.A_Java_template_passes_the_debugger_conformance_checks"],
        [ProjectTemplates.KotlinId] = ["ToolchainLanguageToolsTests.A_Kotlin_Gradle_template_passes_the_debugger_conformance_checks"],
        [ProjectTemplates.KotlinScriptId] = ["ToolchainLanguageToolsTests.A_Kotlin_Gradle_template_runs_the_Kotlin_script_template_too"],
        [ProjectTemplates.PhpId] = ["ToolchainLanguageToolsTests.A_PHP_template_passes_the_debugger_conformance_checks", "ToolchainLanguageToolsTests.A_PHP_template_debugger_conformance_for_PHPUnit_tests"],
        [ProjectTemplates.PhpSiteId] = ["ToolchainLanguageToolsTests.A_PHP_template_serves_the_PHP_site_template_too"],
        [ProjectTemplates.SwiftId] = ["ToolchainLanguageToolsTests.A_Swift_package_template_passes_the_debugger_conformance_checks"],
        [ProjectTemplates.StaticSiteId] = ["StaticSiteTests"],
        [ProjectTemplates.AndroidId] = ["MobileWorkflowTests"],
        [ProjectTemplates.CapacitorId] = ["MobileWorkflowTests"],
        [ProjectTemplates.FlutterId] = ["FlutterTests"],
        [ProjectTemplates.FlutterWebId] = ["FlutterTests"],
        [ProjectTemplates.Pic18Id] = ["ProjectTemplatesTests.Pic_templates_include_mplab_metadata_and_the_portable_makefile"],
        [ProjectTemplates.Pic32Id] = ["ProjectTemplatesTests.Pic_templates_include_mplab_metadata_and_the_portable_makefile"],
    };

    private readonly SharedToolchain _toolchain;
    private readonly TempDirectory _temp = new();
    private readonly List<WorkspaceViewModel> _workspaces = [];

    public TemplateConformanceTests(SharedToolchain toolchain)
    {
        _toolchain = toolchain;
    }

    [Fact]
    public void Every_template_names_the_tests_that_create_build_run_and_debug_it_and_they_exist()
    {
        var types = typeof(TemplateConformanceTests).Assembly.GetTypes();
        var missing = new List<string>();
        foreach (var template in ProjectTemplates.All.Where(t => !t.Id.StartsWith("embedded-", StringComparison.Ordinal)))
        {
            if (!Checks.TryGetValue(template.Id, out var checks))
            {
                missing.Add($"{template.Id} ({template.Name}) names no tests in TemplateConformanceTests.Checks");
                continue;
            }

            foreach (var check in checks)
            {
                var parts = check.Split('.');
                var (typeName, method) = parts.Length == 2 ? (parts[0], parts[1]) : check.Contains('_', StringComparison.Ordinal) ? (nameof(TemplateConformanceTests), check) : (check, null);
                var type = types.FirstOrDefault(t => t.Name == typeName);
                if (type is null || (method is not null && type.GetMethod(method, BindingFlags.Public | BindingFlags.Instance) is null))
                {
                    missing.Add($"{template.Id}: {check} is not a test");
                }
            }
        }

        Assert.True(missing.Count == 0, string.Join("\n", missing));
        Assert.Contains(ProjectTemplates.All, t => t.Id.StartsWith("embedded-", StringComparison.Ordinal));
    }

    [AvaloniaTheory]
    [InlineData(ProjectTemplates.ConsoleId)]
    [InlineData(ProjectTemplates.LibraryId)]
    [InlineData(ProjectTemplates.ConsoleWithLibraryId)]
    [InlineData(ProjectTemplates.VisualBasicConsoleId)]
    [InlineData(ProjectTemplates.VisualBasicLibraryId)]
    [InlineData(ProjectTemplates.VisualBasicWindowsFormsId)]
    [InlineData(ProjectTemplates.WindowsFormsId)]
    [InlineData(ProjectTemplates.XunitId)]
    [InlineData(ProjectTemplates.NunitId)]
    [InlineData(ProjectTemplates.MstestId)]
    [InlineData(ProjectTemplates.WebId)]
    [InlineData(ProjectTemplates.AvaloniaId)]
    [InlineData(ProjectTemplates.WpfId)]
    [InlineData(TabStudio.Uno.Templates.UnoTemplates.UnoAppId)]
    [InlineData(TabStudio.Uno.Templates.UnoTemplates.WinUIAppId)]
    [InlineData(TabStudio.Web.Templates.WebTemplates.BlazorWebAppId)]
    [InlineData(TabStudio.Web.Templates.WebTemplates.BlazorWebAssemblyId)]
    [InlineData(TabStudio.Web.Templates.WebTemplates.RazorPagesId)]
    [InlineData(TabStudio.Web.Templates.WebTemplates.MvcId)]
    public async Task Dotnet_templates_template_conformance_build_with_no_errors_or_warnings(string template)
    {
        var workspace = await OpenAsync(template);

        await workspace.BuildCommand.ExecuteAsync(null);

        // The WinUI template is Visual Studio's, with its Mica backdrop and title bar, which Uno does not implement: the
        // build says so (TS7101), as the Uno tests require, and those three are the only warnings it may have.
        var expected = template == TabStudio.Uno.Templates.UnoTemplates.WinUIAppId
            ? new[] { "Window.SystemBackdrop", "MicaBackdrop", "TitleBar" }
            : [];
        var unexpected = workspace.ErrorList.Items
            .Where(i => i.IsError || (i.IsWarning && !(i.Diagnostic.Id == "TS7101" && expected.Any(e => i.Description.Contains(e, StringComparison.Ordinal)))))
            .ToList();
        var problems = string.Join("\n", unexpected.Select(i => $"{i.Diagnostic.Id} {i.File}({i.Line}): {i.Description}"));
        Assert.True(unexpected.Count == 0, $"{template}:\n{problems}\n{workspace.Output.Text}");
        Assert.Equal(expected.Length, workspace.ErrorList.WarningCount);
        Assert.Contains(" succeeded, 0 failed", workspace.Output.Text, StringComparison.Ordinal);
    }

    [AvaloniaTheory]
    [InlineData(ProjectTemplates.ConsoleId, "Program.cs", "Console.WriteLine(", "Hello from TabStudio!")]
    [InlineData(ProjectTemplates.ConsoleWithLibraryId, "Messages.cs", "Welcome(string name)", "Hello from TabStudio!")]
    [InlineData(ProjectTemplates.VisualBasicConsoleId, "Program.vb", "Dim answer = 42", "Hello, Ada! This is Visual Basic")]
    public async Task Console_templates_template_conformance_run_and_stop_at_a_breakpoint(string template, string file, string line, string output)
    {
        var workspace = await OpenAsync(template);
        AnswerInput(workspace, "Ada");

        // Ctrl+F5 runs it without the debugger.
        await workspace.RunCommand.ExecuteAsync(null);
        Assert.Contains(output, workspace.Output.Text, StringComparison.Ordinal);
        Assert.Contains("has exited with code 0", workspace.Output.Text, StringComparison.Ordinal);

        // F5 stops at a breakpoint, in the library for the template with one, and continues to the end.
        var path = Directory.EnumerateFiles(workspace.Workspace!.RootDirectory, file, SearchOption.AllDirectories).Single();
        var document = (await workspace.OpenDocumentAsync(path))!;
        var breakpoint = document.Document.Lines.First(l => document.Document.GetText(l).Contains(line, StringComparison.Ordinal)).LineNumber;
        workspace.Breakpoints.Toggle(path, breakpoint);
        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(
            () => workspace.IsPaused && workspace.CallStack.Frames.Count > 0 && workspace.CallStack.Frames[0].Location?.FilePath == path,
            TimeSpan.FromMinutes(2),
            because: $"the breakpoint in {file}");
        await workspace.PauseShown;
        Assert.Equal(breakpoint, workspace.CallStack.Frames[0].Location?.Line);

        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => !workspace.IsDebugging, TimeSpan.FromMinutes(1), because: "the program to end");
        Assert.Contains(output, workspace.Output.Text, StringComparison.Ordinal);
        Assert.Equal(IdeState.Ready, workspace.State);
    }

    [AvaloniaTheory]
    [InlineData(ProjectTemplates.XunitId, "UnitTest1.cs", "var total = 1 + 1;")]
    [InlineData(ProjectTemplates.NunitId, "UnitTest1.cs", "_total += 2;")]
    [InlineData(ProjectTemplates.MstestId, "Test1.cs", "var total = 1 + 1;")]
    public async Task Test_templates_template_conformance_run_and_debug_their_tests(string template, string file, string line)
    {
        var workspace = await OpenAsync(template);
        var tests = workspace.Tests;
        await UiTestHelpers.WaitUntilAsync(() => tests.TotalCount == 1, TimeSpan.FromMinutes(1), because: "the template's test, found in source");

        await tests.RunAllCommand.ExecuteAsync(null);
        Assert.True(tests.PassedCount == 1, $"{tests.RunStatusText}\n{workspace.Output.Text}");

        var path = Directory.EnumerateFiles(workspace.Workspace!.RootDirectory, file, SearchOption.AllDirectories).Single();
        var document = (await workspace.OpenDocumentAsync(path))!;
        var breakpoint = document.Document.Lines.First(l => document.Document.GetText(l).Contains(line, StringComparison.Ordinal)).LineNumber;
        workspace.Breakpoints.Toggle(path, breakpoint);
        var debugging = tests.DebugAllCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(
            () => workspace.IsPaused && workspace.CallStack.Frames.FirstOrDefault()?.Location?.Line == breakpoint,
            TimeSpan.FromMinutes(2),
            because: "the breakpoint in the test");
        await workspace.PauseShown;
        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        await debugging.WaitAsync(TimeSpan.FromMinutes(1));

        Assert.False(workspace.IsDebugging);
        Assert.True(tests.PassedCount == 1, $"{tests.RunStatusText}\n{workspace.Output.Text}");
    }

    public void Dispose()
    {
        foreach (var workspace in _workspaces)
        {
            workspace.Dispose();
        }

        _temp.Dispose();
    }

    private async Task<WorkspaceViewModel> OpenAsync(string template)
    {
        var device = new DefaultDeviceStatus(new DeviceInfo(8L * 1024 * 1024 * 1024, 8, IsLowRamDevice: false));
        var workspace = new WorkspaceViewModel(
            _toolchain.Compiler, device, DeviceProfile.For(device.Device), new AvaloniaUiDispatcher(), new SessionStore(_toolchain.Paths), _toolchain.Paths);
        _workspaces.Add(workspace);
        var solution = new ProjectTemplates(new AppPaths(_temp.Resolve(template))).Create(template, "Conformance");
        await workspace.LoadAsync(solution);
        return workspace;
    }

    /// <summary>Types a line into the program whenever it waits for input.</summary>
    private static void AnswerInput(WorkspaceViewModel workspace, string line)
    {
        var output = workspace.Output;
        output.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(OutputViewModel.IsWaitingForInput) && output.IsWaitingForInput)
            {
                output.InputText = line;
                output.SubmitInputCommand.Execute(null);
            }
        };
    }
}
