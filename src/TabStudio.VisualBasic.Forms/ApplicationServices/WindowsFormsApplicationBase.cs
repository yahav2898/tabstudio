using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.VisualBasic.Devices;
using TabStudio.Forms.Runtime;

namespace Microsoft.VisualBasic.ApplicationServices;

/// <summary>
/// The Windows Forms application framework, the base of <c>My.Application</c> in a Windows Forms app: <see cref="Run"/>
/// raises <see cref="ApplyApplicationDefaults"/> and <see cref="Startup"/>, shows the splash screen, creates the main form
/// with <see cref="OnCreateMainForm"/>, runs it on TabStudio's Forms layer until it closes (or until every form closes,
/// as <see cref="ShutdownStyle"/> says), then raises <see cref="Shutdown"/>. Exceptions the app does not handle go to
/// <see cref="UnhandledException"/> when it has handlers.
/// </summary>
public class WindowsFormsApplicationBase : ConsoleApplicationBase
{
    /// <summary>How long, in milliseconds, the splash screen stays at least, unless the app says otherwise.</summary>
    internal const int MinimumSplashExposureDefault = 2000;

    /// <summary>The single-instance apps running in this process, by their identifier.</summary>
    private static readonly Dictionary<string, WindowsFormsApplicationBase> RunningInstances = new(StringComparer.Ordinal);

    private readonly WinFormsAppContext _appContext;
    private readonly object _networkAvailabilityChangeLock = new();
    private readonly List<NetworkAvailableEventHandler> _networkAvailabilityEventHandlers = [];
    private readonly List<UnhandledExceptionEventHandler> _unhandledExceptionHandlers = [];
    private bool _didSplashScreen;
    private bool _finishedOnInitialize;
    private Network? _networkObject;
    private bool _processingUnhandledExceptionEvent;
    private ShutdownMode _shutdownStyle;
    private Form? _splashScreen;
    private long _splashShownAt;
    private FormsRuntime? _threadExceptionRuntime;
    private bool _turnOnNetworkListener;

    /// <summary>Creates the application object with Windows authentication: the device's user.</summary>
    public WindowsFormsApplicationBase()
        : this(AuthenticationMode.Windows)
    {
    }

    /// <summary>Creates the application object.</summary>
    /// <param name="authenticationMode">
    /// <see cref="AuthenticationMode.Windows"/> makes the device's user the principal of <c>My.User</c>;
    /// <see cref="AuthenticationMode.ApplicationDefined"/> leaves it to the app.
    /// </param>
    /// <exception cref="InvalidEnumArgumentException">The mode is not one of the enumeration's values.</exception>
    public WindowsFormsApplicationBase(AuthenticationMode authenticationMode)
    {
        ValidateAuthenticationModeEnumValue(authenticationMode, nameof(authenticationMode));
        if (authenticationMode == AuthenticationMode.Windows)
        {
            Thread.CurrentPrincipal = User.DevicePrincipal();
        }

        _appContext = new WinFormsAppContext(this);
    }

    /// <summary>
    /// Occurs before the first form is created, so the app can set its default font, high-DPI mode, color mode and the
    /// splash screen's minimum display time.
    /// </summary>
    public event ApplyApplicationDefaultsEventHandler? ApplyApplicationDefaults;

    /// <summary>Occurs when the app ends after its forms closed. It does not occur when the app ends with an exception.</summary>
    public event ShutdownEventHandler? Shutdown;

    /// <summary>Occurs when the app starts, before its main form is created; setting Cancel ends the app.</summary>
    public event StartupEventHandler? Startup;

    /// <summary>Occurs in a single-instance app when it is started again while it runs.</summary>
    public event StartupNextInstanceEventHandler? StartupNextInstance;

    /// <summary>
    /// Occurs when code on the UI thread throws an exception the app does not handle. While the event has handlers the
    /// app does not crash; it ends after the event unless a handler sets <c>ExitApplication</c> to <see langword="false"/>.
    /// </summary>
    public event UnhandledExceptionEventHandler? UnhandledException
    {
        add
        {
            if (value is null)
            {
                return;
            }

            _unhandledExceptionHandlers.Add(value);
            HookThreadException();
        }

        remove
        {
            if (value is null || !_unhandledExceptionHandlers.Remove(value))
            {
                return;
            }

            if (_unhandledExceptionHandlers.Count == 0)
            {
                UnhookThreadException();
            }
        }
    }

    /// <summary>Occurs when the device connects to or disconnects from a network, on the app's UI thread.</summary>
    public event NetworkAvailableEventHandler? NetworkAvailabilityChanged
    {
        add
        {
            if (value is null)
            {
                return;
            }

            lock (_networkAvailabilityChangeLock)
            {
                _networkAvailabilityEventHandlers.Add(value);
                _turnOnNetworkListener = true;
                if (_networkObject is null && _finishedOnInitialize)
                {
                    ListenToNetwork();
                }
            }
        }

        remove
        {
            lock (_networkAvailabilityChangeLock)
            {
                if (value is null || !_networkAvailabilityEventHandlers.Remove(value))
                {
                    return;
                }

                if (_networkAvailabilityEventHandlers.Count == 0 && _networkObject is { } network)
                {
                    network.NetworkAvailabilityChanged -= NetworkAvailableEventAdaptor;
                    network.DisconnectListener();
                    _networkObject = null;
                }
            }
        }
    }

    /// <summary>The context of the app's message loop, whose main form is <see cref="MainForm"/>.</summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public ApplicationContext ApplicationContext => _appContext;

    /// <summary>How long, in milliseconds, the splash screen stays at least.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public int MinimumSplashScreenDisplayTime { get; set; } = MinimumSplashExposureDefault;

    /// <summary>The app's open forms.</summary>
    public FormCollection OpenForms => Application.OpenForms;

    /// <summary>Whether <c>My.Settings</c> is saved when the app ends, which <c>My.Settings</c> itself does.</summary>
    public bool SaveMySettingsOnExit { get; set; }

    /// <summary>The splash screen, shown while the main form is created; <see cref="OnCreateSplashScreen"/> sets it.</summary>
    /// <exception cref="ArgumentException">The form is the main form.</exception>
    public Form? SplashScreen
    {
        get => _splashScreen;
        set
        {
            if (value is not null && value == _appContext.MainForm)
            {
                throw new ArgumentException("Splash screen and main form cannot be the same form.", nameof(value));
            }

            _splashScreen = value;
        }
    }

    /// <summary>Whether the app uses GDI+ text rendering. TabStudio draws text with the device's renderer.</summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    protected static bool UseCompatibleTextRendering => false;

    /// <summary>The app's color mode. TabStudio draws forms in Windows' classic colors whatever it is.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected SystemColorMode ColorMode { get; set; }

    /// <summary>Whether the app turns on visual styles. Controls always have them in TabStudio.</summary>
    protected bool EnableVisualStyles { get; set; }

    /// <summary>The high-DPI mode. TabStudio's app preview scales forms itself.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected HighDpiMode HighDpiMode { get; set; } = HighDpiMode.SystemAware;

    /// <summary>
    /// Whether only one instance of the app runs: starting it again while it runs raises
    /// <see cref="StartupNextInstance"/> in the running one instead.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    protected bool IsSingleInstance { get; set; }

    /// <summary>The main form, whose closing ends the app; <see cref="OnCreateMainForm"/> sets it.</summary>
    /// <exception cref="ArgumentNullException">The form is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The form is the splash screen.</exception>
    protected Form? MainForm
    {
        get => _appContext.MainForm;
        set
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value), "Property MainForm cannot be set to Nothing.");
            }

            if (value == _splashScreen)
            {
                throw new ArgumentException("Splash screen and main form cannot be the same form.", nameof(value));
            }

            _appContext.MainForm = value;
        }
    }

    /// <summary>When the app ends: after the main form closes, or after every form closes.</summary>
    /// <exception cref="InvalidEnumArgumentException">The mode is not one of the enumeration's values.</exception>
    protected internal ShutdownMode ShutdownStyle
    {
        get => _shutdownStyle;
        set
        {
            ValidateShutdownModeEnumValue(value, nameof(value));
            _shutdownStyle = value;
        }
    }

    /// <summary>Handles the messages waiting for the app's UI thread, and draws its windows.</summary>
    public void DoEvents() => Application.DoEvents();

    /// <summary>
    /// Runs the app: the application framework's startup, the main form's message loop and the shutdown, on the
    /// calling thread, which is the app's UI thread. The My namespace's <c>Sub Main</c> calls it.
    /// </summary>
    /// <param name="commandLine">The command-line arguments Main got.</param>
    /// <exception cref="NoStartupFormException">No main form was created.</exception>
    /// <exception cref="CantStartSingleInstanceException">A single-instance app could not reach the running instance.</exception>
    public void Run(string[] commandLine)
    {
        InternalCommandLine = new ReadOnlyCollection<string>(commandLine);
        HookThreadException();
        if (!IsSingleInstance)
        {
            DoApplicationModel();
            return;
        }

        var instance = GetApplicationInstanceID(GetType().Assembly);
        WindowsFormsApplicationBase? running;
        lock (RunningInstances)
        {
            if (!RunningInstances.TryGetValue(instance, out running))
            {
                RunningInstances[instance] = this;
            }
        }

        if (running is null)
        {
            try
            {
                DoApplicationModel();
            }
            finally
            {
                lock (RunningInstances)
                {
                    RunningInstances.Remove(instance);
                }
            }

            return;
        }

        if (!running.PostNextInstance(commandLine))
        {
            throw new CantStartSingleInstanceException();
        }
    }

    /// <summary>Hides the splash screen and activates the main form.</summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    protected void HideSplashScreen()
    {
        MainForm?.Activate();
        if (_splashScreen is { IsDisposed: false } splash)
        {
            splash.Dispose();
            _splashScreen = null;
        }
    }

    /// <summary>Shows the splash screen that <see cref="OnCreateSplashScreen"/> creates, once.</summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    protected void ShowSplashScreen()
    {
        if (_didSplashScreen)
        {
            return;
        }

        _didSplashScreen = true;
        if (_splashScreen is null)
        {
            OnCreateSplashScreen();
        }

        if (_splashScreen is { } splash)
        {
            // Windows shows the splash screen on a thread of its own; the Forms layer has one UI thread per app, so it
            // is shown on it and drawn now, before the main form is created.
            _splashShownAt = Stopwatch.GetTimestamp();
            splash.Show();
            Application.DoEvents();
        }
    }

    /// <summary>Creates the main form and sets <see cref="MainForm"/>; the My namespace's designer code overrides it.</summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    protected virtual void OnCreateMainForm()
    {
    }

    /// <summary>Creates the splash screen and sets <see cref="SplashScreen"/>; the designer code overrides it when the app has one.</summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    protected virtual void OnCreateSplashScreen()
    {
    }

    /// <summary>
    /// Applies the app's defaults: raises <see cref="ApplyApplicationDefaults"/>, sets the default font, high-DPI mode
    /// and visual styles, and shows the splash screen unless the command line has <c>/nosplash</c> or <c>-nosplash</c>.
    /// </summary>
    /// <param name="commandLineArgs">The command-line arguments.</param>
    /// <returns>Whether the app goes on starting.</returns>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    protected virtual bool OnInitialize(ReadOnlyCollection<string> commandLineArgs)
    {
        var defaults = new ApplyApplicationDefaultsEventArgs(MinimumSplashScreenDisplayTime, HighDpiMode, ColorMode);
        ApplyApplicationDefaults?.Invoke(this, defaults);
        if (defaults.Font is { } font)
        {
            Application.SetDefaultFont(font);
        }

        MinimumSplashScreenDisplayTime = defaults.MinimumSplashScreenDisplayTime;
        HighDpiMode = defaults.HighDpiMode;
        ColorMode = defaults.ColorMode;
        if (Application.SetHighDpiMode(HighDpiMode))
        {
            HighDpiMode = Application.HighDpiMode;
        }

        if (EnableVisualStyles)
        {
            Application.EnableVisualStyles();
        }

        if (!(commandLineArgs.Contains("/nosplash") || CommandLineArgs.Contains("-nosplash")))
        {
            ShowSplashScreen();
        }

        _finishedOnInitialize = true;
        return true;
    }

    /// <summary>
    /// Creates the main form with <see cref="OnCreateMainForm"/> unless it exists, and runs the message loop until the
    /// app ends.
    /// </summary>
    /// <exception cref="NoStartupFormException">No main form was created.</exception>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    protected virtual void OnRun()
    {
        if (MainForm is null)
        {
            OnCreateMainForm();
            var main = MainForm ?? throw new NoStartupFormException();
            main.Load += MainFormLoadingDone;
        }

        try
        {
            Application.Run(_appContext);
        }
        finally
        {
            _networkObject?.DisconnectListener();
        }
    }

    /// <summary>Raises <see cref="Shutdown"/>.</summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    protected virtual void OnShutdown() => Shutdown?.Invoke(this, EventArgs.Empty);

    /// <summary>Raises <see cref="Startup"/>.</summary>
    /// <param name="eventArgs">The command line; a handler sets Cancel to end the app.</param>
    /// <returns>Whether the app goes on starting.</returns>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    protected virtual bool OnStartup(StartupEventArgs eventArgs)
    {
        eventArgs.Cancel = false;
        lock (_networkAvailabilityChangeLock)
        {
            if (_turnOnNetworkListener && _networkObject is null)
            {
                ListenToNetwork();
            }
        }

        Startup?.Invoke(this, eventArgs);
        return !eventArgs.Cancel;
    }

    /// <summary>Raises <see cref="StartupNextInstance"/>, then brings the main form to the front if the handlers want it.</summary>
    /// <param name="eventArgs">The new instance's command line.</param>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    protected virtual void OnStartupNextInstance(StartupNextInstanceEventArgs eventArgs)
    {
        StartupNextInstance?.Invoke(this, eventArgs);
        if (eventArgs.BringToForeground && MainForm is { } main)
        {
            if (main.WindowState == FormWindowState.Minimized)
            {
                main.WindowState = FormWindowState.Normal;
            }

            main.Activate();
        }
    }

    /// <summary>
    /// Raises <see cref="UnhandledException"/> when it has handlers, and ends the app afterwards if
    /// <c>ExitApplication</c> is still set.
    /// </summary>
    /// <param name="e">The exception.</param>
    /// <returns>Whether a handler saw the exception; otherwise it ends the app.</returns>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    protected virtual bool OnUnhandledException(UnhandledExceptionEventArgs e)
    {
        if (_unhandledExceptionHandlers.Count == 0)
        {
            return false;
        }

        _processingUnhandledExceptionEvent = true;
        foreach (var handler in _unhandledExceptionHandlers.ToList())
        {
            handler(this, e);
        }

        _processingUnhandledExceptionEvent = false;
        if (e.ExitApplication && FormsRuntime.Current is not null)
        {
            Application.Exit();
        }

        return true;
    }

    /// <summary>The identifier of a single-instance app: its GUID and version, or its module's identifier.</summary>
    internal static string GetApplicationInstanceID(Assembly entry)
    {
        if (entry.GetCustomAttribute<GuidAttribute>() is { } guid)
        {
            return entry.GetName().Version is { } version
                ? FormattableString.Invariant($"{guid.Value}{version.Major}.{version.Minor}")
                : guid.Value;
        }

        return entry.ManifestModule.ModuleVersionId.ToString();
    }

    private static void ValidateAuthenticationModeEnumValue(AuthenticationMode value, string paramName)
    {
        if (value is < AuthenticationMode.Windows or > AuthenticationMode.ApplicationDefined)
        {
            throw new InvalidEnumArgumentException(paramName, (int)value, typeof(AuthenticationMode));
        }
    }

    private static void ValidateShutdownModeEnumValue(ShutdownMode value, string paramName)
    {
        if (value is < ShutdownMode.AfterMainFormCloses or > ShutdownMode.AfterAllFormsClose)
        {
            throw new InvalidEnumArgumentException(paramName, (int)value, typeof(ShutdownMode));
        }
    }

    /// <summary>The application model: initialize, start, run and shut down, with unhandled exceptions to the event.</summary>
    private void DoApplicationModel()
    {
        var eventArgs = new StartupEventArgs(CommandLineArgs);
        if (Debugger.IsAttached)
        {
            // Under a debugger, exceptions stop where they are thrown.
            RunApplicationModel(eventArgs);
            return;
        }

        try
        {
            RunApplicationModel(eventArgs);
        }
        catch (Exception ex) when (!_processingUnhandledExceptionEvent)
        {
            if (!OnUnhandledException(new UnhandledExceptionEventArgs(true, ex)))
            {
                throw;
            }
        }
    }

    private void RunApplicationModel(StartupEventArgs eventArgs)
    {
        if (OnInitialize(CommandLineArgs) && OnStartup(eventArgs))
        {
            OnRun();
            OnShutdown();
        }
    }

    /// <summary>Hands Application.ThreadException to the UnhandledException event, once the app's UI thread runs.</summary>
    private void HookThreadException()
    {
        if (_unhandledExceptionHandlers.Count == 0 || FormsRuntime.Current is not { } runtime || runtime == _threadExceptionRuntime)
        {
            return;
        }

        UnhookThreadException();
        runtime.ThreadException += OnUnhandledExceptionEventAdaptor;
        _threadExceptionRuntime = runtime;
    }

    private void UnhookThreadException()
    {
        if (_threadExceptionRuntime is { } runtime)
        {
            runtime.ThreadException -= OnUnhandledExceptionEventAdaptor;
            _threadExceptionRuntime = null;
        }
    }

    private void OnUnhandledExceptionEventAdaptor(object sender, ThreadExceptionEventArgs e) =>
        OnUnhandledException(new UnhandledExceptionEventArgs(true, e.Exception));

    /// <summary>Hides the splash screen once the main form has loaded and the splash screen's minimum time is over.</summary>
    private void MainFormLoadingDone(object? sender, EventArgs e)
    {
        if (sender is Form form)
        {
            form.Load -= MainFormLoadingDone;
        }

        if (_splashScreen is not null && MinimumSplashScreenDisplayTime > 0)
        {
            // Windows blocks the UI thread here too; the splash screen goes on drawing meanwhile.
            var due = TimeSpan.FromMilliseconds(MinimumSplashScreenDisplayTime);
            while (Stopwatch.GetElapsedTime(_splashShownAt) < due && FormsRuntime.Current is { IsStopping: false })
            {
                Application.DoEvents();
                Thread.Sleep(15);
            }
        }

        HideSplashScreen();
    }

    /// <summary>Starts listening for network changes for <see cref="NetworkAvailabilityChanged"/>. The caller holds the lock.</summary>
    private void ListenToNetwork()
    {
        _networkObject = new Network();
        _networkObject.NetworkAvailabilityChanged += NetworkAvailableEventAdaptor;
    }

    private void NetworkAvailableEventAdaptor(object sender, NetworkAvailableEventArgs e)
    {
        NetworkAvailableEventHandler[] handlers;
        lock (_networkAvailabilityChangeLock)
        {
            handlers = [.. _networkAvailabilityEventHandlers];
        }

        foreach (var handler in handlers)
        {
            try
            {
                handler(sender, e);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                if (!OnUnhandledException(new UnhandledExceptionEventArgs(true, ex)))
                {
                    throw;
                }
            }
        }
    }

    /// <summary>Hands a new instance's command line to this running instance, on its UI thread.</summary>
    /// <returns>Whether this instance took it.</returns>
    private bool PostNextInstance(string[] commandLine)
    {
        if (MainForm is not { IsDisposed: false } main)
        {
            return false;
        }

        var arguments = new ReadOnlyCollection<string>([.. commandLine]);
        try
        {
            main.BeginInvoke(() => OnStartupNextInstance(new StartupNextInstanceEventArgs(arguments, bringToForegroundFlag: true)));
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>The message loop's context: with <see cref="ShutdownMode.AfterAllFormsClose"/> another open form becomes the main form.</summary>
    private sealed class WinFormsAppContext(WindowsFormsApplicationBase app) : ApplicationContext
    {
        /// <inheritdoc />
        protected override void OnMainFormClosed(object? sender, EventArgs e)
        {
            if (app.ShutdownStyle == ShutdownMode.AfterMainFormCloses)
            {
                base.OnMainFormClosed(sender, e);
                return;
            }

            // Identify a new main form, so the app keeps running while it is open.
            var forms = Application.OpenForms;
            if (forms.Count > 0 && forms[0] is { } next && !ReferenceEquals(next, sender))
            {
                MainForm = next;
            }
            else
            {
                base.OnMainFormClosed(sender, e);
            }
        }
    }
}
