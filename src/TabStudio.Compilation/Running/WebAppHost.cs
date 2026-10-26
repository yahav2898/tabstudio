using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace TabStudio.Compilation.Running;

/// <summary>
/// Runs an ASP.NET Core app inside TabStudio and serves it on a free local port, for the preview beside the
/// editor or a browser on the device.
/// </summary>
/// <remarks>
/// <para>
/// The app runs like any program (<see cref="ProgramRunner"/>), with the arguments the .NET host would pass in
/// development: its address, content root and environment. ASP.NET Core reads them through
/// <c>WebApplication.CreateBuilder(args)</c>.
/// </para>
/// <para>
/// To stop the app cleanly, the host it builds is captured the way <c>WebApplicationFactory</c> captures it:
/// the hosting library announces every host it builds on the <c>Microsoft.Extensions.Hosting</c> diagnostic
/// listener. Stopping calls the host's <c>StopAsync</c>, so the server closes its port and <c>Main</c> returns.
/// </para>
/// </remarks>
public sealed class WebAppHost : IAsyncDisposable
{
    private const string HostingListenerName = "Microsoft.Extensions.Hosting";

    private readonly CancellationTokenSource _run = new();
    private readonly HostCapture _capture = new();
    private Task<RunResult>? _completion;

    private WebAppHost(Uri address) => Address = address;

    /// <summary>Where the app listens, such as <c>http://127.0.0.1:52713/</c>.</summary>
    public Uri Address { get; }

    /// <summary>Whether the app is still running.</summary>
    public bool IsRunning => _completion is { IsCompleted: false };

    /// <summary>Finishes when the app exits, with how it ended.</summary>
    public Task<RunResult> Completion => _completion ?? Task.FromResult(new RunResult(RunOutcome.Stopped, -1, TimeSpan.Zero));

    /// <summary>Starts an app and waits until it accepts connections.</summary>
    /// <param name="entryAssembly">The web project's assembly.</param>
    /// <param name="dependencies">The assemblies of the projects it references.</param>
    /// <param name="frameworkAndPackageAssemblies">ASP.NET Core's runtime assemblies and the app's package assemblies.</param>
    /// <param name="contentRoot">The project folder, where <c>appsettings.json</c> and <c>wwwroot</c> are.</param>
    /// <param name="console">Receives the app's console output and logs.</param>
    /// <param name="options">The environment, extra arguments, port and startup timeout; the defaults when omitted.</param>
    /// <param name="cancellationToken">Gives up waiting, and stops the app.</param>
    /// <returns>
    /// The app, listening on <see cref="Address"/>. When it exits before it listens, such as when it crashes at
    /// startup, <see cref="IsRunning"/> is <see langword="false"/> and <see cref="Completion"/> says why.
    /// </returns>
    public static Task<WebAppHost> StartAsync(
        CompiledAssembly entryAssembly,
        IReadOnlyList<CompiledAssembly> dependencies,
        IReadOnlyList<string> frameworkAndPackageAssemblies,
        string contentRoot,
        IConsoleSink console,
        WebAppOptions? options = null,
        CancellationToken cancellationToken = default) =>
        StartAsync(entryAssembly, dependencies, frameworkAndPackageAssemblies, contentRoot, console, options, loaded: null, cancellationToken);

    /// <summary>Starts a web app with a callback identifying its exact loaded entry assembly for Hot Reload.</summary>
    /// <param name="entryAssembly">The web project's assembly.</param>
    /// <param name="dependencies">Its project assemblies.</param>
    /// <param name="frameworkAndPackageAssemblies">Its framework and package assemblies.</param>
    /// <param name="contentRoot">The project folder.</param>
    /// <param name="console">Receives output and logs.</param>
    /// <param name="options">The web host options.</param>
    /// <param name="loaded">Called before Main with the exact loaded entry assembly.</param>
    /// <param name="cancellationToken">Gives up waiting and stops the app.</param>
    public static async Task<WebAppHost> StartAsync(
        CompiledAssembly entryAssembly,
        IReadOnlyList<CompiledAssembly> dependencies,
        IReadOnlyList<string> frameworkAndPackageAssemblies,
        string contentRoot,
        IConsoleSink console,
        WebAppOptions? options,
        Action<Assembly>? loaded,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entryAssembly);
        ArgumentNullException.ThrowIfNull(console);
        options ??= new WebAppOptions();

        var port = options.Port is { } preferred && IsFree(preferred) ? preferred : FindFreePort();
        var host = new WebAppHost(new Uri($"http://127.0.0.1:{port}/"));
        List<string> arguments =
        [
            "--urls", host.Address.GetLeftPart(UriPartial.Authority),
            "--contentRoot", contentRoot,
            "--environment", options.Environment,
            "--applicationName", entryAssembly.Name,
        ];

        // Watching configuration files costs battery on a tablet; the app restarts when they change anyway.
        if (OperatingSystem.IsAndroid())
        {
            arguments.AddRange(["--hostBuilder:reloadConfigOnChange", "false"]);
        }

        arguments.AddRange(options.Arguments);

        try
        {
            host._capture.Start();
            var runner = new ProgramRunner();
            if (loaded is not null) runner.Loaded += loaded;
            host._completion = runner.RunAsync(
                entryAssembly, dependencies, frameworkAndPackageAssemblies, arguments, console, input: null, host._run.Token);
            await host.WaitUntilListeningAsync(port, options.StartupTimeout, cancellationToken).ConfigureAwait(false);
            return host;
        }
        catch
        {
            await host.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Stops the app: its host shuts down gracefully, and the run ends if it does not within a few seconds.</summary>
    public async Task<RunResult> StopAsync()
    {
        if (_capture.Host is { } captured)
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await InvokeStopAsync(captured, timeout.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // The app's own shutdown failed; ending the run below still frees the console.
            }
        }

        var completion = Completion;
        if (await Task.WhenAny(completion, Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(false) != completion)
        {
            await _run.CancelAsync().ConfigureAwait(false);
        }

        _capture.Dispose();
        try
        {
            return await completion.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Stopped while it waited for the console, before it started.
            return new RunResult(RunOutcome.Stopped, -1, TimeSpan.Zero);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (IsRunning)
        {
            await StopAsync().ConfigureAwait(false);
        }

        _capture.Dispose();
        _run.Dispose();
    }

    /// <summary>A port nothing listens on right now.</summary>
    public static int FindFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    /// <summary>Whether nothing listens on a port, so an app restarted on it keeps its address.</summary>
    private static bool IsFree(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private async Task WaitUntilListeningAsync(int port, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var waited = Stopwatch.StartNew();
        while (IsRunning && waited.Elapsed < timeout)
        {
            if (await AcceptsConnectionsAsync(port, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<bool> AcceptsConnectionsAsync(int port, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            attempt.CancelAfter(TimeSpan.FromMilliseconds(500));
            await client.ConnectAsync(IPAddress.Loopback, port, attempt.Token).ConfigureAwait(false);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <summary>Calls <c>IHost.StopAsync</c> on a host from the app's own copy of the hosting library.</summary>
    private static async Task InvokeStopAsync(object host, CancellationToken cancellationToken)
    {
        var stop = host.GetType().GetInterfaces()
            .Where(i => i.FullName == "Microsoft.Extensions.Hosting.IHost")
            .Select(i => i.GetMethod("StopAsync", [typeof(CancellationToken)]))
            .FirstOrDefault(m => m is not null);
        if (stop?.Invoke(host, [cancellationToken]) is Task task)
        {
            await task.ConfigureAwait(false);
        }
    }

    /// <summary>Listens for the hosts the app builds.</summary>
    private sealed class HostCapture : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        private readonly List<IDisposable> _subscriptions = [];
        private readonly Lock _gate = new();

        public object? Host { get; private set; }

        public void Start()
        {
            lock (_gate)
            {
                _subscriptions.Add(DiagnosticListener.AllListeners.Subscribe(this));
            }
        }

        public void OnNext(DiagnosticListener value)
        {
            if (value.Name == HostingListenerName)
            {
                lock (_gate)
                {
                    _subscriptions.Add(value.Subscribe(this));
                }
            }
        }

        public void OnNext(KeyValuePair<string, object?> value)
        {
            if (value.Key == "HostBuilt" && value.Value is not null)
            {
                Host = value.Value;
            }
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void Dispose()
        {
            lock (_gate)
            {
                foreach (var subscription in _subscriptions)
                {
                    subscription.Dispose();
                }

                _subscriptions.Clear();
            }
        }
    }
}
