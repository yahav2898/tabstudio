using System.Diagnostics;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Services;

/// <summary>The outcome of <see cref="SelfTest"/>.</summary>
/// <param name="Success">Whether the sample built and ran to the end.</param>
/// <param name="Message">A one-line summary: what the program printed, or what went wrong.</param>
/// <param name="Duration">How long it took, from opening the sample to the program's exit.</param>
public sealed record SelfTestResult(bool Success, string Message, TimeSpan Duration);

/// <summary>
/// Does what a person does first: opens the sample solution, presses F5, types a name when the program asks,
/// and reads the Output panel; then debugs it, debugs it again with a TabStudio Link host compiling, serves a new web
/// app and new Blazor apps, shows a new Avalonia app, installs an extension, installs and runs Python and Node.js,
/// runs and designs a new Windows Forms app, looks at the IDE and the Start screen in both themes, and runs WPF. CI runs it on an
/// Android emulator to prove the whole IDE works on a device.
/// </summary>
public static partial class SelfTest
{
    /// <summary>The name typed when the sample asks for one.</summary>
    public const string Name = "Tablet";

    /// <summary>Runs the self test in the running app.</summary>
    /// <param name="main">The app's root view model.</param>
    /// <param name="samples">The sample library.</param>
    /// <param name="cancellationToken">Gives up, for example after a timeout.</param>
    public static Task<SelfTestResult> RunAsync(MainViewModel main, SampleLibrary samples, CancellationToken cancellationToken = default) =>
        RunAsync(main, samples, templates: null, screenshots: null, cancellationToken);

    /// <summary>
    /// Runs the self test in the running app, then, when <paramref name="templates"/> is given, a web app, Blazor apps,
    /// an Avalonia app, a grammar extension, toolchains, a Windows Forms app, a WPF app, and Swarm: the Avalonia app on virtual
    /// devices and on a phone that joins the swarm.
    /// </summary>
    /// <param name="main">The app's root view model.</param>
    /// <param name="samples">The sample library.</param>
    /// <param name="templates">Creates the web and Avalonia apps; without it, those parts are skipped.</param>
    /// <param name="screenshots">A folder for the design part's pictures, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Gives up, for example after a timeout.</param>
    public static async Task<SelfTestResult> RunAsync(MainViewModel main, SampleLibrary samples, ProjectTemplates? templates, string? screenshots, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(main);
        ArgumentNullException.ThrowIfNull(samples);
        var clock = Stopwatch.StartNew();
        using var preparationStop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task? preparation = null;
        try
        {
            var solution = await samples.EnsureExtractedAsync(cancellationToken);
            await main.OpenWorkspaceAsync(solution);
            if (main.Workspace is not { } workspace)
            {
                return new SelfTestResult(false, "The sample did not open.", clock.Elapsed);
            }

            if (!workspace.RunCommand.CanExecute(null))
            {
                return new SelfTestResult(false, $"F5 is not available: {workspace.StatusText}.", clock.Elapsed);
            }

            void Answer(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(OutputViewModel.IsWaitingForInput) && workspace.Output.IsWaitingForInput)
                {
                    workspace.Output.InputText = Name;
                    workspace.Output.SubmitInputCommand.Execute(null);
                }
            }

            workspace.Output.PropertyChanged += Answer;
            try
            {
                await workspace.RunCommand.ExecuteAsync(null).WaitAsync(cancellationToken);
            }
            finally
            {
                workspace.Output.PropertyChanged -= Answer;
            }

            var output = workspace.Output.Text;
            var exited = output.Contains("has exited with code 0", StringComparison.Ordinal);
            var greeted = output.Contains($", {Name}!", StringComparison.Ordinal);
            var summary = string.Join(" | ", output.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).TakeLast(6));
            if (!exited || !greeted)
            {
                return new SelfTestResult(false, summary, clock.Elapsed);
            }

            var debugged = await DebugSampleAsync(workspace, cancellationToken);
            if (debugged.Success)
            {
                var linked = await LinkSampleAsync(main, workspace, cancellationToken);
                debugged = new SelfTestResult(linked.Success, $"{debugged.Message} | {linked.Message}", debugged.Duration + linked.Duration);
                if (debugged.Success)
                {
                    var servers = await CheckLanguageServersAsync(main, cancellationToken);
                    debugged = debugged with { Success = servers.Success, Message = $"{debugged.Message} | {servers.Message}" };
                }
            }

            if (!debugged.Success || templates is null)
            {
                return new SelfTestResult(debugged.Success, $"{summary} | {debugged.Message}", clock.Elapsed);
            }

            var web = await ServeWebAppAsync(main, templates, cancellationToken);
            if (!web.Success)
            {
                return new SelfTestResult(false, $"{summary} | {debugged.Message} | {web.Message}", clock.Elapsed);
            }

            // Keep the browser's trusted-input check independent of package decompression and downloads.
            // Preparation still overlaps all the remaining .NET/UI checks before any native toolchain runs.
            preparation = PrepareSelfTestToolchainsAsync(main.Workspace!.Toolchains, preparationStop.Token);

            var blazor = await ServeBlazorAppsAsync(main, templates, cancellationToken);
            if (!blazor.Success)
            {
                return new SelfTestResult(false, $"{summary} | {debugged.Message} | {web.Message} | {blazor.Message}", clock.Elapsed);
            }

            // Remember the Avalonia app so desktop mode can continue in its workspace after all feature checks.
            var avalonia = await ShowAvaloniaAppAsync(main, templates, cancellationToken);
            if (!avalonia.Success || main.Workspace is not { } last)
            {
                return new SelfTestResult(false, $"{summary} | {debugged.Message} | {web.Message} | {blazor.Message} | {avalonia.Message}", clock.Elapsed);
            }

            var avaloniaApp = last.Workspace is { } loaded ? loaded.Solution?.FilePath ?? loaded.RootDirectory : null;
            var extensions = await InstallGrammarExtensionAsync(last, FindEditor, cancellationToken);
            var forms = extensions.Success ? await ShowWindowsFormsAppAsync(main, templates, cancellationToken) : new SelfTestResult(false, "Windows Forms: not run", TimeSpan.Zero);
            var design = forms.Success ? await CheckDesignAsync(main, screenshots, cancellationToken) : new SelfTestResult(false, "Design: not run", TimeSpan.Zero);
            var wpf = design.Success ? await ShowWpfAppAsync(main, templates, cancellationToken) : new SelfTestResult(false, "WPF: not run", TimeSpan.Zero);
            var uno = wpf.Success ? await ShowUnoAppAsync(main, templates, cancellationToken) : new SelfTestResult(false, "WinUI: not run", TimeSpan.Zero);

            // These UI checks do not use the Linux environment being prepared in the background.
            var screens = uno.Success ? await CheckSwarmScreensAsync(main, templates, cancellationToken) : new SelfTestResult(false, "Swarm screens: not run", TimeSpan.Zero);
            var swarm = screens.Success ? await RunOnSwarmAsync(main, templates, cancellationToken) : new SelfTestResult(false, "Swarm devices: not run", TimeSpan.Zero);
            if (swarm.Success && preparation is not null)
            {
                // Finish all package operations before opening a native debugger or running its program.
                await preparation;
            }

            var toolchains = swarm.Success ? await RunToolchainsAsync(main, templates, cancellationToken) : new SelfTestResult(false, "Toolchains: not run", TimeSpan.Zero);
            var processes = toolchains.Success ? await StartProgramsWhileCollectingAsync(cancellationToken) : new SelfTestResult(false, "Processes: not run", TimeSpan.Zero);
            var pythonTools = processes.Success ? await CheckPythonToolsAsync(main, cancellationToken) : new SelfTestResult(false, "Python tools: not run", TimeSpan.Zero);
            var goTools = pythonTools.Success ? await CheckGoToolchainAsync(main, templates, cancellationToken) : new SelfTestResult(false, "Go: not run", TimeSpan.Zero);
            var rustTools = goTools.Success ? await CheckRustToolchainAsync(main, templates, cancellationToken) : new SelfTestResult(false, "Rust: not run", TimeSpan.Zero);
            var cppTools = rustTools.Success ? await CheckCppToolchainAsync(main, templates, cancellationToken) : new SelfTestResult(false, "C/C++: not run", TimeSpan.Zero);
            var javaTools = cppTools.Success ? await CheckJavaToolchainAsync(main, templates, cancellationToken) : new SelfTestResult(false, "Java: not run", TimeSpan.Zero);
            if (javaTools.Success && avaloniaApp is not null)
            {
                // The self test ends in the Avalonia app, which the desktop part continues with.
                await main.OpenWorkspaceAsync(avaloniaApp);
            }

            return new SelfTestResult(
                extensions.Success && toolchains.Success && processes.Success && pythonTools.Success && goTools.Success && rustTools.Success && cppTools.Success && javaTools.Success && forms.Success && design.Success && wpf.Success && uno.Success && screens.Success && swarm.Success,
                $"{summary} | {debugged.Message} | {web.Message} | {blazor.Message} | {avalonia.Message} | {extensions.Message} | {forms.Message} | {design.Message} | {wpf.Message} | {uno.Message} | {screens.Message} | {swarm.Message} | {toolchains.Message} | {processes.Message} | {pythonTools.Message} | {goTools.Message} | {rustTools.Message} | {cppTools.Message} | {javaTools.Message}",
                clock.Elapsed);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new SelfTestResult(false, $"{ex.GetType().Name}: {ex.Message}{DescribeActivity(main)}".ReplaceLineEndings(" "), clock.Elapsed);
        }
        finally
        {
            await FinishSelfTestPreparationAsync(preparation, preparationStop);
        }
    }
}
