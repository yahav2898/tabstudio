using System.Runtime.CompilerServices;
using TabStudio.App.Services.Toolchains;
using TabStudio.Core.Workspaces;
using TabStudio.LanguageServers.DebugAdapters;
using TabStudio.Toolchains.Processes;

namespace TabStudio.App.ViewModels;

/// <summary>
/// F5 on Python and Node.js projects: the program F5 would run (its entry point, or the file being edited) runs under
/// the toolchain's debug adapter, debugpy or js-debug, with breakpoints, stepping, Locals, Watch, the Call Stack and
/// exceptions through the debugger's adapter path. Ctrl+F5 still runs it without the debugger.
/// </summary>
/// <remarks>
/// The adapters run the program through TabStudio (the protocol's <c>runInTerminal</c>): it starts in the
/// toolchain's runtime with its console in the Output panel, so its output streams there and lines typed in the input
/// box reach its standard input, and <c>input()</c> works while debugging as it does without the debugger.
/// </remarks>
public sealed partial class WorkspaceViewModel
{
    private static readonly TimeSpan ProgramDrainTime = TimeSpan.FromSeconds(3);

    private readonly ConditionalWeakTable<LaunchTarget, IReadOnlyDictionary<string, string?>> _programEnvironments = new();
    private readonly ConditionalWeakTable<DebugAdapterSession, StrongBox<bool>> _adapterConsoles = new();
    private AdapterProgram? _adapterProgramRun;
    private Task? _adapterProgramFinish;

    /// <summary>Records adapter messages during the emulator's real-tool debugging check.</summary>
    internal bool TraceDebugAdapterMessages { get; set; }

    /// <summary>Feeds a socket adapter's inherited console input, without ever writing text to DAP's protocol stream.</summary>
    private void StartAdapterInput(DebugAdapterSession session)
    {
        if (StartNativeDebugInput())
        {
            return;
        }

        if (!session.AcceptsProgramInput)
        {
            return;
        }

        var input = Output.BeginProcessInput();
        _ = Task.Run(async () =>
        {
            try
            {
                while (input.ReadLine() is { } line)
                {
                    await session.WriteProgramInputAsync(line);
                }
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
            {
                // The debuggee or adapter ended while the user was typing.
            }
        });
    }

    /// <summary>F5, F10, F11 and Ctrl+F10 on a toolchain project: debugs what Ctrl+F5 runs, with the toolchain's adapter.</summary>
    /// <param name="info">The project.</param>
    /// <param name="start">How the program starts.</param>
    private async Task DebugToolchainProjectAsync(ProjectInfo info, DebugStart start)
    {
        if (!await SaveAllCoreAsync())
        {
            return;
        }

        if (FindToolchainProject(info) is not { } item)
        {
            return;
        }

        var project = item.Project;
        var toolchain = item.Toolchain;
        if (TabStudio.Toolchains.Embedded.Projects.EmbeddedProjectDetector.Read(project.Directory) is not null)
        {
            if (File.Exists(Path.Combine(project.Directory, ".tabstudio", "embedded-debug.json"))) await DebugEmbeddedChipAsync();
            else await RunToolchainProjectAsync(info);
            return;
        }
        if (TabStudio.Toolchains.Microchip.MicrochipInstallation.IsCompiler(toolchain.Id))
        {
            await DebugPicProjectAsync(info, start);
            return;
        }
        if (toolchain.Id == "flutter")
        {
            await StartDartOrFlutterAsync(item, noDebug: false);
            return;
        }
        if (toolchain.DebugAdapter?.Type is not { } type || !ToolchainLaunch.Supports(type) || _languageServers?.Configuration.Registry.FindAdapter(type) is null)
        {
            // A toolchain without a debug adapter TabStudio drives runs as with Ctrl+F5.
            await RunToolchainProjectAsync(info);
            return;
        }

        if (!await EnsureToolchainReadyAsync(toolchain, $"{project.Name} runs with {toolchain.Name}, which is not installed yet."))
        {
            return;
        }

        await SaveAllCoreAsync();
        if (PlanToolchainProject(project, ActiveDocument?.FilePath) is not { } plan)
        {
            // Nothing to run, which the run explains, or a command the adapter cannot launch, which runs as it is.
            await RunToolchainProjectAsync(info);
            return;
        }

        ErrorList.SetRunError(project.Directory, null);
        _runDirectory = plan.Run.WorkingDirectory ?? project.Directory;
        if (plan.Build is { } build)
        {
            Output.Clear();
            SelectedPanel = BottomPanel.Output;
            IsBottomPanelVisible = true;
            State = IdeState.Running;
            StatusText = $"Building {project.Name}";
            _run = new CancellationTokenSource();
            try
            {
                Output.WriteLine("> " + build.CommandLine, OutputKind.Info);
                var compiled = await BuildCargoOrToolchainAsync(project, plan, _run.Token);
                plan = compiled.Plan;
                var result = compiled.Result;
                if (!result.Succeeded)
                {
                    ReportToolchainRun(project, plan.ProgramName, result);
                    return;
                }
            }
            finally
            {
                _run.Dispose();
                _run = null;
            }
        }

        if (ToolchainLaunch.Create(type, plan) is not { } configuration)
        {
            State = IdeState.Failed;
            StatusText = "No executable to debug";
            return;
        }

        var withInput = PrepareNativeDebugInput(configuration);
        if (withInput is null)
        {
            return;
        }

        configuration = withInput;
        if (type == "php")
        {
            // The database its migrations need starts first, as it does for Ctrl+F5.
            if (!await PrepareServicesAsync(project.Directory))
            {
                return;
            }

            using (var migrating = new CancellationTokenSource())
            {
                _run = migrating;
                try
                {
                    if (await PrepareLaravelDatabaseAsync(project, migrating.Token) is { } failed)
                    {
                        ReportToolchainRun(project, plan.ProgramName, failed);
                        return;
                    }
                }
                finally
                {
                    _run = null;
                }
            }

            configuration = PreparePhpDebug(project, configuration);
        }

        if (type == "java")
        {
            var prepared = toolchain.Id == "kotlin"
                ? await PrepareKotlinDebugAsync(project, configuration)
                : await PrepareJavaDebugAsync(project, configuration);
            if (prepared is null)
            {
                return;
            }

            configuration = prepared;
        }

        var target = new LaunchTarget(configuration.Name, configuration, IsCurrentFile: false);
        _programEnvironments.AddOrUpdate(target, WithServiceEnvironment(plan.Run).Environment);
        await LaunchAdapterAsync(target, start, noDebug: false);
        if (!IsDebuggingWithAdapter)
        {
            StopNativeDebugInput();
        }
    }

    /// <summary>
    /// The protocol's <c>runInTerminal</c>: starts the program an adapter asks for, as debugpy and js-debug start the
    /// debugged program, in the toolchain's runtime, with its console in the Output panel.
    /// </summary>
    /// <returns>The program's process id, once it started.</returns>
    /// <exception cref="DebugAdapterException">It could not start; the adapter is told why.</exception>
    private Task<int?> RunAdapterProgramAsync(DapRunInTerminalRequest request, CancellationToken cancellationToken) =>
        InvokeOnUiAsync(() => StartAdapterProgramAsync(request));

    private async Task<int?> StartAdapterProgramAsync(DapRunInTerminalRequest request)
    {
        if (_adapterRoot is null || Workspace is not { } workspace)
        {
            throw new DebugAdapterException("The debug session has ended.");
        }

        if (_adapterProgramRun is not null)
        {
            throw new DebugAdapterException("The debugged program already runs in TabStudio's Output panel.");
        }

        // A toolchain project's program gets its run's environment, such as its .venv on PATH, under the adapter's.
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (_adapterTarget is { } target && _programEnvironments.TryGetValue(target, out var run))
        {
            foreach (var (name, value) in run)
            {
                environment[name] = value;
            }
        }

        foreach (var (name, value) in request.Environment)
        {
            environment[name] = value;
        }

        var spec = new ProcessStartSpec
        {
            Program = request.Arguments[0],
            Arguments = [.. request.Arguments.Skip(1)],
            WorkingDirectory = request.WorkingDirectory is { Length: > 0 } folder ? folder : workspace.RootDirectory,
            Environment = environment,
        };
        var started = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stop = new CancellationTokenSource();
        _adapterProgramFinish = null;
        var input = Output.BeginProcessInput();
        // Retain the output owner for this root and all its child sessions.
        // DAP stdout/stderr can arrive after FinishAdapterProgram clears the
        // process field while its final output is still draining.
        Volatile.Write(ref _adapterConsoles.GetOrCreateValue(_adapterRoot).Value, true);
        var listening = BeginServerRun();
        var completion = OutputProcess.RunAsync(_toolchains.Runtime, spec, Output, input, stop.Token, process => started.TrySetResult(process.Id), listening);
        _adapterProgramRun = new AdapterProgram(completion, stop);
        _ = ObserveAsync();
        return await started.Task;

        async Task ObserveAsync()
        {
            try
            {
                var result = await completion;
                _ui.Post(EndServerRun);
                started.TrySetException(new DebugAdapterException(result.StartError ?? "The program ended before it started."));
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                started.TrySetException(new DebugAdapterException(ex.Message, ex));
            }
        }
    }

    /// <summary>
    /// Ends a session once the program it started has finished writing to the Output panel. The root session ends
    /// this way when it reports its end, and when its last child session ends.
    /// </summary>
    private async Task EndAdapterSessionAfterProgramAsync(DebugAdapterSession session)
    {
        await FinishAdapterProgramAsync(session);
        EndAdapterSession(session);
    }

    /// <summary>
    /// Before the root session ends: lets the program the adapter started finish writing to the Output panel, then
    /// stops it if it is still running, and takes its exit code when the adapter reported none. Never throws.
    /// </summary>
    private Task FinishAdapterProgramAsync(DebugAdapterSession session)
    {
        if (!ReferenceEquals(session, _adapterRoot ?? _adapter))
        {
            return Task.CompletedTask;
        }

        if (_adapterProgramRun is { } run)
        {
            _adapterProgramRun = null;
            _adapterProgramFinish = DrainAdapterProgramAsync(run, session.Adapter.Name);
        }

        return _adapterProgramFinish ?? Task.CompletedTask;
    }

    private async Task DrainAdapterProgramAsync(AdapterProgram run, string adapterName)
    {
        try
        {
            if (await Task.WhenAny(run.Completion, Task.Delay(ProgramDrainTime)) != run.Completion)
            {
                await run.Stop.CancelAsync();
            }

            var result = await run.Completion;
            if (result.Exit is { WasStopped: false } exit)
            {
                // The program's own exit code wins over the adapter's: php-debug reports 0 for every program it
                // asked TabStudio to start, so exit(3) and a fatal error (255) ended "with code 0".
                _adapterExitCode = exit.ExitCode;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            DebugAdapterOutput.WriteLine($"[{adapterName}] {ex.Message}", OutputKind.Failure);
        }
        finally
        {
            Output.EndInput();
            run.Stop.Dispose();
        }
    }

    /// <summary>Stops the program an adapter started, when the workspace closes. Called when the toolchains are disposed.</summary>
    private void StopAdapterProgram()
    {
        StopNativeDebugInput();
        if (Interlocked.Exchange(ref _adapterProgramRun, null) is { } run)
        {
            run.Stop.Cancel();
        }
    }

    /// <summary>A program a debug adapter asked TabStudio to run: how it ends, and how to stop it.</summary>
    /// <param name="Completion">Completes when it exited and its output is read.</param>
    /// <param name="Stop">Stops it and everything it started.</param>
    private sealed record AdapterProgram(Task<OutputProcessResult> Completion, CancellationTokenSource Stop);
}
