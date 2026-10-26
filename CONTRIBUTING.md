# Contributing to TabStudio

Thanks for helping build an IDE for the devices most people actually have. This guide covers how to set up the code,
how it is organized, how CI checks it, how to add a language, and what a good pull request looks like.

- [Setting up](#setting-up)
- [How the code is organized](#how-the-code-is-organized)
- [Code style](#code-style)
- [Tests](#tests)
- [Continuous integration](#continuous-integration)
- [Adding a language or toolchain](#adding-a-language-or-toolchain)
- [Releasing](#releasing)
- [Pull requests](#pull-requests)
- [Reporting security issues](#reporting-security-issues)

## Setting up

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download). For the Android app you also need the Android
workload (`dotnet workload install android`), JDK 17 or newer and the Android SDK (API 36).

```bash
git clone https://github.com/yahav2898/tabstudio.git
cd tabstudio
dotnet test                                  # every test project
dotnet run --project src/TabStudio.Desktop   # the IDE on your computer
```

The desktop host runs the same UI as the tablet and is the fastest way to work on the IDE. To try a change on a
tablet, build the APK with `dotnet publish src/TabStudio.Android -c Release -f net10.0-android` and install
`bin/Release/net10.0-android/publish/*.apk`, or download the APK that CI builds for your pull request.

## How the code is organized

Read [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) first. In short:

- `TabStudio.Core` is the workspace model: solutions, projects, stack detection and the Compatibility Doctor. It has
  no dependencies. Keep it that way.
- `TabStudio.Compilation` is the Roslyn language service and IntelliSense. It knows nothing about the UI.
- The engines each do one job without UI and are tested on their own: `Packages`, `Debugging`, `TestRunner`,
  `LiveTesting`, `Notebooks`, `Toolchains`, `LanguageServers`, `Extensions`, `Web`, `Forms`, `Forms.Design`, `Wpf`,
  `Uno`, `SourceControl`, `Agent`, `Link`, `Swarm` and `WindowsBridge`.
- `TabStudio.App` is the IDE. Views are in `Views`, view models in `ViewModels`, editor integration in `Editing`, and
  everything else the app needs (files, downloads, templates, the self test) in `Services`.
- Platform heads (`TabStudio.Android`, `TabStudio.Desktop`, `TabStudio.WindowsBridge.Windows`) only start the app and
  provide platform services.

Dependencies point down only: Core ← Compilation ← engines ← App ← heads.

## Code style

The repository's `.editorconfig` and analyzers are the style guide, and the build enforces them.

- File-scoped namespaces, one public type per file, `_camelCase` private fields.
- Nullable reference types are on everywhere. Avoid `!` unless the reason is obvious from the line above.
- Every public type and member in Core, Compilation and the engines has an XML doc comment. Say what it is for, not
  what the code already says.
- Prefer immutable records for data, and keep I/O at the edges.
- View models use CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`). Views bind with compiled
  bindings; code-behind is for view-only concerns such as focus and scrolling.
- UI work happens on the UI thread; Roslyn and file work happens off it. `IUiDispatcher` is how services get back to
  the UI thread.
- Colors come from the theme's tokens, never literals, so the light and dark themes both stay WCAG AA.

## Tests

Every change comes with tests. Unit tests live next to the library they test (`tests/TabStudio.<Library>.Tests`), and
`tests/TabStudio.Testing` has shared helpers such as `TempDirectory`.

App tests run headlessly with Avalonia's headless platform: they type, tap and press keys in the real IDE and render
it to PNG. CI saves those screenshots in both themes, so a pull request that changes the UI shows what it looks like.

A feature that only works on Android, such as running a toolchain under PRoot, also adds a step to the app's self test
(`src/TabStudio.App/Services/SelfTest*.cs`), which CI runs on the emulator.

## Continuous integration

GitHub Actions ([`ci.yml`](.github/workflows/ci.yml)) runs these jobs on every push and pull request:

| Job | What it checks |
| --- | --- |
| Build and test libraries | Every library's unit tests, with the real language servers and debug adapters installed |
| Build and test the IDE | The headless UI tests, in the dark and the light theme, and each language's tools end to end |
| Build Android APK | The APK for ARM, its signature and contents; on `main` it updates the [nightly](https://github.com/yahav2898/tabstudio/releases/tag/nightly) build |
| Run on an Android emulator | The app's self test on a tablet emulator (below) |
| Windows Bridge on Windows | The Windows Bridge host and its tests on a Windows runner |
| Publish the release | Publishes `v<version>` once all the others pass (see [Releasing](#releasing)) |

**The emulator self test** starts the app and drives it the way a person would: it runs and debugs the sample, serves
web apps, shows Avalonia, Windows Forms and WPF apps, installs toolchains from Alpine Linux's real mirror and runs
and debugs a program in each language, and checks desktop mode on a second display. It has a 30-minute budget with
the Hot Reload interpreter compatibility mode, so a new self-test step should be quick and must not wait on the
network longer than it needs.

**Retries.** A test run that fails is retried once. If the retry passes, the job passes with a warning, and the
test is named in `FLAKY.txt` in the job's logs. A test that shows up there is a bug to fix, not noise to ignore.

**Logs.** Every job publishes its logs, test results and screenshots to the
[`ci-logs`](https://github.com/yahav2898/tabstudio/tree/ci-logs) branch: `<job>/` for `main`, and
`branches/<branch>/<job>/` for every other branch, with `/` in the branch name written as `-`.

A new push to a branch cancels that branch's CI run in progress, so push once your commits are ready.

## Adding a language or toolchain

Every language other than C# runs in the Toolchain Store's Linux environment. Adding one touches these places:

1. **The catalog.** Add the toolchain to
   [`src/TabStudio.Toolchains/Catalog/toolchains.json`](src/TabStudio.Toolchains/Catalog/toolchains.json): its Alpine
   packages, and any extra download pinned to a URL and a SHA-256. Prefer Alpine packages, which are verified against
   Alpine's signatures.
2. **The language server and debug adapter.** Describe them in
   [`BuiltInLanguages.json`](src/TabStudio.LanguageServers/Configuration/BuiltInLanguages.json): file types, the
   command that starts each one, and its launch settings.
3. **Detection.** Teach `StackDetector` in `src/TabStudio.Core/Detection` to recognize the language's project files,
   so a folder opens as a project and the Compatibility Doctor lists it.
4. **Build and run.** Add the build command and the parsing of its errors into the Error List, next to the other
   languages in `src/TabStudio.Toolchains/Projects` and `src/TabStudio.App/ViewModels/WorkspaceViewModel.Toolchain*.cs`.
5. **A template.** Add a `ProjectTemplates.<Language>.cs` in `src/TabStudio.App/Services`.
6. **Tests and CI.** Unit tests for detection and error parsing, an IDE test that builds, runs and debugs the
   template on the CI host, and a `SelfTest.<Language>.cs` step that does the same on the emulator.
7. **Notices.** List what the toolchain installs, with its licence, in
   [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

The Go, Rust, C/C++ and Java support added in 0.14 follows exactly these steps and is the best example to copy.

## Releasing

Releases are cut from `main`, and CI publishes them, never a person.

1. Features land on an integration branch, `agent/integrate-<version>`, where they are merged together and CI runs
   on the combination.
2. On that branch, set the new `<Version>` in `Directory.Build.props` and write the release notes in
   `docs/releases/<version>.md`: what's new, what's fixed, how to install and what CI verified.
3. When every check on the integration branch is green, `main` is fast-forwarded to it. Once every check of that
   commit on `main` has passed, the release job publishes `v<version>` with its APK and the release notes.

Avoid pushing to `main` while a release run is in progress: the new push cancels it.

## Pull requests

- Keep a pull request to one change, and describe what it does and why.
- Make sure `dotnet test` passes and the build has no new warnings.
- For UI changes, include a screenshot (CI makes one for you, in both themes).
- Be kind in reviews. See the [code of conduct](CODE_OF_CONDUCT.md).

## Reporting security issues

Please do not open public issues for security problems. See [SECURITY.md](SECURITY.md).
