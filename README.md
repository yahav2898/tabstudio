<div align="center">

# TabStudio

**A full IDE for Android tablets.**<br/>
Open a solution, press F5, and it builds, runs and debugs on the tablet itself.

[![License: Apache-2.0](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE)
[![CI](https://github.com/yahav2898/tabstudio/actions/workflows/ci.yml/badge.svg)](https://github.com/yahav2898/tabstudio/actions/workflows/ci.yml)
[![Latest release](https://img.shields.io/github/v/release/yahav2898/tabstudio?label=release)](https://github.com/yahav2898/tabstudio/releases/latest)
![Platform: Android 9+](https://img.shields.io/badge/platform-Android%209%2B%20·%20ARM-3DDC84.svg)
![Built with .NET 10](https://img.shields.io/badge/.NET-10-512BD4.svg)

[**Download**](https://github.com/yahav2898/tabstudio/releases/latest) ·
[עברית](README.he.md) ·
[Architecture](docs/ARCHITECTURE.md) ·
[Roadmap](docs/ROADMAP.md) ·
[Contributing](CONTRIBUTING.md)

<img src="docs/images/02-ide.png" alt="TabStudio on a tablet: the editor, Solution Explorer, Error List and the floating touch bar" width="860" />

</div>

## Contents

- [Overview](#overview)
- [Getting started](#getting-started)
- [Features](#features)
  - [The IDE](#the-ide)
  - [Languages](#languages)
  - [Backing services without Docker](#backing-services-without-docker)
  - [Flutter and Dart (experimental)](#flutter-and-dart-experimental)
  - [App platforms](#app-platforms)
  - [Every screen at once](#every-screen-at-once)
  - [Projects and repositories](#projects-and-repositories)
  - [Database Explorer](#database-explorer)
  - [Remote development over SSH](#remote-development-over-ssh)
  - [Web servers and mobile apps](#web-servers-and-mobile-apps)
  - [GitHub](#github)
  - [AI pair programmer](#ai-pair-programmer)
  - [Offline AI](#offline-ai)
  - [Built for the device in your hands](#built-for-the-device-in-your-hands)
- [How it works](#how-it-works)
- [Requirements](#requirements)
- [Building from source](#building-from-source)
- [Repository layout](#repository-layout)
- [Contributing](#contributing)
- [License and trademarks](#license-and-trademarks)

## Overview

TabStudio turns an Android tablet into a development machine you would choose to use. It is one app: no Termux, no
root, no companion installs and no cloud account. Your code builds and runs on the tablet, next to the editor.

| | |
| --- | --- |
| **Familiar** | The Visual Studio layout and shortcuts, rebuilt for touch: Solution Explorer, Properties, Error List, Output, a big Run button. |
| **Real tools** | The real C# compiler (Roslyn) in the app, and the official toolchains for Python, Node.js, Go, Rust, C/C++ and Java. |
| **Every workload** | Console, ASP.NET Core and Blazor, Avalonia, Windows Forms, WPF and WinUI, all running on the tablet. |
| **Yours** | Works offline, keeps your code on the device, and is open source under Apache-2.0. |

**Why it exists.** Billions of people own an Android device; far fewer own a computer. For many students a tablet or
phone is the only screen they have, and cloud IDEs stop working when the connection drops, the free tier ends or a
credit card is required. Two things make a real IDE on a tablet possible now: AI assistance has shifted development
toward reading, directing and reviewing, which is what a large touch screen does well; and .NET, Roslyn, clang and
the rest now run on ARM64 and Android. TabStudio is the IDE that puts it together.

## Getting started

1. On the tablet, open the [latest release](https://github.com/yahav2898/tabstudio/releases/latest) and download
   `TabStudio-<version>.apk`.
2. Open the file. Android asks once to allow installs from your browser or file manager.
3. Launch TabStudio, tap **Open the sample** and press **F5**.

From there, try **New project** for a template in any language, **Open from GitHub** with a repository's address, or
**Tools > Toolchain Store** to add a language. Each release installs as an update over the previous one.

<table>
  <tr>
    <td align="center"><img src="docs/images/02-ide-light.png" alt="The light theme" width="280" /><br/><sub>Light theme</sub></td>
    <td align="center"><img src="docs/images/blazor-01-web-app-beside-code.png" alt="A Blazor app served on the tablet beside its code" width="280" /><br/><sub>A web app beside its code</sub></td>
    <td align="center"><img src="docs/images/06-portrait.png" alt="Portrait layout" width="150" /><br/><sub>Portrait</sub></td>
  </tr>
</table>

## Features

### The IDE

- **The layout you already know, on any screen.** Menu bar, toolbar, document tabs, Solution Explorer with
  Properties, Error List, Output and a status bar. An activity bar holds every tool window, with badges for changed
  files, CI, failed tests and the agent's proposals. In portrait and on phones the side bar becomes a drawer, and the
  toolbar and status bar fold their least used buttons into **More** instead of clipping.
- **One search for everything.** **Ctrl+Q** finds files, menu commands with their shortcuts, settings, types and
  members, templates and recent solutions, ranked together.
- **Sized for fingers, quiet for keyboards.** Targets are 34 to 48 pixels tall. A floating touch bar puts Rename,
  Peek, Comment, Run to Cursor, Format and Run one tap away; it stays clear of the line you edit and fades while you
  type on a hardware keyboard, where every Visual Studio shortcut works.
- **Scrolls like a native editor.** A drag follows your finger exactly and a flick glides on Android's own fling
  curve, at the display's full refresh rate, even in files of thousands of lines.
- **Desktop mode.** Connect a monitor over USB-C or HDMI, or a wireless display, and the IDE moves to the big screen
  with your documents, caret and debugger as they were. The tablet becomes a touch companion with Run and debugger
  buttons, a Toolbox and a Properties panel.
- **Light, dark or the system's.** A light theme modeled on Visual Studio Light, the dark one, or follow the
  tablet's setting (**Tools > Theme**). Every color passes WCAG AA.

### Languages

| Language | Build and run | IntelliSense | Debugger |
| --- | --- | --- | --- |
| **C#** | Roslyn in the app, compiled to memory, F5 in under a second | Roslyn: completion, signature help, Quick Info, navigation, rename, quick fixes | Breakpoints, stepping, Locals, Watch, Call Stack, DataTips, exceptions |
| **Python** | CPython from the Toolchain Store | Pyright | debugpy |
| **JavaScript / TypeScript** | Node.js from the Toolchain Store | typescript-language-server | vscode-js-debug, including child processes |
| **Go** | `go build` | gopls | Delve |
| **Rust** | Cargo, with workspace members | rust-analyzer | LLDB |
| **C / C++** | CMake, Make or plain sources, with clang | clangd, from CMake's compilation database | LLDB |
| **Java** | Maven, Gradle or plain sources, OpenJDK | Eclipse JDT LS | java-debug |

- **C# on the tablet.** NuGet packages restore on the device, so real projects build as they are. Test Explorer runs
  xUnit, NUnit and MSTest, and **Live Unit Testing** marks every covered line with a pass, fail or not-covered glyph
  and reruns the affected tests as you type. C# notebooks run cells as you write them.
- **C# Hot Reload and Edit and Continue on desktop.** In the desktop host, run a local Debug console, Avalonia, ASP.NET Core or server-side
  Blazor app, then tap **Hot Reload** or press **Alt+F10** to apply current edits without restarting. **Debug
  > Hot Reload C# on Save** applies them on save; while paused at a breakpoint, **Continue** applies them
  first. Avalonia recreates its content and web frameworks clear their metadata caches. Unsupported edits
  offer **Restart to apply edits**. Existing calls finish their old code; later calls use the changes. Android
  keeps the IDE and Roslyn on the JIT and requires a restart to apply C# edits in 0.15. On desktop, `dotnet run --project
  src/TabStudio.Desktop` uses the included launch profile, or start with `DOTNET_MODIFIABLE_ASSEMBLIES=debug`.
  Browser WebAssembly, remote runs and Release user projects need a restart. See [Hot
  Reload](docs/ARCHITECTURE.md#hot-reload).
- **C# history.** **Debug > Start with Recording** captures shallow state into a configurable ring (64 MiB by
  default). Step Back (**Shift+F10**), Step Back Into, Reverse Continue and the timeline inspect earlier
  Locals, Watch, Call Stack and DataTips, marked **historical**, even after exit or a crash. A variable's
  **When did this change?** action finds its last recorded write. Simple divide-by-zero and null-reference
  causes link to the faulty write. Recording is off by default; history inspection never re-executes code.
  During a recording, applying C# edits requires **Restart to apply edits**, which starts a new recording.
  Continue without edits, or after reverting the edits, resumes the existing recording. See [recording details
  and overhead](docs/debugging/time-travel.md).
- **The Toolchain Store.** Languages and SDKs install into a Linux environment inside the app: Alpine Linux run
  with PRoot, with no root and no special permissions. Alpine packages are verified against Alpine's signatures;
  SDK components can also use official upstream archives with pinned SHA-256 checks.
- **Everywhere the same.** Output and program input live in the Output panel, compiler errors land in the Error List
  with links to their lines, **Ctrl+F5** runs without the debugger, and **Ctrl+`** opens a terminal. If a language
  server or debug adapter is missing, the status bar, the editor and F5 offer to install it.
- **More languages through extensions.** VS Code extensions from Open VSX bring grammars, snippets, themes and
  language settings for anything else.

### Backing services without Docker

**View > Services** (also in Ctrl+Q) reads `compose.yaml`, `compose.yml`, `docker-compose.yml` or
`docker-compose.yaml`, plus `.devcontainer/devcontainer.json` Compose references and overrides. PostgreSQL,
Redis/Valkey, MySQL via MariaDB, RabbitMQ, nginx, Mailpit, MinIO, and MongoDB when Alpine packages it, run as native
PRoot processes. Start all, stop, restart, health, per-service logs and localhost preview links are in one pane.
F5 starts the startup project's dependencies first and rewrites Compose/appsettings connection hosts to localhost;
`ConnectionStrings__<service>`, `<SERVICE>_HOST` and `<SERVICE>_PORT` are also supplied to the launched app.

Each workspace keeps its own data in private app storage. PostgreSQL initializes the configured user, password and
database and runs `/docker-entrypoint-initdb.d` SQL and shell scripts once. Image versions are matched against the
signed Alpine index: PostgreSQL majors never silently change, and other version substitutions are shown in logs.
Unsupported images explain why and offer a remote host. This is a native development runner, not Docker emulation:
see [Services](docs/SERVICES.md) for mappings, volumes, networking, supported Compose fields and the CI harness.

### Flutter and Dart (experimental)

The Flutter Toolchain Store entry installs Flutter and its bundled Dart SDK in a separate Ubuntu glibc PRoot environment on ARM64/x64 tablets. Open `pubspec.yaml` projects, accept **flutter pub get**, and run the counter templates in the web preview. Dart IntelliSense uses the analysis server; Dart console debugging uses DAP. Saving Dart code hot-restarts Flutter web; Debug offers Flutter Hot Reload and Hot Restart for native VM-service targets.

Native Android deployment of Flutter apps through the Android SDK toolchain is not verified yet. Embedded `web-server` previews do not provide VM breakpoints without a browser debugging bridge. See [the SDK spike and verification status](docs/flutter.md) before enabling this experimental feature in a release.

### App platforms

| Workload | How it runs on the tablet |
| --- | --- |
| Console apps and class libraries | Compiled in memory and run in-process |
| ASP.NET Core, Blazor, Razor Pages, MVC | Served on the tablet and previewed live beside the code; WebAssembly included |
| Avalonia | Rendered natively beside the editor, with a live XAML designer |
| Windows Forms | TabStudio's Windows Forms layer, with a touch drag-and-drop designer |
| WPF | OpenSilver, in the web preview, with XAML IntelliSense |
| WinUI 3 and Uno Platform | Uno's Skia renderer, natively on Android |
| Anything Windows-only | The Windows Bridge: built and run on a Windows PC you own, streamed beside your code |

- **Windows Forms.** Apps run in a Windows 10 window with dialogs, menus, grids, data binding and message boxes.
  `Form1.cs` opens in a touch designer: tap a tool and then the form, drag and resize controls on a grid, edit them in
  Properties, and double-tap a button to write its Click handler. The designer writes `Form1.Designer.cs` exactly as
  Visual Studio does. See [what the Forms layer supports](src/TabStudio.Forms/README.md).
- **WPF.** XAML compiles on OpenSilver and runs in the web preview; saving restarts the app. The XAML editor has
  completion, Quick Info, Go to Definition, live errors and a light bulb that adds a missing event handler
  ([completion](docs/wpf/images/wpf-01-xaml-completion.png), [event handler](docs/wpf/images/wpf-02-add-event-handler.png)).
  WPF APIs that need Windows are named in the Error List with a route to the Windows Bridge.
- **WinUI 3 and Uno Platform.** Projects build on the tablet and render natively beside the code; saving XAML or C#
  restarts them.
- **Windows Bridge.** For what truly needs Windows (P/Invoke, COM, .NET Framework, Visual C++), pair a Windows PC with
  a QR code (**File > TabStudio Link > Be a Windows Bridge** on the PC). Projects build and run there and stream into
  a pane beside your code, with touch, keyboard and clipboard going back, errors mapped onto your files, and .NET
  debugging through the same debugger.

### Every screen at once

- **Run on Swarm.** Phones and tablets running TabStudio join your swarm with one scan (**File > Join a Swarm**).
  Choose Swarm in the Run menu and F5 starts the app on all of them; every save updates them all.
- **Debug on any device.** A breakpoint hit on a phone stops in the tablet's debugger with that phone's Locals and
  Call Stack. Pause one device or all of them.
- **Device Matrix.** Every screen side by side: your phones, the App Preview, and virtual devices from a small phone
  to a tablet, in light and dark, left to right and right to left, and with large fonts. A tap on one screen happens
  on all of them.
- **Layout checks.** Each screen is checked after every change for clipped text, off-screen and overlapping controls,
  small touch targets and low contrast. Findings land in the Error List with a picture, one tap from **Fix with Agent**.
- **Recorded UI tests.** Record a few taps on any screen and get a UI test that replays on every device, in Test
  Explorer and in CI.

### Projects and repositories

- **Open from GitHub.** Paste a repository's address; TabStudio downloads it, detects what it is built with, opens its
  main solution and selects its build, run and debug plans.
- **Automatic project setup.** **Set projects up when they open** is on by default and available through Tools
  and Ctrl+Q. Opening a Python or Node.js repository installs its toolchain and packages, with progress and
  cancellation: pip uses a project venv; npm uses `npm ci` when a lockfile is present, otherwise `npm
  install`. Setup runs once per manifest change. Monorepo packages remain separate run targets, while declared
  npm workspaces share their root install.
- **Static web sites.** Open a folder with `index.html` or create a **Static Web Site**. F5 serves it in the
  web preview; saved HTML reloads, and saved CSS is replaced without losing page state. Node.js from the
  Toolchain Store supplies HTML, CSS and JSON completion and diagnostics through
  `vscode-langservers-extracted`.
- **Compatibility Doctor.** For every project it shows where it runs on the tablet and why, and fixes what stands in
  the way.
- **Real-world project files.** SDK-style projects, `Directory.Build.props` and `.targets`, project references,
  central package management and multi-targeting are read as MSBuild reads them.

### Database Explorer

**View > Database Explorer** (also in Ctrl+Q and the activity bar) connects to SQLite files, PostgreSQL
through Npgsql and SQL Server through Microsoft.Data.SqlClient. Browse databases, schemas, tables, views,
columns, keys and indexes. Discover connection strings from the solution's `appsettings*.json` and user
secrets, then choose which to save. Android encrypts saved credentials with an Android Keystore key; desktop
credentials last for the session.

Open **New query** for SQL completion from the connected schema. **Ctrl+Enter** runs the selection or the
whole tab; Cancel interrupts the command. Results have sortable columns, CSV/JSON copying, multiple result
sets, row counts and timings. **Edit data** pages through tables and applies individual cell changes with a
primary-key and original-row concurrency check. The **EF Core** tab builds projects in memory to list
DbContexts and migrations, update the configured database, or add a migration. See [Database
Explorer](docs/DATABASE.md) for setup and limits.

### Remote development over SSH

Choose **File > Open Remote Folder (SSH)** to open Remote Explorer, save a host or import `~/.ssh/config`,
generate a device key and open a remote source folder. Android identities are generated inside Android
Keystore and remain non-exportable. SFTP caches files for Solution Explorer, editing and search; Save uploads
with conflict detection, Terminal opens a remote shell, and F5 / Build execute the folder's saved SSH
commands. Forward a remote web port into the web preview. **File > Publish to server** publishes an ASP.NET
Core project locally, uploads a new release through SFTP and restarts its existing systemd service. See [SSH
setup, publishing and limits](docs/REMOTE_SSH.md).

### Web servers and mobile apps

Programs in any toolchain can offer **App listening on port N**: open the local server in the web preview
beside code or in the device browser. The ASP.NET and Blazor preview also has **Open in browser**.

The **Android SDK** Toolchain Store entry installs OpenJDK 17, Gradle, SDK tools/platform 35 and
SHA-256-pinned ARM64/ARMv7 replacements for native Android build tools. Create **Android App (Kotlin)**, then
F5 builds an APK, asks Android to install it and launches it. **Build > Build Android APK** builds without
installing. Java editing uses jdtls; Kotlin uses kotlin-language-server, with compiler diagnostics in the
Error List. **Logcat** shows the built app's own debug process.

Create **Capacitor App** for a Vite + TypeScript app: F5 runs its web part, and **Run Android App**
builds/syncs Capacitor and installs the native APK. **Build for iOS…** generates a reviewable GitHub Actions
workflow and signing guide; **Run iOS Workflow…** uses the existing Actions progress, logs and IPA artifact
download. A Mac is never needed locally, but an Apple Developer account and repository signing secrets are
required. See [mobile workflows](docs/MOBILE.md) for setup and limitations.

### GitHub

- **Git built in.** Clone, branch, commit, pull and push, and open pull requests.
- **Pull request review.** Description, conversation, changed files and commits in one page; comment on diff lines,
  approve or request changes, merge, or check out the branch. **Review with Claude** drafts comments for you to keep
  or dismiss.
- **GitHub Actions.** Your commit's checks in the status bar, runs with their jobs and steps, logs folded by step and
  scrolled to the first error, re-runs, workflow dispatch, and CI errors in the Error List beside your local ones.

### AI pair programmer

- **Claude, inside the IDE.** The agent sees the whole solution, builds it, runs the tests and fixes what fails.
  Every change arrives as a diff you approve.
- **Fix with Agent.** From an error, a CI failure, a layout finding or a Compatibility Doctor row, one tap hands the
  problem to the agent with its context.
- **Port to Avalonia.** Use the Compatibility Doctor, Solution Explorer context menu or Project menu to
  convert a C# WPF or Windows Forms project into a sibling Avalonia project; the original stays unchanged. The
  summary links structured port notes to source lines, can send them to the agent as one task of proposed
  changes, and offers undo. F5 runs the converted project. See [porting behavior and limits](docs/PORTING.md).
- **Sketch to XAML** with the stylus, and **MCP** servers for your own tools.

### Offline AI

Open **Agent settings**, choose a local GGUF model, read and accept its license, then download it. **Local
mode** runs chat, explanations, fixes and test generation on the tablet with the same Keep/Undo proposals. Its
badge says **Code and prompts stay on device**; local mode has no cloud or MCP connection.

Enable **Inline completions** for ghost text in any editor language: **Tab** accepts, **Escape** or typing
cancels. Context includes the current file and unsaved open neighbors. Completion pauses when the device is
hot or below 16% battery while unplugged. The model manager shows download size, estimated RAM and speed
measured on this device, and offers cancel, unload and delete.

Qwen2.5-Coder 1.5B/3B Q4 and Qwen2.5 0.5B chat are offered at pinned revisions with SHA-256 verification.
Models are optional downloads; their individual licenses apply (the 3B model uses the Qwen Research License).
Local AI requires a 64-bit build with the native runtime. See [runtime measurements and
reproduction](docs/local-ai/spike.md).

### Built for the device in your hands

- **Runs on modest tablets.** On a 3 GB tablet TabStudio keeps a full compiler model only of the projects you edit,
  reads .NET metadata from storage, and gives memory back as soon as Android asks.
- **Borrow your phone's power.** With **TabStudio Link**, the same app on a phone or PC becomes a build and language
  server for the tablet over local Wi-Fi, end-to-end encrypted, paired with a QR code.
- **Never loses your place.** Your session, unsaved edits included, is saved as you work and restored when Android
  closes the app.
- **Thermal-aware builds and offline by default.** Builds use fewer cores when the tablet runs hot, and everything
  except downloads, Git remotes and cloud agent mode works without a connection.

The order in which features ship is in the [roadmap](docs/ROADMAP.md).

## How it works

TabStudio is one Android app built with .NET 10 and [Avalonia](https://avaloniaui.net). Everything it needs ships in
the APK or is downloaded by the app itself.

```mermaid
flowchart LR
    subgraph APK["TabStudio · one APK"]
        Shell["IDE shell<br/>Avalonia UI"]
        Lang["Language services<br/>Roslyn in-process · LSP"]
        Build["Build engine"]
        Shell --> Lang
        Shell --> Build
        Build --> Host{"Run host<br/>chosen by F5"}
    end
    Toolchains["Toolchain Store<br/>Alpine Linux on PRoot"] --> Build
    Host --> InProc["In-process<br/>console apps, tests"]
    Host --> Web["WebView<br/>ASP.NET, Blazor, WPF"]
    Host --> Native["Native view<br/>Avalonia, Uno, Windows Forms"]
    Host --> Linux["Linux process<br/>Python, Node, Go, Rust, C/C++, Java"]
    Host --> Bridge["Windows Bridge<br/>your PC, optional"]
```

- **The shell** is the Visual Studio-style layout, scaled for touch.
- **The language services** run [Roslyn](https://github.com/dotnet/roslyn) inside the app for C#, and language
  servers (LSP) and debug adapters (DAP) for every other language.
- **The build engine** reads project files as MSBuild does and restores NuGet packages on the device. C# compiles
  straight to memory; other languages build with their toolchains.
- **Run hosts** are where programs run: in-process, in a WebView, natively beside the editor, as a Linux process, or
  on a paired Windows PC.

Android restricts what an app may execute, so every design decision starts from the platform's rules. The details
are in the [architecture document](docs/ARCHITECTURE.md).

## Requirements

| | Minimum | Recommended |
| --- | --- | --- |
| Android | 9 (API 28) | 14 or newer |
| CPU | 32-bit or 64-bit ARM | A recent 8-core ARM64 |
| Memory | 3 GB, or 2 GB with TabStudio Link | 8 GB or more for large solutions |
| Storage | 1 GB free | 4 GB free once you add toolchains |
| Input | Touch | A keyboard and trackpad for long sessions |

That covers most tablets sold in the last six years, including school tablets that run 32-bit Android on a 64-bit chip.

## Building from source

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) with the Android workload
(`dotnet workload install android`), JDK 17 or newer, and the Android SDK (API 36). Any OS works.

```bash
git clone https://github.com/yahav2898/tabstudio.git
cd tabstudio

dotnet test                                                       # run the tests
dotnet run --project src/TabStudio.Desktop                        # the desktop build, for development
dotnet publish src/TabStudio.Android -c Release -f net10.0-android  # the Android APK
```

The desktop build lets contributors work on TabStudio from any computer. The tablet is the product.

## Repository layout

```
src/
  TabStudio.Core/            Workspace model: solutions, projects, detection, the Compatibility Doctor
  TabStudio.Compilation/     Roslyn: diagnostics, IntelliSense, in-memory compilation, running programs
  TabStudio.Packages/        NuGet restore on the device
  TabStudio.Database/        SQLite, PostgreSQL and SQL Server connections, schema, queries and table editing
  TabStudio.Debugging/       The C# debugger: instrumented builds, breakpoints, stepping, inspection
  TabStudio.TestRunner/      xUnit, NUnit and MSTest discovery and execution
  TabStudio.LiveTesting/     Live Unit Testing: coverage, impact analysis, the live test engine
  TabStudio.Notebooks/       C# notebooks: .ipynb and .dib, the kernel, value formatting
  TabStudio.Toolchains/      Alpine/PRoot toolchains, verified SDK downloads, processes and the terminal
  TabStudio.Services/        Compose and devcontainer backing services run natively on Alpine/PRoot
  TabStudio.LanguageServers/ LSP and DAP clients for every language other than C#
  TabStudio.Extensions/      VS Code extensions from Open VSX
  TabStudio.Web/             Web apps: static web assets, Blazor, .NET on WebAssembly
  TabStudio.Forms/           Windows Forms on Avalonia
  TabStudio.Forms.Design/    The Windows Forms touch designer's model
  TabStudio.Wpf/             WPF on OpenSilver
  TabStudio.Porting/         WPF and Windows Forms conversion to sibling Avalonia projects and port notes
  TabStudio.Uno/             WinUI and Uno Platform on Skia
  TabStudio.SourceControl/   Git and GitHub: changes, branches, pull requests, Actions
  TabStudio.Remote/          SSH.NET, device keys, SFTP source caches, remote shells and server publishing
  TabStudio.Agent/           Cloud and offline coding agents, MCP, sketch to XAML
  TabStudio.Link/            TabStudio Link: pairing, encrypted transport, the remote language service
  TabStudio.Swarm/           Swarm: running, debugging and recording an app on every joined device
  TabStudio.WindowsBridge/   The Windows Bridge protocol, shared by the tablet and the PC
  TabStudio.WindowsBridge.Windows/  The Windows Bridge host that runs on the PC
  TabStudio.App/             The IDE: views and view models, shared by every platform
  TabStudio.Desktop/         Desktop host for development
  TabStudio.Android/         The Android app
tests/                       Tests for every library, and shared test helpers
samples/                     The sample solution bundled with the app
docs/                        Architecture, roadmap, design and release notes
build/                       CI and build scripts
```

## Contributing

TabStudio is built in the open, and one contributor can still shape a whole feature. Start with the
[roadmap](docs/ROADMAP.md), and read [CONTRIBUTING.md](CONTRIBUTING.md) for how the code is organized, how CI works
and what a good pull request looks like.

## Microchip PIC on the tablet

Install **Microchip XC8** (PIC10/12/16/18) or **Microchip XC32** (PIC32) in the Toolchain Store after reading Microchip's licence. The official x86-64 Linux compilers run through Box64 in the tablet's Alpine/PRoot environment, with verified glibc runtimes and device packs. **The compilers are installed by the user under Microchip's licence and are never redistributed.** No PC or licence-manager activation is needed for local builds.

Create **PIC18 Blink (XC8)** or **PIC32 Blink (XC32)**, or open an MPLAB X project with `nbproject/configurations.xml` and its generated makefiles. Builds retain MPLAB's options, populate the Error List, show memory usage and prepare clangd's device headers/builtins. PIC18 Blink uses a real gpsim-supported PIC18F14K22 model for local F5 debugging.

Configure a compatible USB-OTG bootloader/programmer for **Flash**; other devices say **Use the Windows Bridge**. The serial monitor supports CDC-ACM, CH340, CP210x and FTDI. PICkit 4/5 and Snap PIC programming/debugging use the paired PC's MPLAB tools: pymcuprog's documented PIC support is PKOB nano, not those programmers. [Setup, exact device lists, measured ARM64 compiler results and hardware limitations](docs/PIC.md).

## License and trademarks

TabStudio is licensed under the [Apache License 2.0](LICENSE). Third-party software it ships with or downloads, each
under its own licence, is listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

TabStudio is an independent project. It is not affiliated with, endorsed by or sponsored by Microsoft. Visual Studio is
a trademark of Microsoft Corporation, used here only to describe the kind of experience TabStudio offers.
