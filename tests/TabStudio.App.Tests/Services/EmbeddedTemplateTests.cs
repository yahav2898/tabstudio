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
    [InlineData("nrf52840dk", "nrf52840_dk")]
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
    }

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
