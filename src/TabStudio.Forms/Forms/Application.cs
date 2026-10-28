using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using TabStudio.Forms.Runtime;

namespace System.Windows.Forms;

/// <summary>
/// Starts and ends a Windows Forms app: <see cref="Run(Form)"/> shows the main form and runs the message loop on the
/// app's UI thread until the form closes, which ends the app in TabStudio's app preview.
/// </summary>
public sealed class Application
{
    private static readonly Font StandardFont = new("Segoe UI", 9f);

    private Application()
    {
    }

    /// <summary>Occurs when the app is about to end.</summary>
    public static event EventHandler? ApplicationExit
    {
        add => RequireRuntime().ApplicationExit += value;
        remove => RequireRuntime().ApplicationExit -= value;
    }

    /// <summary>Occurs when the message loop has nothing to do.</summary>
    public static event EventHandler? Idle
    {
        add => RequireRuntime().Idle += value;
        remove => RequireRuntime().Idle -= value;
    }

    /// <summary>
    /// Occurs when code on the UI thread throws an exception it does not handle. With a handler, the app goes on;
    /// without one, the app ends with the exception.
    /// </summary>
    public static event System.Threading.ThreadExceptionEventHandler? ThreadException
    {
        add => RequireRuntime().ThreadException += value;
        remove => RequireRuntime().ThreadException -= value;
    }

    /// <summary>The app's product name, from its assembly.</summary>
    public static string ProductName =>
        EntryAssembly?.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? EntryAssembly?.GetName().Name ?? string.Empty;

    /// <summary>The app's version, from its assembly.</summary>
    public static string ProductVersion =>
        EntryAssembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? EntryAssembly?.GetName().Version?.ToString() ?? "1.0.0";

    /// <summary>The app's company name, from its assembly.</summary>
    public static string CompanyName =>
        EntryAssembly?.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? ProductName;

    /// <summary>The folder the app starts in: its project's folder in TabStudio.</summary>
    public static string StartupPath => FormsRuntime.Current?.StartupPath ?? AppContext.BaseDirectory;

    /// <summary>The path the app's executable would have.</summary>
    public static string ExecutablePath => Path.Combine(StartupPath, ProductName + ".exe");

    /// <summary>A folder for the user's data of the app.</summary>
    public static string UserAppDataPath => DataPath(Environment.SpecialFolder.ApplicationData);

    /// <summary>A folder for the user's local data of the app.</summary>
    public static string LocalUserAppDataPath => DataPath(Environment.SpecialFolder.LocalApplicationData);

    /// <summary>A folder for the data of the app shared by every user.</summary>
    public static string CommonAppDataPath => DataPath(Environment.SpecialFolder.LocalApplicationData);

    /// <summary>The forms that are open.</summary>
    public static FormCollection OpenForms => new(FormsRuntime.Current?.Windows.Select(w => w.Form) ?? []);

    /// <summary>Whether a message loop is running.</summary>
    public static bool MessageLoop => FormsRuntime.Current?.MainForm is not null;

    /// <summary>The culture of the UI thread.</summary>
    public static CultureInfo CurrentCulture
    {
        get => Thread.CurrentThread.CurrentCulture;
        set => Thread.CurrentThread.CurrentCulture = value;
    }

    /// <summary>Whether the app draws with visual styles: always.</summary>
    public static bool RenderWithVisualStyles => true;

    /// <summary>The app's high-DPI mode.</summary>
    public static HighDpiMode HighDpiMode => HighDpiMode.SystemAware;

    /// <summary>The color mode set with <see cref="SetColorMode"/>; Classic until then, as on Windows.</summary>
    public static SystemColorMode ColorMode { get; private set; } = SystemColorMode.Classic;

    /// <summary>The system's color mode. TabStudio draws Windows Forms apps in their classic light colors.</summary>
    public static SystemColorMode SystemColorMode => SystemColorMode.Classic;

    /// <summary>Whether the app is drawn in dark mode: never, as <see cref="SystemColorMode"/> is Classic.</summary>
    public static bool IsDarkModeEnabled => false;

    /// <summary>Sets the color mode the app asks for (.NET 9 and later); controls keep their classic colors.</summary>
    /// <param name="systemColorMode">The mode.</param>
    public static void SetColorMode(SystemColorMode systemColorMode) => ColorMode = systemColorMode;

    /// <summary>Whether every form shows the wait cursor.</summary>
    public static bool UseWaitCursor { get; set; }

    /// <summary>The default font of controls.</summary>
    internal static Font DefaultFontInternal => FormsRuntime.Current?.DefaultFont ?? StandardFont;

    private static Assembly? EntryAssembly => FormsRuntime.Current?.EntryAssembly ?? Assembly.GetEntryAssembly();

    /// <summary>Turns on visual styles. Controls always have them.</summary>
    public static void EnableVisualStyles()
    {
        // Controls are always drawn with the Windows visual styles.
    }

    /// <summary>Chooses GDI+ or GDI text rendering. Text is rendered by Avalonia here.</summary>
    /// <param name="defaultValue">Whether to use GDI+ text rendering.</param>
    public static void SetCompatibleTextRenderingDefault(bool defaultValue)
    {
        // Text is drawn by the device's own text renderer.
    }

    /// <summary>Sets the high-DPI mode. The app preview scales forms itself.</summary>
    /// <param name="highDpiMode">The mode.</param>
    /// <returns>Whether the mode was set.</returns>
    public static bool SetHighDpiMode(HighDpiMode highDpiMode) => true;

    /// <summary>Sets the default font of the app's controls.</summary>
    /// <param name="font">The font.</param>
    public static void SetDefaultFont(Font font)
    {
        ArgumentNullException.ThrowIfNull(font);
        RequireRuntime().DefaultFont = font;
    }

    /// <summary>Sets how unhandled exceptions are treated.</summary>
    /// <param name="mode">The mode.</param>
    public static void SetUnhandledExceptionMode(UnhandledExceptionMode mode)
    {
        // Unhandled exceptions go to ThreadException, or end the app.
    }

    /// <summary>Sets how unhandled exceptions are treated.</summary>
    /// <param name="mode">The mode.</param>
    /// <param name="threadScope">Whether only the current thread is affected.</param>
    public static void SetUnhandledExceptionMode(UnhandledExceptionMode mode, bool threadScope)
    {
        // Unhandled exceptions go to ThreadException, or end the app.
    }

    /// <summary>Runs the message loop until <see cref="Exit()"/>.</summary>
    public static void Run()
    {
        var runtime = RequireRuntime();
        runtime.RunLoop(() => runtime.ExitRequested);
        runtime.ThrowIfCrashed();
    }

    /// <summary>Shows a form and runs the message loop until it closes, which ends the app.</summary>
    /// <param name="mainForm">The main form.</param>
    public static void Run(Form mainForm)
    {
        ArgumentNullException.ThrowIfNull(mainForm);
        Run(new ApplicationContext(mainForm));
    }

    /// <summary>Runs the message loop until the context's main form closes or it exits.</summary>
    /// <param name="context">The context.</param>
    public static void Run(ApplicationContext? context)
    {
        var runtime = RequireRuntime();
        context ??= new ApplicationContext();
        var exited = false;
        context.ThreadExit += (_, _) => exited = true;
        runtime.MainForm = context.MainForm;
        try
        {
            context.MainForm?.Show();
            runtime.RunLoop(() => exited || runtime.ExitRequested || (context.MainForm is { } main && !main.IsShown));
            runtime.ThrowIfCrashed();
        }
        finally
        {
            runtime.MainForm = null;
            if (!runtime.IsStopping)
            {
                CloseAll(runtime, CloseReason.ApplicationExitCall);
            }
        }
    }

    /// <summary>Closes every form and ends the message loops.</summary>
    public static void Exit() => Exit(null);

    /// <summary>Closes every form and ends the message loops, unless a FormClosing handler cancels.</summary>
    /// <param name="e">Receives whether the exit was cancelled.</param>
    public static void Exit(CancelEventArgs? e)
    {
        var runtime = RequireRuntime();
        if (!CloseAll(runtime, CloseReason.ApplicationExitCall))
        {
            if (e is not null)
            {
                e.Cancel = true;
            }

            return;
        }

        runtime.ExitRequested = true;
    }

    /// <summary>Ends the message loop of the current thread.</summary>
    public static void ExitThread() => Exit();

    /// <summary>Handles every message waiting now, and draws the windows that changed.</summary>
    public static void DoEvents() => RequireRuntime().DoEvents();

    /// <summary>
    /// Closes the app and starts it again, as Windows does: the forms get FormClosing, and one that cancels keeps the
    /// app running. In the app preview the app is built and loaded again, so its static fields start fresh too.
    /// </summary>
    public static void Restart()
    {
        var runtime = RequireRuntime();
        runtime.RestartRequested = true;
        var cancel = new CancelEventArgs();
        Exit(cancel);
        if (cancel.Cancel)
        {
            runtime.RestartRequested = false;
        }
    }

    /// <summary>
    /// Adds a filter that sees the app's keyboard and mouse messages before they reach their controls, as Windows
    /// delivers them: <c>WM_KEYDOWN</c>, <c>WM_KEYUP</c>, <c>WM_CHAR</c>, mouse buttons, moves and the wheel.
    /// </summary>
    /// <param name="value">The filter; returning <see langword="true"/> from PreFilterMessage stops the message.</param>
    public static void AddMessageFilter(IMessageFilter value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireRuntime().MessageFilters.Add(value);
    }

    /// <summary>Removes a message filter.</summary>
    /// <param name="value">The filter.</param>
    public static void RemoveMessageFilter(IMessageFilter value) => FormsRuntime.Current?.MessageFilters.Remove(value);

    /// <summary>Raises the Idle event.</summary>
    /// <param name="e">The event data.</param>
    public static void RaiseIdle(EventArgs e) => FormsRuntime.Current?.Idle?.Invoke(null, e);

    /// <summary>Reports an exception as an unhandled exception of the UI thread.</summary>
    /// <param name="t">The exception.</param>
    public static void OnThreadException(Exception t)
    {
        ArgumentNullException.ThrowIfNull(t);
        RequireRuntime().HandleException(t);
    }

    private static bool CloseAll(FormsRuntime runtime, CloseReason reason)
    {
        foreach (var window in runtime.Windows.Reverse().ToList())
        {
            window.Form.CloseFromWindow(reason);
            if (window.Form.IsShown)
            {
                return false;
            }
        }

        return true;
    }

    private static string DataPath(Environment.SpecialFolder folder)
    {
        var path = Path.Combine(Environment.GetFolderPath(folder), CompanyName, ProductName, ProductVersion);
        Directory.CreateDirectory(path);
        return path;
    }

    private static FormsRuntime RequireRuntime() =>
        FormsRuntime.Current ?? throw new InvalidOperationException(
            "Windows Forms apps run on their own UI thread in TabStudio: press F5 to run the project, and call Application members from its code.");
}

/// <summary>The context of a message loop: the main form whose closing ends it.</summary>
public class ApplicationContext : IDisposable
{
    private static readonly object EventThreadExit = new();
    private readonly EventHandlerList _events = new();
    private Form? _mainForm;

    /// <summary>Creates a context without a main form.</summary>
    public ApplicationContext()
    {
    }

    /// <summary>Creates a context with a main form.</summary>
    /// <param name="mainForm">The main form.</param>
    public ApplicationContext(Form? mainForm) => MainForm = mainForm;

    /// <summary>Occurs when the message loop ends.</summary>
    public event EventHandler? ThreadExit
    {
        add => _events.AddHandler(EventThreadExit, value);
        remove => _events.RemoveHandler(EventThreadExit, value);
    }

    /// <summary>The form whose closing ends the message loop.</summary>
    public Form? MainForm
    {
        get => _mainForm;
        set
        {
            if (_mainForm is not null)
            {
                _mainForm.FormClosed -= OnMainFormFormClosed;
            }

            _mainForm = value;
            if (value is not null)
            {
                value.FormClosed += OnMainFormFormClosed;
            }
        }
    }

    /// <summary>Data about the context.</summary>
    public object? Tag { get; set; }

    /// <summary>Ends the message loop.</summary>
    public void ExitThread() => ExitThreadCore();

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases the context.</summary>
    /// <param name="disposing">Whether this is called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            MainForm = null;
            _events.Dispose();
        }
    }

    /// <summary>Ends the message loop and raises ThreadExit.</summary>
    protected virtual void ExitThreadCore() => ((EventHandler?)_events[EventThreadExit])?.Invoke(this, EventArgs.Empty);

    /// <summary>Called when the main form closes; ends the message loop.</summary>
    /// <param name="sender">The main form.</param>
    /// <param name="e">The event data.</param>
    protected virtual void OnMainFormClosed(object? sender, EventArgs e) => ExitThreadCore();

    private void OnMainFormFormClosed(object? sender, FormClosedEventArgs e) => OnMainFormClosed(sender, e);
}

/// <summary>The forms that are open.</summary>
public class FormCollection : ReadOnlyCollectionBase
{
    internal FormCollection(IEnumerable<Form> forms) => InnerList.AddRange(forms.ToList());

    /// <summary>The form at an index.</summary>
    /// <param name="index">The index.</param>
    public virtual Form? this[int index] => (Form?)InnerList[index];

    /// <summary>The first form with a name.</summary>
    /// <param name="name">The name.</param>
    public virtual Form? this[string? name] => InnerList.Cast<Form>().FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>A filter of window messages. Message filters are not supported: there are no Windows messages.</summary>
public interface IMessageFilter
{
    /// <summary>Filters a message.</summary>
    /// <param name="m">The message.</param>
    /// <returns>Whether the message was handled.</returns>
    bool PreFilterMessage(ref Message m);
}

/// <summary>How unhandled exceptions of the UI thread are treated.</summary>
public enum UnhandledExceptionMode
{
    /// <summary>As the app's configuration says.</summary>
    Automatic = 0,

    /// <summary>The exception is thrown on.</summary>
    ThrowException = 1,

    /// <summary>The exception goes to ThreadException.</summary>
    CatchException = 2,
}
