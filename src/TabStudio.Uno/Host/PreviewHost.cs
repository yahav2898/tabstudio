#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Uno.ApplicationModel.Core;
using Uno.ApplicationModel.DataTransfer;
using Uno.Foundation.Extensibility;
using Uno.Foundation.Logging;
using Uno.Helpers.Theming;
using Uno.UI.Hosting;
using Uno.UI.Xaml.Controls;
using Windows.ApplicationModel;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;

namespace TabStudio.UnoHost;

/// <summary>
/// TabStudio's Uno Platform host: runs Uno's Skia renderer inside TabStudio, with a window of its own whose frames the
/// app preview shows, and runs each build of an app as an Uno secondary app, loaded in an assembly load context of its
/// own, whose windows show their content in the preview's window.
/// </summary>
/// <remarks>
/// TabStudio calls these members through reflection, with .NET types only: TabStudio and this host meet only in .NET
/// itself, SkiaSharp (which both draw with) and the delegates TabStudio passes in. Uno's assemblies live in the default
/// load context for the life of the process, as Uno's secondary app support requires; each build of an app is loaded
/// in a new context and closed when the next one starts.
/// </remarks>
public static class PreviewHost
{
    private static readonly object Gate = new();
    private static PreviewDispatcher? _dispatcher;
    private static PreviewPlatform? _platform;
    private static PreviewWindowFactory? _windows;
    private static PreviewPointerInput? _pointer;
    private static PreviewKeyboardInput? _keyboard;
    private static AlcContentHost? _contentHost;
    private static Application? _hostApp;
    private static Application? _app;
    private static TaskCompletionSource? _started;
    private static (double Width, double Height, float Scale) _size = (1024, 640, 1);
    private static Action? _frameReady;
    private static Action<int, string, Exception?>? _log;
    private static Action<Exception>? _unhandled;

    /// <summary>Whether Uno Platform is running in this process.</summary>
    public static bool IsStarted => _started is { Task.IsCompletedSuccessfully: true };

    /// <summary>The width of the last frame, in pixels.</summary>
    public static int FrameWidth => _windows?.Preview?.Renderer?.FrameSize.Width ?? 0;

    /// <summary>The height of the last frame, in pixels.</summary>
    public static int FrameHeight => _windows?.Preview?.Renderer?.FrameSize.Height ?? 0;

    /// <summary>
    /// Starts Uno Platform on its own UI thread, once for the process: its window, which the app preview shows, its
    /// input, display and theme, and an application of the host's own that holds the window's content.
    /// </summary>
    /// <param name="contentDirectory">Where <c>ms-appx:///</c> addresses point: the running app's files and its packages' fonts.</param>
    /// <param name="dark">Whether TabStudio uses its dark theme, which apps see as the system theme.</param>
    /// <param name="width">The preview's width in device-independent pixels.</param>
    /// <param name="height">The preview's height in device-independent pixels.</param>
    /// <param name="scale">Pixels per device-independent pixel.</param>
    /// <param name="frameReady">Called on the renderer's thread when a new frame can be copied.</param>
    /// <param name="log">Receives Uno's warnings and errors: a <see cref="PreviewLogLevel"/>, the message, and an exception.</param>
    /// <param name="unhandled">Receives the exceptions an app's code throws and does not handle.</param>
    public static Task StartAsync(
        string contentDirectory,
        bool dark,
        double width,
        double height,
        float scale,
        Action frameReady,
        Action<int, string, Exception?> log,
        Action<Exception> unhandled)
    {
        lock (Gate)
        {
            if (_started is { } started)
            {
                return started.Task;
            }

            _started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _size = (width, height, scale);
            _frameReady = frameReady;
            _log = log;
            _unhandled = unhandled;
            _dispatcher = new PreviewDispatcher(ReportUnhandled);
            var completion = _started;
            _dispatcher.Post(() =>
            {
                try
                {
                    Initialize(contentDirectory, dark);
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                }
            });
            return completion.Task;
        }
    }

    /// <summary>
    /// Starts an app: creates its <see cref="Application"/> class in the context its assembly was loaded in, and
    /// launches it, closing the app that ran before.
    /// </summary>
    /// <param name="assembly">The app's assembly.</param>
    /// <returns>Completes when the app's <c>OnLaunched</c> returned, or fails with what its startup code threw.</returns>
    public static Task LaunchAsync(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return RunAsync(() =>
        {
            CloseApp();
            var type = FindApplicationType(assembly)
                ?? throw new InvalidOperationException(
                    $"{assembly.GetName().Name} has no Application class to start. An Uno Platform or WinUI app defines one in App.xaml and App.xaml.cs.");

            using (AssemblyLoadContext.EnterContextualReflection(assembly))
            {
                Application.Start(new Func<ApplicationInitializationCallbackParams, Application>(_ =>
                {
                    var app = Create(type);
                    app.UnhandledException += OnUnhandledException;
                    _app = app;
                    return app;
                }));
            }
        });
    }

    /// <summary>Closes the running app: its windows close and their content leaves the preview.</summary>
    public static Task CloseAsync() => IsStarted ? RunAsync(CloseApp) : Task.CompletedTask;

    /// <summary>Sizes the preview's window.</summary>
    public static void Resize(double width, double height, float scale)
    {
        _size = (width, height, scale);
        _dispatcher?.Post(() => _windows?.Preview?.ApplySize(width, height, scale));
    }

    /// <summary>Follows TabStudio's theme.</summary>
    public static void SetTheme(bool dark) => _dispatcher?.Post(() => _platform?.SetTheme(dark));

    /// <summary>Copies the last frame into a buffer of 32-bit premultiplied BGRA pixels. Any thread.</summary>
    /// <returns>Whether there was a frame.</returns>
    public static bool CopyFrame(IntPtr pixels, int width, int height, int rowBytes) =>
        _windows?.Preview?.Renderer?.CopyTo(pixels, width, height, rowBytes) == true;

    /// <summary>
    /// Raises a pointer event. Any thread.
    /// </summary>
    /// <param name="action">A <see cref="PreviewPointerAction"/>.</param>
    /// <param name="pointerId">Which finger, mouse or pen.</param>
    /// <param name="device">0 touch, 1 pen, 2 mouse, as <see cref="PointerDeviceType"/>.</param>
    /// <param name="x">Horizontal position in device-independent pixels.</param>
    /// <param name="y">Vertical position in device-independent pixels.</param>
    /// <param name="left">Whether the left button is down, or the finger or pen touches.</param>
    /// <param name="right">Whether the right button is down.</param>
    /// <param name="middle">Whether the middle button is down.</param>
    /// <param name="wheelDelta">How far the wheel turned, in WinUI's units of 120 a notch.</param>
    /// <param name="horizontalWheel">Whether the wheel scrolls sideways.</param>
    /// <param name="modifiers">Shift, Control, Menu (Alt) and Windows, as <see cref="VirtualKeyModifiers"/>.</param>
    /// <param name="timestamp">When it happened, in microseconds.</param>
    public static void Pointer(int action, uint pointerId, int device, double x, double y, bool left, bool right, bool middle, int wheelDelta, bool horizontalWheel, int modifiers, ulong timestamp) =>
        _pointer?.Raise((PreviewPointerAction)action, pointerId, (PointerDeviceType)device, x, y, left, right, middle, wheelDelta, horizontalWheel, (VirtualKeyModifiers)modifiers, timestamp);

    /// <summary>Raises a key press or release. Any thread.</summary>
    /// <param name="down">Whether the key went down.</param>
    /// <param name="key">The key, as <see cref="VirtualKey"/>.</param>
    /// <param name="modifiers">The modifiers held, as <see cref="VirtualKeyModifiers"/>.</param>
    /// <param name="scanCode">The key's scan code, or 0.</param>
    /// <param name="character">The character the key types, if any.</param>
    public static void Key(bool down, int key, int modifiers, uint scanCode, string? character) =>
        _keyboard?.RaiseKey(down, (VirtualKey)key, (VirtualKeyModifiers)modifiers, scanCode, character is { Length: 1 } ? character[0] : null);

    /// <summary>Types text, as an input method or the on-screen keyboard commits it. Any thread.</summary>
    public static void Text(string text, int modifiers) => _keyboard?.RaiseText(text ?? string.Empty, (VirtualKeyModifiers)modifiers);

    /// <summary>The texts the app shows, in visual tree order: its text blocks' and text boxes'.</summary>
    public static Task<string[]> GetTextsAsync() => RunAsync(() =>
    {
        var texts = new List<string>();
        foreach (var element in Descendants(_contentHost))
        {
            var text = element switch
            {
                TextBlock block => block.Text,
                TextBox box => box.Text,
                _ => null,
            };
            if (!string.IsNullOrEmpty(text))
            {
                texts.Add(text);
            }
        }

        return texts.ToArray();
    });

    /// <summary>
    /// Where an element of the app is: the first one whose <c>x:Name</c> or automation id is <paramref name="name"/>.
    /// </summary>
    /// <returns>Its bounds in the preview, in device-independent pixels (left, top, width, height), or <see langword="null"/>.</returns>
    public static Task<double[]?> FindElementAsync(string name) => RunAsync(() =>
    {
        foreach (var element in Descendants(_contentHost))
        {
            if (element is FrameworkElement framework
                && (framework.Name == name || AutomationProperties.GetAutomationId(framework) == name))
            {
                var bounds = framework.TransformToVisual(null).TransformBounds(new Rect(0, 0, framework.ActualWidth, framework.ActualHeight));
                return new[] { bounds.X, bounds.Y, bounds.Width, bounds.Height };
            }
        }

        return (double[]?)null;
    });

    /// <summary>Whether the app's focused element takes typed text, so the on-screen keyboard should show for it.</summary>
    public static Task<bool> IsEditingTextAsync() => RunAsync(() =>
        _contentHost?.XamlRoot is { } root && FocusManager.GetFocusedElement(root) is TextBox or PasswordBox);

    /// <summary>The title of the running app's newest window, such as a WinUI 3 app's <c>Title</c>.</summary>
    public static Task<string?> GetTitleAsync() => RunAsync(() => _windows?.AppWindows.LastOrDefault()?.Title);

    /// <summary>Sends a message to TabStudio's Output.</summary>
    internal static void Report(PreviewLogLevel level, string message, Exception? exception) => _log?.Invoke((int)level, message, exception);

    /// <summary>Reports an exception the app did not handle.</summary>
    internal static void ReportUnhandled(Exception exception) => _unhandled?.Invoke(exception);

    /// <summary>Called by the host's application when Uno launched it: creates the window the preview shows.</summary>
    internal static void OnHostLaunched(Application hostApp)
    {
        _hostApp = hostApp;
        hostApp.UnhandledException += OnUnhandledException;

        var window = new Window();
        _contentHost = new AlcContentHost
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
        };
        if (hostApp.Resources.TryGetValue("ApplicationPageBackgroundThemeBrush", out var background) && background is Brush brush)
        {
            _contentHost.Background = brush;
        }

        // The host's own content first; from then on, the content of the windows apps create goes here.
        window.Content = _contentHost;
        Window.ContentHostOverride = _contentHost;
        window.Activate();
        _started?.TrySetResult();
    }

    private static void Initialize(string contentDirectory, bool dark)
    {
        // Before anything in Uno creates a logger, which keeps the level it was created with.
        LoggerFactory.ExternalLoggerFactory = new PreviewLoggerFactory();

        // ms-appx:/// addresses resolve in the content directory, where TabStudio puts the app's files.
        StorageFile.ResourcePathBase = contentDirectory;
        Package.SetEntryAssembly(typeof(PreviewHost).Assembly);

        // Before any text is laid out.
        Report(PreviewLogLevel.Information, "Uno Platform lays out text with " + PreviewIcu.Initialize(typeof(PreviewHost).Assembly) + ".", null);

        _platform = new PreviewPlatform(dark);
        _windows = new PreviewWindowFactory(() => _size, () => _frameReady?.Invoke());
        var display = new PreviewDisplay(() => _size);
        ApiExtensibility.Register(typeof(INativeWindowFactoryExtension), _ => _windows);
        ApiExtensibility.Register(typeof(ICoreApplicationExtension), _ => _platform);
        ApiExtensibility.Register(typeof(ISystemThemeHelperExtension), _ => _platform);
        ApiExtensibility.Register(typeof(IClipboardExtension), _ => _platform);
        ApiExtensibility.Register<DisplayInformation>(typeof(IDisplayInformationExtension), _ => display);
        ApiExtensibility.Register<IXamlRootHost>(typeof(IUnoCorePointerInputSource), host =>
            ReferenceEquals(host, _windows.Preview) ? _pointer ??= new PreviewPointerInput(host) : new DetachedPointerInput());
        ApiExtensibility.Register<IXamlRootHost>(typeof(IUnoKeyboardInputSource), host =>
            ReferenceEquals(host, _windows.Preview) ? _keyboard ??= new PreviewKeyboardInput(host) : new DetachedKeyboardInput());

        var dispatcher = _dispatcher!;
        CoreDispatcher.DispatchOverride = (work, _) => dispatcher.Post(work);
        CoreDispatcher.HasThreadAccessOverride = () => dispatcher.HasThreadAccess;

        Compositor.GetSharedCompositor().IsSoftwareRenderer = true;
        Application.HasSecondaryApps = true;
        Application.Start(new Func<ApplicationInitializationCallbackParams, Application>(_ => new PreviewHostApplication()));
    }

    private static void CloseApp()
    {
        if (_app is { } app)
        {
            _app = null;
            app.UnhandledException -= OnUnhandledException;
            try
            {
                app.Exit();
            }
            catch (Exception ex)
            {
                Report(PreviewLogLevel.Warning, "The app did not close cleanly.", ex);
            }
        }

        if (_contentHost is not null)
        {
            _contentHost.Content = null;
        }
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        if (!e.Handled && e.Exception is { } exception)
        {
            ReportUnhandled(exception);
        }
    }

    /// <summary>Creates an app's application object, failing with what its constructor threw.</summary>
    private static Application Create(Type type)
    {
        try
        {
            return (Application)Activator.CreateInstance(type)!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is { } inner)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(inner).Throw();
            throw;
        }
    }

    private static Type? FindApplicationType(Assembly assembly)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t is not null).ToArray()!;
        }

        var candidates = types
            .Where(t => typeof(Application).IsAssignableFrom(t) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) is not null)
            .ToList();
        return candidates.FirstOrDefault(t => t.Name == "App") ?? candidates.FirstOrDefault();
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject? root)
    {
        if (root is null)
        {
            yield break;
        }

        var pending = new Stack<DependencyObject>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            yield return current;
            var count = VisualTreeHelper.GetChildrenCount(current);
            for (var i = count - 1; i >= 0; i--)
            {
                pending.Push(VisualTreeHelper.GetChild(current, i));
            }
        }
    }

    private static Task RunAsync(Action work) => RunAsync(() =>
    {
        work();
        return true;
    });

    private static Task<T> RunAsync<T>(Func<T> work)
    {
        if (_dispatcher is not { } dispatcher || _started is not { } started)
        {
            return Task.FromException<T>(new InvalidOperationException("Uno Platform is not running."));
        }

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        started.Task.ContinueWith(
            _ => dispatcher.Post(() =>
            {
                try
                {
                    completion.TrySetResult(work());
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                }
            }),
            TaskScheduler.Default);
        return completion.Task;
    }
}

/// <summary>The host's own application: Uno's Fluent theme, and the window the app preview shows.</summary>
internal sealed class PreviewHostApplication : Application
{
    public PreviewHostApplication() => Resources.MergedDictionaries.Add(new XamlControlsResources());

    protected override void OnLaunched(LaunchActivatedEventArgs args) => PreviewHost.OnHostLaunched(this);
}

/// <summary>The display the preview's window is on: the size and density TabStudio gives it.</summary>
internal sealed class PreviewDisplay : IDisplayInformationExtension
{
    private readonly Func<(double Width, double Height, float Scale)> _size;

    public PreviewDisplay(Func<(double Width, double Height, float Scale)> size) => _size = size;

    public DisplayOrientations CurrentOrientation => _size().Width >= _size().Height ? DisplayOrientations.Landscape : DisplayOrientations.Portrait;

    public uint ScreenWidthInRawPixels => (uint)Math.Round(_size().Width * _size().Scale);

    public uint ScreenHeightInRawPixels => (uint)Math.Round(_size().Height * _size().Scale);

    public float LogicalDpi => _size().Scale * DisplayInformation.BaseDpi;

    public double RawPixelsPerViewPixel => _size().Scale;

    public ResolutionScale ResolutionScale => (ResolutionScale)(int)(Math.Round(_size().Scale * 4) / 4 * 100);

    public double? DiagonalSizeInInches => null;
}
