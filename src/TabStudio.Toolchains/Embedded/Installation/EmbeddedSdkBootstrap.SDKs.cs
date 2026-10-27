using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Processes;

namespace TabStudio.Toolchains.Embedded.Installation;

public static partial class EmbeddedSdkBootstrap
{
    /// <summary>Native ARM64/x64 SDK source pins and complete installation commands.</summary>
    public static partial class Recipes
    {
        private const string PicoSource = "/opt/tabstudio/pico-sdk";
        private const string ZephyrWorkspace = "/opt/tabstudio/zephyr-workspace";
        private const string ZephyrSource = ZephyrWorkspace + "/zephyr";
        private const string ZephyrPython = "/opt/tabstudio/zephyr-python";
        private const string EspSource = "/opt/tabstudio/esp-idf";
        private const string EspTools = "/opt/tabstudio/esp-idf-tools";
        private const string EspPython = EspTools + "/python_env/idf5.4_py3.12_env";

        /// <summary>Returns a supported SDK recipe, or null when another installer owns the identifier.</summary>
        /// <param name="id">The Toolchain Store identifier.</param>
        public static EmbeddedSdkRecipe? Get(string id) => id switch
        {
            "pico-sdk" => Pico,
            "zephyr" => Zephyr,
            "esp-idf" => Esp,
            "embedded-rust" => Rust,
            _ => null,
        };

        private static ProcessStartSpec Command(string program, IReadOnlyList<string> arguments, string directory) =>
            new() { Program = program, Arguments = arguments, WorkingDirectory = directory };

        private static string Requirements(string name)
        {
            using var stream = typeof(EmbeddedSdkBootstrap).Assembly.GetManifestResourceStream("TabStudio.Toolchains.Embedded." + name)
                ?? throw new InvalidOperationException("The embedded SDK dependency lock is missing: " + name);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        private static readonly EmbeddedSdkRecipe Pico = new("pico-sdk", "bddd20f928ce76142793bef434d4f75f4af6e433",
            new Uri("https://github.com/raspberrypi/pico-sdk.git"), PicoSource,
            ["gcc-arm-none-eabi", "libnewlib-arm-none-eabi", "libstdc++-arm-none-eabi-newlib"],
            [
                Command("cmake", ["-S", "/opt/tabstudio/pico-sdk-check", "-B", "/opt/tabstudio/pico-sdk-check/build", "-G", "Ninja", "-DPICO_BOARD=pico", "-DCMAKE_EXPORT_COMPILE_COMMANDS=ON"], "/"),
                Command("cmake", ["--build", "/opt/tabstudio/pico-sdk-check/build", "--parallel", "2"], "/"),
            ],
            [
                new() { Path = "/opt/tabstudio/pico-sdk-check/CMakeLists.txt", Content = """
                    cmake_minimum_required(VERSION 3.20)
                    include(/opt/tabstudio/pico-sdk/external/pico_sdk_import.cmake)
                    project(tabstudio_pico_sdk_check C CXX ASM)
                    pico_sdk_init()
                    add_executable(tabstudio_pico_sdk_check main.c)
                    target_link_libraries(tabstudio_pico_sdk_check pico_stdlib)
                    pico_add_extra_outputs(tabstudio_pico_sdk_check)
                    """ + "\n" },
                new() { Path = "/opt/tabstudio/pico-sdk-check/main.c", Content = """
                    #include "pico/stdlib.h"
                    int main(void) {
                        gpio_init(PICO_DEFAULT_LED_PIN);
                        gpio_set_dir(PICO_DEFAULT_LED_PIN, GPIO_OUT);
                        while (true) { gpio_put(PICO_DEFAULT_LED_PIN, 1); sleep_ms(100); gpio_put(PICO_DEFAULT_LED_PIN, 0); sleep_ms(100); }
                    }
                    """ + "\n" },
            ],
            new Dictionary<string, string?>
            {
                ["PICO_SDK_PATH"] = PicoSource,
                // The official archive includes upstream's CMake package/targets. The SDK must find
                // this existing pinned native package instead of Findpicotool's rolling develop fetch.
                ["picotool_DIR"] = EmbeddedFlashToolInstallation.PicotoolCMakeDirectory,
            })
            {
                Removes = ["/opt/tabstudio/pico-sdk-check"],
                RequiredToolchains = ["embedded-picotool"],
                RequiredArtifacts =
                [
                    "/opt/tabstudio/pico-sdk-check/build/tabstudio_pico_sdk_check.elf",
                    "/opt/tabstudio/pico-sdk-check/build/tabstudio_pico_sdk_check.uf2",
                    .. EmbeddedFlashToolInstallation.PicotoolArtifacts,
                ],
            };

        private static readonly EmbeddedSdkRecipe Zephyr = new("zephyr", "7823374e872145b5bd018bfe447839eb36042611",
            new Uri("https://github.com/zephyrproject-rtos/zephyr.git"), ZephyrSource,
            ["gcc-arm-none-eabi", "libnewlib-arm-none-eabi", "device-tree-compiler"],
            [
                Command("python3", ["-m", "venv", ZephyrPython], "/"),
                Command(ZephyrPython + "/bin/python", ["-m", "pip", "install", "--require-hashes", "-r", ZephyrWorkspace + "/bootstrap.requirements.txt"], "/"),
                Command(ZephyrPython + "/bin/python", ["-m", "pip", "install", "--require-hashes", "--no-build-isolation", "-r", ZephyrWorkspace + "/requirements.txt"], "/"),
                Command(ZephyrPython + "/bin/python", [ZephyrWorkspace + "/initialize.py", ZephyrWorkspace, ZephyrSource, ZephyrPython + "/bin/west"], ZephyrWorkspace),
                Command(ZephyrPython + "/bin/west", ["update", "--narrow", "-o=--depth=1", "cmsis", "cmsis_6", "hal_nordic", "hal_stm32", "hal_rpi_pico"], ZephyrWorkspace),
                Command(ZephyrPython + "/bin/west", ["build", "--build-opt=-j2", "-b", "qemu_cortex_m3", "-d", ZephyrWorkspace + "/build-check", ZephyrSource + "/samples/hello_world"], ZephyrWorkspace),
            ],
            [
                new() { Path = "/usr/local/bin/west", Executable = true, Content = "#!/bin/sh\nexport ZEPHYR_BASE=" + ZephyrSource + "\nexport VIRTUAL_ENV=" + ZephyrPython + "\nexport PATH=" + ZephyrPython + "/bin:$PATH\nexec " + ZephyrPython + "/bin/west \"$@\"\n" },
                new() { Path = ZephyrWorkspace + "/bootstrap.requirements.txt", Content = Requirements("zephyr-bootstrap.requirements.txt") },
                new() { Path = ZephyrWorkspace + "/requirements.txt", Content = Requirements("zephyr.requirements.txt") },
                new() { Path = ZephyrWorkspace + "/initialize.py", Content = """
                    import configparser
                    import pathlib
                    import subprocess
                    import sys

                    workspace, source = (pathlib.Path(p).resolve() for p in sys.argv[1:3])
                    config_path = workspace / '.west' / 'config'
                    if not config_path.exists():
                        subprocess.run([sys.argv[3], 'init', '-l', str(source)], cwd=workspace, check=True)
                    else:
                        config = configparser.ConfigParser(interpolation=None)
                        with config_path.open(encoding='utf-8') as stream:
                            config.read_file(stream)
                        manifest = (workspace / config.get('manifest', 'path')).resolve()
                        if manifest != source or config.get('manifest', 'file', fallback='west.yml') != 'west.yml':
                            raise RuntimeError('The existing west workspace points at a different manifest; it was preserved.')
                    """ + "\n" },
                new() { Path = ZephyrWorkspace + "/TABSTUDIO-SDK.txt", Content = "Zephyr 4.1.0 is checked out at its immutable source commit. CMSIS and Nordic/ST/Raspberry Pi HAL modules use west.yml's immutable revisions. Run west update in this workspace for additional boards' modules. The default GNU Arm toolchain uses signed Ubuntu packages; other architectures need their matching toolchain selection.\nWest 1.3.0 and all base Python dependencies are release- and SHA-256-locked for native Linux ARM64/x64 CPython 3.12. Source distributions use pinned setuptools/wheel without downloading isolated build dependencies.\n" },
            ],
            new Dictionary<string, string?>
            {
                ["ZEPHYR_BASE"] = ZephyrSource,
                ["ZEPHYR_TOOLCHAIN_VARIANT"] = "cross-compile",
                ["CROSS_COMPILE"] = "/usr/bin/arm-none-eabi-",
                ["WEST_PYTHON"] = ZephyrPython + "/bin/python",
                ["VIRTUAL_ENV"] = ZephyrPython,
                ["PATH"] = ZephyrPython + "/bin:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin",
            })
            {
                Submodules = false,
                Removes = [ZephyrWorkspace, ZephyrPython],
                RequiredArtifacts = [ZephyrWorkspace + "/build-check/zephyr/zephyr.elf"],
            };

        private static readonly EmbeddedSdkRecipe Esp = new("esp-idf", "4c2820d377d1375e787bcef612f0c32c1427d183",
            new Uri("https://github.com/espressif/esp-idf.git"), EspSource,
            ["flex", "bison", "gperf", "ccache", "libffi-dev", "libssl-dev", "dfu-util", "libusb-1.0-0"],
            [
                Command("python3", ["-c", "import sys; assert sys.version_info[:2] == (3, 12), 'The pinned ESP-IDF environment requires Ubuntu CPython 3.12'"], EspSource),
                Command("python3", [EspSource + "/tools/idf_tools.py", "--non-interactive", "install", "--targets=all"], EspSource),
                Command("python3", ["-m", "venv", EspPython], "/"),
                Command(EspPython + "/bin/python", ["-m", "pip", "install", "--require-hashes", "--only-binary=:all:", "--force-reinstall", "-r", EspTools + "/bootstrap.requirements.txt"], "/"),
                Command(EspPython + "/bin/python", ["-m", "pip", "install", "--require-hashes", "--only-binary=:all:", "--no-binary=esptool", "--no-build-isolation", "--force-reinstall", "-r", EspTools + "/requirements.txt"], "/"),
                Command(EspPython + "/bin/python", ["-m", "pip", "check"], "/"),
                Command(EspPython + "/bin/python", [EspSource + "/tools/idf_tools.py", "--non-interactive", "check-python-dependencies"], EspSource),
            ],
            [
                new() { Path = "/usr/local/bin/idf.py", Executable = true, Content = "#!/bin/bash\nset -e\nexport IDF_PATH=" + EspSource + "\nexport IDF_TOOLS_PATH=" + EspTools + "\nexport IDF_PYTHON_ENV_PATH=" + EspPython + "\n. \"$IDF_PATH/export.sh\" >&2\nexport PATH=/opt/tabstudio/embedded/build-bin:$PATH\nexec python \"$IDF_PATH/tools/idf.py\" \"$@\"\n" },
                new() { Path = EspTools + "/bootstrap.requirements.txt", Content = Requirements("esp-idf-bootstrap.requirements.txt") },
                new() { Path = EspTools + "/requirements.txt", Content = Requirements("esp-idf.requirements.txt") },
                new() { Path = EspTools + "/espidf.constraints.v5.4.txt", Content = Requirements("esp-idf-constraints.requirements.txt") },
                new() { Path = EspPython + "/idf_version.txt", Content = "5.4\n" },
                new() { Path = EspTools + "/TABSTUDIO-SDK.txt", Content = "ESP-IDF 5.4.1 source and Git submodules are immutable. Espressif's pinned tools.json verifies each native host-tool archive's SHA-256 and selects native Linux ARM64/x64 packages. All 58 Python core/build distributions are exact-release and SHA-256 locked for Ubuntu CPython 3.12. The only source distribution is esptool, built with pinned setuptools/wheel and no isolated dependency downloads. The vendor v5.4 constraint snapshot is app-owned and checked offline; install-python-env does not download rolling constraints or upgrade packages. User-selected project components retain their own manifests/locks.\n" },
            ],
            new Dictionary<string, string?> { ["IDF_PATH"] = EspSource, ["IDF_TOOLS_PATH"] = EspTools, ["IDF_PYTHON_ENV_PATH"] = EspPython }) { Removes = [EspTools] };
    }
}
