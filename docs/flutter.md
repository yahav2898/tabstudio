# Flutter and Dart: SDK spike and verification

## Runtime choice

Use a separate Ubuntu Base 24.04.3 glibc rootfs under the **same packaged PRoot and loader**. Ubuntu is a Debian-family glibc environment. Alpine remains the default for existing toolchains and the Terminal. Flutter and Dart commands use the side environment, with workspaces bound at their original device paths. This avoids relying on gcompat to emulate the glibc ABI, or maintaining unofficial musl builds of Dart/Flutter.

Flutter source is pinned to `3.38.0`, commit `a0e9b9dbf78c8a5ef39b45a7efd40ed2de19c1a7`, engine `cb467e31a54207dc987aca5bb0941c4d2e3fd9ed`. The installer verifies the Git revision before executing bootstrap. The matching official Dart SDK and Ubuntu rootfs are separately SHA-256 pinned. ARMv7 is explicitly unsupported. SDKs and caches are optional downloads, not APK payloads.

## Spike evidence (2026-10-26, Linux x64 host)

| Check | Result |
| --- | --- |
| Flutter source tag | Resolved and cloned at the pinned commit |
| Official Linux x64 Dart ZIP | 209,951,205 bytes; SHA-256 `6dc4f5c13611de54eced525280c78d25999b41b28a9d45922500b80f8dcda2f0` |
| Official Linux ARM64 Dart ZIP | 208,308,148 bytes; SHA-256 `41b209d2dfb4dde1a33214dc5ac25521272cab43bd40d20467c113c82b6894dc` |
| ELF interpreter, x64 / ARM64 | `/lib64/ld-linux-x86-64.so.2` / `/lib/ld-linux-aarch64.so.1` |
| Ubuntu Base amd64 | 29,983,772 bytes; SHA-256 verified against upstream SHA256SUMS |
| Ubuntu libc | `glibc 2.39`, executed using the extracted rootfs loader |
| Dart analysis server through TabStudio LSP | Completion and undefined-name diagnostic passed, 773 ms test body |
| Dart debug adapter through TabStudio DAP | Real breakpoint, step over, variable evaluation/locals and session termination passed |
| IDE build | Passed locally with .NET 10.0.100 |
| Toolchain tests | 230 passed; one unrelated process-tree test excluded after a sandbox-only failure |
| Protocol/registry tests | 18 passed, including real Dart LSP/DAP |
| Flutter bootstrap, SDK install under Android PRoot | **Not verified** |
| Flutter web preview / Hot Restart / Swarm | Implemented; **end-to-end execution pending** |
| ARM64 SDK execution and pub get | Archive/ABI verified; **execution pending on an ARM64 runner** |

The local Flutter bootstrap downloaded its Dart SDK and reached compilation of the Flutter tool. Automatic approval review then blocked further bootstrap execution because it attempted a cloud metadata endpoint, potentially exposing host credentials. The blocked bootstrap was not retried indirectly. Standalone Dart analysis/debug protocol checks use local programs without SDK/package bootstrap. Full Flutter validation requires an approved isolated environment that cannot access cloud metadata or host credentials. No startup, pub-get, web-build, or reload timing is claimed for the unexecuted checks.

The Dart server exposed a pre-existing malformed LSP capability: `completionItemKind.valueSet` contained an object rather than an array. This integration fixes that shared capability, with the real Dart initialization/completion/diagnostic test as regression coverage.

## Using the integration

1. Install **Flutter** in the Toolchain Store. Allow at least 4 GB free. Bootstrap is cancellable and the ready marker is written only after successful SDK/web cache preparation. Remove deletes only the Flutter side environment.
2. Open a folder containing `pubspec.yaml`, or create **Flutter App** / **Flutter Web App**. Accept the **flutter pub get** notice (also available under Tools > Install Project Packages).
3. F5/Ctrl+F5 runs Flutter projects as `flutter run -d web-server --web-hostname 127.0.0.1 --web-port 0`, using a no-debug Flutter DAP launch. The adapter's loopback web-launch URL opens in the preview beside the source.
4. Save a `.dart` file (Ctrl+S) for Hot Restart on web. Debug > Flutter Hot Reload / Hot Restart sends the corresponding adapter requests; Ctrl+Shift+F5 uses Hot Restart in a Flutter session.
5. Plain Dart packages use `dart run` and Dart's DAP for breakpoints, variables, watches and stepping. Source paths stay unchanged through PRoot.
6. Choose the Swarm target for a Flutter project to tunnel the same web app to the phones. This path still needs an actual multi-device smoke test.

The default Terminal is still Alpine: it does not automatically enter the Flutter side environment. IDE-started Flutter/Dart processes, LSP and DAP do use that environment.

## Native Android and web debugging boundaries

The base of this branch is main `f9ea07f1` (0.13.0); `agent/mobile` has not landed there. Native on-tablet Android deployment is therefore **not implemented/verified by this PR**. Once that branch lands, integrate its ARM64 Gradle/Android SDK executable replacements and device installation path into this glibc environment, then test native Flutter and VM-service connectivity on the tablet. Upstream Android build tools cannot simply be assumed to run on an ARM64 host.

The generic embedded WebView has no Dart Debug Extension connection. Web-server preview and hot restart do not imply working web VM breakpoints. Dart console DAP and explicit native Flutter launch configurations use the normal debugger UI; Flutter web debugging needs a browser debugging bridge as additional work.

An example native configuration after that toolchain/device path is available:

```json
{
  "version": "0.2.0",
  "configurations": [{
    "name": "Flutter device",
    "type": "flutter",
    "request": "launch",
    "program": "${workspaceFolder}/lib/main.dart",
    "cwd": "${workspaceFolder}",
    "toolArgs": ["-d", "DEVICE_ID"]
  }]
}
```

## Required verification before merge

- On an approved x64 **and ARM64 glibc runner**, install the pinned SDK, run `flutter --version`, `dart --version`, `flutter precache --web`, create both templates and run pub get. Capture timings and installed/download sizes. This is a release gate, not a completed CI result.
- `.github/workflows/flutter-sdk.yml` prepares that x64/ARM64 validation as an explicitly manual workflow. It has not been dispatched while the bootstrap review is unresolved. Normal CI does not run that SDK workflow; move it into required PR CI only after clearing the spike.
- With Flutter and Dart on PATH, set `TABSTUDIO_FLUTTER_TESTS=1` and run the language-server and App tests. The Dart tests exercise real completion/diagnostics and DAP; the opt-in web test opens the IDE, serves the preview, edits/saves, checks the adapter's Hot Restart acknowledgement, and captures the IDE screenshot. It does not assert rendered canvas pixels.
- Run `build/ci/android-smoke-test.sh APK OUTPUT 900 flutter` on an emulator. Its dedicated `tabstudio.selftest.flutter` entry point installs Flutter through the real Store, fetches packages, checks the preview's HTTP entry page and saves a Dart edit. The existing default emulator test is preserved. The Flutter test is opt-in until the SDK spike is cleared; it has not run in this environment.
- Verify the changed UI in a real WebView and on Swarm phones, including a visible widget-text change after saving. Run all standard CI jobs on the final commit. A draft PR must not be treated as green feature verification just because optional SDK tests are skipped.

Upstream references: [Flutter SDK installation](https://docs.flutter.dev/install/manual), [Dart SDK](https://dart.dev/get-dart), [Flutter DAP protocol and custom reload requests](https://github.com/flutter/flutter/blob/3.38.0/packages/flutter_tools/lib/src/debug_adapters/README.md), [Ubuntu Base downloads/checksums](https://cdimage.ubuntu.com/ubuntu-base/releases/24.04.3/release/).
