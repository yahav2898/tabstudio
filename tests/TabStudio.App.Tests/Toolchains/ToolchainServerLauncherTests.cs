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
        Assert.Equal(environment, spec.Environment);
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

    [Theory]
    [InlineData("arduino")]
    [InlineData("esp")]
    [InlineData("zephyr")]
    [InlineData("pico")]
    [InlineData("cube")]
    public void Embedded_clangd_uses_the_actual_planned_database_directory_before_the_first_build(string kind)
    {
        using var folder = new TempDirectory();
        CreateEmbeddedProject(folder, kind);
        var runtime = new FakeRuntime();
        string[] original = ["--background-index", "--header-insertion=never"];
        var expected = Path.Combine(folder.Path, ".tabstudio", "embedded", "build");
        Assert.False(Directory.Exists(expected));

        using var process = new ToolchainServerLauncher(runtime).Start(new ServerStartInfo("/usr/bin/clangd", original,
            folder.Path, new Dictionary<string, string?> { ["SDK_PATH"] = "/opt/sdk" }));

        var spec = Assert.Single(runtime.Started).Spec;
        Assert.Equal([.. original, "--compile-commands-dir=" + expected], spec.Arguments);
        Assert.Equal(folder.Path, spec.WorkingDirectory);
        Assert.Equal("/opt/sdk", spec.Environment["SDK_PATH"]);
        Assert.Equal(["share " + folder.Path, "start /usr/bin/clangd"], runtime.Calls);
        Assert.DoesNotContain(spec.Arguments, argument => argument.StartsWith("--query-driver", StringComparison.Ordinal));
        Assert.True(Directory.Exists(expected));
        Assert.False(File.Exists(Path.Combine(expected, "compile_commands.json")));
        Assert.False(File.Exists(Path.Combine(folder.Path, ".clangd")));
    }

    [Theory]
    [InlineData("--compile-commands-dir=/reviewed/database")]
    [InlineData("-compile-commands-dir=/reviewed/database")]
    [InlineData("--compile-commands-dir")]
    [InlineData("-compile-commands-dir")]
    public void Explicit_clangd_database_arguments_are_preserved(string option)
    {
        using var folder = new TempDirectory();
        CreateEmbeddedProject(folder, "pico");
        var runtime = new FakeRuntime();
        string[] arguments = option.Contains('=') ? ["--background-index", option] : ["--background-index", option, "/reviewed/database"];
        using var process = new ToolchainServerLauncher(runtime).Start(new ServerStartInfo("/usr/bin/clangd", arguments, folder.Path, new Dictionary<string, string?>()));
        Assert.Equal(arguments, Assert.Single(runtime.Started).Spec.Arguments);
        Assert.False(Directory.Exists(Path.Combine(folder.Path, ".tabstudio", "embedded", "build")));
    }

    [Theory]
    [InlineData("compile_commands.json", "[{\"command\":\"reviewed-compiler -DUSER_FLAGS\"}]")]
    [InlineData(".clangd", "CompileFlags: { CompilationDatabase: reviewed-build }\n")]
    public void Existing_user_database_and_yaml_database_choices_keep_normal_clangd_discovery(string name, string contents)
    {
        using var folder = new TempDirectory();
        CreateEmbeddedProject(folder, "pico");
        var path = folder.Write(name, contents);
        var runtime = new FakeRuntime();
        string[] arguments = ["--background-index"];
        using var process = new ToolchainServerLauncher(runtime).Start(new ServerStartInfo("/usr/bin/clangd", arguments, folder.Path, new Dictionary<string, string?>()));
        Assert.Equal(arguments, Assert.Single(runtime.Started).Spec.Arguments);
        Assert.Equal(contents, File.ReadAllText(path));
        Assert.False(Directory.Exists(Path.Combine(folder.Path, ".tabstudio", "embedded", "build")));
    }

    [Fact]
    public void An_existing_ancestor_database_keeps_clangds_normal_discovery_authoritative()
    {
        using var folder = new TempDirectory();
        var database = folder.Write("compile_commands.json", "[]\n");
        var project = Path.Combine(folder.Path, "firmware");
        Directory.CreateDirectory(project);
        File.WriteAllText(Path.Combine(project, "CMakeLists.txt"), "include(pico_sdk_import.cmake)\npico_sdk_init()\n");
        var runtime = new FakeRuntime();
        using var process = new ToolchainServerLauncher(runtime).Start(new ServerStartInfo("/usr/bin/clangd-18", [], project, new Dictionary<string, string?>()));
        Assert.Empty(Assert.Single(runtime.Started).Spec.Arguments);
        Assert.Equal("[]\n", File.ReadAllText(database));
        Assert.False(Directory.Exists(Path.Combine(project, ".tabstudio")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Invalid_or_oversized_embedded_configuration_cannot_prevent_a_language_server_start(bool oversized)
    {
        using var folder = new TempDirectory();
        CreateEmbeddedProject(folder, "pico");
        folder.Write(".tabstudio/embedded.json", oversized ? new string('x', 65537) : "{ invalid JSON");
        var runtime = new FakeRuntime();
        using var process = new ToolchainServerLauncher(runtime).Start(new ServerStartInfo("/usr/bin/clangd", ["--background-index"], folder.Path, new Dictionary<string, string?>()));
        Assert.Equal(["--background-index"], Assert.Single(runtime.Started).Spec.Arguments);
        Assert.False(Directory.Exists(Path.Combine(folder.Path, ".tabstudio", "embedded", "build")));
    }

    [Fact]
    public void Other_servers_do_not_receive_clangd_options_or_create_build_directories()
    {
        using var folder = new TempDirectory();
        CreateEmbeddedProject(folder, "pico");
        var runtime = new FakeRuntime();
        using var process = new ToolchainServerLauncher(runtime).Start(new ServerStartInfo("rust-analyzer", [], folder.Path, new Dictionary<string, string?>()));
        Assert.Empty(Assert.Single(runtime.Started).Spec.Arguments);
        Assert.False(Directory.Exists(Path.Combine(folder.Path, ".tabstudio", "embedded", "build")));
    }

    [Fact]
    public void A_symlinked_generated_directory_does_not_redirect_automatic_clangd_setup()
    {
        if (OperatingSystem.IsWindows()) return;
        using var folder = new TempDirectory();
        using var other = new TempDirectory();
        CreateEmbeddedProject(folder, "pico");
        Directory.CreateSymbolicLink(Path.Combine(folder.Path, ".tabstudio"), other.Path);
        var runtime = new FakeRuntime();
        using var process = new ToolchainServerLauncher(runtime).Start(new ServerStartInfo("/usr/bin/clangd", [], folder.Path, new Dictionary<string, string?>()));
        Assert.Empty(Assert.Single(runtime.Started).Spec.Arguments);
        Assert.False(Directory.Exists(Path.Combine(other.Path, "embedded", "build")));
    }

    private static void CreateEmbeddedProject(TempDirectory folder, string kind)
    {
        switch (kind)
        {
            case "arduino":
                folder.Write(Path.GetFileName(folder.Path) + ".ino", "void setup(){} void loop(){}\n");
                folder.Write(".tabstudio/embedded.json", "{\"board\":\"arduino:avr:uno\"}");
                break;
            case "esp":
                folder.Write("CMakeLists.txt", "include($ENV{IDF_PATH}/tools/cmake/project.cmake)\n");
                folder.Write("sdkconfig", "CONFIG_IDF_TARGET=\"esp32c3\"\n");
                break;
            case "zephyr":
                folder.Write("CMakeLists.txt", "find_package(Zephyr REQUIRED HINTS $ENV{ZEPHYR_BASE})\n");
                folder.Write(".tabstudio/embedded.json", "{\"board\":\"nrf52840dk/nrf52840\"}");
                break;
            case "cube":
                folder.Write("blink.ioc", "Mcu.CPN=STM32F401RE\n");
                folder.Write("CMakeLists.txt", "project(blink C ASM)\n");
                break;
            default:
                folder.Write("CMakeLists.txt", "include(pico_sdk_import.cmake)\npico_sdk_init()\n");
                break;
        }
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
