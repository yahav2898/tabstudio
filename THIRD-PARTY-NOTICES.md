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
