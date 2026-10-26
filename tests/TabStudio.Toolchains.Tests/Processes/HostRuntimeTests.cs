using System.Text;
using TabStudio.Testing;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Processes.Unix;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Toolchains.Tests.Processes;

/// <summary>The process API on the machine the tests run on: Linux in CI.</summary>
public sealed class HostRuntimeTests : IDisposable
{
    private readonly HostRuntime _runtime = new();
    private readonly TempDirectory _temp = new();

    [Fact]
    public async Task Runs_a_program_with_its_output_error_and_exit_code()
    {
        using var process = _runtime.Start(Shell("echo out; echo err >&2; exit 3"));
        var output = new ProcessOutput(process);

        var exit = await process.Completion.WaitAsync(TimeSpan.FromSeconds(20));
        await output.CompletedAsync();

        Assert.Equal(3, exit.ExitCode);
        Assert.Null(exit.Signal);
        Assert.False(exit.WasStopped);
        Assert.Equal("out\n", output.Output);
        Assert.Equal("err\n", output.Error);
        Assert.Equal("exited with code 3", exit.Description);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(128)]
    public async Task Native_spawn_reports_the_actual_child_pid_and_its_nonzero_exit_status(int extraEntries)
    {
        // Checking output alone misses a corrupt out PID: the child still runs, but waitpid has no such child.
        // Exercise repeated native calls and collections, including a nonzero exit rather than assuming success.
        for (var i = 0; i < 30; i++)
        {
            using var process = _runtime.Start(new ProcessStartSpec
            {
                Program = "/bin/sh",
                Arguments = ["-c", "printf '%s\\n' $$ \"$#\" \"$TABSTUDIO_PID_PROBE\"; exit 23", "pid-probe",
                    .. Enumerable.Range(0, extraEntries).Select(n => $"argument '{n}' $(literal)")],
                Environment = Enumerable.Range(0, extraEntries)
                    .ToDictionary(n => $"TABSTUDIO_EXTRA_{n}", n => (string?)$"value '{n}' $(literal)")
                    .Append(new KeyValuePair<string, string?>("TABSTUDIO_PID_PROBE", "kept unchanged"))
                    .ToDictionary(p => p.Key, p => p.Value),
            });
            var output = new ProcessOutput(process);
            GC.Collect();
            var exit = await process.Completion.WaitAsync(TimeSpan.FromSeconds(20));
            await output.CompletedAsync();

            Assert.Equal(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{process.Id}\n{extraEntries}\nkept unchanged\n"), output.Output);
            Assert.Equal(23, exit.ExitCode);
            Assert.Null(exit.Signal);
        }
    }

    [Fact]
    public async Task Reads_standard_input_until_it_is_closed()
    {
        using var process = _runtime.Start(Shell("read name; echo \"Hello, $name!\"; cat"));
        var output = new ProcessOutput(process);

        process.Input.Write(Encoding.UTF8.GetBytes("Tablet\nrest of the input\n"));
        await output.WaitForAsync("Hello, Tablet!");
        process.CloseInput();
        var exit = await process.Completion.WaitAsync(TimeSpan.FromSeconds(20));
        await output.CompletedAsync();

        Assert.True(exit.Succeeded);
        Assert.Equal("Hello, Tablet!\nrest of the input\n", output.Output);
    }

    [Fact]
    public async Task Starts_in_the_working_directory_with_the_extra_environment()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_temp.Path, "work folder")).FullName;
        var spec = Shell("pwd; echo \"$GREETING\"; echo \"${HOME:+home is set}\"") with
        {
            WorkingDirectory = folder,
            Environment = new Dictionary<string, string?> { ["GREETING"] = "hi there" },
        };

        using var process = _runtime.Start(spec);
        var output = new ProcessOutput(process);
        await process.Completion.WaitAsync(TimeSpan.FromSeconds(20));
        await output.CompletedAsync();

        Assert.Equal($"{folder}\nhi there\nhome is set\n", output.Output);
    }

    [Fact]
    public async Task Runs_in_a_pseudo_terminal_of_the_requested_size()
    {
        using var process = _runtime.Start(Shell("tty; stty size; test -t 0 && echo interactive") with { Terminal = new TerminalSize(30, 100) });
        var output = new ProcessOutput(process);

        var exit = await process.Completion.WaitAsync(TimeSpan.FromSeconds(20));
        await output.CompletedAsync();

        Assert.True(process.IsTerminal);
        Assert.Null(process.Error);
        Assert.True(exit.Succeeded, output.Output);
        Assert.Contains("/dev/pts/", output.Output, StringComparison.Ordinal);
        Assert.Contains("30 100", output.Output, StringComparison.Ordinal);
        Assert.Contains("interactive", output.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tells_a_terminal_program_its_new_size()
    {
        using var process = _runtime.Start(Shell("stty size; read line; stty size") with { Terminal = new TerminalSize(24, 80) });
        var output = new ProcessOutput(process);
        await output.WaitForAsync("24 80");

        process.Resize(new TerminalSize(40, 132));
        process.Input.Write("\n"u8);
        await process.Completion.WaitAsync(TimeSpan.FromSeconds(20));
        await output.CompletedAsync();

        Assert.Contains("40 132", output.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ctrl_C_in_the_terminal_interrupts_the_foreground_program()
    {
        using var process = _runtime.Start(Shell("trap 'echo interrupted; exit 7' INT; echo ready; while true; do sleep 0.1; done") with { Terminal = new TerminalSize(24, 80) });
        var output = new ProcessOutput(process);
        await output.WaitForAsync("ready");

        process.Input.Write([0x03]);
        var exit = await process.Completion.WaitAsync(TimeSpan.FromSeconds(20));
        await output.CompletedAsync();

        Assert.Contains("interrupted", output.Output, StringComparison.Ordinal);
        Assert.Equal(7, exit.ExitCode);
    }

    [Fact]
    public async Task Stop_kills_the_whole_process_tree()
    {
        using var process = _runtime.Start(Shell("sleep 300 & sleep 301 & (sh -c 'sleep 302') & echo started; wait"));
        var output = new ProcessOutput(process);
        await output.WaitForAsync("started");
        await Task.Delay(200);
        var descendants = ProcessTree.Descendants(process.Id);
        Assert.True(descendants.Count >= 3, $"Expected the shell's children, found {descendants.Count}.");

        process.Stop();
        var exit = await process.Completion.WaitAsync(TimeSpan.FromSeconds(20));

        Assert.True(exit.WasStopped);
        Assert.Equal(ProcessExit.KillSignal, exit.Signal);
        Assert.False(exit.WasKilledBySystem);
        await WaitUntilGoneAsync(descendants);
    }

    [Fact]
    public async Task A_kill_it_did_not_ask_for_counts_as_the_system_s()
    {
        using var process = _runtime.Start(Shell("kill -9 $$"));

        var exit = await process.Completion.WaitAsync(TimeSpan.FromSeconds(20));

        Assert.Equal(9, exit.Signal);
        Assert.Equal(137, exit.ExitCode);
        Assert.True(exit.WasKilledBySystem);
    }

    [Fact]
    public async Task WaitAsync_stops_the_program_when_cancelled()
    {
        using var process = _runtime.Start(Shell("sleep 300"));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        var exit = await process.WaitAsync(cancellation.Token).WaitAsync(TimeSpan.FromSeconds(20));

        Assert.True(exit.WasStopped);
    }

    [Fact]
    public async Task Exited_gives_the_exit_code_as_language_server_hosts_want_it()
    {
        using var process = _runtime.Start(Shell("exit 5"));

        Assert.Equal(5, await process.Exited.WaitAsync(TimeSpan.FromSeconds(20)));
    }

    [Fact]
    public async Task Programs_start_while_collections_keep_stopping_the_process()
    {
        // posix_spawn runs without a GC transition (see UnixNative.posix_spawn): collections wait for it rather than
        // signalling a forking thread. Four threads start programs while a fifth makes garbage; none may hang or fail.
        using var stop = new CancellationTokenSource();
        var collections = GC.CollectionCount(0);
        var churn = Task.Factory.StartNew(
            () =>
            {
                var recent = new byte[64][];
                for (var i = 0; !stop.IsCancellationRequested; i++)
                {
                    recent[i & 63] = new byte[16 * 1024];
                }

                GC.KeepAlive(recent);
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        var exits = new System.Collections.Concurrent.ConcurrentBag<ProcessExit>();
        try
        {
            await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(async () =>
            {
                for (var i = 0; i < 50; i++)
                {
                    using var process = _runtime.Start(new ProcessStartSpec { Program = "/bin/true" });
                    exits.Add(await process.Completion.WaitAsync(TimeSpan.FromSeconds(20)));
                }
            }))).WaitAsync(TimeSpan.FromSeconds(90));
        }
        finally
        {
            await stop.CancelAsync();
            await churn;
        }

        Assert.Equal(200, exits.Count);
        Assert.All(exits, e => Assert.True(e.Succeeded, e.Description));
        Assert.True(GC.CollectionCount(0) > collections, "No collection ran while the programs started.");
    }

    [Fact]
    public void Resolves_commands_to_full_paths()
    {
        var shell = _runtime.Resolve("sh");

        Assert.NotNull(shell);
        Assert.True(Path.IsPathRooted(shell));
        Assert.True(File.Exists(shell));
        Assert.Null(_runtime.Resolve("tabstudio-no-such-program"));
        Assert.Equal(shell, _runtime.Resolve(shell));
    }

    [Fact]
    public void A_missing_program_is_reported_in_words()
    {
        var error = Assert.Throws<ToolchainRuntimeException>(() => _runtime.Start(new ProcessStartSpec { Program = "tabstudio-no-such-program" }));

        Assert.Contains("tabstudio-no-such-program", error.Message, StringComparison.Ordinal);
        Assert.False(_runtime.HasCommand("tabstudio-no-such-program"));
        Assert.True(_runtime.HasCommand("sh"));
    }

    [Fact]
    public async Task Runs_python_from_the_machine_reading_its_input()
    {
        Assert.True(_runtime.HasCommand("python3"), "The CI image has python3.");
        using var process = _runtime.Start(new ProcessStartSpec { Program = "python3", Arguments = ["-u", "-c", "name = input('Name? '); print(f'Hello, {name}!')"] });
        var output = new ProcessOutput(process);

        process.Input.Write("Tablet\n"u8);
        var exit = await process.Completion.WaitAsync(TimeSpan.FromSeconds(30));
        await output.CompletedAsync();

        Assert.True(exit.Succeeded, output.Error);
        Assert.Equal("Name? Hello, Tablet!\n", output.Output);
    }

    [Fact]
    public void Maps_a_printed_path_to_the_file_on_the_device()
    {
        var file = _temp.Write("app/main.py", "print(1)\n");

        Assert.Equal(file, _runtime.ToDevicePath(file, null));
        Assert.Equal(file, _runtime.ToDevicePath("main.py", Path.GetDirectoryName(file)));
        Assert.Null(_runtime.ToDevicePath("/no/such/file.py", null));
    }

    public void Dispose() => _temp.Dispose();

    private static ProcessStartSpec Shell(string script) => new() { Program = "/bin/sh", Arguments = ["-c", script] };

    private static async Task WaitUntilGoneAsync(IEnumerable<int> pids)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (pids.Any(IsAlive))
        {
            Assert.True(DateTime.UtcNow < deadline, "Processes of the stopped tree are still running.");
            await Task.Delay(50);
        }
    }

    private static bool IsAlive(int pid)
    {
        var stat = $"/proc/{pid}/stat";
        try
        {
            // A zombie waits for its parent to collect it, but runs no more.
            return File.Exists(stat) && !File.ReadAllText(stat).Contains(") Z ", StringComparison.Ordinal);
        }
        catch (IOException)
        {
            return false;
        }
    }
}
