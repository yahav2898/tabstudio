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
| Npgsql (integration tests and user template test only) | PostgreSQL | https://github.com/npgsql/npgsql |

Alpine's signed package indexes and package metadata include source origins, versions, maintainers and licences.
Corresponding package build scripts/source references are at https://gitlab.alpinelinux.org/alpine/aports; the Services
CI harness uses the same pinned PRoot sources and rootfs SHA-256 values as the Android runtime.
