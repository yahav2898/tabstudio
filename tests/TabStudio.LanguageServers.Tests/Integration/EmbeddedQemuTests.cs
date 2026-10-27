using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using TabStudio.LanguageServers.Configuration;
using TabStudio.LanguageServers.DebugAdapters;
using TabStudio.LanguageServers.Hosting;
using TabStudio.Testing;
using Xunit.Abstractions;

namespace TabStudio.LanguageServers.Tests.Integration;

/// <summary>Real cross-compiler → emulator → GDB DAP → TabStudio client sessions, with no probe or physical MCU.</summary>
public sealed class EmbeddedQemuTests(ITestOutputHelper output)
{
    [EmbeddedEmulatorTheory]
    [InlineData("cortex")]
    [InlineData("riscv")]
    [InlineData("avr")]
    public async Task Firmware_hits_a_source_breakpoint_reads_locals_and_steps_through_the_real_DAP_client(string architecture)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var cancellation = deadline.Token;
        using var folder = new TempDirectory();
        var fixtures = Path.Combine(RepositoryRoot(), "build", "embedded", "emulators");
        var source = folder.Write("checkpoint.c", File.ReadAllText(Path.Combine(fixtures, "checkpoint.c")));
        var line = File.ReadAllLines(source).Select((text, index) => (text, index)).Single(p => p.text.Contains("TABSTUDIO_BREAKPOINT", StringComparison.Ordinal)).index + 1;
        var firmware = Path.Combine(folder.Path, "firmware.elf");
        var port = architecture == "avr" ? 1234 : FreePort();
        string compiler;
        string emulator;
        List<string> compile;
        List<string> emulate;
        if (architecture == "cortex")
        {
            compiler = "arm-none-eabi-gcc";
            var start = folder.Write("start.c", File.ReadAllText(Path.Combine(fixtures, "cortex-start.c")));
            var linker = folder.Write("firmware.ld", File.ReadAllText(Path.Combine(fixtures, "cortex.ld")));
            compile = ["-mcpu=cortex-m3", "-mthumb", "-g3", "-O0", "-ffreestanding", "-nostdlib", "-Wl,-T," + linker, source, start, "-o", firmware];
            emulator = "qemu-system-arm";
            emulate = ["-M", "lm3s6965evb", "-kernel", firmware, "-display", "none", "-serial", "none", "-monitor", "none", "-S", "-gdb", "tcp:127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture)];
        }
        else if (architecture == "riscv")
        {
            compiler = "riscv64-unknown-elf-gcc";
            var start = folder.Write("start.S", File.ReadAllText(Path.Combine(fixtures, "riscv-start.S")));
            var linker = folder.Write("firmware.ld", File.ReadAllText(Path.Combine(fixtures, "riscv.ld")));
            compile = ["-march=rv32imac", "-mabi=ilp32", "-msmall-data-limit=0", "-mno-relax", "-g3", "-O0", "-ffreestanding", "-nostdlib", "-Wl,-T," + linker, source, start, "-o", firmware];
            emulator = "qemu-system-riscv32";
            emulate = ["-M", "virt", "-bios", "none", "-kernel", firmware, "-display", "none", "-serial", "none", "-monitor", "none", "-S", "-gdb", "tcp:127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture)];
        }
        else
        {
            compiler = "avr-gcc";
            compile = ["-mmcu=atmega328p", "-gdwarf-2", "-O0", source, "-o", firmware];
            emulator = "simavr";
            emulate = ["-m", "atmega328p", "-f", "16000000", "-g", firmware];
        }

        await CompileAsync(compiler, compile, folder.Path, cancellation);
        using var target = Start(emulator, emulate, folder.Path);
        var stdout = target.StandardOutput.ReadToEndAsync(cancellation);
        var stderr = target.StandardError.ReadToEndAsync(cancellation);
        try
        {
            await WaitForPortAsync(target, port, cancellation);
            var debuggerName = architecture == "avr" ? Environment.GetEnvironmentVariable("TABSTUDIO_AVR_GDB") ?? "avr-gdb-dap" : "gdb-multiarch";
            var debugger = ProcessServerLauncher.Instance.Resolve(debuggerName) ?? throw new InvalidOperationException(debuggerName + " is required. Ubuntu gdb-multiarch omits AVR; use build/embedded/build-avr-gdb.sh.");
            var adapterArguments = new List<string> { "--quiet", "--nx", "--interpreter=dap" };
            var dataDirectory = architecture == "avr" ? Environment.GetEnvironmentVariable("TABSTUDIO_AVR_GDB_DATA_DIRECTORY") : Environment.GetEnvironmentVariable("TABSTUDIO_GDB_DATA_DIRECTORY");
            if (dataDirectory is { Length: > 0 } gdbData)
            {
                adapterArguments.Add("--data-directory=" + gdbData);
            }

            var adapter = new DebugAdapterDefinition { Id = "embedded-gdb-ci", Name = "GDB DAP", Command = debugger, Arguments = adapterArguments };
            await using var session = await DebugAdapterSession.StartAsync(adapter, debugger, folder.Path, ProcessServerLauncher.Instance,
                new DebugAdapterSessionOptions { StartTimeout = TimeSpan.FromSeconds(20), TraceMessages = true, StartupLog = Log }, cancellation);
            var stopped = Channel.CreateUnbounded<DapStoppedEventArgs>();
            session.Stopped += (_, e) => stopped.Writer.TryWrite(e);
            session.AdapterLog += (_, text) => Log(text);
            session.OutputReceived += (_, e) => Log(e.Output);
            var configuration = new LaunchConfiguration
            {
                Name = architecture + " emulator", Type = adapter.Id, Request = "attach",
                Properties = new JsonObject { ["name"] = architecture, ["type"] = adapter.Id, ["request"] = "attach", ["program"] = firmware, ["target"] = "127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) }
            };
            var breakpoints = new Dictionary<string, IReadOnlyList<DapSourceBreakpoint>> { [source] = [new(line)] };
            var applied = await session.LaunchAsync(configuration, breakpoints, [], cancellation);
            Assert.True(Assert.Single(applied[source]).IsVerified);
            while (stopped.Reader.TryRead(out _)) { }
            var threads = await session.GetThreadsAsync(cancellation);
            var thread = Assert.Single(threads).Id;
            await session.ContinueAsync(thread, cancellation);
            var hit = await ReadStopAsync(stopped.Reader, "breakpoint", cancellation);
            thread = hit.ThreadId ?? thread;
            var frames = await session.GetStackTraceAsync(thread, cancellation);
            Assert.Equal("checkpoint", frames[0].Name);
            Assert.Equal(line, frames[0].Line);
            Assert.Equal("checkpoint.c", Path.GetFileName(frames[0].FilePath));
            Assert.Contains(frames, f => f.Name == "main");
            var scopes = await session.GetScopesAsync(frames[0].Id, cancellation);
            var variables = new List<DapVariable>();
            foreach (var scope in scopes.Where(s => !s.IsExpensive))
            {
                variables.AddRange(await session.GetVariablesAsync(scope.VariablesReference, cancellation));
            }

            Assert.Contains(variables, v => v.Name == "local" && v.Value.Contains("42", StringComparison.Ordinal));
            await session.StepOverAsync(thread, cancellation);
            await ReadStopAsync(stopped.Reader, "step", cancellation);
            frames = await session.GetStackTraceAsync(thread, cancellation);
            var value = await session.EvaluateAsync("counter", frames[0].Id, DapEvaluateContext.Watch, cancellation);
            Assert.Contains("42", value.Result, StringComparison.Ordinal);
            var disassembly = await session.EvaluateAsync("x/4i $pc", frames[0].Id, DapEvaluateContext.Repl, cancellation);
            Assert.False(string.IsNullOrWhiteSpace(disassembly.Result));
            Assert.True(session.Capabilities.Disassemble, "The real GDB DAP adapter must advertise native disassembly support.");
            var pc = await session.EvaluateAsync("(unsigned long)$pc", frames[0].Id, DapEvaluateContext.Watch, cancellation);
            var pcText = pc.Result.Trim();
            var pcAddress = pcText.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? ulong.Parse(pcText.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture)
                : ulong.Parse(pcText, NumberStyles.None, CultureInfo.InvariantCulture);
            var instructions = await session.DisassembleAsync(pcAddress, 4, cancellation);
            Assert.Equal(4, instructions.Count);
            Assert.Contains(instructions, instruction => !instruction.IsInvalid && !string.IsNullOrWhiteSpace(instruction.Instruction));
            if (architecture != "avr")
            {
                var address = await session.EvaluateAsync("(unsigned long)&counter", frames[0].Id, DapEvaluateContext.Watch, cancellation);
                var memory = await session.ReadMemoryAsync(ulong.Parse(address.Result, CultureInfo.InvariantCulture), 4, cancellation);
                Assert.Equal(new byte[] { 42, 0, 0, 0 }, memory);
            }
        }
        finally
        {
            if (!target.HasExited)
            {
                target.Kill(entireProcessTree: true);
            }

            await target.WaitForExitAsync(CancellationToken.None);
            Log(await stdout);
            Log(await stderr);
        }
    }

    private static async Task<DapStoppedEventArgs> ReadStopAsync(ChannelReader<DapStoppedEventArgs> reader, string reason, CancellationToken token)
    {
        while (true)
        {
            var value = await reader.ReadAsync(token);
            if (value.Reason == reason)
            {
                return value;
            }
        }
    }

    private static async Task CompileAsync(string program, List<string> args, string directory, CancellationToken token)
    {
        using var process = Start(program, args, directory);
        var stdout = process.StandardOutput.ReadToEndAsync(token);
        var stderr = process.StandardError.ReadToEndAsync(token);
        try
        {
            await process.WaitForExitAsync(token);
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
    }

    private static Process Start(string program, IReadOnlyList<string> arguments, string directory)
    {
        var executable = ProcessServerLauncher.Instance.Resolve(program) ?? throw new InvalidOperationException("Missing required embedded CI tool: " + program);
        var start = new ProcessStartInfo(executable) { WorkingDirectory = directory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        return Process.Start(start) ?? throw new InvalidOperationException("Could not start " + program);
    }

    private static async Task WaitForPortAsync(Process target, int port, CancellationToken token)
    {
        while (!target.HasExited)
        {
            // A probe TCP connection consumes simavr's first debugger session and lets its CPU run on
            // disconnect. Linux exposes listener state without touching the protocol connection.
            if (File.Exists("/proc/net/tcp"))
            {
                var hex = port.ToString("X4", CultureInfo.InvariantCulture);
                if (File.ReadLines("/proc/net/tcp").Skip(1).Any(line =>
                {
                    var columns = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    return columns.Length > 3 && columns[1].EndsWith(":" + hex, StringComparison.OrdinalIgnoreCase) && columns[3] == "0A";
                }))
                {
                    return;
                }

                await Task.Delay(50, token);
                continue;
            }

            try
            {
                using var socket = new TcpClient();
                await socket.ConnectAsync(IPAddress.Loopback, port, token);
                return;
            }
            catch (SocketException)
            {
                await Task.Delay(50, token);
            }
        }

        throw new InvalidOperationException("The emulator exited before opening its GDB server.");
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string RepositoryRoot()
    {
        if (Environment.GetEnvironmentVariable("TABSTUDIO_REPO_ROOT") is { Length: > 0 } root)
        {
            return root;
        }

        for (var folder = new DirectoryInfo(Directory.GetCurrentDirectory()); folder is not null; folder = folder.Parent)
        {
            if (Directory.Exists(Path.Combine(folder.FullName, "build", "embedded", "emulators")))
            {
                return folder.FullName;
            }
        }

        throw new InvalidOperationException("Set TABSTUDIO_REPO_ROOT to the checked-out TabStudio source.");
    }

    private void Log(string text)
    {
        try { output.WriteLine(text); }
        catch (InvalidOperationException) { /* The test already completed. */ }
    }
}
