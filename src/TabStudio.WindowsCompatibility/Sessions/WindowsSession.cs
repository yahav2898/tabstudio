using System.Globalization;
using TabStudio.Toolchains.Processes;
using TabStudio.WindowsCompatibility.Display;
using TabStudio.WindowsCompatibility.Display.X11;

namespace TabStudio.WindowsCompatibility.Sessions;

/// <summary>
/// A running Windows desktop: Xvfb, the X server Wine draws into, with its framebuffer in a file TabStudio maps;
/// a persistent Wine server; and Wine's virtual desktop, which every app TabStudio starts shares, with its
/// windows and dialogs drawn inside it. Apps start fast once the session runs, and it stays up between runs.
/// </summary>
/// <remarks>
/// <para>
/// Every part is a program of the Toolchain Store's Linux environment, started through its runtime: on Android
/// each runs under PRoot as a descendant of TabStudio's own process, and stopping the session ends them all.
/// The X server listens only on its socket file in TabStudio's private storage, and only clients that know its
/// cookie may connect.
/// </para>
/// <para>
/// Windows apps are the real .NET Windows Desktop runtime's <c>dotnet.exe</c> running the app's assembly, so
/// Windows Forms and WPF code that calls Win32, GDI or COM runs as on Windows, within what Wine implements.
/// </para>
/// </remarks>
public sealed class WindowsSession : IAsyncDisposable
{
    /// <summary>The smallest X screen a session uses, so the desktop can grow with the pane without a restart.</summary>
    private static readonly (int Width, int Height) MinimumScreen = (1920, 1200);

    private readonly WindowsLayout _layout;
    private readonly List<ToolchainProcess> _processes = [];
    private readonly CancellationTokenSource _stopped = new();
    private int _disposed;

    private WindowsSession(WindowsLayout layout, int display, WindowsSessionOptions options)
    {
        _layout = layout;
        DisplayNumber = display;
        Options = options;
    }

    /// <summary>The session's X display number.</summary>
    public int DisplayNumber { get; }

    /// <summary>The settings it started with.</summary>
    public WindowsSessionOptions Options { get; private set; }

    /// <summary>The desktop's screen and input.</summary>
    public WindowsDisplay Display { get; private set; } = null!;

    /// <summary>Whether the session still runs.</summary>
    public bool IsRunning => !_stopped.IsCancellationRequested;

    /// <summary>The session ended without being disposed: the X server or Wine's desktop stopped. The text says which.</summary>
    public event EventHandler<string>? Ended;

    /// <summary>Starts a session: the X server, the Windows installation the first time, and Wine's desktop.</summary>
    /// <param name="layout">Where Windows Compatibility's parts are.</param>
    /// <param name="options">The desktop's size and scale, and how WPF draws.</param>
    /// <param name="progress">Receives what happens, for the status bar.</param>
    /// <param name="cancellationToken">Gives up.</param>
    /// <exception cref="WindowsCompatibilityException">A part is missing or did not start; the message says which.</exception>
    public static async Task<WindowsSession> StartAsync(WindowsLayout layout, WindowsSessionOptions options, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(options);
        if (!layout.IsComplete)
        {
            throw new WindowsCompatibilityException("Windows Compatibility is not installed. Install it from the Toolchain Store.");
        }

        var session = new WindowsSession(layout, FreeDisplay(layout), options);
        try
        {
            await session.StartPartsAsync(progress, cancellationToken).ConfigureAwait(false);
            return session;
        }
        catch
        {
            await session.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Starts a Windows desktop app: <c>dotnet.exe</c> on its assembly, in the session's desktop.</summary>
    /// <param name="assembly">The app's assembly, as a full path next to its <c>runtimeconfig.json</c>.</param>
    /// <param name="arguments">The app's command-line arguments.</param>
    /// <returns>The running app.</returns>
    public WindowsProgram Start(string assembly, IReadOnlyList<string>? arguments = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(assembly);
        ThrowIfStopped();
        _layout.Runtime.ShareFolder(Path.GetDirectoryName(assembly)!);
        var environment = Environment();
        environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        environment["DOTNET_NOLOGO"] = "1";

        // Under Wine, mapping code twice for write-xor-execute only slows the runtime's start.
        environment["DOTNET_EnableWriteXorExecute"] = "0";
        var process = _layout.Runtime.Start(new ProcessStartSpec
        {
            Program = _layout.Wine!,
            Arguments = [WindowsLayout.ToWindowsPath(_layout.DotNetRoot + "/dotnet.exe"), WindowsLayout.ToWindowsPath(assembly), .. arguments ?? []],
            WorkingDirectory = Path.GetDirectoryName(assembly),
            Environment = environment,
        });
        return new WindowsProgram(process, Path.GetFileNameWithoutExtension(assembly));
    }

    /// <summary>Changes the desktop's size, display scale or WPF renderer; apps started afterwards use the new settings.</summary>
    /// <param name="options">The new settings.</param>
    /// <param name="cancellationToken">Gives up.</param>
    public async Task ApplyAsync(WindowsSessionOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ThrowIfStopped();
        var settingsChanged = options.Dpi != Options.Dpi || options.WpfRendering != Options.WpfRendering;
        Options = options;
        await Display.ResizeDesktopAsync(options.Width, options.Height, cancellationToken).ConfigureAwait(false);
        if (settingsChanged)
        {
            await new WindowsPrefix(_layout, () => Environment()).ApplySettingsAsync(options, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _stopped.CancelAsync().ConfigureAwait(false);
        if (Display is not null)
        {
            await Display.DisposeAsync().ConfigureAwait(false);
        }

        if (_layout.WineServer is { } server)
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await CommandRunner.RunAsync(_layout.Runtime, new ProcessStartSpec { Program = server, Arguments = ["-k"], Environment = Environment() }, null, timeout.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ToolchainRuntimeException)
            {
                // The processes are stopped below anyway.
            }
        }

        for (var i = _processes.Count - 1; i >= 0; i--)
        {
            _processes[i].Stop();
            _processes[i].Dispose();
        }

        _stopped.Dispose();
    }

    /// <summary>The environment every Wine program of the session runs with.</summary>
    internal Dictionary<string, string?> Environment() => new(StringComparer.Ordinal)
    {
        ["WINEPREFIX"] = _layout.Prefix,
        ["DISPLAY"] = ":" + DisplayNumber.ToString(CultureInfo.InvariantCulture),
        ["XAUTHORITY"] = _layout.AuthorityFile,
        ["WINEDEBUG"] = "-all",
        ["WINEDLLOVERRIDES"] = "winemenubuilder.exe=;winedbg.exe=",
    };

    private async Task StartPartsAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        progress?.Report("Starting the Windows desktop");
        Directory.CreateDirectory(_layout.ToDevicePath(_layout.FramebufferFolder));
        var sockets = _layout.ToDevicePath("/tmp/.X11-unix");
        Directory.CreateDirectory(sockets);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(sockets, (UnixFileMode)0b1_111_111_111);
        }

        var cookie = X11Cookie.Create();
        cookie.WriteAuthorityFile(_layout.ToDevicePath(_layout.AuthorityFile), DisplayNumber);
        var screen = (Width: Math.Max(MinimumScreen.Width, Options.Width), Height: Math.Max(MinimumScreen.Height, Options.Height));
        var xvfb = StartPart("Xvfb", [
            ":" + DisplayNumber.ToString(CultureInfo.InvariantCulture),
            "-screen", "0", string.Create(CultureInfo.InvariantCulture, $"{screen.Width}x{screen.Height}x24"),
            "-fbdir", _layout.FramebufferFolder,
            "-auth", _layout.AuthorityFile,
            "-nolisten", "tcp",
            "-nolisten", "local",
            "-dpi", Options.Dpi.ToString(CultureInfo.InvariantCulture),
            "-noreset",
        ]);

        var connection = new DisplayConnection(
            X11Endpoint.Unix(_layout.ToDevicePath("/tmp/.X11-unix/X" + DisplayNumber.ToString(CultureInfo.InvariantCulture))),
            _layout.ToDevicePath(_layout.FramebufferFolder + "/Xvfb_screen0"),
            cookie);
        Display = await WindowsDisplay.ConnectAsync(connection, (Options.Width, Options.Height), TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
        Watch(xvfb, "The Windows desktop's X server stopped.");

        var prefix = new WindowsPrefix(_layout, () => Environment());
        await prefix.EnsureAsync(progress, cancellationToken).ConfigureAwait(false);
        await prefix.ApplySettingsAsync(Options, cancellationToken).ConfigureAwait(false);

        if (_layout.WineServer is { } server)
        {
            Watch(StartPart(server, ["-f", "-p"]), "The Wine server stopped.");
        }

        var desktop = StartPart(_layout.Wine!, ["explorer", string.Create(CultureInfo.InvariantCulture, $"/desktop={WindowsPrefix.DesktopName},{Options.Width}x{Options.Height}")]);
        Watch(desktop, "Wine's desktop stopped.");
        progress?.Report("Waiting for the Windows desktop");
        await WaitForDesktopAsync(desktop, cancellationToken).ConfigureAwait(false);
    }

    private ToolchainProcess StartPart(string program, IReadOnlyList<string> arguments)
    {
        var process = _layout.Runtime.Start(new ProcessStartSpec { Program = program, Arguments = arguments, Environment = Environment() });
        _processes.Add(process);
        _ = DrainAsync(process.Output);
        if (process.Error is { } error)
        {
            _ = DrainAsync(error);
        }

        return process;
    }

    /// <summary>Waits until Wine's desktop window is on the screen.</summary>
    private async Task WaitForDesktopAsync(ToolchainProcess desktop, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(90);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (desktop.HasExited)
            {
                var exit = await desktop.Completion.ConfigureAwait(false);
                throw new WindowsCompatibilityException($"Wine's desktop did not start ({exit.Description}).");
            }

            if (await Display.FindDesktopWindowAsync(cancellationToken).ConfigureAwait(false) is not null)
            {
                return;
            }

            await Task.Delay(200, cancellationToken).ConfigureAwait(false);
        }

        throw new WindowsCompatibilityException("Wine's desktop did not appear within 90 seconds.");
    }

    private void Watch(ToolchainProcess process, string reason) => _ = process.Completion.ContinueWith(
        _ =>
        {
            if (Volatile.Read(ref _disposed) == 0)
            {
                Ended?.Invoke(this, reason);
            }
        },
        CancellationToken.None,
        TaskContinuationOptions.ExecuteSynchronously,
        TaskScheduler.Default);

    private static async Task DrainAsync(Stream stream)
    {
        var buffer = new byte[4096];
        try
        {
            while (await stream.ReadAsync(buffer).ConfigureAwait(false) > 0)
            {
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // The process ended.
        }
    }

    /// <summary>
    /// A display number no X server uses. A lock file whose server is gone, left by a session that was killed, is
    /// removed with its socket.
    /// </summary>
    private static int FreeDisplay(WindowsLayout layout)
    {
        for (var display = 20; display < 100; display++)
        {
            var number = display.ToString(CultureInfo.InvariantCulture);
            var lockFile = layout.ToDevicePath("/tmp/.X" + number + "-lock");
            var socket = layout.ToDevicePath("/tmp/.X11-unix/X" + number);
            if (File.Exists(lockFile) && !IsServerAlive(lockFile))
            {
                File.Delete(lockFile);
                File.Delete(socket);
            }

            if (!File.Exists(lockFile) && !File.Exists(socket))
            {
                return display;
            }
        }

        throw new WindowsCompatibilityException("Every X display number from 20 to 99 is taken.");
    }

    /// <summary>Whether the process an X server's lock file names still runs.</summary>
    private static bool IsServerAlive(string lockFile)
    {
        try
        {
            return int.TryParse(File.ReadAllText(lockFile).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid) && Directory.Exists("/proc/" + pid.ToString(CultureInfo.InvariantCulture));
        }
        catch (IOException)
        {
            return true;
        }
    }

    private void ThrowIfStopped()
    {
        if (!IsRunning)
        {
            throw new WindowsCompatibilityException("The Windows desktop is not running.");
        }
    }
}
