# Contributing to TabStudio

Thanks for helping build an IDE for the devices most people actually have. This guide covers how to set up
the code, how it is organized and what a good pull request looks like.

## Setting up

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download). For the Android app you also need the
Android workload (`dotnet workload install android`), JDK 17 or newer and the Android SDK.

```bash
git clone https://github.com/yahav2898/tabstudio.git
cd tabstudio
dotnet test tests/TabStudio.Core.Tests
dotnet test tests/TabStudio.Compilation.Tests
dotnet test tests/TabStudio.App.Tests
dotnet run --project src/TabStudio.Desktop
```

The desktop host runs the same UI as the tablet and is the fastest way to work on the IDE. To try a change on
a tablet, build the APK with `dotnet publish src/TabStudio.Android -c Release -f net10.0-android` and install
`bin/Release/net10.0-android/publish/*.apk`.

## How the code is organized

Read [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) first. In short:

- `TabStudio.Core` is the workspace model and has no dependencies. Keep it that way.
- `TabStudio.Compilation` is the Roslyn language service. It knows nothing about the UI.
- `TabStudio.App` is the IDE. Views are in `Views`, view models in `ViewModels`, editor integration in
  `Editing`, and everything else the app needs (files, downloads, templates) in `Services`.
- Platform heads (`TabStudio.Android`, `TabStudio.Desktop`) only start the app and provide platform services.

Dependencies point down only: Core ← Compilation ← App ← heads.

## Code style

The repository's `.editorconfig` and analyzers are the style guide, and the build enforces them.

- File-scoped namespaces, one public type per file, `_camelCase` private fields.
- Nullable reference types are on everywhere. Avoid `!` unless the reason is obvious from the line above.
- Every public type and member in Core and Compilation has an XML doc comment. Say what it is for, not what
  the code already says.
- Prefer immutable records for data, and keep I/O at the edges.
- View models use CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`). Views bind with compiled
  bindings; code-behind is for view-only concerns such as focus and scrolling.
- UI work happens on the UI thread; Roslyn and file work happens off it. `IUiDispatcher` is how services get
  back to the UI thread.

## Tests

Every change comes with tests. Unit tests live next to the library they test (`tests/TabStudio.<Library>.Tests`),
and `tests/TabStudio.Testing` has shared helpers such as `TempDirectory`.

App tests run headlessly with Avalonia's headless platform and can render views to PNG. CI saves those
screenshots, so a pull request that changes the UI shows what it looks like.

## Continuous integration

GitHub Actions builds and tests every push and pull request, and builds the Android APK. Logs, test results
and screenshots of each job on `main` are also published to the
[`ci-logs`](https://github.com/yahav2898/tabstudio/tree/ci-logs) branch.

## Pull requests

- Keep a pull request to one change, and describe what it does and why.
- Make sure `dotnet test` passes and the build has no new warnings.
- For UI changes, include a screenshot (CI makes one for you).
- Be kind in reviews. See the [code of conduct](CODE_OF_CONDUCT.md).

## Reporting security issues

Please do not open public issues for security problems. See [SECURITY.md](SECURITY.md).
