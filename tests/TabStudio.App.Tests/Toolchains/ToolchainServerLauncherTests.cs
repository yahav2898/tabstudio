using TabStudio.App.Services.Toolchains;
using TabStudio.LanguageServers.Hosting;
using TabStudio.Testing;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.App.Tests.Toolchains;

/// <summary>
/// The language servers' launcher over the Toolchain Store's runtime: programs and their files are found through
/// the runtime, a server starts in its shared folder with its arguments and environment, and its streams, exit code,
/// killing and start failures pass through as they are.
/// </summary>
public sealed partial class ToolchainServerLauncherTests
{
    [Fact]
    public void Finds_programs_and_their_files_through_the_runtime()
    {
        var launcher = new ToolchainServerLauncher(new FakeRuntime());

        Assert.Equal("/usr/local/bin/pyright-langserver", launcher.Resolve("pyright-langserver"));
        Assert.Null(launcher.Resolve("gopls"));
        Assert.Null(launcher.Resolve(" "));
        Assert.Equal("/device/rootfs/usr/local/lib/node_modules/typescript/lib/tsserver.js", launcher.ToLocalPath("/usr/local/lib/node_modules/typescript/lib/tsserver.js"));
        Assert.Null(launcher.ToLocalPath("relative/tsserver.js"));
    }

    [Fact]
    public void Starts_a_server_in_its_shared_folder_with_its_arguments_and_environment()
    {
        using var folder = new TempDirectory();
        var runtime = new FakeRuntime();
        var environment = new Dictionary<string, string?> { ["PYTHONPATH"] = "/opt/tabstudio/python/debugpy", ["NODE_OPTIONS"] = null };

        using var process = new ToolchainServerLauncher(runtime).Start(new ServerStartInfo("/usr/local/bin/pyright-langserver", ["--stdio"], folder.Path, environment));

        var spec = Assert.Single(runtime.Started).Spec;
        Assert.Equal(["share " + folder.Path, "start /usr/local/bin/pyright-langserver"], runtime.Calls);
        Assert.Equal(["--stdio"], spec.Arguments);
        Assert.Equal(folder.Path, spec.WorkingDirectory);
        Assert.Same(environment, spec.Environment);
        Assert.Null(spec.Terminal);
    }

    [Fact]
    public async Task Passes_the_streams_the_exit_code_and_killing_through()
    {
        var runtime = new FakeRuntime();
        using var process = new ToolchainServerLauncher(runtime).Start(new ServerStartInfo("/usr/bin/python3", ["-m", "debugpy.adapter"], "/no/such/folder", new Dictionary<string, string?>()));
        var handle = runtime.Started[0].Handle;

        Assert.Equal(["start /usr/bin/python3"], runtime.Calls);
        Assert.Same(handle.Input, process.StandardInput);
        Assert.Same(handle.Output, process.StandardOutput);
        Assert.Same(handle.Error, process.StandardError);
        Assert.False(process.Exited.IsCompleted);

        process.Kill();

        Assert.True(handle.IsKilled);
        Assert.Equal(128 + 9, await process.Exited.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void A_program_the_runtime_cannot_start_fails_to_start_with_the_runtimes_reason()
    {
        var runtime = new FakeRuntime { Failure = "'pyright-langserver' is not installed in TabStudio's Linux environment. Install its toolchain from the Toolchain Store." };

        var error = Assert.Throws<ServerStartException>(() => new ToolchainServerLauncher(runtime).Start(new ServerStartInfo("pyright-langserver", [], "/work", new Dictionary<string, string?>())));

        Assert.Equal(runtime.Failure, error.Message);
        Assert.IsType<ToolchainRuntimeException>(error.InnerException);
    }

    [Fact]
    public async Task Runs_a_program_of_this_computer_with_its_three_streams()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var folder = new TempDirectory();
        var launcher = new ToolchainServerLauncher(new HostRuntime());

        using var process = launcher.Start(new ServerStartInfo(
            launcher.Resolve("sh")!,
            ["-c", "read line; echo \"got $line in $PWD\"; echo oops >&2; exit 3"],
            folder.Path,
            new Dictionary<string, string?>()));
        await process.StandardInput.WriteAsync("hello\n"u8.ToArray());
        await process.StandardInput.FlushAsync();
        using var output = new StreamReader(process.StandardOutput);
        using var error = new StreamReader(process.StandardError);

        Assert.Equal($"got hello in {folder.Path}", await output.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30)));
        Assert.Equal("oops", await error.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30)));
        Assert.Equal(3, await process.Exited.WaitAsync(TimeSpan.FromSeconds(30)));
    }

    /// <summary>A Linux environment whose programs are fakes: it records what it was asked to do.</summary>
    private sealed class FakeRuntime : IToolchainRuntime
    {
        public List<string> Calls { get; } = [];

        public List<(ProcessStartSpec Spec, FakeHandle Handle)> Started { get; } = [];

        public string? Failure { get; init; }

        public string DisplayName => "Fake Linux";

        public RuntimeKind Kind => RuntimeKind.Linux;

        public ToolchainProcess Start(ProcessStartSpec spec)
        {
            Calls.Add("start " + spec.Program);
            if (Failure is { } failure)
            {
                throw new ToolchainRuntimeException(failure);
            }

            var handle = new FakeHandle();
            Started.Add((spec, handle));
            return new ToolchainProcess(handle, spec);
        }

        public bool HasCommand(string command) => Resolve(command) is not null;

        public string? Resolve(string command) => command == "pyright-langserver" ? "/usr/local/bin/pyright-langserver" : null;

        public ProcessStartSpec Shell(string workingDirectory, TerminalSize size) => new() { Program = "/bin/ash", WorkingDirectory = workingDirectory, Terminal = size };

        public void ShareFolder(string folder) => Calls.Add("share " + folder);

        public string? ToDevicePath(string programPath, string? workingDirectory) =>
            workingDirectory is null && programPath.StartsWith('/') ? "/device/rootfs" + programPath : null;
    }

    /// <summary>A process that runs until it is killed.</summary>
    private sealed class FakeHandle : IProcessHandle
    {
        private readonly TaskCompletionSource<(int ExitCode, int? Signal)> _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Id => 4242;

        public bool IsTerminal => false;

        public Stream Input { get; } = new MemoryStream();

        public Stream Output { get; } = new MemoryStream();

        public Stream? Error { get; } = new MemoryStream();

        public bool IsKilled { get; private set; }

        public Task<(int ExitCode, int? Signal)> WaitForExitAsync() => _exit.Task;

        public void Resize(TerminalSize size)
        {
        }

        public void CloseInput()
        {
        }

        public void KillTree()
        {
            IsKilled = true;
            _exit.TrySetResult((0, 9));
        }

        public void Dispose()
        {
            Input.Dispose();
            Output.Dispose();
            Error?.Dispose();
        }
    }
}
