<div align="center">

# TabStudio

**A real IDE for Android tablets. Open a `.sln`, press F5, watch it run.**

[![License: Apache-2.0](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE)
![Status: pre-alpha](https://img.shields.io/badge/status-pre--alpha-orange.svg)
![Platform: Android 10+](https://img.shields.io/badge/platform-Android%2010%2B%20·%20arm64-3DDC84.svg)
![Built with .NET 10](https://img.shields.io/badge/.NET-10-512BD4.svg)

[עברית](README.he.md) · [Architecture](docs/ARCHITECTURE.md) · [Roadmap](docs/ROADMAP.md) · [Contributing](CONTRIBUTING.md)

</div>

---

## The short version

TabStudio turns an Android tablet into a development machine you would actually choose to use.

- **One app.** Download it, open it, write code. No Termux, no root, no companion installs.
- **The layout you already know.** Solution Explorer, Properties, Error List, Output, a toolbar with a big Run button. If you have used Visual Studio, you already know where everything is.
- **Your code runs on the tablet.** Not on a server you rent, not in someone else's cloud. Press F5 and it builds and runs right there, next to the code.
- **Open source, from day one.** Apache-2.0. Read it, fork it, ship it.

## Why this needs to exist

**Billions of people own an Android device. Far fewer own a computer.** For a student without a laptop, a phone or a tablet is the only screen they have, and today none of them can open a real C# solution, build it and debug it.

The usual answer has been "use a cloud IDE". That works until the connection drops, the free tier runs out or the account needs a credit card. A development environment should be something you *own*, that works on a bus, in a classroom without Wi-Fi, or in a country where cloud services are expensive.

**Two things changed that make this possible now:**

1. **Typing stopped being the bottleneck.** With AI assistance, a developer spends more time reading, directing and reviewing than typing. That is exactly what a large touch screen is good at.
2. **The toolchains finally fit.** .NET, Roslyn, clang and the rest now run on ARM64 and on Android's own libc. A tablet in 2026 has more cores and memory than the laptops many of today's professionals learned on.

The missing piece is the IDE that puts it together. That is TabStudio.

## What it does

> **Status:** TabStudio is in early development. The table shows what works today and what is next, so you always know where things stand.

| Capability | Status |
| --- | --- |
| Visual Studio-style shell: menus, toolbar, document tabs, docked panels, status bar | In progress |
| Solution Explorer for real `.sln` and `.csproj` files | In progress |
| C# editor with syntax highlighting and live errors (Roslyn, in-process) | In progress |
| **F5 for C# console apps**, compiled in memory and run on the tablet | In progress |
| Error List and Output panels | In progress |
| Open any repo: detect the stack from `global.json`, `package.json`, `Cargo.toml` and friends | In progress |
| Compatibility Doctor: shows where each project in a solution can run, and why | In progress |
| IntelliSense completion, signature help, quick fixes | Planned |
| Debugger: breakpoints, Locals, Watch, Call Stack | Planned |
| ASP.NET Core and Blazor with a live WebView preview | Planned |
| WinForms designer with touch drag-and-drop | Planned |
| WPF (via OpenSilver) and WinUI (via Uno Platform) | Planned |
| Toolchain Store: Rust, Go, Python, Node, C/C++ and more, built natively for Android | Planned |
| VS Code extensions via Open VSX (any language with an LSP) | Planned |
| Windows Bridge: run Windows-only apps on your own PC, streamed to the tablet | Planned |
| Polyglot notebooks (C#, F#, Python, SQL) | Planned |

The full plan, phase by phase, is in the [roadmap](docs/ROADMAP.md).

## How it works

TabStudio is one Android app built with .NET and [Avalonia](https://avaloniaui.net). Everything it needs ships inside the APK or is downloaded by the app itself.

```mermaid
flowchart LR
    subgraph APK["TabStudio · one APK"]
        Shell["IDE shell<br/>Avalonia UI"]
        Lang["Language service<br/>Roslyn, in-process"]
        Build["Build engine"]
        Shell --> Lang
        Shell --> Build
        Build --> Host{"Run host<br/>chosen by F5"}
    end
    Host --> InProc["In-process<br/>console apps, tests"]
    Host --> Web["WebView<br/>ASP.NET, Blazor, WASM"]
    Host --> Native["Android view<br/>Avalonia, Uno"]
    Host --> Bridge["Windows Bridge<br/>your PC, optional"]
```

**The shell** is the part you see: a docked, Visual Studio-style layout scaled for touch, with 44-pixel tap targets and a floating action bar for the things you would otherwise reach for with a keyboard.

**The language service** runs [Roslyn](https://github.com/dotnet/roslyn), the real C# compiler, inside the app. That is what makes errors appear as you type, and it is what will power completion and refactoring.

**The build engine** turns a project into something runnable. For C#, Roslyn compiles straight to memory against reference assemblies bundled with the app, so the edit-run loop never touches the disk. Larger builds go through MSBuild, and later through native toolchains from the Toolchain Store.

**Run hosts** are where your program actually runs. F5 picks one per project type: console apps run in-process in an isolated load context, web apps run in a WebView next to the editor, and Windows-only apps can go to a PC you own through the optional Windows Bridge.

Android restricts what an app may execute, so every design decision here starts from the platform's rules rather than fighting them. The details, including why the app does not depend on Android's Linux VM, are in [the architecture document](docs/ARCHITECTURE.md).

## Requirements

### To use TabStudio

| | Minimum | Recommended |
| --- | --- | --- |
| Android | 10 (API 29) | 14 or newer |
| CPU | 64-bit ARM (arm64-v8a) | Recent 8-core SoC |
| Memory | 6 GB | 8 GB or more for large solutions |
| Storage | 1 GB free | 4 GB free once you add toolchains |
| Input | Touch | A keyboard and trackpad, for long sessions |

A keyboard is optional. Everything in TabStudio can be reached by touch, and every Visual Studio shortcut works when a keyboard is attached.

### To build TabStudio from source

- [.NET 10 SDK](https://dotnet.microsoft.com/download) with the Android workload: `dotnet workload install android`
- JDK 17 and the Android SDK (API 35), which the Android workload uses
- Any OS: Windows, macOS or Linux

## Getting started

### Install on a tablet

1. Open the [latest release](https://github.com/yahav2898/tabstudio/releases) on the tablet.
2. Download `TabStudio.apk` and open it. Android will ask you to allow installs from your browser once.
3. Launch TabStudio, tap **Open sample solution** and press **F5**.

### Build and run from source

```bash
git clone https://github.com/yahav2898/tabstudio.git
cd tabstudio

# Run the tests
dotnet test

# Run the desktop build, handy for development
dotnet run --project src/TabStudio.Desktop

# Build the Android APK
dotnet publish src/TabStudio.Android -c Release -f net10.0-android
```

The desktop build exists so contributors can work on TabStudio from any computer. The tablet is the product.

## Repository layout

```
src/
  TabStudio.Core/          Workspace model: solutions, projects, stack detection, Compatibility Doctor
  TabStudio.Compilation/   Roslyn: diagnostics, in-memory compilation, running programs
  TabStudio.App/           The IDE itself: views and view models, shared by every platform
  TabStudio.Desktop/       Desktop host for development
  TabStudio.Android/       The Android app
tests/                     Unit tests for every library
samples/                   The sample solution bundled with the app
docs/                      Architecture, roadmap and design notes
```

## Contributing

TabStudio is built in the open, and it is early enough that one contributor can shape a whole feature. Good places to start are in the [roadmap](docs/ROADMAP.md). Read [CONTRIBUTING.md](CONTRIBUTING.md) for how the code is organized and what a good pull request looks like.

## License and trademarks

TabStudio is licensed under the [Apache License 2.0](LICENSE).

TabStudio is an independent project. It is not affiliated with, endorsed by or sponsored by Microsoft. Visual Studio is a trademark of Microsoft Corporation, used here only to describe the kind of experience TabStudio offers. TabStudio contains no Microsoft code beyond the open-source .NET components it builds on, under their own licenses.
