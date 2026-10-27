using System.Text.Json;
using System.Text.Json.Serialization;
using TabStudio.App.Services;
using TabStudio.Toolchains.Embedded.Flashing;
using TabStudio.Testing;
using TabStudio.Toolchains.Embedded.Projects;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Projects;

namespace TabStudio.App.Tests.Services;

public sealed class EmbeddedTemplateTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private static readonly JsonSerializerOptions ProfileJson = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    [Theory]
    [InlineData("uno", "atmelavr@5.3.0")]
    [InlineData("esp32", "espressif32@8.0.0")]
    [InlineData("pico", "raspberrypi@1.20.0")]
    [InlineData("nucleo-f401re", "ststm32@20.0.0")]
    [InlineData("xiao-samd21", "atmelsam@9.0.0")]
    [InlineData("teensy41", "teensy@6.0.0")]
    public void Platformio_starters_pin_the_audited_upstream_platform_release(string id, string platform)
    {
        var root = new ProjectTemplates(new AppPaths(_temp.Path)).Create("embedded-" + id, "Firmware");
        Assert.Contains("platform = " + platform + "\n", File.ReadAllText(Path.Combine(root, "platformio.ini")), StringComparison.Ordinal);
    }

    [Fact]
    public void Ch32v003_starter_uses_pinned_upstream_startup_and_a_bounded_uart_isp_profile()
    {
        var root = new ProjectTemplates(new AppPaths(_temp.Path)).Create("embedded-ch32v003", "Firmware");
        var project = Assert.Single(EmbeddedProjectDetector.Detect(root));
        Assert.Equal(EmbeddedProjectKind.Ch32Fun, project.Kind);
        Assert.Equal("embedded-ch32fun", Assert.Single(ToolchainProjectDetector.Detect(root, ToolchainCatalog.Default)).Toolchain.Id);
        var plan = EmbeddedBuildPlanner.Plan(project);
        Assert.Equal("embedded-ch32fun", plan.ToolchainId);
        Assert.Equal("make", Assert.Single(plan.Build).Program);
        Assert.Contains("build", plan.Build[0].Arguments);
        Assert.DoesNotContain("flash", plan.Build[0].Arguments);
        Assert.Contains("a800cbd1f1ca5e250c73da83c669f0965d2052e5", File.ReadAllText(Path.Combine(root, "Makefile")), StringComparison.Ordinal);
        var request = JsonSerializer.Deserialize<EmbeddedFlashRequest>(File.ReadAllText(Path.Combine(root, ".tabstudio", "embedded-flash.json")), ProfileJson)!;
        Assert.Equal(EmbeddedFlashProtocol.WchIsp, request.Protocol);
        Assert.Equal("CH32V003F4P6", request.Target);
        Assert.True(request.UseSerialTransport);
        Assert.Equal(0ul, request.BinaryAddress);
        Assert.Equal(new FlashAddressRange(0, 16 * 1024), Assert.Single(request.ApplicationFlash));
    }

    [Theory]
    [InlineData("esp32")]
    [InlineData("esp32s3")]
    [InlineData("esp32c3")]
    public void Esp_starters_flash_only_resolved_post_build_metadata_instead_of_guessing_offsets(string id)
    {
        var root = new ProjectTemplates(new AppPaths(_temp.Path)).Create("embedded-" + id, "Firmware");
        var profile = JsonSerializer.Deserialize<EmbeddedFlashRequest>(File.ReadAllText(Path.Combine(root, ".tabstudio", "embedded-flash.json")), ProfileJson)!;
        Assert.Equal(EmbeddedFlashProtocol.Esptool, profile.Protocol);
        Assert.Equal("FROM_METADATA", profile.Target);
        Assert.Equal(".pio/build/" + id + "/tabstudio-flasher-args.json", profile.EspFlasherArguments);
        Assert.Null(profile.BinaryAddress);
        Assert.Empty(profile.ApplicationFlash);
        Assert.Contains("extra_scripts = post:tabstudio_esp_flash.py", File.ReadAllText(Path.Combine(root, "platformio.ini")), StringComparison.Ordinal);
        var exporter = File.ReadAllText(Path.Combine(root, "tabstudio_esp_flash.py"));
        Assert.Contains("UPLOADERFLAGS", exporter, StringComparison.Ordinal);
        Assert.Contains("$ESP32_APP_OFFSET", exporter, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("uno", "uno")]
    [InlineData("nano", "nanoatmega328new")]
    [InlineData("mega", "megaatmega2560")]
    [InlineData("esp32s3", "esp32-s3-devkitc-1")]
    [InlineData("esp32c3", "esp32-c3-devkitm-1")]
    [InlineData("nucleo-f401re", "nucleo_f401re")]
    [InlineData("xiao-samd21", "seeed_xiao")]
    [InlineData("teensy41", "teensy41")]
    public void Platformio_starters_preserve_real_board_id_and_are_detected(string id, string board)
    {
        var templateId = "embedded-" + id;
        Assert.Contains(ProjectTemplates.All, x => x.Id == templateId);
        var root = new ProjectTemplates(new AppPaths(_temp.Path)).Create(templateId, "Firmware");
        Assert.Contains("board = " + board, File.ReadAllText(Path.Combine(root, "platformio.ini")), StringComparison.Ordinal);
        Assert.Contains("Serial.begin(115200)", File.ReadAllText(Path.Combine(root, "src", "main.cpp")), StringComparison.Ordinal);
        var project = Assert.Single(EmbeddedProjectDetector.Detect(root));
        Assert.Equal(EmbeddedProjectKind.PlatformIO, project.Kind);
        Assert.Single(EmbeddedBuildPlanner.Plan(project).Build);
    }

    [Theory]
    [InlineData("picow", "pico_w")]
    [InlineData("pico2", "pico2")]
    [InlineData("pico2w", "pico2_w")]
    public void Pico_sdk_starters_choose_actual_board_and_require_installed_sdk(string id, string board)
    {
        var root = new ProjectTemplates(new AppPaths(_temp.Path)).Create("embedded-" + id, "Firmware");
        var cmake = File.ReadAllText(Path.Combine(root, "CMakeLists.txt"));
        Assert.Contains("set(PICO_BOARD " + board + ")", cmake, StringComparison.Ordinal);
        Assert.Contains("PICO_SDK_PATH", cmake, StringComparison.Ordinal);
        var project = Assert.Single(EmbeddedProjectDetector.Detect(root));
        Assert.Equal(EmbeddedProjectKind.PicoSdk, project.Kind);
        Assert.Equal(board, project.Target);
        Assert.Single(EmbeddedBuildPlanner.Plan(project).Build);
        var profile = ReadProfile(root);
        Assert.Equal(EmbeddedFlashProtocol.Picotool, profile.Protocol);
        Assert.Equal(".tabstudio/embedded/build/Firmware.uf2", profile.FirmwarePath);
        Assert.Equal(new FlashAddressRange(0x10000000, id == "picow" ? 2UL * 1024 * 1024 : 4UL * 1024 * 1024), Assert.Single(profile.ApplicationFlash));
    }

    [Fact]
    public void Nordic_dk_uses_the_actual_52840_Zephyr_board_instead_of_the_incompatible_Arduino_52832_linker()
    {
        var root = new ProjectTemplates(new AppPaths(_temp.Path)).Create("embedded-nrf52840dk", "Firmware");
        Assert.False(File.Exists(Path.Combine(root, "platformio.ini")));
        var project = Assert.Single(EmbeddedProjectDetector.Detect(root));
        Assert.Equal(EmbeddedProjectKind.Zephyr, project.Kind);
        var plan = EmbeddedBuildPlanner.Plan(project);
        Assert.Equal("zephyr", plan.ToolchainId);
        Assert.Contains("nrf52840dk/nrf52840", Assert.Single(plan.Build).Arguments);
        Assert.Contains("k_sleep(K_SECONDS(1))", File.ReadAllText(Path.Combine(root, "src", "main.c")), StringComparison.Ordinal);
        var profile = ReadProfile(root);
        Assert.Equal("nRF52840_xxAA", profile.Target);
        Assert.Equal(EmbeddedFlashProtocol.ProbeRs, profile.Protocol);
        Assert.Null(profile.ProbeSelector); // The actual attachment is selected at upload, never an invented J-Link serial.
        Assert.Equal(new FlashAddressRange(0, 1024 * 1024), Assert.Single(profile.ApplicationFlash));
        Assert.Contains("without a SoftDevice", File.ReadAllText(Path.Combine(root, "README.md")), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("nucleo-f401re", "STM32F401RE", 512 * 1024UL)]
    [InlineData("bluepill", "STM32F103C8", 64 * 1024UL)]
    [InlineData("blackpill", "STM32F411CE", 512 * 1024UL)]
    public void Stm32_starters_select_exact_silicon_and_safe_main_flash_without_guessing_probe_identity(string id, string chip, ulong size)
    {
        var root = new ProjectTemplates(new AppPaths(_temp.Path)).Create("embedded-" + id, "Firmware");
        var profile = ReadProfile(root);
        Assert.Equal(EmbeddedFlashProtocol.ProbeRs, profile.Protocol);
        Assert.Equal(chip, profile.Target);
        Assert.Null(profile.ProbeSelector);
        Assert.Equal(new FlashAddressRange(0x08000000, size), Assert.Single(profile.ApplicationFlash));
        Assert.Equal(".pio/build/" + id + "/firmware.elf", profile.FirmwarePath);
    }

    [Fact]
    public void Xiao_samd21_profile_excludes_its_eight_KiB_bootloader_and_requires_reenumeration()
    {
        var root = new ProjectTemplates(new AppPaths(_temp.Path)).Create("embedded-xiao-samd21", "Firmware");
        var profile = ReadProfile(root);
        Assert.Equal(EmbeddedFlashProtocol.Bossac, profile.Protocol);
        Assert.Equal("SAMD21G18A", profile.Target);
        Assert.True(profile.Touch1200Baud);
        Assert.Equal(0x2000UL, profile.BinaryAddress);
        Assert.Equal(new FlashAddressRange(0x2000, 256 * 1024 - 0x2000), Assert.Single(profile.ApplicationFlash));

        // The exact generated profile must reach the ordinary checked planner without writing the bootloader.
        var image = Path.GetFullPath(profile.FirmwarePath, root);
        Directory.CreateDirectory(Path.GetDirectoryName(image)!);
        File.WriteAllBytes(image, [1, 2, 3, 4]);
        var plan = EmbeddedFlashPlanner.Plan(profile with { FirmwarePath = image, WorkingDirectory = root });
        Assert.True(plan.Touch1200Baud);
        Assert.Contains("8192", Assert.Single(plan.Steps).Arguments);
        Assert.Throws<InvalidOperationException>(() => EmbeddedFlashPlanner.Plan(profile with
        {
            FirmwarePath = image, WorkingDirectory = root, BinaryAddress = 0,
        }));
    }

    [Theory]
    [InlineData("teensy40", 2031616UL)]
    [InlineData("teensy41", 8126464UL)]
    public void Teensy_starters_bound_the_exact_board_external_flash_and_retain_transfer_only_protocol(string id, ulong size)
    {
        var root = new ProjectTemplates(new AppPaths(_temp.Path)).Create("embedded-" + id, "Firmware");
        var profile = ReadProfile(root);
        Assert.Equal(EmbeddedFlashProtocol.Teensy, profile.Protocol);
        Assert.Equal(id.ToUpperInvariant(), profile.Target);
        Assert.Equal(new FlashAddressRange(0x60000000, size), Assert.Single(profile.ApplicationFlash));
        Assert.False(profile.Touch1200Baud);
        Assert.Equal(".pio/build/" + id + "/firmware.hex", profile.FirmwarePath);
    }

    private static EmbeddedFlashRequest ReadProfile(string root) => JsonSerializer.Deserialize<EmbeddedFlashRequest>(File.ReadAllText(Path.Combine(root, ".tabstudio", "embedded-flash.json")), ProfileJson)!;

    [Theory]
    [InlineData("uno", "m328p", "arduino", 32256UL, 115200, false)]
    [InlineData("nano", "m328p", "arduino", 30720UL, 115200, false)]
    [InlineData("mega", "m2560", "wiring", 253952UL, 115200, false)]
    [InlineData("leonardo", "m32u4", "avr109", 28672UL, 57600, true)]
    [InlineData("micro", "m32u4", "avr109", 28672UL, 57600, true)]
    public void Known_avr_starters_have_explicit_bootloader_safe_upload_profiles(string id, string target, string programmer, ulong applicationBytes, int baud, bool touch)
    {
        var root = new ProjectTemplates(new AppPaths(_temp.Path)).Create("embedded-" + id, "Firmware");
        var profile = JsonSerializer.Deserialize<EmbeddedFlashRequest>(File.ReadAllText(Path.Combine(root, ".tabstudio", "embedded-flash.json")), ProfileJson)!;
        Assert.Equal(EmbeddedFlashProtocol.Avrdude, profile.Protocol);
        Assert.Equal(target, profile.Target);
        Assert.Equal(programmer, profile.Programmer);
        Assert.Equal(new FlashAddressRange(0, applicationBytes), Assert.Single(profile.ApplicationFlash));
        Assert.Equal(baud, profile.BaudRate);
        Assert.Equal(touch, profile.Touch1200Baud);
        Assert.Equal(".pio/build/" + id + "/firmware.hex", profile.FirmwarePath);
        Assert.Empty(profile.FirmwareRegions); // The real built image, rather than a guessed image size, is parsed at upload.
    }

    public void Dispose() => _temp.Dispose();
}
