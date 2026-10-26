# Third-party notices

TabStudio is licensed under the Apache License 2.0 (see [LICENSE](LICENSE)). It ships with, or downloads on
request, the third-party software below. Each one is under its own licence.

## Inside the APK

| Component | Licence | Source |
| --- | --- | --- |
| [llama.cpp / ggml](https://github.com/ggml-org/llama.cpp/tree/4762ad7316dcdec20016ab5985fb46a27902204d), b6000, linked into `libtabstudio-ai.so` | MIT | [Pinned sources](https://github.com/ggml-org/llama.cpp/tree/4762ad7316dcdec20016ab5985fb46a27902204d), [license](native/local-ai/LICENSE.llama.cpp.txt) |
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

## Optional local AI models

Models are downloaded only after the user sees and accepts the model's full upstream license. They are not included in release APKs. The catalog pins immutable Hugging Face revisions and SHA-256 hashes. Full license copies are embedded beside the catalog under `src/TabStudio.Agent/Local/Models`, and each entry links to its upstream license.

| Model | License | Upstream |
| --- | --- | --- |
| Qwen2.5-Coder 1.5B Instruct Q4_K_M | Apache-2.0 | [Qwen](https://huggingface.co/Qwen/Qwen2.5-Coder-1.5B-Instruct-GGUF) |
| Qwen2.5-Coder 3B Instruct Q4_K_M | Qwen Research License | [Qwen](https://huggingface.co/Qwen/Qwen2.5-Coder-3B-Instruct-GGUF) |
| Qwen2.5 0.5B Instruct Q4_K_M | Apache-2.0 | [Qwen](https://huggingface.co/Qwen/Qwen2.5-0.5B-Instruct-GGUF) |
| TinyLlamas stories260K, diagnostic self-test builds only | MIT (llama2.c / TinyStories model distribution) | [ggml-org models](https://huggingface.co/ggml-org/models/tree/7ae2acaf51ef0706af00804d8fdd6ae9e70829ae/tinyllamas), [llama2.c](https://github.com/karpathy/llama2.c/blob/master/LICENSE) |

The llama.cpp MIT license is also packaged in the app's `Assets/Notices/llama.cpp.LICENSE.txt`.
