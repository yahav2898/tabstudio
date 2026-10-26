# Mobile and web-server workflows

## Local servers

Run a server with F5 or Ctrl+F5. Its port notice offers **Open in preview** and **Open in browser** at
http://127.0.0.1:PORT. Supported output includes local HTTP URLs, Vite Local URLs and Python's serving message.
When Linux exposes /proc/net/tcp and tcp6, even a silent listener can be found by socket ownership. Android 10+
may hide those tables; in that case make the application print its URL or `Listening on port N` with a newline.
Opening a TCP service in the preview assumes that it serves HTTP. Existing unrelated listeners are excluded.

## Android

Install **Android SDK** in the Toolchain Store and accept the displayed Google SDK license. Allow several GB
of storage and an internet connection for the first Gradle/Maven restore. OpenJDK 17 builds apps; OpenJDK 21 runs
current jdtls. ARM64 and ARMv7 use static upstream build-tools, verified before extraction. D8 and apksigner are
portable Java programs. NDK/CMake native Android projects and arbitrary extra SDK versions are not preinstalled.

Use **Android App (Kotlin)** and F5 to build, install and launch. Android asks to permit TabStudio as an installer
and confirms each installation. **Build Android APK** only builds; APK paths appear in Output. A repository's
Gradle wrapper is preferred. Repositories with multiple application modules/flavors can produce several APKs;
select a single module/output before installing. Existing apps signed with another certificate cannot be updated
by the debug key; use a different application ID or uninstall that app in Android Settings yourself.

The **Logcat** panel receives only the launched debug app's own process through a generated provider and a
random per-run token on loopback. The provider reads logcat with that app's UID and PID. It is injected only into
the debug source set through a generated Gradle init script, without changing original sources/manifests.
Release builds have no relay. Stop (Shift+F5) disconnects the log stream; use Android's app controls to close the
installed app. Java IntelliSense uses jdtls. Kotlin completion and navigation use kotlin-language-server; compiler diagnostics
still come from the project's Gradle build, including Android resources and build variants.

## Capacitor

Create **Capacitor App** (Vite + TypeScript). Install its npm dependencies from the offered notice. F5 starts
Vite and opens the web preview automatically. **Run Android App** runs `npm run build`, adds Android if needed,
runs `cap sync android`, then uses the same Gradle build/install path. This template uses Capacitor 6 for its
Java 17 build baseline; existing newer Capacitor apps may require a newer JDK and additional SDK platforms.

## iOS

Choose **Build for iOS…** in a Capacitor or Xcode workspace. Review the generated YAML and `docs/IOS-BUILD.md`.
It explains every App Store Connect and distribution signing secret. Commit and push the files through Git
Changes; pushing a new workflow requires a classic token with both `repo` and `workflow`, or a fine-grained token
with Contents/Workflows write permission (and Actions write to run it). GitHub requires a dispatchable workflow
on the default branch. **Run iOS Workflow…** opens the existing
Actions dispatch form, where TestFlight upload is optional. Follow progress, job logs and the **iOS-IPA** artifact
there. Native projects require a shared Xcode scheme; review the generated project path and scheme.

A Mac is never needed locally. Signing/TestFlight require an active Apple Developer account, a matching app
record, signing certificate/private key and provisioning profile. GitHub macOS runner minutes can have a cost.
An iOS build cannot run inside Android's web preview; use TestFlight on an iPhone/iPad to run the native build.
