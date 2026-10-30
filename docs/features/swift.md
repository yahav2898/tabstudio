# Swift (experimental)

Swift packages build, run, test and debug on the tablet with the official Swift toolchain for Linux. Swift is
marked **experimental**: it works end to end in CI on ARM64 and x86-64 Linux, but it has not been measured on a
physical tablet, and some of SwiftPM's features depend on what Linux offers.

## Runtime

Swift.org publishes Swift for glibc Linux only, and TabStudio's Alpine environment uses musl. Swift therefore
installs into a shared **Ubuntu 24.04 glibc environment** beside Alpine, run with the same PRoot, as Flutter's and
the embedded SDKs' are. JetBrains' Kotlin language server uses the same environment.

| Part | Version | Source and check |
| --- | --- | --- |
| Ubuntu Base | 24.04.3 | SHA-256 pinned; the system packages Swift.org lists for Ubuntu 24.04 come from Ubuntu's signed archive |
| Swift toolchain | 6.4.1 | `swift-6.4.1-RELEASE-ubuntu24.04-aarch64.tar.gz` (1.1 GB, SHA-256 `b85dd70e…f45`) or the x86-64 archive (SHA-256 `7d246c71…e8b`) from download.swift.org |
| sourcekit-lsp, LLDB, lldb-dap | from the toolchain | the same archive |

The download is about 1.25 GB and the installed toolchain about 3.6 GB; the Store says so before it starts. ARMv7 is
not supported: Swift.org builds no 32-bit ARM toolchain.

The project's own programs (`.build/debug/...`) run in the Ubuntu environment too. The Terminal stays Alpine; it does
not see `swift`.

## What works

| | How |
| --- | --- |
| Packages | A folder with `Package.swift`. Opening it resolves its dependencies (`swift package resolve`). |
| F5 / Ctrl+F5 | `swift build --product <executable>`, then `.build/debug/<executable>`; with several executables, the one the file you are editing belongs to |
| A Vapor app | Runs with `serve --hostname 127.0.0.1 --port <free port>`, in the web preview |
| Loose files | `swiftc -g -Onone file.swift`, then the program; Run Current File |
| Ctrl+Shift+B | `swift build`, with compiler errors in the Error List |
| IntelliSense | sourcekit-lsp: completion, Quick Info, Go to Definition, references, rename and diagnostics |
| Debugger | lldb-dap: breakpoints in the package and its libraries, stepping, Locals, Watch and the Call Stack |
| Test Explorer | XCTest (`test*` methods of an `XCTestCase`) and Swift Testing (`@Test` functions) found in `Tests/`, run with `swift test --parallel --xunit-output`, filtered to the tests you choose |
| Template | **Swift Command-Line Tool (experimental)**: an executable, a library it uses, and a Swift Testing test |

## Verified in CI

- Probe runs on GitHub's ARM64 and x86-64 runners, in Ubuntu 24.04: the toolchain builds and runs a package,
  `swift test` runs XCTest and Swift Testing and writes both xUnit reports, sourcekit-lsp completes `count` on a
  `String`, and lldb-dap verifies a breakpoint in a library, stops there and shows its locals (`answer = 42`).
- The IDE job opens the template: sourcekit-lsp completion, a compiler error in the Error List, the program run
  with its input answered, Swift Testing in Test Explorer, and an LLDB breakpoint with its locals.
  Screenshots: [completion](../images/toolchains/swift-completion.png), [breakpoint](../images/toolchains/swift-breakpoint.png).
- The real-repositories job builds `apple/swift-argument-parser` (1.8.2) and runs its `roll` example, and builds
  `vapor/template-bare` and checks that `/` answers `It works!` in the web preview.

## Limits

- **Not measured on a tablet.** Building a Vapor app compiles SwiftNIO and its dependencies: expect several minutes
  and more than 2 GB of memory on the first build. A device with 4 GB of RAM may need TabStudio Link for large
  packages.
- **Linux, not Apple platforms.** Foundation is swift-corelibs-foundation; SwiftUI, UIKit, AppKit and Combine do not
  exist on Linux. iOS and macOS apps cannot build here; use **Build for iOS…** in a Capacitor or Flutter project.
- **No Hot Reload.** Saving rebuilds on the next F5.
- **The Terminal** is Alpine and has no `swift`; IDE-started builds, runs, tests, sourcekit-lsp and lldb-dap use the
  Ubuntu environment.
- SwiftPM plugins that download prebuilt macOS binaries, and packages that need system libraries Ubuntu 24.04 does not
  have, fail as they would on a Linux computer.
