# Roadmap

TabStudio ships in releases. Each one is published on the
[releases page](https://github.com/yahav2898/tabstudio/releases) with an installable APK, only after CI has built it
and run it on an Android tablet emulator, and each one installs as an update over the one before.

## Shipped

| Release | What it brought |
| --- | --- |
| [0.1](releases/0.1.0.md) | The Visual Studio layout, sized for touch. C# compiled by Roslyn in the app and run in-process with F5, live errors, a start screen with templates and GitHub downloads, and the Compatibility Doctor. |
| [0.2](releases/0.2.0.md) | IntelliSense, the C# debugger, Test Explorer, Git, notebooks, ASP.NET Core apps served on the tablet, and Claude as a coding agent inside the IDE. |
| [0.3](releases/0.3.0.md) | Avalonia apps rendered natively beside the code, a live XAML previewer, and sketch to XAML. |
| [0.4](releases/0.4.0.md) | GitHub in the IDE: pull request review and merge, GitHub Actions runs and logs, CI status in the status bar, Fix with Agent for CI errors. Live Unit Testing in the editor. |
| [0.5](releases/0.5.0.md) | TabStudio Link: a phone or PC compiles for the tablet over local Wi-Fi, end-to-end encrypted, with no cloud and no account. |
| [0.6](releases/0.6.0.md) | Desktop mode: the IDE on a connected monitor, with the tablet as its touch companion. |
| [0.7](releases/0.7.0.md) | Blazor and Razor on the tablet, and VS Code extensions from Open VSX. |
| [0.8](releases/0.8.0.md) | The Toolchain Store: Python and Node.js on Alpine Linux under PRoot, with IntelliSense, a debugger and a terminal. |
| [0.9](releases/0.9.0.md) | Windows Forms apps on the tablet, and a touch designer that writes `Form1.Designer.cs` as Visual Studio does. |
| [0.10](releases/0.10.0.md) | Swarm: one F5 runs the app on every joined phone and tablet, with the Device Matrix, layout checks and recorded UI tests. |
| [0.11](releases/0.11.0.md) | The light theme, and a steadier app on Android. 0.11.1 builds more real-world solutions as Visual Studio does. |
| [0.12](releases/0.12.0.md) | A shell rebuilt for landscape, portrait and phones: the activity bar, one search for everything, and native scrolling. |
| [0.13](releases/0.13.0.md) | The Windows Bridge: Windows-only projects built and run on a PC you own and streamed to the tablet. |
| [0.14](releases/0.14.0.md) | Go, Rust, C/C++ and Java with IntelliSense and debuggers, WPF on OpenSilver, and WinUI and Uno Platform on Skia. |

## Next

The next release, 0.15, is being integrated now. It brings:

- **Repositories that set themselves up.** Opening a repository installs its toolchains and packages, and static
  web sites run with live reload.
- **Backing services.** The databases and services a repository needs run natively on the tablet, and a Database
  Explorer browses SQLite, PostgreSQL and SQL Server.
- **Hot Reload and Edit and Continue** for C# apps under the debugger.
- **Time-travel debugging.** Record a C# run and step backward through its timeline.
- **On-device AI.** Offline models with llama.cpp, model management and inline completions, with no connection.
- **Remote development** over SSH, and publishing ASP.NET Core apps to your server.
- **Android apps built on the tablet,** local server previews, and iOS through cloud builds.
- **Flutter and Dart** with a web preview.
- **Port to Avalonia** completed for WPF and Windows Forms projects.
- **Microcontrollers.** Microchip PIC projects built on the tablet, flashed over USB and debugged in a simulator.

After 0.15:

- **STM32 and other ARM Cortex-M boards,** built, flashed and debugged from the tablet.
- **Windows Compatibility:** Windows-only apps run on the tablet itself through Wine, for when no PC is at hand.

## How the roadmap changes

The roadmap follows what people build with TabStudio. Open an
[issue](https://github.com/yahav2898/tabstudio/issues) to ask for a language, a workload or a tool, or read
[CONTRIBUTING.md](../CONTRIBUTING.md) to build it yourself.
