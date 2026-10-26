# Third-party notices

TabStudio is licensed under the Apache License 2.0 (see [LICENSE](LICENSE)). It ships with, or downloads on
request, the third-party software below. Each one is under its own licence.

## Inside the APK

| Component | Licence | Source |
| --- | --- | --- |
| [PRoot](https://github.com/termux/proot), Termux's fork, 5.1.107.101 | GPL-2.0-or-later | [termux/proot v5.1.107.101](https://github.com/termux/proot/archive/v5.1.107.101.zip) |
| [talloc](https://talloc.samba.org) 2.5.0, linked into PRoot | LGPL-3.0-or-later | [talloc-2.5.0.tar.gz](https://www.samba.org/ftp/talloc/talloc-2.5.0.tar.gz) |
| [box64](https://github.com/ptitSeb/box64) 0.4.4, separate ARM64 program run through PRoot | MIT; [full licence](src/TabStudio.Toolchains/Microchip/box64.LICENSE) | [Pinned source](https://github.com/ptitSeb/box64/tree/2f130fab1d6e1a4ee8a71dc60cfdfcc839ad192a); reproducible child-stack fix in [build/pic/spike.py](build/pic/spike.py) |
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
| Microchip XC8 and XC32 | Microchip software licence and included third-party component licences | Installed by the user directly from Microchip, after explicit acceptance. Compiler binaries are never redistributed by TabStudio. |
| Microchip device family packs (DFPs) | Each pack's included licence | packs.download.microchip.com; exact versions and SHA-256 hashes are pinned in the pack catalog. |
| Debian GNU/Linux glibc, libstdc++, libgcc and zlib runtime packages | Each package's included copyright and licence files (glibc LGPL-2.1-or-later; GCC runtime GPL-3.0 with GCC Runtime Library Exception; zlib licence) | Verified downloads directly from Debian. Copyright and licence files are retained under usr/share/doc. |

## PIC support downloaded or built on request

| Component | Licence | Source / handling |
| --- | --- | --- |
| [gpsim](https://gpsim.sourceforge.net/) 0.32.1 | GPL-2.0-or-later | Pinned upstream source; built natively with the GUI disabled. Source and COPYING remain with the installation. |
| [pymcuprog](https://github.com/microchip-pic-avr-tools/pymcuprog) 3.19.4.61 | MIT | Verified PyPI wheel; its included licence is retained. |
| [pyedbglib](https://github.com/microchip-pic-avr-tools/pyedbglib) 2.24.2.18 | MIT | Verified PyPI wheel. |
| [pk2cmd](https://github.com/jaka-fi/pk2cmd) at 38ad052 | **Non-free Microchip PICkit Software License Agreement**, not GPL | Separate acceptance before source download. No device database or PICkit firmware is redistributed/downloaded by TabStudio. The upstream release/database removal is documented in [PIC.md](docs/PIC.md). |
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
