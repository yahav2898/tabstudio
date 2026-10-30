# Third-party notices

TabStudio is licensed under the Apache License 2.0 (see [LICENSE](LICENSE)). It ships with, or downloads on
request, the third-party software below. Each one is under its own licence.

## Inside the APK

| Component | Licence | Source |
| --- | --- | --- |
| [llama.cpp / ggml](https://github.com/ggml-org/llama.cpp/tree/4762ad7316dcdec20016ab5985fb46a27902204d), b6000, linked into `libtabstudio-ai.so` | MIT | [Pinned sources](https://github.com/ggml-org/llama.cpp/tree/4762ad7316dcdec20016ab5985fb46a27902204d), [license](native/local-ai/LICENSE.llama.cpp.txt) |
| [PRoot](https://github.com/termux/proot), Termux's fork, 5.1.107.101 | GPL-2.0-or-later | [termux/proot v5.1.107.101](https://github.com/termux/proot/archive/v5.1.107.101.zip) |
| [talloc](https://talloc.samba.org) 2.5.0, linked into PRoot | LGPL-3.0-or-later | [talloc-2.5.0.tar.gz](https://www.samba.org/ftp/talloc/talloc-2.5.0.tar.gz) |
| [box64](https://github.com/ptitSeb/box64) 0.4.4, separate ARM64 program run through PRoot | MIT; [full licence](src/TabStudio.Toolchains/Microchip/box64.LICENSE) | [Pinned source](https://github.com/ptitSeb/box64/tree/2f130fab1d6e1a4ee8a71dc60cfdfcc839ad192a); reproducible static-heap/clone-context fixes in [build/pic/box64-static-clone.patch](build/pic/box64-static-clone.patch) |
| The Razor compiler from the .NET SDK | MIT | See [src/TabStudio.Compilation/Razor/Compiler/NOTICE.txt](src/TabStudio.Compilation/Razor/Compiler/NOTICE.txt) |
| The fling curve of Android's `OverScroller` (Copyright © 2010 The Android Open Source Project), ported to C# in [FlingSpline.cs](src/TabStudio.App/Input/Scrolling/FlingSpline.cs) | Apache-2.0 | [OverScroller.java](https://android.googlesource.com/platform/frameworks/base/+/refs/heads/main/core/java/android/widget/OverScroller.java) |
| The NuGet packages in [Directory.Packages.props](Directory.Packages.props), such as Avalonia and Roslyn | Each package's own licence, shown on nuget.org | nuget.org |

PRoot, with talloc linked in, and PRoot's loader are separate programs in the APK (`lib/<abi>/libproot.so` and
`lib/<abi>/libproot-loader.so`), not part of TabStudio's own code. They are built from the unmodified sources above,
checked against pinned SHA-256 hashes, by [build/android/native/build.sh](build/android/native/build.sh), which anyone
can run to rebuild them.

## Downloaded by the Toolchain Store

The following are not part of the APK. The Toolchain Store downloads them only when you install a toolchain.

| Component | Licence | From |
| --- | --- | --- |
| Alpine Linux's minimal root file system and packages, including Python and Node.js | Each package's own licence, listed in its package metadata | Alpine Linux's mirrors |
| [Pyright](https://github.com/microsoft/pyright) | MIT | npm |
| [typescript-language-server](https://github.com/typescript-language-server/typescript-language-server) | Apache-2.0 | npm |
| [vscode-langservers-extracted](https://github.com/hrsh7th/vscode-langservers-extracted) 4.10.0, supplying HTML, CSS and JSON language servers | MIT | npm, installed by the Node.js Toolchain Store setup |
| [TypeScript](https://github.com/microsoft/TypeScript) | Apache-2.0 | npm |
| [debugpy](https://github.com/microsoft/debugpy) | MIT | PyPI |
| [vscode-js-debug](https://github.com/microsoft/vscode-js-debug) | MIT | GitHub releases |
| Microchip XC8 and XC32 | Microchip software licence and included third-party component licences | Installed by the user directly from Microchip, after explicit acceptance. Compiler binaries are never redistributed by TabStudio. |
| Microchip device family packs (DFPs) | Each pack's included licence | packs.download.microchip.com; exact versions and SHA-256 hashes are pinned in the pack catalog. |
| Debian GNU/Linux glibc, libstdc++, libgcc and zlib runtime packages | Each package's included copyright and licence files (glibc LGPL-2.1-or-later; GCC runtime GPL-3.0 with GCC Runtime Library Exception; zlib licence) | Verified downloads directly from Debian. Copyright and licence files are retained under usr/share/doc. |

## PIC support downloaded or built on request

| Component | Licence | Source / handling |
| --- | --- | --- |
| [gpsim](https://gpsim.sourceforge.net/) 0.32.1 | GPL-2.0-or-later | Pinned upstream source; built natively with the GUI disabled. Source and COPYING remain with the installation. |
| [pymcuprog](https://github.com/microchip-pic-avr-tools/pymcuprog) 3.19.4.61 | MIT | Verified PyPI wheel; its included licence is retained. |
| [pyedbglib](https://github.com/microchip-pic-avr-tools/pyedbglib) 2.24.2.18 | MIT | Verified PyPI wheel. |
| [pk2cmd](https://github.com/jaka-fi/pk2cmd) at 38ad052 | **Non-free Microchip PICkit Software License Agreement**, not GPL | Separate acceptance before source download. No device database is downloaded; no PICkit firmware is redistributed or flashed by TabStudio. The upstream release/database removal is documented in [PIC.md](docs/features/pic.md). |
| [pic32prog](https://github.com/sergev/pic32prog) at 4b4c8e5 | GPL-2.0 | Pinned source; built against Alpine's hidapi/libusb. Source and COPYING remain with the installation. |
| pyserial 3.5; IntelHex 2.3.0 | BSD-3-Clause | Verified Python distributions; included notices retained. |
| appdirs 1.4.4; PyYAML 6.0.3 | MIT | Verified distributions; included notices retained. |
| pyelftools 0.33 | Public domain, with the distribution's included licensing terms | Verified PyPI wheel; included licence retained. |
| libusb; hidapi; native build dependencies | Their signed Alpine package metadata and included licences (libusb LGPL-2.1-or-later; hidapi's chosen BSD licence) | Alpine packages; no Android hidraw or unrestricted device discovery. |

The USB serial control sequences in `usb_serial.py` follow usb-serial-for-android v3.9.0, MIT, copyright (c) 2011–2013 Google Inc. and (c) 2013 Mike Wakerly. Its notice follows:

> Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
>
> The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
>
> THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

These optional tools are separate processes. Pinned source/distribution hashes are in [assets.lock.json](build/pic/assets.lock.json). TabStudio's own fd-view shim is supplied as source; no proprietary Microchip programming algorithms are reimplemented or copied into the APK.

## In the templates

The web templates include Bootstrap, jQuery, jQuery Validation and jQuery Validation Unobtrusive, each with its licence
file beside it under [src/TabStudio.Web/Templates/Content/lib](src/TabStudio.Web/Templates/Content/lib).

## Go language tools

The packaged PRoot includes TabStudio's `proot-thread-waits.patch` to implement Linux's cross-thread wait
semantics for Delve. The patch is distributed under PRoot's GPL-2.0-or-later license; its source is in
`build/android/native/`. The upstream source archive retains its pinned SHA-256.

Go and gopls (BSD-3-Clause) and Delve (MIT) are installed as signed Alpine Linux packages on Android, rather
than downloaded executables. CI builds gopls v0.21.1 and Delve v1.26.2 from their Go modules for the host tests.
Sources and licenses: https://go.dev/LICENSE, https://github.com/golang/tools and https://github.com/go-delve/delve.

## Rust language tools

Rust, Cargo and rust-analyzer (MIT OR Apache-2.0), rust-src, and LLVM/LLDB (Apache-2.0 WITH LLVM-exception)
are installed as signed Alpine Linux packages on Android. No additional binary archives are downloaded.
Sources and licenses: https://github.com/rust-lang/rust, https://github.com/rust-lang/rust-analyzer,
https://github.com/llvm/llvm-project.

## C and C++ language tools

Clang, clangd and LLDB (Apache-2.0 WITH LLVM-exception), CMake (BSD-3-Clause), GNU make (GPL-3.0-or-later)
and GCC/build-base (GPL-3.0-or-later with runtime exceptions) come from signed Alpine Linux packages.
No extra executable archives are downloaded. Sources: https://github.com/llvm/llvm-project,
https://gitlab.kitware.com/cmake/cmake, https://www.gnu.org/software/make/ and https://gcc.gnu.org/.

## Java language tools

OpenJDK 21 (GPL-2.0-only WITH Classpath-exception-2.0), Maven (Apache-2.0), Gradle (Apache-2.0) and Eclipse
JDT LS (EPL-2.0) come from signed Alpine Linux packages on Android. Maven and Gradle are installed on demand.

Microsoft's Java Debugger extension v0.60.0, containing java-debug plugin v0.53.2 (MIT), is downloaded from
https://open-vsx.org/api/vscjava/vscode-java-debug/0.60.0/file/vscjava.vscode-java-debug-0.60.0.vsix and verified
against SHA-256 f09ebe358a7b08b3c17b21b8d8207b0649acad120eb6f1604dc4f15379f59d66. Its license and bundled
notices remain in the extracted extension. Source: https://github.com/microsoft/java-debug.

The CI host uses Eclipse JDT LS 1.58.0 from
https://download.eclipse.org/jdtls/milestones/1.58.0/jdt-language-server-1.58.0-202604151538.tar.gz,
verified against SHA-256 2a5bbe55ec91b4325392050dc422cead3220a2459b3766be35e1fff45b4a50d9.
Source and licenses: https://github.com/eclipse-jdtls/eclipse.jdt.ls.

The CI host downloads Gradle 8.14.4 (Apache-2.0) from
https://services.gradle.org/distributions/gradle-8.14.4-bin.zip, pinned to SHA-256
f1771298a70f6db5a29daf62378c4e18a17fc33c9ba6b14362e0cdf40610380d.
## PHP language tools

PHP 8.5, Composer, Xdebug and the PHP extensions are installed as verified Alpine packages (PHP-3.01, MIT,
Xdebug-1.03). Phpactor 2026.10.07.0 (MIT) is downloaded from
https://github.com/phpactor/phpactor/releases/download/2026.10.07.0/phpactor.phar and verified against SHA-256
b28bf6166e016d539fc2f2aec18ef918b12590e436a6f1ccbdf30b5bbdbdb16b. Source: https://github.com/phpactor/phpactor.
PHP Debug 1.40.2 (MIT) is downloaded from
https://open-vsx.org/api/xdebug/php-debug/1.40.2/file/xdebug.php-debug-1.40.2.vsix and verified against SHA-256
17631993fe800083a2fc89a6782c8bf58a79603ad201ddff60d9b59ba2345d5f. Source: https://github.com/xdebug/vscode-php-debug.

## Kotlin language tools

The Kotlin compiler 2.4.21 (Apache-2.0) is downloaded from
https://github.com/JetBrains/kotlin/releases/download/v2.4.21/kotlin-compiler-2.4.21.zip and verified against SHA-256
7cc140e76daf416a0424a5557f99afd7ca89ebd463ea1101261e3e01c33e75cd. JetBrains' Kotlin language server 263.7084.0 is
proprietary freeware under the JetBrains Free Plugin License Agreement, which the Toolchain Store shows before it
installs; it is downloaded from https://download.jetbrains.com/language-server/kotlin-server/263.7084.0/ as
`kotlin-server-0.0.27-linux-aarch64.vsix` (SHA-256 4de2b5d4ab0ccb73f0bfbfdfe3b56b49a68889f198cd8342f5dfd98ff0647052)
or `kotlin-server-0.0.27-linux-amd64.vsix` (SHA-256 6787185abb0e7f3b527484bb6cc4a881ce42b2e39320626937e65839751075e9),
with its own runtime and notices. It is not included in TabStudio. Kotlin debugging uses the Java tools above.

## Swift language tools (experimental)

The Swift 6.4.1 toolchain for Ubuntu 24.04 (Apache-2.0 with the Runtime Library Exception), with SwiftPM,
sourcekit-lsp, LLDB and lldb-dap, is downloaded from https://download.swift.org/swift-6.4.1-release/ and verified
against SHA-256 b85dd70e204bc363ce62f1e35bd1887810d0e04487d13de8fd9d042635972f45 (aarch64) or
7d246c7114077ad01dba4ad897628446ada4c489d6c7dbcf39ae3f25cbe17e8b (x86_64). It runs in the Ubuntu Base 24.04.3
environment described under Flutter, with the system packages Swift.org lists, from Ubuntu's signed archive.
Source: https://github.com/swiftlang.

## Visual Basic

The Visual Basic compiler, workspaces and features (Microsoft.CodeAnalysis.VisualBasic*, MIT) are NuGet packages
inside the APK. `Microsoft.VisualBasic.Forms` in TabStudio is TabStudio's own implementation of that API, written
from its public .NET 10 surface; it contains no code from Microsoft's assembly.

## Android SDK and mobile project tools (downloaded on request)

- **Google Android SDK command-line tools 12, platform 35 and build-tools 35** — Google Android SDK License
  Agreement, displayed before installation: https://developer.android.com/studio/terms . Original notices and
  licenses remain in the installed archives. These tools are downloaded from Google's repository, not bundled
  into TabStudio's APK. D8/R8 and apksigner run as Java bytecode from the official build-tools archive.
- **lzhiyong/android-sdk-tools 35.0.2** — open-source static ARM64/ARMv7 builds of AOSP aapt/aapt2, aidl,
  zipalign and related tools; Apache-2.0 with dependency notices included by upstream. Source and releases:
  https://github.com/lzhiyong/android-sdk-tools . Architecture archives are pinned by SHA-256 in the catalog.
- **Gradle 8.9** — Apache-2.0; https://github.com/gradle/gradle . Downloaded from the pinned official distribution.
- **OpenJDK 17/21 and Eclipse JDT LS** — GPL-2.0 WITH Classpath-exception-2.0 and EPL-2.0 respectively;
  installed as verified Alpine packages. https://openjdk.org/ ; https://github.com/eclipse-jdtls/eclipse.jdt.ls .
- **Capacitor 6.2.1** — MIT; https://github.com/ionic-team/capacitor . **Vite** — MIT; https://github.com/vitejs/vite .
  **TypeScript** — Apache-2.0; https://github.com/microsoft/TypeScript . Templates declare these npm dependencies;
  they are installed into user projects, not bundled in TabStudio.
- **Android Gradle Plugin and Kotlin Gradle Plugin** — Apache-2.0, downloaded by Gradle into user project caches.
  https://android.googlesource.com/platform/tools/base/ ; https://github.com/JetBrains/kotlin .

- **kotlin-language-server 1.3.13** — MIT; https://github.com/fwcd/kotlin-language-server . The SDK installs the pinned JVM server archive for Kotlin completion and navigation.

## Optional local AI models

Models are downloaded only after the user sees and accepts the model's full upstream license. They are not included in release APKs. The catalog pins immutable Hugging Face revisions and SHA-256 hashes. Full license copies are embedded beside the catalog under `src/TabStudio.Agent/Local/Models`, and each entry links to its upstream license.

| Model | License | Upstream |
| --- | --- | --- |
| Qwen2.5-Coder 1.5B Instruct Q4_K_M | Apache-2.0 | [Qwen](https://huggingface.co/Qwen/Qwen2.5-Coder-1.5B-Instruct-GGUF) |
| Qwen2.5-Coder 3B Instruct Q4_K_M | Qwen Research License | [Qwen](https://huggingface.co/Qwen/Qwen2.5-Coder-3B-Instruct-GGUF) |
| Qwen2.5 0.5B Instruct Q4_K_M | Apache-2.0 | [Qwen](https://huggingface.co/Qwen/Qwen2.5-0.5B-Instruct-GGUF) |
| TinyLlamas stories260K, diagnostic self-test builds only | MIT (llama2.c / TinyStories model distribution) | [ggml-org models](https://huggingface.co/ggml-org/models/tree/7ae2acaf51ef0706af00804d8fdd6ae9e70829ae/tinyllamas), [llama2.c](https://github.com/karpathy/llama2.c/blob/master/LICENSE) |

The llama.cpp MIT license is also packaged in the app's `Assets/Notices/llama.cpp.LICENSE.txt`.

## Downloaded by Services

Native service packages are optional downloads from the same signed Alpine repositories as toolchains. Docker images
and their entrypoint scripts are not downloaded, copied or executed. The initialization code is TabStudio's own.
Alpine package metadata is authoritative for the selected version's licence and corresponding source package.

| Component | Licence (depends on selected Alpine version) | Upstream/source |
| --- | --- | --- |
| PostgreSQL | PostgreSQL | https://www.postgresql.org/ / Alpine aports `main/postgresql*` |
| Redis | BSD-3-Clause for older releases; newer releases have different upstream terms; read selected package metadata | https://redis.io/ / Alpine aports `community/redis` |
| Valkey | BSD-3-Clause | https://valkey.io/ / Alpine aports `community/valkey` |
| MariaDB | GPL-2.0-only (server); package dependencies have their own licences | https://mariadb.org/ / Alpine aports `main/mariadb` |
| MongoDB, only when packaged | Version-dependent; contemporary upstream releases use SSPL-1.0 | https://www.mongodb.com/ / selected Alpine package metadata |
| RabbitMQ | MPL-2.0 | https://www.rabbitmq.com/ / Alpine aports `community/rabbitmq-server` |
| nginx | BSD-2-Clause | https://nginx.org/ / Alpine aports `main/nginx` |
| Mailpit | MIT | https://github.com/axllent/mailpit / selected Alpine package metadata |
| MinIO | AGPL-3.0-or-later | https://github.com/minio/minio / Alpine aports `community/minio` |
| YamlDotNet (Compose reader) | MIT | https://github.com/aaubry/YamlDotNet |
| Npgsql (also shipped for the Database Explorer; here used by the Services integration and template tests) | PostgreSQL | https://github.com/npgsql/npgsql |

Alpine's signed package indexes and package metadata include source origins, versions, maintainers and licences.
Corresponding package build scripts/source references are at https://gitlab.alpinelinux.org/alpine/aports; the Services
CI harness uses the same pinned PRoot sources and rootfs SHA-256 values as the Android runtime.

## Flutter and Dart (optional Toolchain Store downloads)

- Flutter, Copyright The Flutter Authors, BSD-3-Clause: https://github.com/flutter/flutter/blob/3.38.0/LICENSE
- Dart SDK and its analysis/debug tools, Copyright the Dart project authors, BSD-3-Clause: https://github.com/dart-lang/sdk/blob/main/LICENSE
- Ubuntu Base 24.04.3 is an optional glibc side environment, not embedded in the APK. It contains separately licensed packages, including GNU libc (LGPL-2.1-or-later), Bash (GPL-3.0-or-later), Git (GPL-2.0-only), OpenSSL (Apache-2.0) and curl (curl license). Package-specific copyright and license texts remain in `/usr/share/doc/*/copyright` in that environment. Ubuntu source packages: https://archive.ubuntu.com/ubuntu/pool/ and https://ports.ubuntu.com/ubuntu-ports/pool/ . Ubuntu distributions and licensing: https://ubuntu.com/legal/intellectual-property-policy .

Flutter and Dart retain their upstream LICENSE and third-party notices in `/opt/flutter` and `/opt/flutter/bin/cache/dart-sdk`. These optional downloads do not change the licensing of TabStudio.

## Embedded firmware tools (optional downloads)

These are separate processes installed on request. Compiler binaries are not newly redistributed in the APK.
Signed Alpine packages retain their license metadata and corresponding-source origin. Ordinary package versions
resolve from that signed repository; they are not separate binary archives with invented SHA-256 values.

| Component | License | Source / handling |
| --- | --- | --- |
| GNU ARM/RISC-V/AVR GCC | GPL-3.0-or-later; applicable target runtime libraries have GCC Runtime Library Exception 3.1 | [GCC](https://gcc.gnu.org/); optional signed cross-compiler packages. The runtime exception does not relicense the compiler itself. |
| GNU binutils | GPL-3.0-or-later, with included component-specific notices/exceptions | [binutils](https://www.gnu.org/software/binutils/); signed cross-binutils packages. |
| newlib | Permissive BSD/MIT/other licenses, per included source copyright files | [newlib](https://sourceware.org/newlib/); signed ARM/RISC-V packages. No single license replaces its component notices. |
| avr-libc | BSD-3-Clause and included per-file notices | [avr-libc](https://avrdudes.github.io/avr-libc/); signed Alpine package. |
| OpenOCD | GPL-2.0-or-later | [OpenOCD](https://openocd.org/); signed Alpine package. Vendor forks are not automatically downloaded. |
| HIDAPI (paired hidraw/libusb backends) | BSD-3-Clause, chosen from the package's GPL-3.0-only OR BSD-3-Clause terms | [HIDAPI](https://github.com/libusb/hidapi); both backends are installed from the same signed Alpine `hidapi` package (currently 0.15.0). For permissioned OpenOCD only, its unchanged HIDAPI calls resolve to that package's libusb backend instead of Android-inaccessible hidraw. The distro package retains its license files; no HIDAPI binary is embedded in the APK. |
| avrdude | GPL-2.0-or-later | [avrdude](https://github.com/avrdudes/avrdude); signed Alpine package. |
| dfu-util | GPL-2.0-or-later | [dfu-util](https://dfu-util.sourceforge.net/); signed Alpine package. |
| GNU GDB / gdb-multiarch and Python DAP adapter | GPL-3.0-or-later and included component notices | [GDB](https://www.gnu.org/software/gdb/); signed Alpine package. CI's AVR-capable GDB is built from signed Ubuntu `gdb-source`, retaining source and COPYING in its build tree. |
| Arduino CLI 1.5.1 | GPL-3.0-only | [arduino-cli](https://github.com/arduino/arduino-cli/tree/v1.5.1); official Linux ARM64, ARMv7 and x64 archives pinned by URL/size/SHA-256 in the catalog. Board cores and tools are separate downloads under their included licenses. |
| PlatformIO Core 6.2.0 | Apache-2.0 | [PlatformIO Core](https://github.com/platformio/platformio-core/tree/v6.2.0); isolated Python environment with the full hashed dependency lock below. Third-party platforms/cores/tools have separate licenses and architecture constraints. |
| Rust Embedded SDK 1.97.0 | MIT OR Apache-2.0; LLVM and bundled dependencies retain their included notices | [Official Rust distribution manifest](https://static.rust-lang.org/dist/channel-rust-1.97.0.toml), SHA-256 `3804d2666f7c12ce64205baa69b6be52f910b45b158091013264beb7aa1de7f5`. Native ARM64/x64 compiler/Cargo/host libraries/analyzer/formatters and ten bare-metal target libraries are independently pinned in `rust-components.lock.json`; rust-src and original installer/license files are retained. No Rust binary is bundled in the APK. Crates retain their own Cargo.lock checksums and licenses. |
| mpremote 1.26.1 | MIT | [MicroPython mpremote](https://github.com/micropython/micropython/tree/v1.26.1/tools/mpremote); isolated Python environment with hashed mpremote/platformdirs/pyserial wheels. |
| Ninja | Apache-2.0 | [Ninja](https://github.com/ninja-build/ninja); signed Alpine package. CMake, Make, Python, Git and clangd retain their notices above. |

`build/embedded/platformio.requirements.txt` and `build/embedded/mpremote.requirements.txt` contain every selected
Python distribution and SHA-256, installed with `--require-hashes --only-binary=:all:`. The same locks appear in
the Toolchain Store setup data. Installed wheel metadata and license files remain with each environment. The
following licenses were checked against those wheels' metadata/license files; downloaded board platforms have
their own dependency notices.

| Python distribution | Locked version | License |
| --- | --- | --- |
| ajsonrpc | 1.2.0 | MIT |
| anyio | 4.15.1 | MIT |
| bottle | 0.13.4 | MIT |
| certifi | 2026.10.23 | MPL-2.0 |
| charset-normalizer | 3.5.2 | MIT |
| click | 8.5.0 | BSD-3-Clause |
| colorama | 0.4.6 | BSD-3-Clause |
| h11 | 0.16.0 | MIT |
| idna | 3.20 | BSD-3-Clause |
| marshmallow | 4.3.1 | MIT |
| pip | 26.2.2 | MIT |
| platformio | 6.2.0 | Apache-2.0 |
| pyelftools | 0.33 | Public domain, with the distribution's included terms |
| pyserial | 3.5 | BSD-3-Clause |
| requests | 2.34.2 | Apache-2.0 |
| semantic-version | 2.10.0 | BSD-2-Clause |
| starlette | 1.10.0 | BSD-3-Clause |
| tabulate | 0.10.0 | MIT |
| typing_extensions | 4.16.0 | PSF-2.0 |
| urllib3 | 2.8.0 | MIT |
| uvicorn | 0.54.0 | BSD-3-Clause |
| wsproto | 1.3.2 | MIT |
| mpremote | 1.26.1 | MIT |
| platformdirs | 4.13.5 | MIT |

The new serial-control bridge and analysis/import code are TabStudio code. USB-serial control sequences retain
the usb-serial-for-android attribution above. A libusb wrapper does not replace libusb's license.

### Embedded ecosystem tools used externally or by CI

Names in build plans/host CI do not imply that complete Android installers/backends are supplied. Upstream source
and dependency notices remain authoritative.

| Component | License | Source / current handling |
| --- | --- | --- |
| Espressif ESP-IDF | Apache-2.0, with separately licensed bundled components | [ESP-IDF](https://github.com/espressif/esp-idf); Source and recursive submodules pinned to 6.1 commit `fff9895c82d744c7237be8847347bdd1b07c6643` by the optional Ubuntu SDK recipe. Native compiler/tools archives are SHA-256 checked against that source's `tools.json`. Python packages and a frozen v6.1 vendor constraint snapshot are app-owned and SHA-256 locked below; the installer does not fetch rolling Python constraints. Compiler distributions retain GCC/runtime notices. |
| esptool | GPL-2.0-or-later | [esptool](https://github.com/espressif/esptool); optional official 5.6.0 standalone ARM64/x64 distributions with pinned SHA-256, installed into glibc. Bundled dependency licenses remain in the extracted distribution; no tool binary is in the APK. |
| Raspberry Pi Pico SDK / picotool | BSD-3-Clause, with included dependency notices | [Pico SDK](https://github.com/raspberrypi/pico-sdk), [picotool](https://github.com/raspberrypi/picotool); SDK 2.3.1 and submodules pinned to `079c6f39023649b154152db30f1d781e884879bc` by the optional Ubuntu SDK recipe. Optional official picotool 2.3.1 ARM64/x64 binaries are SHA-256 pinned and installed from pico-sdk-tools; the [upstream BSD notice](native/embedded/picotool.LICENSE) is installed beside the binary. |
| Zephyr / west | Apache-2.0, with Zephyr's per-component notices | [Zephyr](https://github.com/zephyrproject-rtos/zephyr), [west](https://github.com/zephyrproject-rtos/west); Zephyr 4.4.2 source pinned to `dccb09599635bdff17633fa7e9dab014b91dce90`; west manifest modules retain their component notices. The optional Ubuntu SDK recipe installs the exact Python lock below, including west 1.5.0. |
| probe-rs / nusb | MIT OR Apache-2.0 | [probe-rs](https://github.com/probe-rs/probe-rs), [nusb](https://github.com/kevinmehall/nusb); the official native GNU probe-rs executable uses the separate permissioned usbfs/nusb bridge; native hidraw CMSIS-DAP v1 is a separate transport. |
| libusb-compat | LGPL-2.1-or-later | [libusb compatibility library](https://github.com/libusb/libusb-compat-0.1); signed Alpine package exposing legacy libusb 0.1 clients through libusb 1.0. |
| teensy_loader_cli | GPL-3.0-only | [PJRC Teensy Loader](https://github.com/PaulStoffregen/teensy_loader_cli); optional native build from SHA-256-pinned 2.3 source, with source/license retained. Normal HalfKay transfer has no readback and is excluded from verified production batches. |
| wchisp | GPL-2.0 | [WCH ISP](https://github.com/ch32-rs/wchisp); optional native build from SHA-256-pinned 0.3.0 source and its Cargo.lock; dynamic system libusb. Sources, lock and upstream dependency notices remain authoritative. |
| wlink | MIT OR Apache-2.0 | [Official source](https://github.com/ch32-rs/wlink/tree/7ed4b99321fb8e88eb05f7553211b39d41196a6c), version 0.1.3, archive SHA-256 `09f1220a9c238fc5f3537f2cf27cf7fc8a165b2845539c1b2da0938f7511e837`; unchanged Cargo.lock SHA-256 `048dc8ff2443e1739065d060c2b196cf53e8ea75dde268750ee99b429f1d91c7`. Native source build retains upstream sources, loader and dependency terms. TabStudio separately restricts USB commands; this is not a modified vendor client or permission to alter protection. |
| mspdebug | GPL-2.0-or-later | [MSPDebug](https://github.com/dlbeer/mspdebug); optional native build from SHA-256-pinned v0.26 source with signed libusb-compat dependency; source/license/TI3410 transport firmware retained. Startup scripts, BSL auto-unlock and protection operations are not requested. |
| Nordic nrf5sdk-tools 1.1.0 / original pc-nrfutil 6.1.7 | LicenseRef-Nordic-1-Clause for the complete vendor bundle, plus its included legacy/dependency license notices | [Official exact bundle](https://files.nordicsemi.com/artifactory/swtools/external/nrfutil/packages/nrfutil-nrf5sdk-tools/nrfutil-nrf5sdk-tools-x86_64-unknown-linux-gnu-1.1.0.tar.gz), SHA-256 `84b4214c46b917696b171a692a656b0f93e7200dc93dc3ff33fafd6d88b31e4f`. Downloaded on demand directly from Nordic, never redistributed in the APK. The original executable and all vendor manifests, licenses and documentation are retained unchanged. ARM64 uses the separately pinned Box64 runtime and guest serial shim. Target bootloader validates package trust. |
| CH32fun startup, linker and device declarations | MIT; included libgcc archive carries its original GPL/GCC Runtime Library Exception terms | [Pinned source](https://github.com/cnlohr/ch32fun/tree/a800cbd1f1ca5e250c73da83c669f0965d2052e5), SHA-256 `69da911012466b3e5e13053c677e4578061843a2d6947de2e919a0fdeb43b411`. Optional source SDK retains upstream LICENSE and `misc/LIBGCC_LICENSE`; the starter uses these original startup/linker files with signed native RV32EC GCC. |
| stm32flash | GPL-2.0-or-later | [stm32flash](https://sourceforge.net/projects/stm32flash/); installed through signed Alpine 3.24 community packages. |
| BOSSA / bossac | BSD-3-Clause | [BOSSA](https://github.com/shumatech/BOSSA); optional native build from SHA-256-pinned 1.9.1 source; sources/licenses retained. |
| QEMU | GPL-2.0 and included component-specific licenses | [QEMU](https://www.qemu.org/); host CI package, not physical MCU proof. |
| simavr | GPL-3.0-or-later and included component notices | [simavr](https://github.com/buserror/simavr); host CI package. |
| GNU mtools | GPL-3.0-or-later | [mtools](https://www.gnu.org/software/mtools/); optional host test dependency for independent readback of synthetic FAT images, not included in the APK. |
| Embassy | MIT OR Apache-2.0, with individual crate/dependency notices | [Embassy](https://github.com/embassy-rs/embassy); pinned example source; generated Cargo lock retained where upstream omits one. |

Toolchain Store entries install the declared native flashing distributions or source recipes; binaries are downloaded
or built by the user, never redistributed in the APK. The optional Nordic recipe uses its original self-contained legacy executable instead of patching Python
dependencies; complete vendor terms remain authoritative and the bundle is not included in the APK. Physical-board
validation remains incomplete. pymcuprog UART UPDI reuses pinned Microchip support wheels without downloading XC.


### Zephyr base Python dependency lock

`src/TabStudio.Toolchains/Embedded/Installation/zephyr.requirements.txt` contains all 35 distributions resolved
from the pinned Zephyr 4.4.2 base requirements for native Linux x64/ARM64 Python 3.12, except `reuse`, whose SPDX
checks a build does not need and whose source distribution needs Poetry and gettext to build. Each selected wheel or
source distribution is checked with `pip --require-hashes`. The bootstrap lock installs pinned setuptools and
wheel before any source build; `--no-build-isolation` prevents unpinned build dependencies. Original license
files and package metadata remain in the isolated environment. This list covers the base SDK, not optional
Zephyr documentation/test tooling or downloaded board modules.

| Python distribution | Locked version | License |
| --- | --- | --- |
| anytree | 2.13.0 | Apache-2.0 |
| attrs | 26.1.0 | MIT |
| canopen | 2.4.1 | MIT |
| certifi | 2026.10.23 | MPL-2.0 |
| charset-normalizer | 3.5.2 | MIT |
| colorama | 0.4.6 | BSD-3-Clause |
| docopt | 0.6.2 | MIT |
| idna | 3.20 | BSD-3-Clause |
| intelhex | 2.3.0 | BSD-3-Clause |
| jsonschema | 4.26.0 | MIT |
| jsonschema-specifications | 2025.9.1 | MIT |
| packaging | 26.3 | Apache-2.0 OR BSD-2-Clause |
| patool | 4.1.0 | GPL-3.0, per included license |
| psutil | 7.2.2 | BSD-3-Clause |
| pyelftools | 0.33 | Public domain, per included terms |
| pykwalify | 1.8.0 | MIT |
| pylink-square | 2.0.2 | Apache-2.0 |
| pyserial | 3.5 | BSD-3-Clause |
| python-can | 4.6.1 | LGPL-3.0-only |
| python-dateutil | 3.0.0 | Apache-2.0 OR BSD-3-Clause |
| PyYAML | 6.0.3 | MIT |
| referencing | 0.37.0 | MIT |
| requests | 2.34.2 | Apache-2.0 |
| rpds-py | 2026.9.1 | MIT |
| ruamel.yaml | 0.19.1 | MIT |
| semver | 3.1.0 | BSD-3-Clause |
| setuptools | 80.9.0 | MIT |
| six | 1.17.0 | MIT |
| tqdm | 4.70.1 | MPL-2.0 AND MIT |
| typing-extensions | 4.16.0 | PSF-2.0 |
| tzdata | 2026.5 | Apache-2.0 |
| urllib3 | 2.8.0 | MIT |
| west | 1.5.0 | Apache-2.0 |
| wheel | 0.45.1 | MIT |
| wrapt | 1.17.3 | BSD-2-Clause |

### ESP-IDF core Python dependency lock

`src/TabStudio.Toolchains/Embedded/Installation/esp-idf.requirements.txt` fixes all 63 selected distributions
for the pinned ESP-IDF 6.1 SDK on native Linux ARM64/x64 CPython 3.12. Archive hashes are verified with
`--require-hashes`; a separate lock fixes the bootstrap build tools. The esptool source release uses those
installed tools with build isolation disabled. The app carries a frozen vendor v6.1 constraint snapshot
and uses the offline vendor dependency checker. Installed package metadata and original license files
remain in the SDK environment, including cryptography's bundled component notices. The compatible CPython
3.12/abi3 manylinux wheels cover ARM64/x64; only esptool requires a source build. The frozen constraints
snapshot has SHA-256 `8fd2a7299a476370a1322e87a21ce0e24a9e5b8b6df4b34f0b8acb4a4b03d135`.
User-selected project components retain their own manifests and notices.

| Python distribution | Locked version | License |
| --- | --- | --- |
| annotated-types | 0.8.0 | MIT |
| boolean.py | 5.0 | BSD-2-Clause |
| certifi | 2026.10.23 | MPL-2.0 |
| cffi | 2.1.1 | MIT-0 |
| charset-normalizer | 3.5.2 | MIT |
| click | 8.4.2 | BSD-3-Clause |
| construct | 2.10.70 | MIT |
| cryptography | 48.0.1 | Apache-2.0 OR BSD-3-Clause |
| esptool | 5.6.0 | GPL-2.0-or-later |
| esp-coredump | 1.17.2 | Apache-2.0 |
| esp-idf-diag | 0.2.0 | Apache-2.0 |
| esp-idf-kconfig | 3.14.0 | Apache-2.0 |
| esp-idf-monitor | 1.10.0 | Apache-2.0 |
| esp-idf-nvs-partition-gen | 0.1.9 | Apache-2.0 |
| esp-idf-panic-decoder | 1.5.0 | Apache-2.0 |
| esp-idf-sbom | 1.5.0 | Apache-2.0 |
| esp-idf-size | 2.3.2 | Apache-2.0 |
| esp-pylib | 1.2.0 | Apache-2.0 |
| freertos-gdb | 1.0.4 | Apache-2.0 |
| idf-component-manager | 3.1.2 | Apache-2.0 |
| idf-drivers-gdb | 0.1.1 | Apache-2.0 |
| idna | 3.20 | BSD-3-Clause |
| intelhex | 2.3.0 | BSD-3-Clause |
| jsonref | 1.1.0 | MIT |
| license-expression | 30.4.4 | Apache-2.0 |
| linkify-it-py | 2.2.0 | MIT |
| markdown-it-py | 4.2.0 | MIT |
| mdit-py-plugins | 0.6.1 | MIT |
| mdurl | 0.1.2 | MIT |
| packaging | 26.3 | Apache-2.0 OR BSD-2-Clause |
| pathvalidate | 3.3.1 | MIT |
| platformdirs | 4.13.6 | MIT |
| psutil | 7.2.2 | BSD-3-Clause |
| pyclang | 0.7.0 | MIT |
| pycparser | 3.12 | BSD-3-Clause |
| pydantic | 2.14.1 | MIT |
| pydantic_core | 2.50.1 | MIT |
| pydantic-settings | 2.16.1 | MIT |
| pyelftools | 0.33 | Public domain, per included terms |
| pygdbmi | 0.11.0.0 | MIT |
| Pygments | 2.21.0 | BSD-2-Clause |
| pyparsing | 3.3.3 | MIT |
| pyserial | 3.5 | BSD-3-Clause |
| python-dotenv | 1.2.4 | BSD-3-Clause |
| PyYAML | 6.0.3 | MIT |
| reedsolo | 1.7.0 | Public domain, per included terms |
| requests | 2.34.2 | Apache-2.0 |
| requests-file | 3.0.1 | Apache-2.0 |
| requests-toolbelt | 1.0.0 | Apache-2.0 |
| rich | 15.0.0 | MIT |
| rich-click | 1.9.9 | MIT |
| ruamel.yaml | 0.19.1 | MIT |
| schema | 0.7.8 | MIT |
| setuptools | 80.9.0 | MIT |
| textual | 8.2.8 | MIT |
| tree-sitter | 0.26.0 | MIT |
| tree-sitter-c | 0.24.2 | MIT |
| truststore | 0.10.5 | MIT |
| typing-extensions | 4.16.0 | PSF-2.0 |
| typing-inspection | 0.4.4 | MIT |
| urllib3 | 2.8.0 | MIT |
| websockets | 17.3 | BSD-3-Clause |
| wheel | 0.45.1 | MIT |
