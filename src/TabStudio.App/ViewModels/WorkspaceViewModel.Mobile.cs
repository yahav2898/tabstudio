using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Platform;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Projects;
using TabStudio.Toolchains.Runtimes;
using TabStudio.Core.Diagnostics;

namespace TabStudio.App.ViewModels;

/// <summary>Native Android builds on the tablet and Capacitor's shared web/native project.</summary>
public sealed partial class WorkspaceViewModel
{
    private IAndroidAppInstaller? _androidInstaller;

    /// <summary>The built app's logcat, limited to its process through a debug relay.</summary>
    public OutputViewModel Logcat { get; private set; } = null!;

    /// <summary>
    /// Whether the bottom dock shows the Logcat tab: for an Android or Capacitor app, while it has lines, or while it is
    /// the panel shown, as the debugger's tabs show only while debugging.
    /// </summary>
    public bool ShowsLogcatTab => SelectedPanel == BottomPanel.Logcat || !Logcat.IsEmpty || MobileRoot() is not null;

    private string? MobileRoot()
    {
        if (IsRemoteWorkspace) return null;
        var chosen = CurrentToolchainProject()?.Project.Directory;
        if (chosen is not null && (GradleAndroidProject.IsProject(chosen) || GradleAndroidProject.IsCapacitor(chosen)))
        {
            return chosen;
        }

        return Workspace?.RootDirectory is { } root && (GradleAndroidProject.IsProject(root) || GradleAndroidProject.IsCapacitor(root)) ? root : null;
    }

    /// <summary>Builds an APK. A Capacitor app's F5 remains its Vite web preview.</summary>
    [RelayCommand(CanExecute = nameof(CanBuild))]
    private Task BuildAndroidAsync() => BuildAndroidCoreAsync(install: false);

    /// <summary>Builds, installs with Android's confirmation, launches, and follows logcat.</summary>
    [RelayCommand(CanExecute = nameof(CanBuild))]
    private Task RunAndroidAsync() => BuildAndroidCoreAsync(install: true);

    private async Task BuildAndroidCoreAsync(bool install)
    {
        if (BlockLocalToolchainActionForRemote()) return;
        if (MobileRoot() is not { } root)
        {
            StatusText = "Choose a Gradle Android or Capacitor startup project first";
            return;
        }

        var android = _toolchains.Catalog.Find("android")!;
        if (!await EnsureToolchainReadyAsync(android, "Android builds need the Android SDK from the Toolchain Store."))
        {
            return;
        }

        var capacitor = GradleAndroidProject.IsCapacitor(root);
        if (capacitor && !await EnsureToolchainReadyAsync(_toolchains.Catalog.Find("node")!, "Capacitor builds its web part with Node.js."))
        {
            return;
        }

        await SaveAllCoreAsync();
        Output.Clear();
        Logcat.ClearCommand.Execute(null);
        SelectedPanel = BottomPanel.Output;
        IsBottomPanelVisible = true;
        State = IdeState.Running;
        StatusText = "Building Android app";
        _run = new CancellationTokenSource();
        using var relay = install ? new AndroidLogcatRelay(line => Logcat.WriteLine(line)) : null;
        try
        {
            var runtime = _toolchains.Runtime;
            async Task<bool> RunStep(string program, params string[] args)
            {
                var spec = new ProcessStartSpec { Program = program, Arguments = args, WorkingDirectory = root };
                Output.WriteLine("> " + spec.CommandLine, OutputKind.Info);
                var step = await CommandRunner.RunAsync(runtime, spec, line => Output.WriteLine(line), _run.Token);
                if (!step.Succeeded)
                {
                    StatusText = "Android build failed";
                    State = IdeState.Failed;
                    Output.WriteLine(step.Tail(), OutputKind.Failure);
                }

                return step.Succeeded;
            }

            if (capacitor)
            {
                if (!Directory.Exists(Path.Combine(root, "node_modules")) && !await RunStep("npm", File.Exists(Path.Combine(root, "package-lock.json")) ? "ci" : "install", "--no-fund", "--no-audit"))
                {
                    return;
                }

                if (!await RunStep("npm", "run", "build")
                    || (!Directory.Exists(Path.Combine(root, "android")) && !await RunStep("npx", "--no-install", "cap", "add", "android"))
                    || !await RunStep("npx", "--no-install", "cap", "sync", "android"))
                {
                    return;
                }
            }

            var nativeRoot = capacitor ? Path.Combine(root, "android") : root;
            var build = GradleAndroidProject.Build(nativeRoot, runtime.Kind == RuntimeKind.Linux);
            if (relay is not null)
            {
                build = build with { Arguments = [.. build.Arguments, "--init-script", relay.PrepareBuild(nativeRoot)] };
            }

            _runDirectory = nativeRoot;
            Output.WriteLine("> " + build.CommandLine, OutputKind.Info);
            var result = await CommandRunner.RunAsync(runtime, build, line => Output.WriteLine(line), _run.Token);
            var matcher = Matcher(android);
            // Kotlin's "w:" lines (deprecations, unused variables) are warnings: sunflower, architecture-samples and
            // JetNews built with them listed as errors.
            var diagnostics = result.Output.Split('\n').SelectMany(matcher.Match).Select(location => new BuildDiagnostic(
                "Gradle", AndroidSeverity(location.Severity), location.Message ?? "Android build error",
                new DiagnosticLocation(location.File, OffsetOf(location.File, location.Line), 0, location.Line, location.Column ?? 1), Path.GetFileName(root))).ToList();
            if (!result.Succeeded && diagnostics.Count == 0)
            {
                diagnostics.Add(new BuildDiagnostic("Gradle", DiagnosticSeverityLevel.Error, result.Tail(), null, Path.GetFileName(root)));
            }

            ErrorList.SetRestoreDiagnostics("android:" + root, diagnostics);
            if (!result.Succeeded)
            {
                State = IdeState.Failed;
                StatusText = "Android build failed";
                return;
            }

            var apks = GradleAndroidProject.DebugApks(nativeRoot);
            foreach (var apk in apks)
            {
                Output.WriteLine("APK: " + apk, OutputKind.Success);
            }

            if (apks.Count == 0)
            {
                StatusText = "Gradle produced no debug APK";
                State = IdeState.Failed;
                return;
            }

            if (install && _androidInstaller is not null)
            {
                var selected = apks[0];
                if (apks.Count > 1)
                {
                    var index = await Prompt.ChooseAsync("Choose an APK", "Gradle built more than one debug APK. Choose the one to install and run.", "Install",
                        [.. apks.Select(p => Path.GetRelativePath(nativeRoot, p))]);
                    if (index is not { } chosen)
                    {
                        StatusText = "APK installation cancelled";
                        return;
                    }

                    selected = apks[chosen];
                }

                var package = await _androidInstaller.InstallAndLaunchAsync(selected, _run.Token);
                StatusText = "Running " + package;
                Logcat.WriteLine("Logcat for " + package + " (debug app process)", OutputKind.Info);
                SelectedPanel = BottomPanel.Logcat;
                await Task.Delay(Timeout.InfiniteTimeSpan, _run.Token);
            }
            else
            {
                StatusText = "Android APK built";
                Output.WriteLine(install ? "Install the APK on an Android device to run it." : "Android build succeeded.", OutputKind.Success);
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Android run stopped";
        }
        catch (Exception ex) when (ex is IOException or ToolchainRuntimeException or InvalidOperationException)
        {
            State = IdeState.Failed;
            StatusText = ex.Message;
            Output.WriteLine(ex.Message, OutputKind.Failure);
        }
        finally
        {
            _run.Dispose();
            _run = null;
            if (State == IdeState.Running)
            {
                State = IdeState.Ready;
            }
        }
    }

    /// <summary>The Error List severity of a message of an Android build: Kotlin's <c>w:</c> and javac's <c>warning:</c> are warnings.</summary>
    /// <param name="severity">The severity the compiler printed, if any.</param>
    internal static DiagnosticSeverityLevel AndroidSeverity(string? severity) =>
        severity?.ToUpperInvariant() is "W" or "WARNING" ? DiagnosticSeverityLevel.Warning : DiagnosticSeverityLevel.Error;
}
