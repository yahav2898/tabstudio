using System.Net;
using System.Text;
using TabStudio.Testing;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Downloads;
using TabStudio.Toolchains.Embedded;
using TabStudio.Toolchains.Embedded.Installation;
using TabStudio.Toolchains.Installation;
using TabStudio.Toolchains.Linux;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Toolchains.Tests.Embedded;

public sealed class EmbeddedGlibcTests
{
    [Theory]
    [InlineData("pio", "platformio")]
    [InlineData("/usr/local/bin/pio", "platformio")]
    [InlineData("/opt/tabstudio/platformio/bin/python", "platformio")]
    [InlineData("arduino-cli", "arduino")]
    [InlineData("/usr/local/bin/arduino-cli", "arduino")]
    [InlineData("/root/.arduino15/packages/arduino/tools/avr-gcc/bin/avr-gcc", "arduino")]
    [InlineData("probe-rs", "probe-rs")]
    [InlineData("cargo-embed", "probe-rs")]
    [InlineData("idf.py", "esp-idf")]
    [InlineData("west", "zephyr")]
    [InlineData("/opt/tabstudio/pico-sdk/tools/pioasm", "pico-sdk")]
    [InlineData("/opt/tabstudio/embedded-rust/bin/cargo", "embedded-rust")]
    [InlineData("nrfutil", "embedded-nrfutil")]
    [InlineData("/usr/local/bin/nrfutil", "embedded-nrfutil")]
    [InlineData("/opt/tabstudio/nordic-dfu/lib/nrfutil-nrf5sdk-tools/pc_nrfutil_legacy_v6.1.7", "embedded-nrfutil")]
    [InlineData("/usr/local/bin/cargo-flash", "probe-rs")]
    [InlineData("/opt/tabstudio/platformio-evil/pio", null)]
    [InlineData("python3", null)]
    [InlineData("flutter", null)]
    [InlineData("cargo", null)]
    public void Only_owned_frontend_commands_and_exact_private_paths_select_glibc(string command, string? id) =>
        Assert.Equal(id, EmbeddedGlibcRuntime.ToolchainForCommand(command));

    [Fact]
    public void Runtime_preserves_USB_descriptors_mounts_arguments_and_disables_telemetry()
    {
        var primary = new FakeRuntime();
        var guest = new FakeRuntime();
        var runtime = new EmbeddedGlibcRuntime(primary, guest, _ => true, () => true);
        var command = new ProcessStartSpec
        {
            Program = "arduino-cli", Arguments = ["upload", "--fqbn", "arduino:avr:uno"],
            WorkingDirectory = "/workspace/board", InheritedDescriptors = new Dictionary<int, int> { [73] = 3 },
            Mounts = [new ProcessMount("/private/usb", "/sys/bus/usb/devices")],
            Environment = new Dictionary<string, string?> { ["LD_PRELOAD"] = UsbBridgeInstallation.ToolsDirectory + "/libusb-fd.so", ["PLATFORMIO_SETTING_ENABLE_TELEMETRY"] = "yes" },
        };
        using var process = runtime.Start(command);
        var started = Assert.Single(guest.Started);
        Assert.Empty(primary.Started);
        Assert.Same(command.Arguments, started.Arguments);
        Assert.Same(command.InheritedDescriptors, started.InheritedDescriptors);
        Assert.Same(command.Mounts, started.Mounts);
        Assert.Equal("no", started.Environment["PLATFORMIO_SETTING_ENABLE_TELEMETRY"]);
        Assert.Equal(command.Environment["LD_PRELOAD"], started.Environment["LD_PRELOAD"]);
    }

    [Fact]
    public void The_outer_Python_USB_serial_worker_follows_its_explicit_glibc_marker()
    {
        var primary = new FakeRuntime();
        var guest = new FakeRuntime();
        var runtime = new EmbeddedGlibcRuntime(primary, guest, id => id == "platformio", () => true);
        using var process = runtime.Start(new ProcessStartSpec
        {
            Program = "python3", Arguments = [UsbBridgeInstallation.ToolsDirectory + "/serial_bridge.py", "--", "pio", "run"],
            Environment = new Dictionary<string, string?> { [EmbeddedGlibcRuntime.RoutingMarker] = "1" },
        });
        Assert.Equal("python3", Assert.Single(guest.Started).Program);
        Assert.Empty(primary.Started);
    }

    [Fact]
    public void Unready_SDKs_refuse_to_fall_back_to_a_different_libc_or_CPU()
    {
        var runtime = new EmbeddedGlibcRuntime(new FakeRuntime(), new FakeRuntime(), _ => false, () => true);
        Assert.Null(runtime.Resolve("probe-rs"));
        Assert.Throws<ToolchainRuntimeException>(() => runtime.Start(new ProcessStartSpec { Program = "probe-rs" }));
        Assert.NotNull(runtime.Resolve("python3"));
    }

    [Fact]
    public void Clangd_uses_the_same_headers_as_the_PlatformIO_project_without_rerouting_unrelated_commands()
    {
        using var project = new TempDirectory();
        project.Write("platformio.ini", "[env:uno]\nboard=uno");
        var primary = new FakeRuntime();
        var guest = new FakeRuntime();
        var runtime = new EmbeddedGlibcRuntime(primary, guest, _ => true, () => true);
        using var languageServer = runtime.Start(new ProcessStartSpec { Program = "/usr/bin/clangd", WorkingDirectory = project.Path });
        using var python = runtime.Start(new ProcessStartSpec { Program = "python3", WorkingDirectory = project.Path });
        Assert.Equal("/usr/bin/clangd", Assert.Single(guest.Started).Program);
        Assert.Equal("python3", Assert.Single(primary.Started).Program);
    }

    [Fact]
    public void Glibc_headers_win_over_same_guest_paths_in_Alpine_for_nested_embedded_source_and_LSP_navigation()
    {
        using var project = new TempDirectory();
        project.Write("app/platformio.ini", "[env:uno]\nboard=uno");
        project.Write("app/src/drivers/sensor.cpp", "");
        var alpineHeader = project.Write("alpine/usr/include/stdint.h", "alpine ABI");
        var glibcHeader = project.Write("glibc/usr/include/stdint.h", "glibc ABI");
        var primary = new FakeRuntime { Rootfs = project.Resolve("alpine") };
        var guest = new FakeRuntime { Rootfs = project.Resolve("glibc") };
        var runtime = new EmbeddedGlibcRuntime(primary, guest, _ => true, () => true);
        var nested = project.Resolve("app/src/drivers");
        Assert.Equal(glibcHeader, runtime.ToDevicePath("/usr/include/stdint.h", nested));
        using var server = runtime.Start(new ProcessStartSpec { Program = "clangd", WorkingDirectory = nested });
        Assert.Equal(glibcHeader, runtime.ToDevicePath("/usr/include/stdint.h", null));
        using var desktopServer = runtime.Start(new ProcessStartSpec { Program = "clangd", WorkingDirectory = project.Resolve("alpine") });
        Assert.Equal(alpineHeader, runtime.ToDevicePath("/usr/include/stdint.h", null));
    }

    [Fact]
    public void A_glibc_clangd_context_does_not_hide_another_languages_sources_in_Alpine()
    {
        using var project = new TempDirectory();
        project.Write("app/platformio.ini", "[env:uno]\nboard=uno");
        var rustSource = project.Write("alpine/root/.cargo/registry/src/serde/lib.rs", "// Rust source");
        var primary = new FakeRuntime { Rootfs = project.Resolve("alpine") };
        var guest = new FakeRuntime { Rootfs = project.Resolve("glibc") };
        var runtime = new EmbeddedGlibcRuntime(primary, guest, _ => true, () => true);
        using var server = runtime.Start(new ProcessStartSpec { Program = "clangd", WorkingDirectory = project.Resolve("app") });
        Assert.Equal(rustSource, runtime.ToDevicePath("/root/.cargo/registry/src/serde/lib.rs", null));
    }

    [Fact]
    public void Native_frontends_do_not_force_Python_safe_path_on_vendor_script_children()
    {
        var primary = new FakeRuntime();
        var guest = new FakeRuntime();
        var runtime = new EmbeddedGlibcRuntime(primary, guest, _ => true, () => true);
        using var probe = runtime.Start(new ProcessStartSpec { Program = "probe-rs" });
        Assert.DoesNotContain("PYTHONSAFEPATH", Assert.Single(guest.Started).Environment.Keys);
        using var explicitSetting = runtime.Start(new ProcessStartSpec
        {
            Program = "arduino-cli", Environment = new Dictionary<string, string?> { ["PYTHONSAFEPATH"] = "0" },
        });
        Assert.Equal("0", guest.Started[^1].Environment["PYTHONSAFEPATH"]);
    }

    [Fact]
    public void SDK_commands_keep_vendor_Python_imports_and_limit_generators_even_with_a_user_PATH()
    {
        var guest = new FakeRuntime();
        var runtime = new EmbeddedGlibcRuntime(new FakeRuntime(), guest, _ => true, () => true);
        using var process = runtime.Start(new ProcessStartSpec
        {
            Program = "idf.py", Arguments = ["build"],
            Environment = new Dictionary<string, string?>
            {
                ["PATH"] = "/workspace/custom-tools:/usr/bin", [EmbeddedSdkBootstrap.JobsVariable] = "5",
            },
        });
        var started = Assert.Single(guest.Started);
        Assert.Equal(EmbeddedSdkBootstrap.BuildToolsDirectory + ":/workspace/custom-tools:/usr/bin", started.Environment["PATH"]);
        Assert.Equal("5", started.Environment[EmbeddedSdkBootstrap.JobsVariable]);
        Assert.Equal("/opt/tabstudio/esp-idf", started.Environment["IDF_PATH"]);
        Assert.DoesNotContain("PYTHONSAFEPATH", started.Environment.Keys);
    }

    [Fact]
    public void Bare_metal_Rust_uses_its_native_cross_targets_while_desktop_Cargo_keeps_the_existing_toolchain()
    {
        using var project = new TempDirectory();
        project.Write("firmware/Cargo.toml", "[package]\nname='firmware'\nversion='0.1.0'");
        project.Write("firmware/.cargo/config.toml", "[build]\ntarget='thumbv7em-none-eabihf'");
        project.Write("firmware/src/main.rs", "#![no_std]\n#![no_main]");
        project.Write("desktop/Cargo.toml", "[package]\nname='desktop'\nversion='0.1.0'");
        var primary = new FakeRuntime();
        var guest = new FakeRuntime();
        var runtime = new EmbeddedGlibcRuntime(primary, guest, _ => true, () => true);
        foreach (var program in new[] { "cargo", "rustc", "rust-analyzer", "/usr/bin/rust-analyzer" })
        {
            using var embedded = runtime.Start(new ProcessStartSpec { Program = program, WorkingDirectory = project.Resolve("firmware/src") });
            using var desktop = runtime.Start(new ProcessStartSpec { Program = program, WorkingDirectory = project.Resolve("desktop") });
        }
        Assert.Equal(4, guest.Started.Count);
        Assert.Equal(4, primary.Started.Count);
        Assert.All(guest.Started, command => Assert.Equal("1", command.Environment[EmbeddedGlibcRuntime.RoutingMarker]));
        Assert.All(guest.Started, command => Assert.StartsWith("/opt/tabstudio/embedded-rust/bin/", command.Program, StringComparison.Ordinal));
        Assert.All(guest.Started, command => Assert.Equal("true", command.Environment["CARGO_NET_GIT_FETCH_WITH_CLI"]));
        Assert.All(primary.Started, command => Assert.DoesNotContain("CARGO_NET_GIT_FETCH_WITH_CLI", command.Environment.Keys));
        Assert.Equal("/opt/tabstudio/embedded-rust/bin/rust-analyzer", runtime.ResolveForDirectory("rust-analyzer", project.Resolve("firmware/src")));
        Assert.Equal("rust-analyzer", runtime.ResolveForDirectory("rust-analyzer", project.Resolve("desktop")));
        var unready = new EmbeddedGlibcRuntime(primary, guest, _ => false, () => true);
        Assert.Null(unready.ResolveForDirectory("rust-analyzer", project.Resolve("firmware/src")));
    }

    [Fact]
    public async Task Source_SDK_installation_verifies_its_commit_and_firmware_before_readiness_and_removes_owned_caches()
    {
        using var fixture = new InstallerFixture();
        var sdk = ToolchainCatalog.Default.Find("pico-sdk")!;
        var recipe = EmbeddedSdkBootstrap.Recipes.Get(sdk.Id)!;
        fixture.Guest.CommandOutput = spec => spec.Program == "git" && spec.Arguments.Contains("rev-parse") ? recipe.Revision : null;
        var failure = await Assert.ThrowsAsync<ToolchainInstallException>(() => fixture.Installer.InstallAsync(sdk, null, CancellationToken.None));
        Assert.Contains("required firmware", failure.Message, StringComparison.Ordinal);
        Assert.False(fixture.Installer.IsToolchainReady(sdk.Id));
        fixture.Guest.BeforeStart = spec =>
        {
            if (spec.Program == "cmake" && spec.Arguments.Contains("--build"))
            {
                foreach (var artifact in recipe.RequiredArtifacts)
                {
                    var path = Path.Combine(fixture.Installer.Rootfs, artifact.TrimStart('/'));
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(path, "firmware produced by test runtime");
                }
            }
        };
        await fixture.Installer.InstallAsync(sdk, null, CancellationToken.None);
        Assert.True(fixture.Installer.IsToolchainReady(sdk.Id));
        Assert.Contains(fixture.Guest.Started, spec => spec.Program == "git" && spec.Arguments.Contains(recipe.Revision));
        Assert.Contains(fixture.Guest.Started, spec => spec.Program == "git" && spec.Arguments.Contains(recipe.Repository.AbsoluteUri));
        Assert.Contains(fixture.Guest.Started, spec => spec.Program == "git" && spec.Arguments.Contains("protocol.file.allow=never"));
        Assert.All(fixture.Guest.Started.Where(spec => spec.Program == "cmake"), spec =>
            Assert.StartsWith(EmbeddedSdkBootstrap.BuildToolsDirectory + ":", spec.Environment["PATH"], StringComparison.Ordinal));
        await fixture.Installer.RemoveAsync(sdk, [], null, CancellationToken.None);
        Assert.False(fixture.Installer.IsToolchainReady(sdk.Id));
        Assert.False(Directory.Exists(Path.Combine(fixture.Installer.Rootfs, recipe.SourceDirectory.TrimStart('/'))));
        Assert.All(recipe.Removes, path => Assert.False(Directory.Exists(Path.Combine(fixture.Installer.Rootfs, path.TrimStart('/')))));
        Assert.True(File.Exists(Path.Combine(fixture.Installer.Rootfs, "opt/tabstudio/embedded/serial-posix.so")));
    }

    [Fact]
    public async Task A_source_checkout_that_reports_the_wrong_commit_never_runs_SDK_setup()
    {
        using var fixture = new InstallerFixture();
        var sdk = ToolchainCatalog.Default.Find("pico-sdk")!;
        fixture.Guest.CommandOutput = spec => spec.Program == "git" && spec.Arguments.Contains("rev-parse") ? new string('0', 40) : null;
        var failure = await Assert.ThrowsAsync<ToolchainInstallException>(() => fixture.Installer.InstallAsync(sdk, null, CancellationToken.None));
        Assert.Equal(InstallFailure.IntegrityFailed, failure.Failure);
        Assert.DoesNotContain(fixture.Guest.Started, spec => spec.Program == "cmake");
        Assert.False(fixture.Installer.IsToolchainReady(sdk.Id));
    }

    [Fact]
    public async Task Nordic_frozen_client_builds_its_matching_serial_control_ABI_before_install_validation()
    {
        using var fixture = new InstallerFixture();
        var sdk = new ToolchainDefinition
        {
            Id = EmbeddedNordicInstallation.Id, Name = "Nordic serial DFU", Description = "Test installer command order.",
            PinnedVersion = EmbeddedNordicInstallation.Version,
            Setup = [new SetupStepDefinition
            {
                Name = "test launcher", File = new FileDefinition { Path = "/usr/local/bin/nrfutil", Content = "#!/bin/sh\n", Executable = true },
            }],
        };
        fixture.Guest.CommandOutput = spec => spec.Program == "nrfutil" ? "nrfutil version 6.1.7" : null;
        await fixture.Installer.InstallAsync(sdk, null, CancellationToken.None);
        Assert.True(fixture.Installer.IsToolchainReady(sdk.Id));
        var compiled = fixture.Guest.Started.FindIndex(command => command.Arguments.Contains(EmbeddedNordicInstallation.X64SerialShim));
        var validated = fixture.Guest.Started.FindIndex(command => command.Program == "nrfutil");
        Assert.True(compiled >= 0 && compiled < validated);
        Assert.Equal("version", fixture.Guest.Started[validated].Arguments[0]);
        File.Delete(Path.Combine(fixture.Installer.Rootfs, EmbeddedNordicInstallation.X64SerialShim.TrimStart('/')));
        Assert.False(fixture.Installer.IsToolchainReady(sdk.Id));
    }

    [Fact]
    public async Task A_verified_image_gets_signed_prerequisites_ABI_native_bridges_and_only_then_an_atomic_SDK_marker()
    {
        using var fixture = new InstallerFixture();
        await fixture.Installer.InstallAsync(fixture.Pio, null, CancellationToken.None);
        Assert.True(fixture.Installer.IsToolchainReady("platformio"));
        Assert.Equal("update", fixture.Guest.Started.First().Arguments[0]);
        Assert.Contains(fixture.Guest.Started, c => c.Program == "apt-get" && c.Arguments.Contains("libusb-1.0-0-dev"));
        Assert.Equal(3, fixture.Guest.Started.Count(c => c.Program == "cc"));
        Assert.Contains(fixture.Guest.Started, c => c.Arguments.Contains("--virtual"));
        Assert.All(fixture.Guest.Started, c => Assert.Equal("no", c.Environment["PLATFORMIO_SETTING_ENABLE_TELEMETRY"]));
        Assert.True(File.Exists(Path.Combine(fixture.Installer.Rootfs, "opt/tabstudio/embedded/serial_bridge.py")));
        Assert.DoesNotContain(fixture.Guest.Started, c => c.Arguments.Contains("--allow-unauthenticated"));
        var count = fixture.Guest.Started.Count;
        fixture.FreeSpace = 0;
        await fixture.Installer.InstallAsync(fixture.Pio, null, CancellationToken.None);
        Assert.Equal(count, fixture.Guest.Started.Count);
        Assert.Equal(ToolchainState.Installed, Assert.Single(await fixture.Installer.ReadStatusAsync([fixture.Pio], false, CancellationToken.None)).State);
    }

    [Fact]
    public async Task Failed_bridge_builds_never_publish_readiness_and_a_retry_recovers()
    {
        using var fixture = new InstallerFixture();
        fixture.Guest.FailProgram = "cc";
        await Assert.ThrowsAsync<ToolchainInstallException>(() => fixture.Installer.InstallAsync(fixture.Pio, null, CancellationToken.None));
        Assert.False(fixture.Installer.IsToolchainReady("platformio"));
        Assert.False(File.Exists(Path.Combine(fixture.Installer.Rootfs, "var/lib/tabstudio/embedded/platformio.ready")));
        fixture.Guest.FailProgram = null;
        await fixture.Installer.InstallAsync(fixture.Pio, null, CancellationToken.None);
        Assert.True(fixture.Installer.IsToolchainReady("platformio"));
    }

    [Fact]
    public async Task SDK_changes_and_cancellation_invalidate_only_the_affected_marker_and_leave_the_shared_base_reusable()
    {
        using var fixture = new InstallerFixture();
        await fixture.Installer.InstallAsync(fixture.Pio, null, CancellationToken.None);
        var baseInstalls = fixture.Guest.Started.Count(c => c.Program == "apt-get" && c.Arguments[0] == "install");
        var changed = fixture.Pio with { Setup = [.. fixture.Pio.Setup, new SetupStepDefinition { Name = "new step", Command = ["configure-new"] }] };
        using var cancellation = new CancellationTokenSource();
        fixture.Guest.BeforeStart = spec => { if (spec.Program == "configure-new") cancellation.Cancel(); };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Installer.InstallAsync(changed, null, cancellation.Token));
        Assert.False(fixture.Installer.IsToolchainReady("platformio"));
        fixture.Guest.BeforeStart = null;
        await fixture.Installer.InstallAsync(changed, null, CancellationToken.None);
        Assert.True(fixture.Installer.IsToolchainReady("platformio"));
        Assert.Equal(baseInstalls, fixture.Guest.Started.Count(c => c.Program == "apt-get" && c.Arguments[0] == "install"));
    }

    [Fact]
    public async Task A_corrupt_Ubuntu_download_is_rejected_before_any_command_executes()
    {
        using var fixture = new InstallerFixture(seedImage: false);
        var error = await Assert.ThrowsAsync<ToolchainInstallException>(() => fixture.Installer.InstallAsync(fixture.Pio, null, CancellationToken.None));
        Assert.Equal(InstallFailure.IntegrityFailed, error.Failure);
        Assert.Empty(fixture.Guest.Started);
        Assert.False(fixture.Installer.IsToolchainReady("platformio"));
    }

    [Fact]
    public async Task Removing_one_frontend_keeps_shared_Ubuntu_and_ABI_shims()
    {
        using var fixture = new InstallerFixture();
        await fixture.Installer.InstallAsync(fixture.Pio, null, CancellationToken.None);
        await fixture.Installer.RemoveAsync(fixture.Pio, [], null, CancellationToken.None);
        Assert.False(fixture.Installer.IsToolchainReady("platformio"));
        Assert.True(Directory.Exists(fixture.Installer.Rootfs));
        Assert.True(File.Exists(Path.Combine(fixture.Installer.Rootfs, "opt/tabstudio/embedded/serial-posix.so")));
    }

    [Fact]
    public async Task ARMv7_does_not_download_an_x64_or_ARM64_image_as_an_implicit_fallback()
    {
        using var fixture = new InstallerFixture(architecture: "armv7");
        Assert.Equal(ToolchainState.Unavailable, Assert.Single(await fixture.Installer.ReadStatusAsync([fixture.Pio], false, CancellationToken.None)).State);
        await Assert.ThrowsAsync<ToolchainInstallException>(() => fixture.Installer.InstallAsync(fixture.Pio, null, CancellationToken.None));
        Assert.Empty(fixture.Guest.Started);
    }

    private sealed class InstallerFixture : IDisposable
    {
        private readonly TempDirectory _temp = new();
        private readonly HttpClient _http = new(new BadDownload());
        public long FreeSpace { get; set; } = 20_000_000_000;
        public FakeRuntime Guest { get; }
        public EmbeddedGlibcToolchainInstaller Installer { get; }
        public ToolchainDefinition Pio { get; } = ToolchainCatalog.Default.Find("platformio")! with
        {
            Setup = [new SetupStepDefinition
            {
                Name = "test launcher", File = new FileDefinition { Path = "/usr/local/bin/pio", Executable = true, Content = "#!/bin/sh\n" },
            }],
        };

        public InstallerFixture(bool seedImage = true, string architecture = "x86_64")
        {
            Guest = new FakeRuntime();
            var root = _temp.Resolve("embedded");
            Guest.Rootfs = Path.Combine(root, "rootfs");
            if (seedImage)
            {
                _temp.Write("embedded/rootfs/usr/bin/apt-get", "");
                _temp.Write("embedded/rootfs/usr/bin/cmake", "");
                _temp.Write("embedded/rootfs/etc/os-release", "ID=ubuntu");
                _temp.Write("embedded/rootfs/.tabstudio-embedded-image", FlutterToolchainInstaller.RootImage(architecture)?.Sha256 ?? "unsupported");
            }
            Installer = new EmbeddedGlibcToolchainInstaller(new FakeInstaller(), root, new ToolchainPlatform
            {
                Architecture = architecture, NativeLibraryDirectory = _temp.Resolve("native"), FreeSpace = _ => FreeSpace,
                DnsServers = () => ["192.0.2.53"],
            }, new Downloader(_http) { RetryDelay = TimeSpan.Zero }, [], Guest);
        }
        public void Dispose() { Installer.Dispose(); _http.Dispose(); _temp.Dispose(); }
    }

    private sealed class FakeRuntime : IToolchainRuntime
    {
        public List<ProcessStartSpec> Started { get; } = [];
        public string? Rootfs { get; set; }
        public string? FailProgram { get; set; }
        public Action<ProcessStartSpec>? BeforeStart { get; set; }
        public Func<ProcessStartSpec, string?>? CommandOutput { get; set; }
        public string DisplayName => "fake";
        public RuntimeKind Kind => RuntimeKind.Linux;
        public ToolchainProcess Start(ProcessStartSpec spec)
        {
            BeforeStart?.Invoke(spec);
            Started.Add(spec);
            if (Rootfs is not null && spec.Program is "cc" or "x86_64-linux-gnu-gcc")
            {
                var output = spec.Arguments.ToList().IndexOf("-o");
                var file = Path.Combine(Rootfs, spec.Arguments[output + 1].TrimStart('/'));
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                File.WriteAllText(file, "test ABI shared library");
            }
            var text = CommandOutput?.Invoke(spec) ?? (spec.Arguments.Contains("--virtual") ? "EMBEDDED_BRIDGE_PASSED" : "PlatformIO Core, version 6.1.18");
            return new ToolchainProcess(new FakeHandle(spec.Program == FailProgram ? 1 : 0, text), spec);
        }
        public bool HasCommand(string command) => true;
        public string? Resolve(string command) => command;
        public ProcessStartSpec Shell(string workingDirectory, TerminalSize size) => new() { Program = "primary-shell", WorkingDirectory = workingDirectory, Terminal = size };
        public void ShareFolder(string folder) { }
        public string? ToDevicePath(string programPath, string? workingDirectory)
        {
            if (Rootfs is null) return programPath;
            var candidate = Path.Combine(Rootfs, programPath.TrimStart('/'));
            return File.Exists(candidate) ? candidate : null;
        }
    }

    private sealed class FakeInstaller : IToolchainInstaller
    {
        public IToolchainRuntime Runtime { get; } = new FakeRuntime();
        public bool CanInstall => true;
        public string? Problem => null;
        public bool IsRuntimeReady => true;
        public Task PrepareRuntimeAsync(IProgress<InstallProgress>? progress, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<ToolchainStatus>> ReadStatusAsync(IReadOnlyList<ToolchainDefinition> toolchains, bool checkForUpdates, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ToolchainStatus>>([.. toolchains.Select(t => new ToolchainStatus { Toolchain = t, State = ToolchainState.Installed })]);
        public Task InstallAsync(ToolchainDefinition toolchain, IProgress<InstallProgress>? progress, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task UpdateAsync(ToolchainDefinition toolchain, IProgress<InstallProgress>? progress, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RemoveAsync(ToolchainDefinition toolchain, IReadOnlyList<ToolchainDefinition> installed, IProgress<InstallProgress>? progress, CancellationToken cancellationToken) => Task.CompletedTask;
        public DiskUsage MeasureDiskUsage(IReadOnlyList<ToolchainDefinition> toolchains) => DiskUsage.None;
    }

    private sealed class FakeHandle(int exitCode, string output) : IProcessHandle
    {
        public int Id => 1;
        public bool IsTerminal => false;
        public Stream Input { get; } = new MemoryStream();
        public Stream Output { get; } = new MemoryStream(Encoding.UTF8.GetBytes(output));
        public Stream? Error => null;
        public Task<(int ExitCode, int? Signal)> WaitForExitAsync() => Task.FromResult<(int, int?)>((exitCode, null));
        public void Resize(TerminalSize size) { }
        public void CloseInput() { }
        public void KillTree() { }
        public void Dispose() { Input.Dispose(); Output.Dispose(); }
    }

    private sealed class BadDownload : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent("bad archive"u8.ToArray()) });
    }
}
