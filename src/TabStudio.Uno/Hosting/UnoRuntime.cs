using System.Reflection;
using System.Runtime.Loader;
using TabStudio.Compilation;
using TabStudio.Compilation.Running;
using TabStudio.Core.Workspaces;

namespace TabStudio.Uno.Hosting;

/// <summary>
/// Uno Platform running inside TabStudio: its assemblies loaded once for the process, TabStudio's host compiled
/// against them, and Uno's Skia renderer drawing a window whose frames the app preview shows. Each build of an app then
/// runs in it as an Uno secondary app, in a load context of its own (see <see cref="LaunchAsync"/>).
/// </summary>
/// <remarks>
/// <para>
/// Uno's secondary app support keeps the framework in the default load context, where Uno's statics, resources and
/// dispatcher live for the whole process, and loads each app in another context whose windows show their content in
/// the host's window. So restarting an app after a save takes the time to load the app, not Uno. It also means one
/// Uno version per process: an app that needs another version asks for TabStudio to be restarted.
/// </para>
/// <para>
/// Uno draws with SkiaSharp 3. On Android TabStudio's own Avalonia draws with the same copy, which the app preview
/// therefore shares.
/// </para>
/// </remarks>
public sealed class UnoRuntime
{
    /// <summary>The assemblies started before the host compiles: Uno Platform's core, which the host is compiled against.</summary>
    private static readonly HashSet<string> CoreAssemblies = new(StringComparer.OrdinalIgnoreCase)
    {
        "Uno", "Uno.UI", "Uno.Foundation", "Uno.Foundation.Logging", "Uno.UI.Dispatching", "Uno.UI.Composition", "Uno.Xaml",
        "Uno.UI.FluentTheme", "Uno.UI.FluentTheme.v1", "Uno.UI.FluentTheme.v2", "Uno.UI.Toolkit", "Uno.Diagnostics.Eventing",
    };

    private static readonly SemaphoreSlim StartGate = new(1, 1);
    private static UnoRuntime? _current;

    private readonly Lock _gate = new();
    private readonly Dictionary<string, string> _paths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Assembly> _loaded = new(StringComparer.OrdinalIgnoreCase);
    private UnoHostBridge? _host;
    private UnoApp? _app;

    private UnoRuntime(string unoVersion, string contentDirectory)
    {
        UnoVersion = unoVersion;
        ContentDirectory = contentDirectory;
    }

    /// <summary>Raised on the renderer's thread when a new frame can be copied with <see cref="CopyFrame"/>.</summary>
    public event EventHandler? FrameReady;

    /// <summary>Raised, on any thread, for Uno's warnings and errors.</summary>
    public event EventHandler<UnoLogEventArgs>? Logged;

    /// <summary>The runtime of this process, once started.</summary>
    public static UnoRuntime? Current => _current;

    /// <summary>The Uno Platform version running.</summary>
    public string UnoVersion { get; }

    /// <summary>The folder <c>ms-appx:///</c> addresses resolve in.</summary>
    public string ContentDirectory { get; }

    /// <summary>The app running, if any.</summary>
    public UnoApp? App
    {
        get
        {
            lock (_gate)
            {
                return _app;
            }
        }
    }

    /// <summary>The size of the last frame, in pixels.</summary>
    public (int Width, int Height) FrameSize => _host is { } host ? (host.FrameWidth(), host.FrameHeight()) : (0, 0);

    /// <summary>
    /// Whether an assembly comes from the default load context, where Uno lives, rather than from an app's own context:
    /// Uno Platform's assemblies, SkiaSharp and HarfBuzz, which Uno draws text with, and the Microsoft.Extensions
    /// libraries Uno's logging and hosting share with apps.
    /// </summary>
    /// <param name="assemblyName">The assembly's simple name.</param>
    public static bool IsShared(string assemblyName)
    {
        ArgumentNullException.ThrowIfNull(assemblyName);
        return assemblyName == "Uno"
            || assemblyName.StartsWith("Uno.", StringComparison.Ordinal)
            || assemblyName.StartsWith("SkiaSharp", StringComparison.Ordinal)
            || assemblyName.StartsWith("HarfBuzzSharp", StringComparison.Ordinal)
            || assemblyName.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal)
            || assemblyName == UnoHostCompiler.AssemblyName;
    }

    /// <summary>
    /// Starts Uno Platform in this process, or returns the runtime already started when it runs the same version.
    /// </summary>
    /// <param name="assets">What the app runs with, which names the Uno version.</param>
    /// <param name="options">Where the runtime keeps its files, and the preview's size and theme.</param>
    /// <param name="cancellationToken">Cancels waiting for another start.</param>
    /// <exception cref="UnoHostException">Uno cannot run here, with the reason: another Uno version already runs, or the host does not support this one.</exception>
    public static async Task<UnoRuntime> StartAsync(UnoRuntimeAssets assets, UnoRuntimeOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(options);
        if (assets.UnoUIPath is null)
        {
            throw new UnoHostException("The app does not reference Uno Platform: its packages have no Uno.UI.");
        }

        await StartGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var version = assets.UnoVersion ?? "unknown";
            if (_current is { } running)
            {
                if (!string.Equals(running.UnoVersion, version, StringComparison.OrdinalIgnoreCase))
                {
                    throw new UnoHostException(
                        $"The app preview already runs Uno Platform {running.UnoVersion}, and this app uses {version}. Uno Platform runs once per TabStudio session: close and reopen TabStudio to preview this app.");
                }

                running.Add(assets);
                return running;
            }

            var runtime = new UnoRuntime(version, Path.Combine(options.CacheDirectory, "preview"));
            runtime.Add(assets);
            await Task.Run(() => runtime.Load(cancellationToken), cancellationToken).ConfigureAwait(false);
            await runtime._host!.Start(
                runtime.ContentDirectory,
                options.DarkTheme,
                options.Width,
                options.Height,
                (float)options.Scale,
                runtime.OnFrameReady,
                runtime.OnLogged,
                runtime.OnUnhandled).WaitAsync(cancellationToken).ConfigureAwait(false);
            _current = runtime;
            return runtime;
        }
        finally
        {
            StartGate.Release();
        }
    }

    /// <summary>
    /// Starts a build of an app, closing the one running: its files go to the content folder, its assemblies load in a
    /// context of their own, and its <c>Application</c> class launches as an Uno secondary app, whose main window shows
    /// in the preview.
    /// </summary>
    /// <param name="project">The app's project, whose content files the app opens through <c>ms-appx:///</c>.</param>
    /// <param name="entryAssembly">The app's compiled assembly.</param>
    /// <param name="dependencies">The assemblies of the projects it references.</param>
    /// <param name="assets">What its packages bring.</param>
    /// <param name="cancellationToken">Cancels the start before the app launches.</param>
    /// <returns>The running app.</returns>
    public async Task<UnoApp> LaunchAsync(
        ProjectInfo project,
        CompiledAssembly entryAssembly,
        IReadOnlyList<CompiledAssembly> dependencies,
        UnoRuntimeAssets assets,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(entryAssembly);
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(assets);
        var host = _host ?? throw new InvalidOperationException("Uno Platform is not running.");

        if (App is { } previous)
        {
            await previous.DisposeAsync().ConfigureAwait(false);
        }

        Add(assets);
        await Task.Run(
            () => UnoAppContent.Prepare(ContentDirectory, project, assets, [UnoHostCompiler.AssemblyName + ".dll", UnoHostCompiler.AssemblyName + ".dll.key"]),
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        // Apps stay loaded, as for Avalonia apps: Uno keeps references to the types an app registers until it closes.
        var program = LoadedProgram.Load(entryAssembly, dependencies, assets.Assemblies, IsShared, unloadable: false);
        var app = new UnoApp(this, program);
        lock (_gate)
        {
            _app = app;
        }

        try
        {
            await host.Launch(program.EntryAssembly).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await app.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        return app;
    }

    /// <summary>Sizes the preview's window, in device-independent pixels at a pixel density.</summary>
    public void Resize(double width, double height, double scale) => _host?.Resize(width, height, (float)scale);

    /// <summary>Follows TabStudio's theme, which apps see as the system theme.</summary>
    public void SetTheme(bool dark) => _host?.SetTheme(dark);

    /// <summary>Copies the last frame into a buffer of 32-bit premultiplied BGRA pixels. Any thread.</summary>
    /// <returns>Whether there was a frame to copy.</returns>
    public bool CopyFrame(IntPtr pixels, int width, int height, int rowBytes) => _host?.CopyFrame(pixels, width, height, rowBytes) == true;

    /// <summary>Sends a pointer event to the app. Any thread.</summary>
    public void SendPointer(UnoPointerEvent input)
    {
        ArgumentNullException.ThrowIfNull(input);
        _host?.Pointer(
            (int)input.Action,
            input.PointerId,
            (int)input.Device,
            input.X,
            input.Y,
            input.IsLeftPressed,
            input.IsRightPressed,
            input.IsMiddlePressed,
            input.WheelDelta,
            input.IsHorizontalWheel,
            (int)input.Modifiers,
            input.Timestamp);
    }

    /// <summary>Sends a key press or release to the app. Any thread.</summary>
    /// <param name="down">Whether the key went down.</param>
    /// <param name="key">The key, as WinUI's <c>VirtualKey</c>.</param>
    /// <param name="modifiers">The modifiers held.</param>
    /// <param name="character">The character the key types, if any.</param>
    public void SendKey(bool down, int key, UnoKeyModifiers modifiers, char? character) =>
        _host?.Key(down, key, (int)modifiers, 0, character?.ToString());

    /// <summary>Types text in the app, as the on-screen keyboard or an input method commits it. Any thread.</summary>
    public void SendText(string text, UnoKeyModifiers modifiers)
    {
        ArgumentNullException.ThrowIfNull(text);
        _host?.Text(text, (int)modifiers);
    }

    /// <summary>Closes the app, as <see cref="UnoApp.DisposeAsync"/> does.</summary>
    internal async Task CloseAsync(UnoApp app)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(_app, app))
            {
                return;
            }

            _app = null;
        }

        if (_host is { } host)
        {
            await host.Close().ConfigureAwait(false);
        }
    }

    /// <summary>The host's queries, for the running app.</summary>
    internal UnoHostBridge Host => _host ?? throw new InvalidOperationException("Uno Platform is not running.");

    /// <summary>Remembers where the shared assemblies of an app are, for the default context to load them when first needed.</summary>
    private void Add(UnoRuntimeAssets assets)
    {
        lock (_gate)
        {
            foreach (var path in assets.Assemblies)
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (IsShared(name))
                {
                    _paths.TryAdd(name, path);
                }
            }
        }
    }

    /// <summary>Loads Uno's core into the default context, then compiles and loads the host. Off the UI thread.</summary>
    private void Load(CancellationToken cancellationToken)
    {
        RequireSkiaSharp3();
        AssemblyLoadContext.Default.Resolving += OnResolving;

        List<string> core;
        lock (_gate)
        {
            core = _paths.Where(p => CoreAssemblies.Contains(p.Key)).Select(p => p.Value).ToList();
        }

        foreach (var path in core)
        {
            LoadShared(Path.GetFileNameWithoutExtension(path));
        }

        var hostPath = UnoHostCompiler.Compile(core, ContentDirectory, cancellationToken);
        var hostAssembly = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(a => a.GetName().Name == UnoHostCompiler.AssemblyName)
            ?? AssemblyLoadContext.Default.LoadFromAssemblyPath(hostPath);
        _host = UnoHostBridge.For(hostAssembly);
    }

    /// <summary>Uno Platform 6 draws with SkiaSharp 3, which must be the one TabStudio itself runs on.</summary>
    private static void RequireSkiaSharp3()
    {
        Version? version;
        try
        {
            version = AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName("SkiaSharp")).GetName().Version;
        }
        catch (Exception ex) when (ex is FileNotFoundException or FileLoadException)
        {
            // The app's packages bring it, and it loads with them.
            return;
        }

        if (version is { Major: < 3 })
        {
            throw new UnoHostException($"Uno Platform draws with SkiaSharp 3, and this build of TabStudio draws with SkiaSharp {version}; the app preview cannot run Uno apps here.");
        }
    }

    private Assembly? LoadShared(string name)
    {
        lock (_gate)
        {
            if (_loaded.TryGetValue(name, out var loaded))
            {
                return loaded;
            }

            loaded = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(a => string.Equals(a.GetName().Name, name, StringComparison.OrdinalIgnoreCase));
            if (loaded is null && _paths.TryGetValue(name, out var path))
            {
                loaded = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
            }

            if (loaded is not null)
            {
                _loaded[name] = loaded;
            }

            return loaded;
        }
    }

    /// <summary>Loads the shared assemblies the default context does not have yet, such as an Uno library an app adds.</summary>
    private Assembly? OnResolving(AssemblyLoadContext context, AssemblyName name) =>
        ReferenceEquals(context, AssemblyLoadContext.Default) && name.Name is { } simple && IsShared(simple) ? LoadShared(simple) : null;

    private void OnFrameReady() => FrameReady?.Invoke(this, EventArgs.Empty);

    private void OnLogged(int level, string message, Exception? exception) =>
        Logged?.Invoke(this, new UnoLogEventArgs((UnoLogLevel)level, message, exception));

    private void OnUnhandled(Exception exception)
    {
        if (App is { } app)
        {
            app.OnUnhandled(exception);
        }
        else
        {
            Logged?.Invoke(this, new UnoLogEventArgs(UnoLogLevel.Error, "Uno Platform: an exception was not handled.", exception));
        }
    }
}
