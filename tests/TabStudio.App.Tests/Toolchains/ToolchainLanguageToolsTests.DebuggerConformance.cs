using TabStudio.App.Services;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Tests.Toolchains;

/// <summary>
/// The same debugger checks for every language with a real debug adapter, on a program made from its New Project
/// template: a conditional breakpoint, Step Into, Over and Out, a hit count, the threads, breaking on an uncaught
/// exception, Stop, and Restart after an edit, which must debug the edited code.
/// </summary>
/// <remarks>
/// They take minutes, so CI runs each in its language's tool step, not with the rest of the IDE's tests: their names
/// contain <c>debugger_conformance</c>.
/// </remarks>
public sealed partial class ToolchainLanguageToolsTests
{
    private const string PythonConformance = """
        import threading

        ready = threading.Event()
        release = threading.Event()


        def square(n):
            result = n * n
            return result


        def worker():
            ready.set()
            release.wait()


        def main():
            print("Conformance v1", flush=True)
            helper = threading.Thread(target=worker, name="worker", daemon=True)
            helper.start()
            ready.wait()
            total = 0
            for i in range(5):
                total += square(i)
            print("total", total, flush=True)
            raise ValueError("boom from main")


        if __name__ == "__main__":
            main()

        """;

    private const string NodeConformance = """
        function square(n) {
          const result = n * n;
          return result;
        }

        function main() {
          console.log('Conformance v1');
          let total = 0;
          for (let i = 0; i < 5; i++) {
            total += square(i);
          }
          console.log(`total ${total}`);
          throw new Error('boom from main');
        }

        main();

        """;

    private const string GoConformance = """
        package main

        import (
        	"errors"
        	"fmt"
        	"sync"
        )

        func square(n int) int {
        	result := n * n
        	return result
        }

        func main() {
        	fmt.Println("Conformance v1")
        	var ready, release sync.WaitGroup
        	ready.Add(1)
        	release.Add(1)
        	go func() {
        		ready.Done()
        		release.Wait()
        	}()
        	ready.Wait()
        	total := 0
        	for i := 0; i < 5; i++ {
        		total += square(i)
        	}
        	fmt.Println("total", total)
        	panic(errors.New("boom from main"))
        }

        """;

    private const string RustConformance = """
        use std::sync::{Arc, Barrier};
        use std::thread;

        fn square(n: i32) -> i32 {
            let result = n * n;
            return result;
        }

        fn main() {
            println!("Conformance v1");
            let barrier = Arc::new(Barrier::new(2));
            let worker = Arc::clone(&barrier);
            let _helper = thread::spawn(move || {
                worker.wait();
                thread::park();
            });
            barrier.wait();
            let mut total = 0;
            for i in 0..5 {
                total += square(i);
            }
            println!("total {}", total);
            panic!("boom from main");
        }

        """;

    private const string CppConformance = """
        #include <chrono>
        #include <condition_variable>
        #include <iostream>
        #include <mutex>
        #include <stdexcept>
        #include <thread>

        int square(int n) {
            int result = n * n;
            return result;
        }

        int main() {
            std::cout << "Conformance v1" << std::endl;
            static std::mutex gate;
            static std::condition_variable changed;
            static bool started = false;
            std::thread helper([] {
                {
                    std::lock_guard<std::mutex> lock(gate);
                    started = true;
                }
                changed.notify_one();
                std::this_thread::sleep_for(std::chrono::hours(1));
            });
            helper.detach();
            {
                std::unique_lock<std::mutex> lock(gate);
                changed.wait(lock, [] { return started; });
            }
            int total = 0;
            for (int i = 0; i < 5; i++) {
                total += square(i);
            }
            std::cout << "total " << total << std::endl;
            throw std::runtime_error("boom from main");
        }

        """;

    private const string JavaConformance = """
        package com.example;

        import java.util.concurrent.CountDownLatch;

        public class App {
            static int square(int n) {
                int result = n * n;
                return result;
            }

            public static void main(String[] args) throws InterruptedException {
                System.out.println("Conformance v1");
                CountDownLatch ready = new CountDownLatch(1);
                Thread helper = new Thread(() -> {
                    ready.countDown();
                    try {
                        Thread.sleep(3_600_000);
                    } catch (InterruptedException e) {
                        Thread.currentThread().interrupt();
                    }
                }, "worker");
                helper.setDaemon(true);
                helper.start();
                ready.await();
                int total = 0;
                for (int i = 0; i < 5; i++) {
                    total += square(i);
                }
                System.out.println("total " + total);
                throw new IllegalStateException("boom from main");
            }
        }

        """;

    private const string KotlinConformance = """
        import java.util.concurrent.CountDownLatch
        import kotlin.concurrent.thread

        fun greet(name: String): String = "Hello, $name!"

        fun square(n: Int): Int {
            val result = n * n
            return result
        }

        fun main() {
            println("Conformance v1")
            val ready = CountDownLatch(1)
            thread(isDaemon = true, name = "worker") {
                ready.countDown()
                Thread.sleep(3_600_000)
            }
            ready.await()
            var total = 0
            for (i in 0 until 5) {
                total += square(i)
            }
            println("total $total")
            throw IllegalStateException("boom from main")
        }

        """;

    private const string PhpConformance = """
        <?php
        declare(strict_types=1);

        function square(int $n): int
        {
            $result = $n * $n;
            return $result;
        }

        echo 'Conformance v1', PHP_EOL;
        $total = 0;
        for ($i = 0; $i < 5; $i++) {
            $total += square($i);
        }
        echo "total $total", PHP_EOL;
        throw new RuntimeException('boom from main');

        """;

    private const string SwiftConformance = """
        import Foundation

        func square(_ n: Int) -> Int {
            let result = n * n
            return result
        }

        func run() {
            print("Conformance v1")
            fflush(nil)
            var total = 0
            for i in 0..<5 {
                total += square(i)
            }
            print("total \(total)")
            fflush(nil)
            fatalError("boom from main")
        }

        run()

        """;

    [RealServerAvaloniaFact("python3", PythonModule = "debugpy")]
    public Task A_Python_template_passes_the_debugger_conformance_checks() =>
        DebuggerConformanceAsync(new(ProjectTemplates.PythonId, "main.py", PythonConformance, "python", "i == 2", "i")
        {
            CalleeFirst = "result = n * n", CalleeNext = "return result", Caller = "main", Threads = 2,
        });

    [RealServerAvaloniaFact("node", "js-debug-adapter")]
    public Task A_Node_template_passes_the_debugger_conformance_checks() =>
        DebuggerConformanceAsync(new(ProjectTemplates.NodeId, "index.js", NodeConformance, "node", "i === 2", "i")
        {
            CalleeFirst = "const result = n * n", CalleeNext = "return result", Caller = "main", Threads = 1,
        });

    [RealServerAvaloniaFact("go", "gopls", "dlv")]
    public Task A_Go_template_passes_the_debugger_conformance_checks() =>
        DebuggerConformanceAsync(new(ProjectTemplates.GoId, "main.go", GoConformance, "go", "i == 2", "i")
        {
            CalleeFirst = "result := n * n", CalleeNext = "return result", Caller = "main", Threads = 2,
        });

    [RealServerAvaloniaFact("cargo", "rust-analyzer", "lldb-dap")]
    public Task A_Rust_template_passes_the_debugger_conformance_checks() =>
        DebuggerConformanceAsync(new(ProjectTemplates.RustId, "main.rs", RustConformance, "rust", "i == 2", "i")
        {
            // The panic stops at rust_panic, shown at the program's own frame that panicked.
            CalleeFirst = "let result = n * n", CalleeNext = "return result", Caller = "main", Threads = 2, ExitText = "boom from main",
        });

    [RealServerAvaloniaFact("cmake", "c++", "clangd", "lldb-dap")]
    public Task A_Cpp_template_passes_the_debugger_conformance_checks() =>
        DebuggerConformanceAsync(new(ProjectTemplates.CppId, "main.cpp", CppConformance, "cpp", "i == 2", "i")
        {
            CalleeFirst = "int result = n * n", CalleeNext = "return result", Caller = "main", Threads = 2,
        });

    [RealServerAvaloniaFact("java", "mvn", "jdtls")]
    public Task A_Java_template_passes_the_debugger_conformance_checks() =>
        DebuggerConformanceAsync(new(ProjectTemplates.JavaId, "App.java", JavaConformance, "java", "i == 2", "i")
        {
            CalleeFirst = "int result = n * n", CalleeNext = "return result", Caller = "main", Threads = 2, Launch = TimeSpan.FromSeconds(240),
        });

    [RealServerAvaloniaFact("kotlinc", "gradle", "kotlin-lsp", "jdtls")]
    public Task A_Kotlin_Gradle_template_passes_the_debugger_conformance_checks() =>
        DebuggerConformanceAsync(new(ProjectTemplates.KotlinId, "Main.kt", KotlinConformance, "kotlin", "i == 2", "i")
        {
            CalleeFirst = "val result = n * n", CalleeNext = "return result", Caller = "main", Threads = 2, Launch = TimeSpan.FromSeconds(300),
        });

    [RealServerAvaloniaFact("php", "composer", "phpactor", "php-debug-adapter")]
    public Task A_PHP_template_passes_the_debugger_conformance_checks() =>
        DebuggerConformanceAsync(new(ProjectTemplates.PhpId, "main.php", PhpConformance, "php", "$i == 2", "$i")
        {
            Loop = "$total += square($i)", CalleeFirst = "$result = $n * $n", CalleeNext = "return $result", Caller = "{main}", Threads = 1,
            BreaksOnUncaught = false, ExitText = "boom from main",
        });

    [RealServerAvaloniaFact("swift", "sourcekit-lsp", "lldb-dap")]
    public Task A_Swift_package_template_passes_the_debugger_conformance_checks() =>
        DebuggerConformanceAsync(new(ProjectTemplates.SwiftId, "main.swift", SwiftConformance, "swift", "i == 2", "i")
        {
            CalleeFirst = "let result = n * n", CalleeNext = "return result", Caller = "run", Threads = 1, Launch = TimeSpan.FromSeconds(300),
        });

    /// <summary>Runs the debugger checks on the scenario's program, saving the logs when one fails.</summary>
    private async Task DebuggerConformanceAsync(DebuggerScenario scenario)
    {
        var window = Show();
        var (workspace, document) = await OpenTemplateAsync(scenario.Template, scenario.Entry);
        using var trace = new SavedLogs(workspace, "debugger-conformance-" + scenario.Name);
        document.Document.Text = scenario.Program;
        var loop = LineOf(document, scenario.Loop);
        var first = LineOf(document, scenario.CalleeFirst);
        var next = LineOf(document, scenario.CalleeNext);
        var step = TimeSpan.FromSeconds(90);

        // A conditional breakpoint stops only when its condition holds.
        var breakpoint = workspace.Breakpoints.Add(document.FilePath, loop, scenario.Condition);
        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        Assert.True(workspace.IsDebugging, workspace.Output.Text);
        await PausedAtAsync(workspace, loop, scenario.Launch, "the conditional breakpoint");
        await LocalIsAsync(workspace, scenario.Index, "2");
        UiTestHelpers.SaveScreenshot(window, $"debugger-conformance-{scenario.Name}-1-condition");

        // Step Into enters the call, Step Over goes to its next line, and Step Out returns to the caller.
        await workspace.StepIntoCommand.ExecuteAsync(null);
        await PausedAtAsync(workspace, first, step, "Step Into", scenario.Callee);
        await workspace.StepOverCommand.ExecuteAsync(null);
        await PausedAtAsync(workspace, next, step, "Step Over", scenario.Callee);
        workspace.StepOutCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(
            () => workspace.IsPaused && workspace.CallStack.Frames.Count > 0
                && !workspace.CallStack.Frames[0].MethodName.Contains(scenario.Callee, StringComparison.Ordinal)
                && workspace.CallStack.Frames[0].MethodName.Contains(scenario.Caller, StringComparison.Ordinal),
            step,
            because: "Step Out to return to " + scenario.Caller);
        Assert.Equal(loop, workspace.CallStack.Frames[0].Location?.Line);

        // A hit count of = 2 set now stops on the second hit from here: i is 3, then 4.
        workspace.Breakpoints.SetCondition(breakpoint, null);
        workspace.Breakpoints.SetOptions(breakpoint, "= 2", null);
        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        await PausedAtAsync(workspace, loop, step, "the hit count");
        await LocalIsAsync(workspace, scenario.Index, "4");

        // The Call Stack lists the program's threads; picking one shows its frames.
        Assert.True(workspace.CallStack.Threads.Count >= scenario.Threads, $"{workspace.CallStack.Threads.Count} threads: {string.Join(", ", workspace.CallStack.Threads.Select(t => t.Name))}");
        UiTestHelpers.SaveScreenshot(window, $"debugger-conformance-{scenario.Name}-2-hit-count");

        // The uncaught exception at the end stops the debugger with the exception helper, and Stop ends the program.
        workspace.Breakpoints.Remove(breakpoint);
        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        if (scenario.BreaksOnUncaught)
        {
            await UiTestHelpers.WaitUntilAsync(() => workspace.IsPaused && workspace.ActiveDocument?.ExceptionHelper is not null, step, because: "the uncaught exception");
            Assert.StartsWith("Exception", workspace.StatusText, StringComparison.Ordinal);
            UiTestHelpers.SaveScreenshot(window, $"debugger-conformance-{scenario.Name}-3-exception");
            workspace.StopDebuggingCommand.Execute(null);
        }

        await UiTestHelpers.WaitUntilAsync(() => !workspace.IsDebugging, step, because: "the program to end");
        Assert.Contains("total 30", workspace.Output.Text, StringComparison.Ordinal);
        if (scenario.ExitText is { } exitText)
        {
            Assert.Contains(exitText, workspace.Output.Text, StringComparison.Ordinal);
        }

        Assert.False(workspace.Output.IsAcceptingInput);

        // Restart after an edit debugs the edited program: saved, and built again where the language builds.
        workspace.Breakpoints.Add(document.FilePath, loop);
        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        await PausedAtAsync(workspace, loop, scenario.Launch, "the breakpoint before Restart");
        document.Document.Text = document.Document.Text.Replace("Conformance v1", "Conformance v2", StringComparison.Ordinal);
        workspace.RestartCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(
            () => workspace.IsPaused && workspace.Output.Text.Contains("Conformance v2", StringComparison.Ordinal)
                && workspace.CallStack.Frames.FirstOrDefault()?.Location?.Line == loop,
            scenario.Launch,
            because: "Restart to debug the edited program");
        Assert.DoesNotContain("Conformance v1", workspace.Output.Text, StringComparison.Ordinal);
        workspace.StopDebuggingCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => !workspace.IsDebugging, step, because: "Stop after Restart");
        Assert.True(workspace.State is IdeState.Ready or IdeState.Failed, workspace.State.ToString());
        trace.Passed = true;
    }

    private static async Task PausedAtAsync(WorkspaceViewModel workspace, int line, TimeSpan timeout, string because, string? method = null)
    {
        await UiTestHelpers.WaitUntilAsync(
            () => workspace.IsPaused && workspace.CallStack.Frames.Count > 0 && workspace.CallStack.Frames[0].Location?.Line == line
                && (method is null || workspace.CallStack.Frames[0].MethodName.Contains(method, StringComparison.Ordinal)),
            timeout,
            because: $"{because} to pause at line {line}");
    }

    private static Task LocalIsAsync(WorkspaceViewModel workspace, string name, string value) =>
        UiTestHelpers.WaitUntilAsync(
            () => !workspace.Locals.Tree.IsStale && workspace.Locals.Tree.Rows.Any(r => r.Name == name && r.Value.Trim() == value),
            TimeSpan.FromSeconds(30),
            because: $"{name} to be {value}");

    /// <summary>A language's program for the debugger checks, and what its frames and locals are called.</summary>
    /// <param name="Template">The New Project template the program goes into.</param>
    /// <param name="Entry">The file the template opens, which the program replaces.</param>
    /// <param name="Program">The program: a loop that calls <c>square</c>, a thread, and an uncaught exception at its end.</param>
    /// <param name="Name">The language, for the logs and screenshots.</param>
    /// <param name="Condition">The breakpoint condition that holds when <c>i</c> is 2, in the language.</param>
    /// <param name="Index">The loop variable, as Locals names it.</param>
    private sealed record DebuggerScenario(string Template, string Entry, string Program, string Name, string Condition, string Index)
    {
        /// <summary>The loop's line, which calls the function.</summary>
        public string Loop { get; init; } = "total += square(i)";

        /// <summary>The called function, as its frame is named.</summary>
        public string Callee { get; init; } = "square";

        /// <summary>The first line of the called function.</summary>
        public required string CalleeFirst { get; init; }

        /// <summary>Its next line.</summary>
        public required string CalleeNext { get; init; }

        /// <summary>The caller's frame name.</summary>
        public required string Caller { get; init; }

        /// <summary>How many threads the Call Stack lists at least.</summary>
        public int Threads { get; init; } = 1;

        /// <summary>Whether the uncaught exception stops the debugger.</summary>
        public bool BreaksOnUncaught { get; init; } = true;

        /// <summary>What the Output panel shows once the program ended, beyond its total.</summary>
        public string? ExitText { get; init; }

        /// <summary>How long a launch may take: building, and the adapter starting.</summary>
        public TimeSpan Launch { get; init; } = TimeSpan.FromSeconds(180);
    }
}
