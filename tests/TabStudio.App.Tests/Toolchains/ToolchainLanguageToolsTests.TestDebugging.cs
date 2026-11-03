using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Tests.Toolchains;

/// <summary>
/// Test Explorer's Debug for the test runners of other languages, with their real debug adapters: Debug All stops at
/// a breakpoint in the code the tests call, and the results land in Test Explorer as they do for a run.
/// </summary>
/// <remarks>CI runs them with the debugger checks of their language (their names contain <c>debugger_conformance</c>).</remarks>
public sealed partial class ToolchainLanguageToolsTests
{
    [RealServerAvaloniaFact("python3", PythonModule = "debugpy")]
    public async Task A_Python_template_debugger_conformance_for_pytest_tests()
    {
        _temp.Write("calc/conftest.py", string.Empty);
        var path = _temp.Write("calc/test_calc.py", """
            def add(a, b):
                total = a + b
                return total


            def test_adds():
                assert add(2, 3) == 5


            def test_adds_wrongly():
                assert add(2, 2) == 5

            """);
        await DebugTestsAsync(_temp.Resolve("calc"), path, "total = a + b", "a", "2", passed: 1, failed: 1, "pytest");
    }

    [RealServerAvaloniaFact("node", "js-debug-adapter")]
    public async Task A_Node_template_debugger_conformance_for_node_test_tests()
    {
        _temp.Write("calc/package.json", """{ "name": "calc", "version": "1.0.0", "scripts": { "test": "node --test" } }""");
        var path = _temp.Write("calc/calc.test.js", """
            const test = require('node:test');
            const assert = require('node:assert');

            function add(a, b) {
              const total = a + b;
              return total;
            }

            test('adds', () => {
              assert.strictEqual(add(2, 3), 5);
            });

            """);
        await DebugTestsAsync(_temp.Resolve("calc"), path, "const total = a + b", "a", "2", passed: 1, failed: 0, "node-test");
    }

    [RealServerAvaloniaFact("go", "gopls", "dlv")]
    public async Task A_Go_template_debugger_conformance_for_go_tests()
    {
        var folder = _services.GetRequiredService<ProjectTemplates>().Create(ProjectTemplates.GoId, "Greeter");
        await File.WriteAllTextAsync(Path.Combine(folder, "main_test.go"), """
            package main

            import "testing"

            func TestGreet(t *testing.T) {
            	if greet("Ada") == "" {
            		t.Fatal("no greeting")
            	}
            }

            """);
        await DebugTestsAsync(folder, Path.Combine(folder, "main.go"), "message := \"Hello, \"", "name", "Ada", passed: 1, failed: 0, "go-test");
    }

    [RealServerAvaloniaFact("php", "composer", "phpactor", "php-debug-adapter")]
    public async Task A_PHP_template_debugger_conformance_for_PHPUnit_tests()
    {
        var folder = _services.GetRequiredService<ProjectTemplates>().Create(ProjectTemplates.PhpId, "Greeter");
        await DebugTestsAsync(folder, Path.Combine(folder, "src", "Greeter.php"), "$answer = 42;", "$name", "Ada", passed: 1, failed: 0, "phpunit", TimeSpan.FromSeconds(300));
    }

    /// <summary>
    /// Opens a folder, sets a breakpoint on a line of a file, debugs every test from Test Explorer, checks a local at
    /// the first stop, continues to the end and checks the results.
    /// </summary>
    private async Task DebugTestsAsync(string folder, string file, string line, string local, string value, int passed, int failed, string name, TimeSpan? setup = null)
    {
        var window = Show();
        await _main.OpenWorkspaceAsync(folder);
        var workspace = _main.Workspace!;
        using var trace = new SavedLogs(workspace, "debugger-conformance-" + name);
        var document = await workspace.OpenDocumentAsync(file) ?? throw new InvalidOperationException(file + " did not open.");
        var tests = workspace.Tests;
        await UiTestHelpers.WaitUntilAsync(() => tests.HasTestProjects && tests.DebugAllCommand.CanExecute(null), setup ?? TimeSpan.FromSeconds(120), because: "Test Explorer's tests");
        var breakpoint = LineOf(document, line);
        workspace.Breakpoints.Toggle(file, breakpoint);

        var debugging = tests.DebugAllCommand.ExecuteAsync(null);
        await PausedAtAsync(workspace, breakpoint, setup ?? TimeSpan.FromSeconds(180), "the breakpoint in the tested code");
        await UiTestHelpers.WaitUntilAsync(
            () => !workspace.Locals.Tree.IsStale && workspace.Locals.Tree.Rows.Any(r => r.Name == local && r.Value.Contains(value, StringComparison.Ordinal)),
            TimeSpan.FromSeconds(30),
            because: $"{local} to be {value}");
        Assert.True(tests.IsRunning);
        UiTestHelpers.SaveScreenshot(window, $"debugger-conformance-{name}-breakpoint");

        // Every test that reaches the breakpoint stops there; Continue lets each go on.
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(120);
        while (workspace.IsDebugging && DateTime.UtcNow < deadline)
        {
            if (workspace.IsPaused)
            {
                await workspace.StartDebuggingCommand.ExecuteAsync(null);
            }

            await Task.Delay(100);
        }

        await debugging.WaitAsync(TimeSpan.FromSeconds(60));
        Assert.False(workspace.IsDebugging);
        Assert.False(tests.IsRunning);
        Assert.Equal((passed, failed), (tests.PassedCount, tests.FailedCount));
        trace.Passed = true;
    }
}
