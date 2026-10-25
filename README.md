<div align="center">

# TabStudio

**A real IDE for Android tablets. Open a `.sln`, press F5, watch it run.**

[![License: Apache-2.0](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE)
[![CI](https://github.com/yahav2898/tabstudio/actions/workflows/ci.yml/badge.svg)](https://github.com/yahav2898/tabstudio/actions/workflows/ci.yml)
![Platform: Android 9+](https://img.shields.io/badge/platform-Android%209%2B%20·%20ARM-3DDC84.svg)
![Built with .NET 10](https://img.shields.io/badge/.NET-10-512BD4.svg)

[עברית](README.he.md) · [Architecture](docs/ARCHITECTURE.md) · [Roadmap](docs/ROADMAP.md) · [Contributing](CONTRIBUTING.md)

</div>

---

## At a glance

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

## What you get

### A real Visual Studio experience, built for touch

- **The layout you already know.** Menu bar, toolbar, document tabs, Solution Explorer, Properties, Error List, Output and a status bar, docked exactly where your hands expect them.
- **Sized for fingers.** Buttons, tabs and rows are fingertip-sized, 34 to 44 pixels tall. A floating action bar puts Rename, Comment, Format, Peek and Run to Cursor one tap away.
- **Keyboard when you want it.** Attach a keyboard and every Visual Studio shortcut works: F5, Ctrl+Shift+B, F12, Ctrl+. and the rest.
- **Desktop mode.** Plug in a monitor over USB-C or HDMI, or use a wireless display, and the full IDE moves to the big screen with your documents, caret and debugger exactly as they were. The tablet's keyboard types there and its mouse or touchpad drives a pointer there, while the tablet becomes a touch companion: Run and debugger buttons, a Toolbox that puts controls into your XAML at the caret, and a Properties panel for the element at the caret.
- **Light, dark or the system's.** A light theme modeled on Visual Studio Light, the dark one, or follow the tablet's dark-mode setting. Switching recolors the whole IDE at once, the editor's syntax colors included, and every color passes WCAG AA. Choosing an extension's color theme sets the IDE's light or dark to match.

### C# that runs on the tablet

- **Roslyn inside the app.** The real C# compiler powers semantic highlighting, live errors as you type, and the Error List.
- **F5 in under a second.** Projects compile straight to memory and run next to the code. Console input and output live in the Output panel.
- **IntelliSense.** Completion, signature help, Quick Info, Go to Definition, Find All References, Rename and quick fixes.
- **A debugger.** Breakpoints, Step Into, Over and Out, Locals, Watch, Call Stack, DataTips and break on exceptions.
- **Tests.** Test Explorer for xUnit, NUnit and MSTest, and Live Unit Testing: a green check, red cross or blue dash beside every line of the code under test, and the tests an edit affects run again by themselves a moment after you stop typing.
- **NuGet on the device.** Packages restore on the tablet, so real projects build as they are.

### Python, Node.js and a terminal

- **The Toolchain Store.** **Tools > Toolchain Store** installs Python and Node.js on the tablet, in a Linux environment that lives inside the app: Alpine Linux, run with PRoot, with no root and no special permissions. Every package comes from Alpine's own mirror and is checked against Alpine's signatures before anything is installed.
- **Projects that open as they are.** The start screen has a Python Application and a Node.js Console App. Open a folder with `pyproject.toml`, `requirements.txt` or `package.json`, and TabStudio offers to install its toolchain and packages. F5 runs it with its output and `input()` in the Output panel, its errors in the Error List, and every line of a traceback a link to its code.
- **IntelliSense.** Pyright brings completion, squiggles and navigation to Python, and typescript-language-server does the same for JavaScript and TypeScript.
- **A debugger.** F5 debugs with debugpy or vscode-js-debug, including Node's child processes: breakpoints, stepping, Locals, Watch, the Call Stack and exceptions. Ctrl+F5 runs without the debugger.
- **One tap to install.** If a language server or debug adapter is missing, the status bar, the editor and F5 offer to install the toolchain that brings it, and the server starts as soon as the install finishes.
- **A terminal.** Ctrl+` opens a shell in the Linux environment, drawn as xterm draws it.

### Every Visual Studio workload, on Android

| Workload | How it runs on the tablet |
| --- | --- |
| Console and class libraries | Compiled in memory, run in-process |
| ASP.NET Core and Blazor | Served locally, previewed live in a WebView |
| Avalonia | Rendered natively in a pane beside the editor, with a live XAML designer |
| Windows Forms | A cross-platform Forms layer, with a touch drag-and-drop designer |
| WPF | OpenSilver 3.3.3 in a WebView, with XAML IntelliSense and event-handler quick fixes |
| WinUI | Uno Platform, natively on Android |
| C, C++, Rust, Go, Python, Node, Java | Native toolchains from the Toolchain Store |
| Anything Windows-only | Windows Compatibility: the real app on the tablet itself through Wine; or the Windows Bridge, which builds and runs it on your PC and streams it to the tablet |

**Windows Forms apps.** Create a Windows Forms App, or open one from Visual Studio, and press F5. It runs beside the code on TabStudio's own Windows Forms layer, in a Windows 10 window, with dialogs, menus, grids, data binding and message boxes, and saving a file restarts it. `Form1.cs` opens in a touch designer: tap a tool and then the form, drag controls on the grid, resize them by their handles, change them in the Properties panel, and double-tap a button to write its Click handler. The designer writes `Form1.Designer.cs` exactly as Visual Studio does, so the project still opens on a PC. Code that calls Windows itself, such as P/Invoke, COM or the registry, runs through Windows Compatibility or the Windows Bridge instead; [the Forms layer's README](src/TabStudio.Forms/README.md) lists what it supports.

**WPF apps.** Create a **WPF Application**, or open a C# WPF project from Visual Studio, and press F5. TabStudio compiles its XAML on OpenSilver and runs it in the web preview; saving restarts it, and console output and exceptions appear in Output. The .NET 10 preview needs an up-to-date Android System WebView. The XAML editor offers completion, Quick Info, Go to Definition, live errors, and a light bulb that adds a missing event handler to the code-behind. Unsupported WPF APIs are named in the Error List with a route to the Windows Bridge. The browser preview has no C# debugger, and APIs that require Windows, including WPF 3D and modal windows, need the Windows Bridge.

[XAML completion](docs/wpf/images/wpf-01-xaml-completion.png) · [Add an event handler](docs/wpf/images/wpf-02-add-event-handler.png)

### Every screen at once

- **Run on Swarm.** Phones and tablets running TabStudio join your swarm with one scan (**File > Join a Swarm** on the phone). Choose Swarm in the Run menu, press F5, and the app you are building starts on all of them together, and every save updates them all. Avalonia apps run natively, web apps in each device's browser view, and console apps stream their output back.
- **Debug on any device.** Tap a button on the phone, and its breakpoint stops on your tablet with the phone's Locals and Call Stack. Pause that device or all of them.
- **Mirror input.** A tap, scroll or keystroke on one device happens on every device, so you compare the same screen at every size.
- **Device Matrix.** Every screen side by side: your phones, the App Preview and virtual devices. No spare phones? The tablet shows your app as a small phone, a large phone and a tablet, in light and dark, left to right and right to left, and with large fonts.
- **Layout checks.** After every change, TabStudio inspects each screen for clipped text, controls pushed off screen, overlapping controls, touch targets under 44 pixels and low contrast. Each finding lands in the Error List with its device and a screenshot, and **Fix with Agent** is one tap away.
- **Recorded end-to-end tests.** Record a few taps and get a UI test that replays them on every device and in CI, with results per device in Test Explorer.

### Open any repository

- **Paste a GitHub link and press F5.** TabStudio reads `global.json`, `package.json`, `Cargo.toml`, `go.mod`, `CMakeLists.txt` and friends, installs the toolchains it needs and sets up build, run and debug.
- **Compatibility Doctor.** For every project in a solution it shows where it runs on the tablet and why, and fixes what stands in the way.
- **Extensions.** VS Code extensions from Open VSX, so any language with a language server works.
- **Notebooks.** C# notebooks whose cells run as you write them.

### GitHub, inside the IDE

- **Git built in.** Clone, branch, commit, pull and push, and open pull requests on GitHub without leaving the IDE.
- **Pull request review.** A pull request opens in a page with its description, conversation, changed files and commits. Comment on lines in its diffs, then finish the review as a comment, an approval or a request for changes. Merge it, or check it out to run its code and tests; on its branch the editor shows its conversations beside their lines. **Review with Claude** drafts comments that you keep, edit or dismiss before anything is posted.
- **CI in the status bar.** Your commit's checks show next to the branch: a spinner with "2/5" while they run, then red or green. Tap it to open the GitHub Actions window.
- **GitHub Actions.** The branch's runs with their jobs and steps. Re-run every job or only the failed ones, cancel a run, or start a workflow from a form built from its inputs.
- **Logs you can read.** A job's log opens in a tab, folded by step and scrolled to the first error. F8 jumps to the next one.
- **CI errors in the Error List.** What failed on CI sits beside your local errors, and **Fix with Agent** knows which check reported it.
- **Watched repositories.** Follow the CI of any repository, and get a notification on the tablet when a run finishes.

### An AI pair programmer

- **Claude, inside the IDE.** The agent sees your whole solution, builds it, runs the tests and fixes the errors it finds, and every change it makes arrives as a diff you approve.
- **One tap from problem to fix.** When the Compatibility Doctor finds an API that does not run on the tablet, **Fix with Agent** rewrites that code and shows you the result.
- **Sketch to XAML.** Draw a screen with the stylus and get XAML that runs.
- **Port to cross-platform.** One tap converts a WPF or Windows Forms project into an Avalonia project that runs natively everywhere. A converter rewrites everything that maps exactly, Claude does the rest, and you review it all as a diff.
- **Speaks MCP.** The agent connects to any Model Context Protocol server, so your own tools and services are one setting away.

### Built for the device in your hands

- **Runs on modest tablets.** TabStudio measures the device it runs on and adapts. On a 3 GB tablet it keeps a full compiler model only of the projects you are editing, reads .NET's metadata straight from storage instead of holding it in memory, and gives memory back the moment Android asks for it.
- **Borrow your phone's power.** With TabStudio Link, the same app on your phone or PC becomes a build and language server for your tablet over local Wi-Fi. The tablet shows the IDE; the stronger device does the heavy lifting. No cloud and no account: pair once with a QR code, and the connection is end-to-end encrypted. If the phone leaves, the tablet keeps compiling on its own and reconnects by itself.
- **Never loses your place.** Android closes background apps on small devices. TabStudio saves your session as you work, so it reopens exactly where you left it, unsaved edits included.
- **Thermal-aware builds.** When the tablet runs hot, TabStudio compiles on fewer cores instead of letting Android throttle everything, and the status bar tells you why.
- **Works offline.** Everything except downloads, Git remotes and the agent runs without a connection.

The order in which all of this ships is in the [roadmap](docs/ROADMAP.md).

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

**The shell** is the part you see: a docked, Visual Studio-style layout scaled for touch, with fingertip-sized tap targets and a floating action bar for the things you would otherwise reach for with a keyboard.

**The language service** runs [Roslyn](https://github.com/dotnet/roslyn), the real C# compiler, inside the app. It keeps a live model of every project in the solution, which is what makes errors appear as you type and what powers semantic highlighting, completion and refactoring. Other languages get the same features from their own language servers.

**The build engine** turns a project into something runnable. TabStudio has its own project system that reads SDK-style project files, `Directory.Build.props`, project references and NuGet packages, which are restored on the device. For C#, Roslyn compiles straight to memory against reference assemblies bundled with the app, so the edit-run loop never touches the disk. Other languages build with native toolchains from the Toolchain Store.

**Run hosts** are where your program actually runs. F5 picks one per project type: console apps run in-process in an isolated load context, web apps run in a WebView next to the editor, and Windows-only apps run on the tablet itself through Windows Compatibility, or on a PC you own through the optional Windows Bridge.

Android restricts what an app may execute, so every design decision here starts from the platform's rules rather than fighting them. The details, including why the app does not depend on Android's Linux VM, are in [the architecture document](docs/ARCHITECTURE.md).

## Requirements

### To use TabStudio

| | Minimum | Recommended |
| --- | --- | --- |
| Android | 9 (API 28) | 14 or newer |
| CPU | 32-bit or 64-bit ARM (armeabi-v7a, arm64-v8a) | Recent 8-core ARM64 |
| Memory | 3 GB on its own, 2 GB with TabStudio Link | 8 GB or more for large solutions |
| Storage | 1 GB free | 4 GB free once you add toolchains |
| Input | Touch | A keyboard and trackpad, for long sessions |

A keyboard is optional. Everything in TabStudio can be reached by touch, and every Visual Studio shortcut works when a keyboard is attached.

That covers most tablets sold in the last six years, including entry-level and school tablets that run a 32-bit Android on a 64-bit chip.

### To build TabStudio from source

- [.NET 10 SDK](https://dotnet.microsoft.com/download) with the Android workload: `dotnet workload install android`
- JDK 17 or newer and the Android SDK (API 36), which the Android workload uses
- Any OS: Windows, macOS or Linux

## Getting started

### Install on a tablet

1. Open the [latest release](https://github.com/yahav2898/tabstudio/releases) on the tablet.
2. Download the `TabStudio-<version>.apk` file and open it. Android will ask you to allow installs from your browser once.
3. Launch TabStudio, tap **Open the sample** and press **F5**.

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
  TabStudio.Compilation/   Roslyn: diagnostics, IntelliSense, in-memory compilation, running programs
  TabStudio.Packages/      NuGet restore on the device
  TabStudio.Debugging/     The debugger: instrumented builds, breakpoints, stepping, inspection
  TabStudio.TestRunner/    xUnit, NUnit and MSTest discovery and execution
  TabStudio.LiveTesting/   Live Unit Testing: coverage instrumentation, impact analysis, the live test engine
  TabStudio.Notebooks/     C# notebooks: .ipynb and .dib, the C# kernel, value formatting
  TabStudio.SourceControl/ Git through GitHub: changes, commits, pulls and merges, branches, pull requests, Actions
  TabStudio.Agent/         Claude as a coding agent: tools over the workspace, MCP servers, sketch to XAML
  TabStudio.Link/          TabStudio Link: pairing, encrypted transport, QR codes and the remote language service
  TabStudio.Swarm/         Swarm: running, updating, debugging and recording an app on every joined device
  TabStudio.Forms/         Windows Forms on Avalonia: System.Windows.Forms and System.Drawing, drawn as on Windows 10
  TabStudio.Forms.Design/  The touch designer's model: reads and writes InitializeComponent, the canvas and Toolbox
  TabStudio.Web/           Web apps: static web assets, Blazor's scripts, a host for .NET apps on WebAssembly
  TabStudio.Toolchains/    The Toolchain Store: Alpine Linux on PRoot, packages, processes and the terminal
  TabStudio.LanguageServers/ Language Server and Debug Adapter Protocol clients for every non-C# language
  TabStudio.Extensions/    VS Code extensions from Open VSX: grammars, language rules, snippets, themes
  TabStudio.App/           The IDE itself: views and view models, shared by every platform
  TabStudio.Desktop/       Desktop host for development
  TabStudio.Android/       The Android app
tests/                     Unit tests for every library, plus shared test helpers
samples/                   The sample solution bundled with the app
docs/                      Architecture, roadmap and design notes
build/                     CI scripts
```

## Contributing

TabStudio is built in the open, and it is early enough that one contributor can shape a whole feature. Good places to start are in the [roadmap](docs/ROADMAP.md). Read [CONTRIBUTING.md](CONTRIBUTING.md) for how the code is organized and what a good pull request looks like.

## License and trademarks

TabStudio is licensed under the [Apache License 2.0](LICENSE). The third-party software it ships with or downloads, each under its own licence, is listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

TabStudio is an independent project. It is not affiliated with, endorsed by or sponsored by Microsoft. Visual Studio is a trademark of Microsoft Corporation, used here only to describe the kind of experience TabStudio offers. TabStudio contains no Microsoft code beyond the open-source .NET components it builds on, under their own licenses.
