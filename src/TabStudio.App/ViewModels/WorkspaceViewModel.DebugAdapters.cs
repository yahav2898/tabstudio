using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Editing;
using TabStudio.App.Services.LanguageServers;
using TabStudio.App.Services.Toolchains;
using TabStudio.Debugging;
using TabStudio.Debugging.Symbols;
using TabStudio.LanguageServers.Configuration;
using TabStudio.LanguageServers.DebugAdapters;
using TabStudio.LanguageServers.Hosting;
using TabStudio.Toolchains.Projects;

namespace TabStudio.App.ViewModels;

/// <summary>
/// What F5 can run besides a C# project: a launch configuration of <c>.tabstudio/launch.json</c>, or the current
/// file of a language with a debug adapter, such as "Python: Current File".
/// </summary>
/// <param name="Name">The name on the Run button.</param>
/// <param name="Configuration">The launch configuration, with its variables not yet substituted.</param>
/// <param name="IsCurrentFile">Whether it runs the file in the active editor.</param>
public sealed record LaunchTarget(string Name, LaunchConfiguration Configuration, bool IsCurrentFile);

/// <summary>An entry of the Run button's menu: a startup project or a launch configuration.</summary>
/// <param name="Name">The name.</param>
/// <param name="Detail">What it is, such as "C# project" or "Python (debugpy)".</param>
/// <param name="Select">Makes it what F5 runs.</param>
public sealed record RunTargetOption(string Name, string Detail, IRelayCommand Select)
{
    /// <summary>The menu's text.</summary>
    public string Header => $"{Name}  ·  {Detail}";
}

/// <summary>An exception filter a debug adapter offers, such as "Uncaught Exceptions", in the Debug menu.</summary>
public sealed partial class ExceptionFilterOption : ObservableObject
{
    /// <summary>Creates the option.</summary>
    /// <param name="filter">The adapter's filter.</param>
    /// <param name="isEnabled">Whether it starts on.</param>
    public ExceptionFilterOption(DapExceptionFilter filter, bool isEnabled)
    {
        ArgumentNullException.ThrowIfNull(filter);
        Filter = filter.Filter;
        Label = filter.Label;
        IsEnabled = isEnabled;
    }

    /// <summary>The identifier the adapter takes.</summary>
    public string Filter { get; }

    /// <summary>The name shown, such as "Raised Exceptions".</summary>
    public string Label { get; }

    /// <summary>Whether the program pauses on these exceptions.</summary>
    [ObservableProperty]
    public partial bool IsEnabled { get; set; }
}

/// <summary>What the editor's breakpoint menu offers for files a debug adapter runs: hit counts and logpoints.</summary>
public interface IEditorBreakpointOptions
{
    /// <summary>Whether a file's breakpoints can have hit counts and log messages: a debug adapter runs its language.</summary>
    /// <param name="filePath">Full path of the file.</param>
    bool HasBreakpointOptions(string filePath);

    /// <summary>Asks for the hit count of the breakpoint on a line, setting one there first if needed.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="line">One-based line.</param>
    Task EditHitCountAsync(string filePath, int line);

    /// <summary>Asks for the message the breakpoint on a line logs instead of stopping, setting one there first if needed.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="line">One-based line.</param>
    Task EditLogMessageAsync(string filePath, int line);
}

/// <summary>
/// The debugger for every language with a debug adapter, such as Python with debugpy: F5 launches the launch
/// configuration or the current file, and the same toolbar, touch bar, breakpoints, Locals, Watch, Call Stack,
/// DataTips and exception helper as for C# drive the adapter's session. The program's output is in the Output panel;
/// the adapter's own log under "Debug Adapters".
/// </summary>
public sealed partial class WorkspaceViewModel : IEditorBreakpointOptions
{
    private readonly Dictionary<string, List<DapSourceBreakpoint>> _sentBreakpoints = new(StringComparer.Ordinal);

    /// <summary>
    /// Where the next adapter session reads breakpoints from when not from their own files, such as the Java sources
    /// that put a Kotlin file's breakpoints in its classes; set before <see cref="LaunchAdapterAsync"/>.
    /// </summary>
    private Func<string, IReadOnlyList<DapSourceBreakpoint>, IReadOnlyList<DapBreakpointSource>?>? _pendingBreakpointSources;
    private readonly List<DebugAdapterSession> _adapterChildren = [];
    private DebugAdapterSession? _adapter;
    private DebugAdapterSession? _adapterRoot;
    private int? _adapterExitCode;
    private LaunchTarget? _adapterTarget;
    private string _adapterProgram = string.Empty;
    private int? _adapterThread;
    private IReadOnlyList<DapStackFrame> _adapterFrames = [];
    private DebugSourceSpan? _adapterCurrent;
    private (string FilePath, int Line)? _cursorBreakpoint;
    private bool _adapterLaunched;
    private bool _adapterRestart;
    private int _adapterPauses;

    /// <summary>What the Run button's menu offers: the startup projects, launch configurations and the current file.</summary>
    public ObservableCollection<RunTargetOption> RunTargets { get; } = [];

    /// <summary>The launch configuration chosen in the Run button's menu, if one was.</summary>
    [ObservableProperty]
    public partial LaunchTarget? SelectedLaunchTarget { get; private set; }

    private Task _adapterBreakpointUpdate = Task.CompletedTask;

    /// <summary>The exception filters of the debug adapter's session, for the Debug menu.</summary>
    public ObservableCollection<ExceptionFilterOption> ExceptionFilters { get; } = [];

    /// <summary>Whether the Debug menu shows the adapter's exception filters.</summary>
    public bool HasExceptionFilters => ExceptionFilters.Count > 0;

    /// <summary>
    /// What F5 runs when no C# project is the startup project: the chosen launch configuration, else the active
    /// file of a language with a debug adapter, else the workspace's first launch configuration.
    /// </summary>
    public LaunchTarget? CurrentLaunchTarget
    {
        get
        {
            if (SelectedLaunchTarget is { } selected)
            {
                return selected;
            }

            if (StartupProject is not null || _languageServers is not { } host)
            {
                return null;
            }

            return CurrentFileTarget(host) ?? host.Configuration.LaunchConfigurations.Select(ToTarget).FirstOrDefault();
        }
    }

    /// <summary>Whether a debug adapter's session runs, paused or not.</summary>
    public bool IsDebuggingWithAdapter => _adapter is not null;

    /// <summary>The adapter's session, for tests.</summary>
    internal DebugAdapterSession? AdapterSession => _adapter;

    /// <summary>The Run button's label for a launch target, when one is what F5 runs.</summary>
    private string? LaunchTargetLabel => StartupProject is null ? CurrentLaunchTarget?.Name : null;

    /// <summary>The Run button's "Run on" text for a launch target: its debug adapter.</summary>
    private string? LaunchTargetHost => StartupProject is null && CurrentLaunchTarget is { } target ? FindAdapter(target)?.Name : null;

    /// <summary>Whether F5 runs a launch target rather than the startup project.</summary>
    private bool UsesLaunchTarget => StartupProject is null && CurrentLaunchTarget is not null;

    /// <inheritdoc />
    bool IEditorBreakpointOptions.HasBreakpointOptions(string filePath) => UsesAdapterBreakpoints(filePath);

    /// <inheritdoc />
    Task IEditorBreakpointOptions.EditHitCountAsync(string filePath, int line) => EditAdapterBreakpointAsync(filePath, line, BreakpointOption.HitCount);

    /// <inheritdoc />
    Task IEditorBreakpointOptions.EditLogMessageAsync(string filePath, int line) => EditAdapterBreakpointAsync(filePath, line, BreakpointOption.LogMessage);

    /// <summary>Makes a launch target what F5 runs, as choosing it in the Run button's menu does.</summary>
    /// <param name="target">The target.</param>
    public void SelectLaunchTarget(LaunchTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        SetStartupProject(null);
        SelectedLaunchTarget = target;
        OnLaunchTargetChanged();
    }

    /// <summary>Sets up debug adapters. Called once, when language servers are set up.</summary>
    private void InitializeDebugAdapters()
    {
        Breakpoints.Changed += (_, _) => _adapterBreakpointUpdate = UpdateAdapterBreakpointsAfterAsync(_adapterBreakpointUpdate);
        Locals.Tree.EditValueRequested += async (_, row) => await EditVariableAsync(row);
        Watch.Tree.EditValueRequested += async (_, row) => await EditVariableAsync(row);
        CallStack.ThreadChosen += async (_, thread) => await ShowAdapterThreadAsync(thread.Id);
        StartupProjects.CollectionChanged += (_, _) => UpdateRunTargets();
        ExceptionFilters.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasExceptionFilters));
        PropertyChanged += OnPropertyChangedForDebugAdapters;
    }

    /// <summary>Whether a file's breakpoints go to a debug adapter: its language has one, and Roslyn does not debug it.</summary>
    private bool UsesAdapterBreakpoints(string filePath) =>
        !FileTypes.For(filePath).IsRoslyn
        && _languageServers?.Configuration.Registry is { } registry
        && registry.FindLanguage(filePath) is { } language
        && registry.FindAdaptersForLanguage(language.Id).Any();

    /// <summary>Starts the launch target under its debug adapter: F5, F10, F11 and Ctrl+F10, or Ctrl+F5 without debugging.</summary>
    private async Task LaunchAdapterAsync(LaunchTarget target, DebugStart start, bool noDebug)
    {
        if (_adapter is not null || _languageServers is not { } host || Workspace is not { } workspace)
        {
            return;
        }

        if (target.IsCurrentFile && ActiveDocument is null)
        {
            StatusText = "Open the file to debug first.";
            return;
        }

        if (FindAdapter(target) is not { } adapter)
        {
            StatusText = $"No debug adapter is set up for \"{target.Configuration.Type}\" launch configurations.";
            return;
        }

        adapter = ToolchainLaunch.ForRuntime(adapter, _toolchains.Runtime.Kind);

        // A configuration with "debugServer" connects to an adapter that already runs.
        var executable = target.Configuration.DebugServerPort is not null
            ? string.Empty
            : _languageServerServices.Launcher.ResolveFirst(adapter.Commands);
        executable ??= await InstallMissingAdapterAsync(adapter);
        if (executable is null)
        {
            var install = MissingToolHint(adapter.Name, adapter.Command, adapter.Package);
            StatusText = $"{adapter.Name} is not installed.{install}";
            DebugAdapterOutput.WriteLine($"[{adapter.Name}] {adapter.Command} was not found.{install}", OutputKind.Failure);
            return;
        }

        if (!await PrepareServicesAsync(StartupProject?.DirectoryPath ?? workspace.RootDirectory)) return;

        await SaveAllCoreAsync();
        var variables = new LaunchVariables(workspace.RootDirectory, ActiveDocument?.FilePath);
        var configuration = target.Configuration.Substitute(variables);
        if (_serviceEnvironment.Count > 0)
        {
            var properties = (System.Text.Json.Nodes.JsonObject)configuration.Properties.DeepClone();
            var environment = properties["env"] as System.Text.Json.Nodes.JsonObject ?? new System.Text.Json.Nodes.JsonObject();
            foreach (var (name, value) in _serviceEnvironment) environment[name] = value;
            properties["env"] = environment.DeepClone();
            configuration = configuration with { Properties = properties };
        }

        if (noDebug || start == DebugStart.StepInto)
        {
            var properties = (System.Text.Json.Nodes.JsonObject)configuration.Properties.DeepClone();
            if (noDebug)
            {
                properties["noDebug"] = true;
            }

            if (start == DebugStart.StepInto)
            {
                properties["stopOnEntry"] = true;
            }

            configuration = configuration with { Properties = properties };
        }

        _adapterProgram = configuration.Program is { Length: > 0 } program ? Path.GetFileName(program) : configuration.Name;
        Output.Clear();
        SelectedPanel = BottomPanel.Output;
        IsBottomPanelVisible = true;
        Output.WriteLine($"{(noDebug ? "Running" : "Debugging")} {_adapterProgram} with {adapter.Name}...", OutputKind.Info);
        State = IdeState.Busy;
        StatusText = $"Starting {adapter.Name}";

        DebugAdapterSession session;
        try
        {
            var options = new DebugAdapterSessionOptions
            {
                TraceMessages = _languageServerServices.TraceMessages || TraceDebugAdapterMessages, SupportsChildSessions = true, SupportsRunInTerminal = true,
                StartupLog = line => DebugAdapterOutput.WriteLine($"[{adapter.Name}] {line}", OutputKind.Info),
            };
            session = configuration.DebugServerPort is { } port
                ? await DebugAdapterSession.ConnectAsync(adapter, port, options)
                : await DebugAdapterSession.StartAsync(adapter, executable, workspace.RootDirectory, _languageServerServices.Launcher, options);
        }
        catch (Exception ex) when (ex is DebugAdapterException or ServerStartException or OperationCanceledException)
        {
            FailAdapterStart(adapter, ex.Message);
            return;
        }

        _adapter = session;
        _adapterRoot = session;
        _adapterExitCode = null;
        _adapterTarget = target;
        session.BreakpointSources = _pendingBreakpointSources;
        _pendingBreakpointSources = null;
        _adapterLaunched = false;
        _sentBreakpoints.Clear();
        WireAdapterEvents(session);
        StartAdapterInput(session);
        if (session.Adapter.Id == "flutter") ConfigureFlutterTarget(configuration);
        AcceptChildSessions(session, executable, workspace.RootDirectory, noDebug);
        if (session.HasEnded)
        {
            // The adapter ended before its events were wired, such as one that crashed at once.
            EndAdapterSession(session);
            return;
        }

        ShowExceptionFilters(session);
        if (start == DebugStart.RunToCursor && ActiveDocument is { } cursorDocument)
        {
            _cursorBreakpoint = (cursorDocument.FilePath, cursorDocument.CaretLine);
        }

        State = IdeState.Debugging;
        StatusText = $"{(noDebug ? "Running" : "Debugging")} {_adapterProgram}";
        OnPropertyChanged(nameof(IsDebuggingWithAdapter));
        var breakpoints = noDebug
            ? new Dictionary<string, IReadOnlyList<DapSourceBreakpoint>>(StringComparer.Ordinal)
            : AdapterBreakpoints(session);
        try
        {
            var results = await session.LaunchAsync(configuration, breakpoints, EnabledExceptionFilters(), CancellationToken.None);

            // A short program can end before the launch's answer is handled: its breakpoints then stay unbound.
            if (ReferenceEquals(session, _adapter))
            {
                _adapterLaunched = true;
                foreach (var (file, set) in results)
                {
                    Breakpoints.ApplyAdapterResults(file, set);
                }
            }
        }
        catch (Exception ex) when (ex is DebugAdapterException or TimeoutException or OperationCanceledException)
        {
            Output.WriteLine($"{adapter.Name} could not start {_adapterProgram}: {ex.Message}", OutputKind.Failure);
            await session.DisposeAsync();
        }
    }

    private void FailAdapterStart(DebugAdapterDefinition adapter, string reason)
    {
        StopNativeDebugInput();
        Output.WriteLine($"{adapter.Name} did not start: {reason}", OutputKind.Failure);
        DebugAdapterOutput.WriteLine($"[{adapter.Name}] {reason}", OutputKind.Failure);
        State = IdeState.Failed;
        StatusText = $"{adapter.Name} did not start";
    }

    /// <summary>
    /// Starts the child sessions an adapter asks for, as js-debug does for every Node process: the debugger then
    /// drives the child, and the session ends with the first one.
    /// </summary>
    private void AcceptChildSessions(DebugAdapterSession parent, string executable, string workingDirectory, bool noDebug) =>
        parent.ChildSessionHandler = (request, _) => InvokeOnUiAsync(() => StartChildSessionAsync(parent, executable, workingDirectory, request, noDebug));

    private async Task<bool> StartChildSessionAsync(DebugAdapterSession parent, string executable, string workingDirectory, DapChildSessionRequest request, bool noDebug)
    {
        if (parent.HasEnded || _adapterRoot is null)
        {
            throw new DebugAdapterException("The debug session ended.");
        }

        DebugAdapterSession child;
        var options = new DebugAdapterSessionOptions { TraceMessages = _languageServerServices.TraceMessages, SupportsChildSessions = true, SupportsRunInTerminal = true };
        try
        {
            child = parent.Port is { } port
                ? await DebugAdapterSession.ConnectAsync(parent.Adapter, port, options)
                : await DebugAdapterSession.StartAsync(parent.Adapter, executable, workingDirectory, _languageServerServices.Launcher, options);
        }
        catch (Exception ex) when (ex is ServerStartException or OperationCanceledException)
        {
            throw new DebugAdapterException($"The child session did not start: {ex.Message}", ex);
        }

        _adapterChildren.Add(child);
        WireAdapterEvents(child);
        AcceptChildSessions(child, executable, workingDirectory, noDebug);
        _adapter = child;
        _adapterLaunched = false;
        _sentBreakpoints.Clear();
        ShowExceptionFilters(child);
        var configuration = new LaunchConfiguration
        {
            Name = request.Configuration["name"]?.GetValue<string>() ?? _adapterProgram,
            Type = parent.Adapter.Id,
            Request = request.Request,
            Properties = request.Configuration,
        };
        var breakpoints = noDebug
            ? new Dictionary<string, IReadOnlyList<DapSourceBreakpoint>>(StringComparer.Ordinal)
            : AdapterBreakpoints(child);

        // The adapter waits for this answer before the child can attach, so the child launches after it.
        _ = LaunchChildSessionAsync(child, configuration, breakpoints);
        return true;
    }

    private async Task LaunchChildSessionAsync(DebugAdapterSession child, LaunchConfiguration configuration, Dictionary<string, IReadOnlyList<DapSourceBreakpoint>> breakpoints)
    {
        await Task.Yield();
        try
        {
            var results = await child.LaunchAsync(configuration, breakpoints, EnabledExceptionFilters(), CancellationToken.None);
            if (ReferenceEquals(child, _adapter))
            {
                _adapterLaunched = true;
                foreach (var (file, set) in results)
                {
                    Breakpoints.ApplyAdapterResults(file, set);
                }
            }
        }
        catch (Exception ex) when (ex is DebugAdapterException or TimeoutException or OperationCanceledException)
        {
            DebugAdapterOutput.WriteLine($"[{child.Adapter.Name}] The child session {configuration.Name} did not start: {ex.Message}", OutputKind.Failure);
            await child.DisposeAsync();
        }
    }

    private void WireAdapterEvents(DebugAdapterSession session)
    {
        var console = _adapterConsoles.GetOrCreateValue(_adapterRoot ?? session);
        session.RunInTerminalHandler = RunAdapterProgramAsync;
        WireFlutterEvents(session);
        session.Stopped += (_, e) => _ui.Post(() =>
        {
            // The session that stopped is the one the debugger shows, as when a child of js-debug hits a breakpoint.
            if (!session.HasEnded && (ReferenceEquals(session, _adapterRoot) || _adapterChildren.Contains(session)))
            {
                _adapter = session;
            }

            _pauseShown = ShowAdapterPauseAsync(session, e);
        });
        session.Exited += (_, e) => _ui.Post(() => _adapterExitCode ??= e.ExitCode);
        session.Continued += (_, _) => _ui.Post(() => ShowAdapterRunning(session));
        // Programs and adapters write ordinary text to stderr too: each stream is colored by what its lines say.
        var classifiers = new System.Collections.Concurrent.ConcurrentDictionary<string, StreamClassifier>(StringComparer.Ordinal);
        session.OutputReceived += (_, e) =>
        {
            // A program in the Output panel's console writes there itself; the adapter's copies go to its own log.
            if (Volatile.Read(ref console.Value) && e.Category is "stdout" or "stderr")
            {
                DebugAdapterOutput.Write($"[{session.Adapter.Name}] ({e.Category}) {e.Output}", OutputKind.Info);
                return;
            }

            if (e.Category is "console" or "important")
            {
                Output.Write(e.Output, OutputKind.Info);
            }
            else
            {
                OutputProcess.WriteClassified(Output, classifiers.GetOrAdd(e.Category ?? "stdout", _ => new StreamClassifier(AdapterToolchain(session))), e.Output);
            }
            if ((session.AcceptsProgramInput || _nativeDebugInput is not null) && e.Category == "stdout" && !e.Output.EndsWith('\n'))
            {
                Output.ShowWaitingForInput();
            }
        };
        session.BreakpointChanged += (_, e) => _ui.Post(() => OnAdapterBreakpointChanged(e));
        session.AdapterLog += (_, line) => DebugAdapterOutput.WriteLine($"[{session.Adapter.Name}] {line}", OutputKind.Info);
        session.Ended += (_, _) => _ui.Post(() => _ = EndAdapterSessionAfterProgramAsync(session));
    }

    /// <summary>The toolchain whose programs an adapter debugs, for the patterns of output only it prints.</summary>
    private static string? AdapterToolchain(DebugAdapterSession session)
    {
        var languages = session.Adapter.Languages;
        return languages.Contains("python", StringComparer.Ordinal) ? "python"
            : languages.Contains("go", StringComparer.Ordinal) ? "go"
            : languages.Contains("rust", StringComparer.Ordinal) ? "rust"
            : languages.Contains("java", StringComparer.Ordinal) ? "java"
            : languages.Contains("javascript", StringComparer.Ordinal) || languages.Contains("typescript", StringComparer.Ordinal) ? "node"
            : languages.Contains("c", StringComparer.Ordinal) || languages.Contains("cpp", StringComparer.Ordinal) ? "cpp"
            : null;
    }

    /// <summary>The breakpoints to set at launch, by file: those of files in the adapter's languages, and Run to Cursor's.</summary>
    private Dictionary<string, IReadOnlyList<DapSourceBreakpoint>> AdapterBreakpoints(DebugAdapterSession session)
    {
        var files = Breakpoints.All.Select(b => b.FilePath)
            .Concat(CursorBreakpointFile())
            .Distinct(StringComparer.Ordinal)
            .Where(f => IsAdapterFile(session, f));
        var result = new Dictionary<string, IReadOnlyList<DapSourceBreakpoint>>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            var list = AdapterBreakpointsIn(file);
            _sentBreakpoints[file] = list;
            result[file] = list;
        }

        return result;
    }

    private string[] CursorBreakpointFile() => _cursorBreakpoint is { } cursor ? new[] { cursor.FilePath } : Array.Empty<string>();

    private List<DapSourceBreakpoint> AdapterBreakpointsIn(string file)
    {
        var list = Breakpoints.ForAdapter(file);
        if (_cursorBreakpoint is { } cursor && string.Equals(cursor.FilePath, file, StringComparison.Ordinal) && list.All(b => b.Line != cursor.Line))
        {
            list.Add(new DapSourceBreakpoint(cursor.Line));
        }

        return list;
    }

    private bool IsAdapterFile(DebugAdapterSession session, string file)
    {
        if (FileTypes.For(file).IsRoslyn)
        {
            return IsWindowsBridgeSession(session);
        }

        return session.Adapter.Languages.Count == 0
            || (_languageServers?.Configuration.Registry.FindLanguage(file) is { } language && session.Adapter.Languages.Contains(language.Id, StringComparer.Ordinal));
    }

    private async Task UpdateAdapterBreakpointsAfterAsync(Task previous)
    {
        await previous;
        await SyncAdapterBreakpointsAsync();
    }

    /// <summary>Sends the breakpoints of files that changed to the adapter's session, as the user sets and clears them.</summary>
    private async Task SyncAdapterBreakpointsAsync()
    {
        if (_adapter is not { } session || !_adapterLaunched)
        {
            return;
        }

        var files = Breakpoints.All.Select(b => b.FilePath)
            .Concat(_sentBreakpoints.Keys)
            .Concat(CursorBreakpointFile())
            .Distinct(StringComparer.Ordinal)
            .Where(f => IsAdapterFile(session, f))
            .ToList();
        foreach (var file in files)
        {
            var list = AdapterBreakpointsIn(file);
            if (_sentBreakpoints.TryGetValue(file, out var sent) && sent.SequenceEqual(list))
            {
                continue;
            }

            _sentBreakpoints[file] = list;
            try
            {
                var results = await session.SetBreakpointsAsync(file, list, CancellationToken.None);
                if (ReferenceEquals(session, _adapter))
                {
                    Breakpoints.ApplyAdapterResults(file, results);
                }
            }
            catch (DebugAdapterException ex)
            {
                DebugAdapterOutput.WriteLine($"[{session.Adapter.Name}] Could not set the breakpoints of {Path.GetFileName(file)}: {ex.Message}", OutputKind.Failure);
            }
        }
    }

    private void OnAdapterBreakpointChanged(DapBreakpointEventArgs e)
    {
        // An adapter says when it verified a breakpoint once the code it is in loaded; the margin shows it.
        if (e.Breakpoint.IsVerified && _adapter is not null)
        {
            foreach (var breakpoint in Breakpoints.All.Where(b => b.IsVerifiedByAdapter == false && b.Line == e.Breakpoint.Line))
            {
                breakpoint.BindToAdapter(true, e.Breakpoint.Message);
            }

            ShowExecutionPoints();
        }
    }

    private async Task ShowAdapterPauseAsync(DebugAdapterSession session, DapStoppedEventArgs stop)
    {
        if (!ReferenceEquals(session, _adapter))
        {
            return;
        }

        var version = ++_adapterPauses;
        _embeddedRtosStoppedEvidence = (session, version, stop.AllThreadsStopped);
        IReadOnlyList<DapThread> threads;
        try
        {
            threads = await session.GetThreadsAsync(CancellationToken.None);
        }
        catch (DebugAdapterException)
        {
            return;
        }

        var thread = stop.ThreadId ?? (threads.Count > 0 ? threads[0].Id : 1);
        _adapterThread = thread;
        State = IdeState.Paused;
        _exceptionHelper = null;
        var options = threads.Select(t => new DebugThreadOption(t.Id, t.Name)).ToList();
        CallStack.ShowThreads(options, options.FirstOrDefault(t => t.Id == thread));
        if (!await ShowAdapterStackAsync(session, thread, version))
        {
            return;
        }

        if (stop.Reason == "exception" && session.Capabilities.ExceptionInfo)
        {
            await ShowAdapterExceptionAsync(session, thread, stop, version);
        }

        StatusText = DescribeAdapterPause(stop);

        // A breakpoint of Run to Cursor is only for that run.
        if (_cursorBreakpoint is not null)
        {
            _cursorBreakpoint = null;
            await SyncAdapterBreakpointsAsync();
        }

        if (!IsBottomPanelVisible || SelectedPanel is not (BottomPanel.Locals or BottomPanel.Watch or BottomPanel.CallStack))
        {
            SelectPanel(BottomPanel.Locals);
        }

        await ShowAdapterFrameAsync(CallStack.ActiveFrame);
        // Actual exception stops may capture only the bound embedded GDB session. The capture core checks
        // ICSR.VECTACTIVE, exact profile/ELF and this halt epoch before publishing any automatic diagnosis.
        await TryCaptureEmbeddedFaultAutomaticallyAsync(session, stop, version);
    }

    /// <summary>Reads a thread's call stack into the Call Stack panel.</summary>
    /// <returns><see langword="false"/> when a newer pause or the end of the session made it out of date.</returns>
    private async Task<bool> ShowAdapterStackAsync(DebugAdapterSession session, int thread, int version)
    {
        IReadOnlyList<DapStackFrame> frames;
        try
        {
            frames = await session.GetStackTraceAsync(thread, CancellationToken.None);
        }
        catch (DebugAdapterException)
        {
            return false;
        }

        if (version != _adapterPauses || !ReferenceEquals(session, _adapter))
        {
            return false;
        }

        _adapterFrames = frames;
        var shown = frames.Select(ToStackFrame).ToList();
        _adapterCurrent = shown.FirstOrDefault()?.Location;
        CallStack.Show(shown);
        return true;
    }

    private async Task ShowAdapterThreadAsync(int thread)
    {
        if (_adapter is not { } session || !IsPaused)
        {
            return;
        }

        // A different DAP thread can represent another target core. Invalidate paused snapshots and
        // pending register-write confirmations even when every core remains halted.
        var version = _adapterThread == thread ? _adapterPauses : ++_adapterPauses;
        _adapterThread = thread;
        if (await ShowAdapterStackAsync(session, thread, version))
        {
            await ShowAdapterFrameAsync(CallStack.ActiveFrame);
        }
    }

    private async Task ShowAdapterExceptionAsync(DebugAdapterSession session, int thread, DapStoppedEventArgs stop, int version)
    {
        DapExceptionInfo? info;
        try
        {
            info = await session.GetExceptionInfoAsync(thread, CancellationToken.None);
        }
        catch (DebugAdapterException)
        {
            info = null;
        }

        if (version != _adapterPauses)
        {
            return;
        }

        var type = info?.ExceptionId ?? stop.Description ?? "Exception";
        var message = info?.Message ?? info?.Description ?? stop.Text ?? string.Empty;
        var details = new StringBuilder().Append(info?.TypeName ?? type).Append(": ").AppendLine(message);
        if (info?.StackTrace is { Length: > 0 } trace)
        {
            details.AppendLine(trace.TrimEnd());
        }
        else
        {
            foreach (var frame in _adapterFrames)
            {
                details.Append("   at ").Append(frame.Name);
                if (frame.FilePath is { } path)
                {
                    details.Append(" in ").Append(Path.GetFileName(path)).Append(":line ").Append(frame.Line.ToString(CultureInfo.InvariantCulture));
                }

                details.AppendLine();
            }
        }

        _exceptionHelper = new ExceptionHelperViewModel(type, message, details.ToString().TrimEnd(), StartDebuggingCommand, StopDebuggingCommand);
    }

    /// <summary>Shows a frame: its statement in the editor, and its variables in Locals and Watch.</summary>
    private async Task ShowAdapterFrameAsync(StackFrameViewModel? frame)
    {
        ShowExecutionPoints();
        if (_adapter is not { } session || frame is null || frame.Index >= _adapterFrames.Count)
        {
            return;
        }

        var version = _adapterPauses;
        var dapFrame = _adapterFrames[frame.Index];
        if (frame.Location is { } location && await OpenDocumentAsync(location.FilePath) is { } document)
        {
            ApplyExecutionPoints(document);
            document.NavigateTo(location.Line, location.Column);
        }

        IReadOnlyList<DapScope> scopes;
        try
        {
            scopes = await session.GetScopesAsync(dapFrame.Id, CancellationToken.None);
        }
        catch (DebugAdapterException)
        {
            scopes = [];
        }

        if (version != _adapterPauses || !ReferenceEquals(session, _adapter))
        {
            return;
        }

        var locals = scopes.FirstOrDefault(s => s.IsLocals) ?? (scopes.Count > 0 ? scopes[0] : null);
        var sources = new List<VariableSource>();
        if (locals is not null)
        {
            try
            {
                foreach (var variable in await session.GetVariablesAsync(locals.VariablesReference, CancellationToken.None))
                {
                    sources.Add(new VariableSource(variable.Name, variable.EvaluateName ?? variable.Name, null)
                    {
                        Variable = AdapterVariable.ForVariable(session, variable, locals.VariablesReference, dapFrame.Id),
                    });
                }
            }
            catch (DebugAdapterException ex)
            {
                sources.Add(new VariableSource(locals.Name, locals.Name, null, ex.Message));
            }
        }

        // Other scopes, such as the globals, are rows to expand, read only when asked.
        foreach (var scope in scopes.Where(s => !ReferenceEquals(s, locals) && s.VariablesReference > 0))
        {
            sources.Add(new VariableSource(scope.Name, scope.Name, null) { Variable = AdapterVariable.ForScope(session, scope, dapFrame.Id) });
        }

        if (version != _adapterPauses)
        {
            return;
        }

        var note = frame.IsTop ? null : $"These are the locals of {frame.MethodName}, which called the paused function.";
        await Task.WhenAll(
            Locals.ShowVariablesAsync(sources, note),
            Watch.EvaluateWithAsync(expression => EvaluateForWatchAsync(session, expression, dapFrame.Id)));
    }

    private static async Task<VariableSource> EvaluateForWatchAsync(DebugAdapterSession session, string expression, int frameId)
    {
        try
        {
            var value = await session.EvaluateAsync(expression, frameId, DapEvaluateContext.Watch, CancellationToken.None);
            return new VariableSource(expression, expression, null)
            {
                Variable = new AdapterVariable(session, expression, value.Result, value.Type, expression, value.VariablesReference, 0, frameId),
            };
        }
        catch (DebugAdapterException ex)
        {
            return new VariableSource(expression, expression, null, ex.Message);
        }
    }

    /// <summary>A DataTip's value, evaluated by the adapter in the frame the editor shows.</summary>
    private async Task<VariableTreeViewModel?> EvaluateAdapterDataTipAsync(string expression)
    {
        if (_adapter is not { } session || !IsPaused || string.IsNullOrWhiteSpace(expression)
            || CallStack.ActiveFrame is not { } frame || frame.Index >= _adapterFrames.Count)
        {
            return null;
        }

        var source = await EvaluateHoverAsync(session, expression, _adapterFrames[frame.Index].Id);
        if (source is null || !IsPaused)
        {
            return null;
        }

        var tree = new VariableTreeViewModel();
        await tree.ShowAsync([source]);
        return tree;
    }

    private static async Task<VariableSource?> EvaluateHoverAsync(DebugAdapterSession session, string expression, int frameId)
    {
        try
        {
            var value = await session.EvaluateAsync(expression, frameId, DapEvaluateContext.Hover, CancellationToken.None);
            return new VariableSource(expression, expression, null)
            {
                Variable = new AdapterVariable(session, expression, value.Result, value.Type, expression, value.VariablesReference, 0, frameId),
            };
        }
        catch (DebugAdapterException)
        {
            // A DataTip is best effort: an expression the adapter cannot evaluate shows nothing.
            return null;
        }
    }

    private async Task EditVariableAsync(VariableViewModel row)
    {
        if (row.Variable is not { CanSetValue: true } variable || _adapter is null || !IsPaused)
        {
            return;
        }

        var value = await Prompt.AskAsync("Edit Value", $"The new value of {row.Name}, as an expression.", "Set", row.Value);
        if (value is null)
        {
            return;
        }

        try
        {
            await variable.SetValueAsync(value, CancellationToken.None);
        }
        catch (DebugAdapterException ex)
        {
            StatusText = $"{row.Name} could not be set: {ex.Message}";
            return;
        }

        // Other values may depend on the one that changed: read the frame again.
        await ShowAdapterFrameAsync(CallStack.ActiveFrame);
    }

    private async Task ContinueAdapterAsync() => await ResumeAdapterAsync((session, thread) => session.ContinueAsync(thread, CancellationToken.None));

    private Task StepAdapterAsync(AdapterStep step) => ResumeAdapterAsync((session, thread) => step switch
    {
        AdapterStep.Into => session.StepIntoAsync(thread, CancellationToken.None),
        AdapterStep.Out => session.StepOutAsync(thread, CancellationToken.None),
        _ => session.StepOverAsync(thread, CancellationToken.None),
    });

    private async Task ResumeAdapterAsync(Func<DebugAdapterSession, int, Task> command)
    {
        if (_adapter is not { } session || !IsPaused || _adapterThread is not { } thread)
        {
            return;
        }

        await _adapterBreakpointUpdate;
        if (!ReferenceEquals(session, _adapter) || !IsPaused)
        {
            return;
        }

        ShowAdapterRunning(session);
        try
        {
            await command(session, thread);
        }
        catch (DebugAdapterException ex) when (!ex.IsConnectionClosed)
        {
            StatusText = ex.Message;
        }
        catch (DebugAdapterException)
        {
            // The session ended; Ended shows how.
        }
    }

    private async Task PauseAdapterAsync()
    {
        if (_adapter is not { } session || IsPaused)
        {
            return;
        }

        try
        {
            IReadOnlyList<DapThread> threads = _adapterThread is null ? await session.GetThreadsAsync(CancellationToken.None) : [];
            var thread = _adapterThread ?? (threads.Count > 0 ? threads[0].Id : 1);
            await session.PauseAsync(thread, CancellationToken.None);
            StatusText = "Pausing";
        }
        catch (DebugAdapterException ex)
        {
            StatusText = ex.Message;
        }
    }

    private async Task RunAdapterToCursorAsync(DocumentViewModel document)
    {
        if (!UsesAdapterBreakpoints(document.FilePath))
        {
            StatusText = $"{_adapter?.Adapter.Name} does not run {document.FileName}.";
            return;
        }

        _cursorBreakpoint = (document.FilePath, document.CaretLine);
        await SyncAdapterBreakpointsAsync();
        await ContinueAdapterAsync();
    }

    private async Task StopAdapterAsync()
    {
        if ((_adapterRoot ?? _adapter) is not { } root)
        {
            return;
        }

        StatusText = "Stopping";
        foreach (var session in _adapterChildren.Where(c => !c.HasEnded).Reverse().Append(root).ToList())
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await session.TerminateAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                // Disposing ends what did not end in time.
            }

            await session.DisposeAsync();
        }
    }

    private void RestartAdapter()
    {
        if (_adapter?.Adapter.Id is "embedded-gdb" or "embedded-probe-rs")
        {
            _ = RestartEmbeddedDebugAsync();
            return;
        }
        if (_adapter?.Adapter.Id == "flutter")
        {
            _ = ReloadFlutterAsync(fullRestart: true);
            return;
        }
        if (_adapter is not null)
        {
            _adapterRestart = true;
            _ = StopAdapterAsync();
        }
    }

    private void ShowAdapterRunning(DebugAdapterSession session)
    {
        if (!ReferenceEquals(session, _adapter) || State != IdeState.Paused)
        {
            return;
        }

        _adapterPauses++;
        _adapterCurrent = null;
        _exceptionHelper = null;
        State = IdeState.Debugging;
        StatusText = $"Debugging {_adapterProgram}";
        CallStack.Clear();
        CallStack.ClearThreads();
        Locals.Tree.IsStale = true;
        _ = Watch.EvaluateWithAsync(null);
        Watch.MarkStale();
        ShowExecutionPoints();
    }

    private void EndAdapterSession(DebugAdapterSession session)
    {
        if (_adapterChildren.Remove(session))
        {
            EndChildSession(session);
            return;
        }

        if (!ReferenceEquals(session, _adapterRoot ?? _adapter))
        {
            return;
        }

        foreach (var child in _adapterChildren)
        {
            _ = child.DisposeAsync().AsTask();
        }

        _adapterChildren.Clear();
        StopNativeDebugInput();
        if (session.AcceptsProgramInput)
        {
            Output.EndInput();
        }
        _adapterRoot = null;
        _adapter = null;
        if (session.Adapter.Id is "embedded-gdb" or "embedded-probe-rs") DisposeEmbeddedDebug();
        _adapterPauses++;
        _adapterCurrent = null;
        _adapterFrames = [];
        _adapterThread = null;
        _exceptionHelper = null;
        _cursorBreakpoint = null;
        _sentBreakpoints.Clear();
        ExceptionFilters.Clear();
        Breakpoints.UnbindAdapter();
        CallStack.Clear();
        CallStack.ClearThreads();
        Locals.Clear();
        _ = Watch.EvaluateWithAsync(null);
        _ = Watch.EvaluateAsync(null);
        ShowExecutionPoints();
        _ = session.DisposeAsync().AsTask();

        var code = _adapterExitCode ?? session.ExitCode;
        Output.WriteLine();
        Output.WriteLine(
            code is { } exit
                ? $"The program '{_adapterProgram}' has exited with code {exit.ToString(CultureInfo.InvariantCulture)} (0x{exit:x})."
                : $"The program '{_adapterProgram}' has stopped.",
            code is null or 0 ? OutputKind.Info : OutputKind.Failure);
        State = IdeState.Ready;
        StatusText = code is null or 0 ? "Ready" : "The program stopped with an error";
        OnPropertyChanged(nameof(IsDebuggingWithAdapter));
        if (SelectedPanel is BottomPanel.Locals or BottomPanel.Watch or BottomPanel.CallStack)
        {
            SelectedPanel = BottomPanel.Output;
        }

        if (_adapterRestart && _adapterTarget is { } target)
        {
            _adapterRestart = false;
            _ = LaunchAdapterAsync(target, DebugStart.Run, noDebug: false);
        }
    }

    /// <summary>A child session ended: the debugger goes back to the session that is left, or ends when none is.</summary>
    private void EndChildSession(DebugAdapterSession child)
    {
        _ = child.DisposeAsync().AsTask();
        if (!ReferenceEquals(child, _adapter))
        {
            return;
        }

        ShowAdapterRunning(child);
        _adapter = _adapterChildren.LastOrDefault(c => !c.HasEnded) ?? _adapterRoot;
        if (_adapterRoot is { HasEnded: true } root)
        {
            _ = EndAdapterSessionAfterProgramAsync(root);
        }
    }

    private void DisposeAdapterSession()
    {
        foreach (var child in _adapterChildren)
        {
            _ = child.DisposeAsync().AsTask();
        }

        _adapterChildren.Clear();
        var root = _adapterRoot ?? _adapter;
        _adapterRoot = null;
        _adapter = null;
        DisposeEmbeddedDebug();
        if (root is not null)
        {
            _ = root.DisposeAsync().AsTask();
        }
    }

    private void ShowExceptionFilters(DebugAdapterSession session)
    {
        ExceptionFilters.Clear();
        foreach (var filter in session.Capabilities.ExceptionFilters)
        {
            // Break on Unhandled Exceptions decides the adapter's filters for uncaught exceptions; the others start as the adapter says.
            var enabled = IsUnhandledFilter(filter.Filter) ? BreakOnUnhandledExceptions : filter.IsDefault;
            var option = new ExceptionFilterOption(filter, enabled);
            option.PropertyChanged += OnExceptionFilterChanged;
            ExceptionFilters.Add(option);
        }
    }

    private void OnExceptionFilterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ExceptionFilterOption.IsEnabled))
        {
            _ = SendExceptionFiltersAsync();
        }
    }

    /// <summary>Break on Unhandled Exceptions, for an adapter: turns its filters for uncaught exceptions on or off.</summary>
    private void ApplyBreakOnUnhandledToAdapter(bool value)
    {
        foreach (var option in ExceptionFilters.Where(o => IsUnhandledFilter(o.Filter)))
        {
            option.IsEnabled = value;
        }
    }

    private async Task SendExceptionFiltersAsync()
    {
        if (_adapter is { } session && _adapterLaunched)
        {
            try
            {
                await session.SetExceptionBreakpointsAsync(EnabledExceptionFilters(), CancellationToken.None);
            }
            catch (DebugAdapterException ex)
            {
                StatusText = ex.Message;
            }
        }
    }

    private List<string> EnabledExceptionFilters() => ExceptionFilters.Where(o => o.IsEnabled).Select(o => o.Filter).ToList();

    private async Task EditAdapterBreakpointAsync(string filePath, int line, BreakpointOption option)
    {
        var breakpoint = Breakpoints.Find(filePath, line) ?? Breakpoints.Add(filePath, line);
        var (title, message, current) = option switch
        {
            BreakpointOption.Condition => (
                "Breakpoint Condition",
                $"Line {breakpoint.Line} of {breakpoint.FileName}. Stop only when this expression, in the program's language, is true, such as count == 3.",
                breakpoint.Condition),
            BreakpointOption.HitCount => (
                "Breakpoint Hit Count",
                $"Line {breakpoint.Line} of {breakpoint.FileName}. Stop when the line was reached this many times, such as 5, or as the debugger writes it, such as >= 10.",
                breakpoint.HitCount),
            _ => (
                "Logpoint",
                $"Line {breakpoint.Line} of {breakpoint.FileName}. Log this message in the Output panel instead of stopping; expressions in braces are evaluated, such as Total is {{total}}.",
                breakpoint.LogMessage),
        };
        var answer = await Prompt.AskAsync(title, message, "Apply", current ?? string.Empty);
        if (answer is null)
        {
            return;
        }

        switch (option)
        {
            case BreakpointOption.Condition:
                Breakpoints.SetCondition(breakpoint, answer);
                break;
            case BreakpointOption.HitCount:
                Breakpoints.SetOptions(breakpoint, answer, breakpoint.LogMessage);
                break;
            default:
                Breakpoints.SetOptions(breakpoint, breakpoint.HitCount, answer);
                break;
        }
    }

    private void OnPropertyChangedForDebugAdapters(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ActiveDocument) or nameof(Workspace):
                OnLaunchTargetChanged();
                break;
            case nameof(StartupProject) when StartupProject is not null && SelectedLaunchTarget is not null:
                SelectedLaunchTarget = null;
                OnLaunchTargetChanged();
                break;
        }
    }

    private void OnLaunchTargetChanged()
    {
        OnPropertyChanged(nameof(RunLabel));
        OnPropertyChanged(nameof(RunHostText));
        OnPropertyChanged(nameof(CurrentLaunchTarget));
        RunCommand.NotifyCanExecuteChanged();
        NotifyDebugCommands();
        UpdateRunTargets();
    }

    private void UpdateRunTargets()
    {
        var targets = new List<RunTargetOption>();
        foreach (var project in StartupProjects)
        {
            var kind = FindToolchainProject(project.Project) is { } toolchainProject ? toolchainProject.Toolchain.Name : "C#";
            targets.Add(new RunTargetOption(project.Name, $"{kind} project", WithoutSwarm(project.Select)));
        }

        AddSwarmRunTarget(targets);
        AddWindowsBridgeRunTargets(targets);

        if (_languageServers is { } host)
        {
            var launch = host.Configuration.LaunchConfigurations.Select(ToTarget).ToList();
            if (CurrentFileTarget(host) is { } currentFile && launch.All(t => t.Name != currentFile.Name))
            {
                launch.Add(currentFile);
            }

            foreach (var target in launch)
            {
                targets.Add(new RunTargetOption(target.Name, FindAdapter(target)?.Name ?? target.Configuration.Type, new RelayCommand(() => SelectLaunchTarget(target))));
            }
        }

        if (!targets.Select(t => t.Name + t.Detail).SequenceEqual(RunTargets.Select(t => t.Name + t.Detail)))
        {
            RunTargets.Clear();
            foreach (var target in targets)
            {
                RunTargets.Add(target);
            }
        }
    }

    /// <summary>"Python: Current File", for the active document when its language has a debug adapter.</summary>
    private LaunchTarget? CurrentFileTarget(LanguageServerHost host)
    {
        var registry = host.Configuration.Registry;
        if (ActiveDocument is not { HasLanguageService: false } document
            || registry.FindLanguage(document.FilePath) is not { } language
            || registry.FindAdaptersForLanguage(language.Id).FirstOrDefault() is not { } adapter)
        {
            return null;
        }

        var configuration = LaunchConfiguration.ForCurrentFile(adapter, language.Name);
        if (language.Id == "dart" && registry.FindAdapter("dart") is { } dart)
        {
            configuration = LaunchConfiguration.ForCurrentFile(dart, language.Name);
        }
        return new LaunchTarget(configuration.Name, configuration, IsCurrentFile: true);
    }

    private static LaunchTarget ToTarget(LaunchConfiguration configuration) => new(configuration.Name, configuration, IsCurrentFile: false);

    private DebugAdapterDefinition? FindAdapter(LaunchTarget target) =>
        _languageServers?.Configuration.Registry.FindAdapter(target.Configuration.Type);

    /// <summary>A frame of the adapter as the Call Stack shows it, with its statement as a span of the file.</summary>
    private readonly HashSet<string> _unresolvedFrameSources = new(StringComparer.Ordinal);

    private DebugStackFrame ToStackFrame(DapStackFrame frame)
    {
        // A file only the adapter's environment has, such as Python's library on Android, is read where the device keeps it.
        var path = frame.FilePath is { } uri && uri.StartsWith("file:", StringComparison.Ordinal) && Uri.TryCreate(uri, UriKind.Absolute, out var address)
            ? address.LocalPath
            : frame.FilePath;
        path = path is { } reported && !File.Exists(reported) ? _languageServerServices.Launcher.ToLocalPath(reported) ?? reported : path;
        path = path is null ? KotlinFileOfFrame(frame.Name) : KotlinSourceOf(path);
        // Java's debugger may name a Kotlin frame's source only by the file the class records (Main.kt), or by a path
        // that is not a file here: the workspace's file of that name, under the package's folders.
        if ((path is null || !File.Exists(path)) && frame.SourceName is { } sourceName
            && (sourceName.EndsWith(".kt", StringComparison.Ordinal) || sourceName.EndsWith(".kts", StringComparison.Ordinal)))
        {
            path = path is { } named && !named.Contains("://", StringComparison.Ordinal) && named.EndsWith(sourceName, StringComparison.Ordinal) ? named : sourceName;
        }

        if (path is { } relative && !Path.IsPathRooted(relative) && (relative.EndsWith(".kt", StringComparison.Ordinal) || relative.EndsWith(".kts", StringComparison.Ordinal))
            && Workspace?.RootDirectory is { } root)
        {
            // Java's debugger names a Kotlin file by its package path when it finds it in no source folder of JDT's.
            var suffix = Path.DirectorySeparatorChar + relative.Replace('/', Path.DirectorySeparatorChar);
            path = Directory.EnumerateFiles(root, Path.GetFileName(relative), new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, MaxRecursionDepth = 10 })
                .FirstOrDefault(f => f.EndsWith(suffix, StringComparison.Ordinal) && !f.Contains($"{Path.DirectorySeparatorChar}build{Path.DirectorySeparatorChar}", StringComparison.Ordinal)) ?? relative;
        }
        if (path is null || ReadText(path) is not { } text)
        {
            // Once per source, so a frame shown as external code says why in the Debug Adapters output.
            if (_unresolvedFrameSources.Add(frame.FilePath ?? frame.SourceName ?? frame.Name))
            {
                DebugAdapterOutput.WriteLine($"{frame.Name}: its source ({frame.FilePath ?? "no path"}, {frame.SourceName ?? "no name"}) is not a file in this workspace.", OutputKind.Info);
            }

            return new DebugStackFrame(frame.Name, null);
        }

        var lines = text.Split('\n');
        var lineIndex = Math.Clamp(frame.Line, 1, Math.Max(1, lines.Length)) - 1;
        var lineText = lines.Length == 0 ? string.Empty : lines[lineIndex].TrimEnd('\r');
        var lineStart = 0;
        for (var i = 0; i < lineIndex; i++)
        {
            lineStart += lines[i].Length + 1;
        }

        // The statement: from the column the adapter gives (or the first non-blank character) to the end of the line.
        var indent = lineText.Length - lineText.TrimStart().Length;
        var column = Math.Clamp(frame.Column <= 1 ? indent + 1 : frame.Column, 1, lineText.Length + 1);
        var end = frame.EndLine == frame.Line && frame.EndColumn is { } endColumn && endColumn > column
            ? Math.Min(endColumn, lineText.TrimEnd().Length + 1)
            : lineText.TrimEnd().Length + 1;
        var start = lineStart + column - 1;
        var length = Math.Max(0, end - column);
        return new DebugStackFrame(frame.Name, new DebugSourceSpan(path, frame.Line, column, frame.Line, column + length, start, length));
    }

    private string DescribeAdapterPause(DapStoppedEventArgs stop)
    {
        var where = _adapterCurrent is { } location ? $"{Path.GetFileName(location.FilePath)}, line {location.Line.ToString(CultureInfo.CurrentCulture)}" : "external code";
        return stop.Reason switch
        {
            "breakpoint" or "function breakpoint" or "data breakpoint" => $"Paused at a breakpoint · {where}",
            "exception" => $"Exception: {_exceptionHelper?.ExceptionType ?? stop.Description ?? stop.Text ?? "the program stopped at an exception"}",
            "pause" => $"Paused (Break All) · {where}",
            "entry" => $"Paused at the first statement · {where}",
            "goto" or "step" => $"Paused · {where}",
            _ => $"Paused ({stop.Reason}) · {where}",
        };
    }

    private static bool IsUnhandledFilter(string filter) =>
        filter.Contains("uncaught", StringComparison.OrdinalIgnoreCase) || filter.Contains("unhandled", StringComparison.OrdinalIgnoreCase);

    /// <summary>How the program steps.</summary>
    private enum AdapterStep
    {
        /// <summary>Step Over (F10).</summary>
        Over,

        /// <summary>Step Into (F11).</summary>
        Into,

        /// <summary>Step Out (Shift+F11).</summary>
        Out,
    }

    /// <summary>What a breakpoint dialog edits.</summary>
    private enum BreakpointOption
    {
        /// <summary>When it stops.</summary>
        Condition,

        /// <summary>After how many hits it stops.</summary>
        HitCount,

        /// <summary>The message it logs instead of stopping.</summary>
        LogMessage,
    }
}
