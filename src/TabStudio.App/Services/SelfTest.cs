using System.Diagnostics;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Services;

/// <summary>The outcome of <see cref="SelfTest"/>.</summary>
/// <param name="Success">Whether the sample built and ran to the end.</param>
/// <param name="Message">A one-line summary: what the program printed, or what went wrong.</param>
/// <param name="Duration">How long it took, from opening the sample to the program's exit.</param>
public sealed record SelfTestResult(bool Success, string Message, TimeSpan Duration);

/// <summary>Independent device-test groups. Together the three named shards run the complete self test.</summary>
public enum SelfTestShard
{
    /// <summary>The original, complete self test, for manual device runs.</summary>
    All,
    /// <summary>Console, debugger, Link, web apps, and Go/Rust builds and debuggers.</summary>
    Managed,
    /// <summary>Native app previews, design, extensions, Swarm, scrolling and external-display input.</summary>
    Ui,
    /// <summary>Python/Node, C/C++/Java, native backing services and native process integrity.</summary>
    Native,
    /// <summary>OCI image PostgreSQL, Redis, nginx preview and the docker terminal command.</summary>
    Containers,
    /// <summary>Opt-in paired-runtime latency measurements; not a functional CI shard.</summary>
    Performance,
}

/// <summary>
/// Opens and runs real projects on a device. CI partitions the same assertions into three independent shards;
/// the default run still executes all of them. A shard owns its workspace and does not depend on another job.
/// </summary>
public static partial class SelfTest
{
    /// <summary>The name typed when the sample asks for one.</summary>
    public const string Name = "Tablet";

    /// <summary>Parses the optional device-test shard, rejecting misspellings and numeric enum values.</summary>
    /// <param name="value">An absent value means the full self test.</param>
    /// <param name="shard">The selected shard, when recognized.</param>
    /// <returns>Whether the value names a supported shard.</returns>
    public static bool TryParseShard(string? value, out SelfTestShard shard)
    {
        shard = value?.ToLowerInvariant() switch
        {
            null or "" or "all" => SelfTestShard.All,
            "managed" => SelfTestShard.Managed,
            "ui" => SelfTestShard.Ui,
            "native" => SelfTestShard.Native,
            "containers" => SelfTestShard.Containers,
            "performance" => SelfTestShard.Performance,
            _ => (SelfTestShard)(-1),
        };
        return Enum.IsDefined(shard);
    }

    /// <summary>Runs the self test in the running app.</summary>
    /// <param name="main">The app's root view model.</param>
    /// <param name="samples">The sample library.</param>
    /// <param name="cancellationToken">Gives up, for example after a timeout.</param>
    public static Task<SelfTestResult> RunAsync(MainViewModel main, SampleLibrary samples, CancellationToken cancellationToken = default) =>
        RunAsync(main, samples, templates: null, screenshots: null, cancellationToken);

    /// <summary>Runs every device-test group, with web and native apps when templates are available.</summary>
    /// <param name="main">The app's root view model.</param>
    /// <param name="samples">The sample library.</param>
    /// <param name="templates">Creates the test apps; without it, only the sample checks run.</param>
    /// <param name="screenshots">A folder for design pictures, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Gives up, for example after a timeout.</param>
    public static Task<SelfTestResult> RunAsync(MainViewModel main, SampleLibrary samples, ProjectTemplates? templates, string? screenshots, CancellationToken cancellationToken = default) =>
        RunShardAsync(main, samples, templates, screenshots, SelfTestShard.All, cancellationToken);

    /// <summary>Runs a complete independent subset of the device test, without weakening its assertions.</summary>
    /// <param name="main">The app's root view model.</param>
    /// <param name="samples">The sample library.</param>
    /// <param name="templates">Creates the test apps; without it, only the sample checks run.</param>
    /// <param name="screenshots">A folder for design pictures, or <see langword="null"/> for none.</param>
    /// <param name="shard">The device-test group, or <see cref="SelfTestShard.All"/>.</param>
    /// <param name="cancellationToken">Gives up, for example after a timeout.</param>
    public static async Task<SelfTestResult> RunShardAsync(MainViewModel main, SampleLibrary samples, ProjectTemplates? templates, string? screenshots, SelfTestShard shard, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(main);
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentOutOfRangeException.ThrowIfNegative((int)shard);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((int)shard, (int)SelfTestShard.Performance);
        if (shard == SelfTestShard.Containers)
        {
            return templates is null ? new(false, "Containers: templates are required", TimeSpan.Zero)
                : await RunContainersAsync(main, templates, screenshots, cancellationToken);
        }
        if (shard == SelfTestShard.Performance)
        {
            return await RunPerformanceAsync(main, samples, screenshots, cancellationToken);
        }
        var clock = Stopwatch.StartNew();
        var messages = new List<string> { $"Shard: {shard.ToString().ToLowerInvariant()}" };
        SelfTestResult Finish(bool success)
        {
            if (!success && screenshots is not null && main.Workspace is { } failed)
            {
                Directory.CreateDirectory(screenshots);
                File.WriteAllText(Path.Combine(screenshots, "output.txt"), failed.Output.Text);
                File.WriteAllText(Path.Combine(screenshots, "language-servers.txt"), failed.LanguageServerOutput.Text);
                File.WriteAllText(Path.Combine(screenshots, "debug-adapter.txt"), failed.DebugAdapterOutput.Text);
            }

            return new(success, string.Join(" | ", messages), clock.Elapsed);
        }
        async Task<bool> CheckAsync(Func<Task<SelfTestResult>> run)
        {
            var result = await run();
            messages.Add(result.Message);
            return result.Success;
        }

        var managed = shard is SelfTestShard.All or SelfTestShard.Managed;
        var ui = shard is SelfTestShard.All or SelfTestShard.Ui;
        var native = shard is SelfTestShard.All or SelfTestShard.Native;
        var previousProcessTrace = Environment.GetEnvironmentVariable("TABSTUDIO_TRACE_PROCESSES");
        if (native && OperatingSystem.IsAndroid()) Environment.SetEnvironmentVariable("TABSTUDIO_TRACE_PROCESSES", "1");
        using var preparationStop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task? preparation = null;
        string? avaloniaApp = null;
        try
        {
            var solution = await samples.EnsureExtractedAsync(cancellationToken);
            await main.OpenWorkspaceAsync(solution);
            if (main.Workspace is not { } workspace)
            {
                messages.Add("The sample did not open.");
                return Finish(false);
            }

            if (managed)
            {
                if (!workspace.RunCommand.CanExecute(null))
                {
                    messages.Add($"F5 is not available: {workspace.StatusText}.");
                    return Finish(false);
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
                messages.Add(string.Join(" | ", output.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).TakeLast(6)));
                if (!output.Contains("has exited with code 0", StringComparison.Ordinal)
                    || !output.Contains($", {Name}!", StringComparison.Ordinal)
                    || !await CheckAsync(() => DebugSampleAsync(workspace, cancellationToken))
                    || !await CheckAsync(() => RecordSampleAsync(workspace, cancellationToken))
                    || !await CheckAsync(() => CheckLocalAIAsync(cancellationToken))
                    || !await CheckAsync(() => LinkSampleAsync(main, workspace, cancellationToken))
                    || !await CheckAsync(() => LiveSampleAsync(main, workspace, cancellationToken))
                    || !await CheckAsync(() => CheckLanguageServersAsync(main, cancellationToken)))
                {
                    return Finish(false);
                }
            }

            if (templates is null)
            {
                return Finish(managed);
            }

            async Task<bool> CheckGoAndRustAsync() =>
                await CheckAsync(() => CheckGoToolchainAsync(main, templates, cancellationToken))
                && await CheckAsync(() => CheckRustToolchainAsync(main, templates, cancellationToken));

            if (managed && !await CheckAsync(() => CheckRemoteSshAsync(main.Workspace!, cancellationToken)))
            {
                return Finish(false);
            }

            if (managed && !await CheckAsync(() => CheckDatabaseAsync(main.Workspace!, cancellationToken)))
            {
                return Finish(false);
            }

            if (managed && !await CheckAsync(() => ServeWebAppAsync(main, templates, cancellationToken)))
            {
                return Finish(false);
            }

            // Check the runtime capability boundary and the complete native process/GC sequence before packages.
            if (native && (!await CheckAsync(() => Task.FromResult(CheckHotReload(cancellationToken)))
                || !await CheckAsync(() => StartProgramsWhileCollectingAsync(cancellationToken))))
            {
                return Finish(false);
            }

            // In a full run the browser's trusted-input assertions finish before downloads/decompression start.
            // Each independent shard installs only its own tools. Full runs retain the original complete plan.
            // Native runtime/process-integrity checks belong immediately before this preparation starts.
            if (native || shard == SelfTestShard.Managed)
            {
                preparation = PrepareSelfTestToolchainsAsync(main.Workspace!.Toolchains, shard, preparationStop.Token);
            }

            if (managed && !await CheckAsync(() => ShowStaticSiteAsync(main, templates, cancellationToken)))
            {
                return Finish(false);
            }

            if (managed && !await CheckAsync(() => ServeBlazorAppsAsync(main, templates, cancellationToken)))
            {
                return Finish(false);
            }

            if (shard == SelfTestShard.Managed)
            {
                // Go/Rust balance the shorter managed shard. A full manual run keeps their original position
                // after Python tools below, so every assertion still executes once in its original order.
                await preparation!;
                if (!await CheckGoAndRustAsync())
                {
                    return Finish(false);
                }
            }

            if (ui)
            {
                if (!await CheckAsync(() => ShowAvaloniaAppAsync(main, templates, cancellationToken)) || main.Workspace is not { } last)
                {
                    return Finish(false);
                }

                avaloniaApp = last.Workspace is { } loaded ? loaded.Solution?.FilePath ?? loaded.RootDirectory : null;
                if (!await CheckAsync(() => InstallGrammarExtensionAsync(last, FindEditor, cancellationToken))
                    || !await CheckAsync(() => ShowWindowsFormsAppAsync(main, templates, cancellationToken))
                    || !await CheckAsync(() => PortFormsAppAsync(main, templates, screenshots, cancellationToken))
                    || !await CheckAsync(() => CheckDesignAsync(main, screenshots, cancellationToken))
                    || !await CheckAsync(() => ShowWpfAppAsync(main, templates, cancellationToken))
                    || !await CheckAsync(() => ShowUnoAppAsync(main, templates, cancellationToken))
                    || !await CheckAsync(() => CheckSwarmScreensAsync(main, templates, cancellationToken))
                    || !await CheckAsync(() => RunOnSwarmAsync(main, templates, cancellationToken)))
                {
                    return Finish(false);
                }

            }

            if (native)
            {
                // Package operations must finish before native debuggers and their programs use the environment.
                await preparation!;
                // Backing services run after the toolchains: they share the Alpine package database, and the check
                // reopens the Python workspace that RunToolchainsAsync leaves for the Python tools check.
                if (!await CheckAsync(() => RunToolchainsAsync(main, templates, cancellationToken))
                    || !await CheckAsync(() => CheckEmbeddedBridgeAsync(main.Workspace!, cancellationToken))
                    || !await CheckAsync(() => RunServicesAsync(main, templates, cancellationToken))
                    || !await CheckAsync(() => CheckPythonToolsAsync(main, cancellationToken))
                    || !await CheckAsync(() => CheckListeningPortAsync(main, templates, cancellationToken))
                    || (shard == SelfTestShard.All && !await CheckGoAndRustAsync())
                    || !await CheckAsync(() => CheckCppToolchainAsync(main, templates, cancellationToken))
                    || !await CheckAsync(() => CheckJavaToolchainAsync(main, templates, cancellationToken)))
                {
                    return Finish(false);
                }
            }

            if (avaloniaApp is not null)
            {
                // Scrolling and external-display checks continue with this known Avalonia project.
                await main.OpenWorkspaceAsync(avaloniaApp);
            }

            return Finish(true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            messages.Add($"{ex.GetType().Name}: {ex.Message}{DescribeActivity(main)}".ReplaceLineEndings(" "));
            return Finish(false);
        }
        finally
        {
            await FinishSelfTestPreparationAsync(preparation, preparationStop);
            if (native && OperatingSystem.IsAndroid()) Environment.SetEnvironmentVariable("TABSTUDIO_TRACE_PROCESSES", previousProcessTrace);
        }
    }
}

