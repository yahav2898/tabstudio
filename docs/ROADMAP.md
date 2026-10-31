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
| [0.15](releases/0.15.0.md) | Repositories that set themselves up, static web sites, the Database Explorer, remote development over SSH, Android apps built on the tablet and iOS through cloud builds, Microchip PIC, C# Hot Reload on the desktop and time-travel debugging, offline AI, and Port to Avalonia completed. |
| [0.16](releases/0.16.0.md) | Real-world .NET repositories built as the SDK builds them, backing services from Compose files without Docker, and experimental Flutter and Dart. |
| [0.17](releases/0.17.0.md) | Classic .NET Framework WPF and Windows Forms projects run on the tablet, with Upgrade project, and every panel of 0.15 and 0.16 redesigned to the design system. |
| [0.18](releases/0.18.0.md) | TabStudio Live: several devices code together in real time with no cloud. Windows Forms apps draw on images, WPF windows fit the preview, packages with native libraries run, and real Python, Java and Rust repositories set up and run with one F5. |
| [0.19](releases/0.19.0.md) | Every microcontroller, from the tablet: PlatformIO, Arduino, STM32Cube, ESP-IDF, Zephyr, Pico SDK and embedded Rust projects built, uploaded and debugged over USB, with crash, memory and stack analysis. [0.19.1](releases/0.19.1.md) runs source generators from NuGet as the SDK does and shows long operations with their progress. [0.19.2](releases/0.19.2.md) runs source generators built from the repository too (projects referenced with `OutputItemType="Analyzer"`), reads MSBuild more as MSBuild does, and fixes what 34 more real repositories ran into. |
| [0.20](releases/0.20.0.md) | PHP and Laravel, Kotlin beyond Android, Visual Basic compiled in the app like C# with its Windows Forms designer, experimental Swift, and Test Explorer for PHPUnit, JUnit and Swift tests. [0.20.1](releases/0.20.1.md) is a quality release from a full pass over the IDE: no lost work from encodings, saves, renames, Live joins or files changed outside the editor, Kotlin's install no longer deleting other toolchains, more errors in the Error List, pytest in Test Explorer, and phone and touch fixes. [0.20.3](releases/0.20.3.md) reads .NET solutions as the SDK reads them and makes the IDE accessible: a text size that follows Android's font size, whole touch targets, TalkBack names, keyboard panes, WCAG AA contrast, Remove animations and right-to-left layouts. |

## Next

- **Swift out of experimental:** build times and memory measured on physical ARM64 tablets, and the Terminal entering
  the glibc environment.
- **Kotlin Multiplatform** projects beyond the JVM target, and Kotlin Hot Swap through java-debug.
- **PHP:** Laravel queues and schedules started beside `artisan serve`, and PHPStan in the Error List.
- **Physical-board verification** of every embedded route, board by board, recorded in the embedded guide.
- **Hot Reload on Android,** for the user's own code, without slowing the IDE.
- **Windows Compatibility:** Windows-only apps run on the tablet itself through Wine, for when no PC is at hand.

## How the roadmap changes

The roadmap follows what people build with TabStudio. Open an
[issue](https://github.com/yahav2898/tabstudio/issues) to ask for a language, a workload or a tool, or read
[CONTRIBUTING.md](../CONTRIBUTING.md) to build it yourself.
