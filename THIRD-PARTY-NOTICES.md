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
| [pk2cmd](https://github.com/jaka-fi/pk2cmd) at 38ad052 | **Non-free Microchip PICkit Software License Agreement**, not GPL | Separate acceptance before source download. No device database is downloaded; no PICkit firmware is redistributed or flashed by TabStudio. The upstream release/database removal is documented in [PIC.md](docs/PIC.md). |
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

## Flutter and Dart (optional Toolchain Store downloads)

- Flutter, Copyright The Flutter Authors, BSD-3-Clause: https://github.com/flutter/flutter/blob/3.38.0/LICENSE
- Dart SDK and its analysis/debug tools, Copyright the Dart project authors, BSD-3-Clause: https://github.com/dart-lang/sdk/blob/main/LICENSE
- Ubuntu Base 24.04.3 is an optional glibc side environment, not embedded in the APK. It contains separately licensed packages, including GNU libc (LGPL-2.1-or-later), Bash (GPL-3.0-or-later), Git (GPL-2.0-only), OpenSSL (Apache-2.0) and curl (curl license). Package-specific copyright and license texts remain in `/usr/share/doc/*/copyright` in that environment. Ubuntu source packages: https://archive.ubuntu.com/ubuntu/pool/ and https://ports.ubuntu.com/ubuntu-ports/pool/ . Ubuntu distributions and licensing: https://ubuntu.com/legal/intellectual-property-policy .

Flutter and Dart retain their upstream LICENSE and third-party notices in `/opt/flutter` and `/opt/flutter/bin/cache/dart-sdk`. These optional downloads do not change the licensing of TabStudio.
