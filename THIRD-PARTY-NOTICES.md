# Third-party notices

TabStudio is licensed under the Apache License 2.0 (see [LICENSE](LICENSE)). It ships with, or downloads on
request, the third-party software below. Each one is under its own licence.

## Inside the APK

| Component | Licence | Source |
| --- | --- | --- |
| [PRoot](https://github.com/termux/proot), Termux's fork, 5.1.107.101 | GPL-2.0-or-later | [termux/proot v5.1.107.101](https://github.com/termux/proot/archive/v5.1.107.101.zip) |
| [talloc](https://talloc.samba.org) 2.5.0, linked into PRoot | LGPL-3.0-or-later | [talloc-2.5.0.tar.gz](https://www.samba.org/ftp/talloc/talloc-2.5.0.tar.gz) |
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
| [TypeScript](https://github.com/microsoft/TypeScript) | Apache-2.0 | npm |
| [debugpy](https://github.com/microsoft/debugpy) | MIT | PyPI |
| [vscode-js-debug](https://github.com/microsoft/vscode-js-debug) | MIT | GitHub releases |

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
