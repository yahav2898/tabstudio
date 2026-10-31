using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Core.Plugins;
using Avalonia.Threading;
using TabStudio.Compilation;
using TabStudio.Compilation.Running;
using TabStudio.Compilation.Xaml;

namespace TabStudio.App.Previews;

/// <summary>
/// Runs an Avalonia app inside TabStudio's own window, where its main view renders natively beside the code.
/// </summary>
/// <remarks>
/// <para>
/// The app shares TabStudio's Avalonia, so it is drawn by the same renderer on the same thread; its own copy of
/// Avalonia from NuGet is only compiled against. Its XAML, which TabStudio's generator put into the assembly as
/// text, is loaded at run time with Avalonia's XAML loader.
/// </para>
/// <para>
/// The app's <c>App</c> class starts as it would on a phone: with a single-view lifetime, it sets the main view
/// in <c>OnFrameworkInitializationCompleted</c>, as Avalonia's cross-platform template does. An app written only
/// for the desktop sets a window instead, which a pane cannot host; then its main window's content is shown,
/// with a new instance of the view model named after the window.
/// </para>
/// </remarks>
public sealed partial class AvaloniaAppHost : IDisposable
{
    /// <summary>The XAML namespace of Avalonia's controls.</summary>
    private const string AvaloniaNamespace = "https://github.com/avaloniaui";

    private static readonly ConcurrentDictionary<string, bool> HostAssemblies = new(StringComparer.Ordinal);

    private readonly LoadedProgram _program;
    private readonly string _assemblyName;
    private bool _disposed;

    private AvaloniaAppHost(LoadedProgram program, Control view, string? note)
    {
        _program = program;
        _assemblyName = program.EntryAssembly.GetName().Name ?? string.Empty;
        View = view;
        Note = note;
        Dispatcher.UIThread.UnhandledException += OnUnhandledException;
    }

    /// <summary>Raised when the app's code throws an exception it does not handle, such as in a click handler.</summary>
    public event EventHandler<Exception>? Crashed;

    /// <summary>The app's main view, wrapped in the app's theme, styles and resources.</summary>
    public Control View { get; private set; }

    /// <summary>The exact entry assembly of this running app, for metadata updates.</summary>
    public Assembly EntryAssembly => _program.EntryAssembly;

    /// <summary>Recreates the main content from the updated assembly without loading or starting a new program.</summary>
    public void RefreshContent()
    {
        Dispatcher.UIThread.VerifyAccess();
        var fresh = CreateView();
        if (MainView is { } old) old.DataContext = null;
        if (View is Decorator decorator) decorator.Child = null;
        View = fresh.Root;
    }

    /// <summary>How the view differs from the app on a device, when it does, such as a window shown as its content.</summary>
    public string? Note { get; }

    /// <summary>The Error List code of an app built for a newer Avalonia than TabStudio's.</summary>
    public const string NewerAvaloniaId = "TS2101";

    /// <summary>
    /// When an app was built for a newer major version of Avalonia than the one TabStudio runs it on, the one line that
    /// says so; otherwise <see langword="null"/>. Its controls and themes may use types that only that version has, as
    /// Dock's samples for Avalonia 12 do, and then its XAML cannot load here.
    /// </summary>
    /// <param name="packageAssemblies">The app's package assemblies, including its own copy of Avalonia's.</param>
    public static string? NewerAvaloniaNote(IReadOnlyList<string> packageAssemblies)
    {
        ArgumentNullException.ThrowIfNull(packageAssemblies);
        if (packageAssemblies.FirstOrDefault(p => string.Equals(Path.GetFileName(p), "Avalonia.Base.dll", StringComparison.OrdinalIgnoreCase)) is not { } file)
        {
            return null;
        }

        Version? version;
        try
        {
            version = AssemblyName.GetAssemblyName(file).Version;
        }
        catch (Exception ex) when (ex is IOException or BadImageFormatException or UnauthorizedAccessException)
        {
            return null;
        }

        var host = typeof(AvaloniaObject).Assembly.GetName().Version;
        return version is null || host is null || version.Major <= host.Major ? null
            : $"This app is built for Avalonia {version.Major}.{version.Minor}, and TabStudio runs Avalonia apps on its own Avalonia {host.Major}.{host.Minor}: what needs Avalonia {version.Major} cannot load here.";
    }

    /// <summary>Loads an app and creates its main view. UI thread only.</summary>
    /// <param name="entryAssembly">The app's assembly.</param>
    /// <param name="dependencies">The assemblies of the projects it references.</param>
    /// <param name="packageAssemblies">The app's package assemblies.</param>
    /// <exception cref="InvalidOperationException">The app has no view to show.</exception>
    public static AvaloniaAppHost Start(CompiledAssembly entryAssembly, IReadOnlyList<CompiledAssembly> dependencies, IReadOnlyList<string> packageAssemblies) =>
        Start(entryAssembly, dependencies, packageAssemblies, loaded: null);

    /// <summary>Loads an app and creates its main view. UI thread only.</summary>
    /// <param name="entryAssembly">The app's assembly.</param>
    /// <param name="dependencies">The assemblies of the projects it references.</param>
    /// <param name="packageAssemblies">The app's package assemblies.</param>
    /// <param name="loaded">
    /// Called with the app's assemblies once they are loaded and before any of their code runs, such as to connect a
    /// device's debugger to them.
    /// </param>
    /// <exception cref="InvalidOperationException">The app has no view to show.</exception>
    public static AvaloniaAppHost Start(CompiledAssembly entryAssembly, IReadOnlyList<CompiledAssembly> dependencies, IReadOnlyList<string> packageAssemblies, Action<IEnumerable<Assembly>>? loaded)
    {
        ArgumentNullException.ThrowIfNull(entryAssembly);
        Dispatcher.UIThread.VerifyAccess();
        AppXamlLoader.Install();

        // Avalonia compiles the XAML it loads into code that references the app's types, so the app stays loaded.
        var program = LoadedProgram.Load(entryAssembly, dependencies, packageAssemblies, IsSharedWithHost, unloadable: false);
        try
        {
            // XAML finds its types among the loaded assemblies, including the XAML namespaces of control libraries.
            program.LoadAll();
            loaded?.Invoke(program.Assemblies);

            // Avalonia's cross-platform template keeps App and its views in the shared project its heads reference.
            var types = LoadableTypes(program.EntryAssembly);
            var appAssembly = program.EntryAssembly;
            if (!types.Any(IsApplicationType)
                && program.Assemblies.Where(a => dependencies.Any(d => string.Equals(d.Name, a.GetName().Name, StringComparison.Ordinal)))
                    .FirstOrDefault(a => LoadableTypes(a).Any(IsApplicationType)) is { } shared)
            {
                appAssembly = shared;
                types = [.. types, .. LoadableTypes(shared)];
            }

            var app = StartApplication(types);
            string? note = null;
            var view = app.Lifetime.MainView
                ?? FromMainWindow(appAssembly, types, out note)
                ?? throw new InvalidOperationException(
                    "The app has no main view to show. In App.axaml.cs, set MainView for ISingleViewApplicationLifetime, as Avalonia's cross-platform template does.");
            var theme = new AppTheme(app.Instance) { Content = view };
            return new AvaloniaAppHost(program, theme.Root, note);
        }
        catch
        {
            program.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Dispatcher.UIThread.UnhandledException -= OnUnhandledException;
        if (View is Decorator { Child: { } child } decorator)
        {
            decorator.Child = null;
            child.DataContext = null;
        }

        _program.Dispose();
    }

    /// <summary>
    /// Avalonia comes from TabStudio, so the app's controls are TabStudio's types and render in its window; any
    /// other library, including Avalonia add-ons TabStudio does not have, comes from the app's packages.
    /// </summary>
    internal static bool IsSharedWithHost(string assemblyName)
    {
        if (!assemblyName.StartsWith("Avalonia", StringComparison.Ordinal)
            && !assemblyName.StartsWith("SkiaSharp", StringComparison.Ordinal)
            && !assemblyName.StartsWith("HarfBuzzSharp", StringComparison.Ordinal)
            && assemblyName != "MicroCom.Runtime")
        {
            return false;
        }

        return HostAssemblies.GetOrAdd(assemblyName, static name =>
        {
            try
            {
                AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(name));
                return true;
            }
            catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or BadImageFormatException)
            {
                return false;
            }
        });
    }

    /// <summary>Whether a type is an app's <c>App</c>: an <see cref="Application"/> that can be created.</summary>
    private static bool IsApplicationType(Type type) =>
        typeof(Application).IsAssignableFrom(type) && !type.IsAbstract && type.GetConstructor(Type.EmptyTypes) is not null;

    /// <summary>Creates the app's <c>App</c> and starts it with a single-view lifetime, as on a phone.</summary>
    private static (Application? Instance, PreviewLifetime Lifetime) StartApplication(Type[] types)
    {
        var (lifetime, state) = PreviewLifetime.Create();
        var appType = types.FirstOrDefault(IsApplicationType);
        if (appType is null)
        {
            return (null, state);
        }

        // Apps change a few of Avalonia's global settings as they start, such as removing a binding validator;
        // in TabStudio those are TabStudio's settings, so they are put back afterwards.
        var validators = BindingPlugins.DataValidators.ToList();
        try
        {
            var app = (Application)Activator.CreateInstance(appType)!;
            app.ApplicationLifetime = lifetime;
            app.Initialize();
            app.OnFrameworkInitializationCompleted();
            return (app, state);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw new InvalidOperationException($"{appType.Name} could not start: {ex.InnerException.Message}", ex.InnerException);
        }
        finally
        {
            BindingPlugins.DataValidators.Clear();
            foreach (var validator in validators)
            {
                BindingPlugins.DataValidators.Add(validator);
            }
        }
    }

    /// <summary>
    /// For an app that only sets a main window: the window's content, loaded from its XAML, with a new view model
    /// named after it, such as <c>MainWindowViewModel</c> for <c>MainWindow</c>.
    /// </summary>
    private static Control? FromMainWindow(Assembly assembly, Type[] types, out string? note)
    {
        note = null;
        var windows = types.Where(t => typeof(Window).IsAssignableFrom(t) && !t.IsAbstract).ToList();
        var window = windows.FirstOrDefault(t => t.Name == "MainWindow") ?? windows.FirstOrDefault();
        if (window is null || XamlOf(assembly, window) is not { } document)
        {
            return null;
        }

        var (xaml, path) = document;
        var content = (Control)AppXamlLoader.Load(ContentOf(xaml), path, assembly);
        var viewModel = types.FirstOrDefault(t => t.Name == window.Name + "ViewModel" && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) is not null);
        if (viewModel is not null)
        {
            content.DataContext = Activator.CreateInstance(viewModel);
        }

        note = viewModel is null
            ? $"Showing the content of {window.Name}. The window's own code does not run here; set MainView for ISingleViewApplicationLifetime in App.axaml.cs to show a view as the app creates it."
            : $"Showing the content of {window.Name}, with a new {viewModel.Name}. The window's own code does not run here; set MainView for ISingleViewApplicationLifetime in App.axaml.cs to show a view as the app creates it.";
        return content;
    }

    /// <summary>The XAML TabStudio's generator compiled into the assembly for a class, with the file's path in the app.</summary>
    internal static (string Xaml, string Path)? XamlOf(Assembly assembly, Type type) =>
        assembly.GetType($"{AvaloniaXamlGenerator.GeneratedNamespace}.TabStudioXaml")
            ?.GetField("Documents", BindingFlags.NonPublic | BindingFlags.Static)
            ?.GetValue(null) is IDictionary documents
            && documents[type] is ValueTuple<string, string> document
            ? document
            : null;

    /// <summary>
    /// Turns a window's XAML into the XAML of a content control with the same content, namespaces, styles and
    /// resources, without the window's class and window-only properties.
    /// </summary>
    internal static string ContentOf(string windowXaml)
    {
        var document = XDocument.Parse(windowXaml);
        var window = document.Root!;
        var control = new XElement(XName.Get("ContentControl", AvaloniaNamespace));
        foreach (var attribute in window.Attributes())
        {
            var keep = attribute.IsNamespaceDeclaration
                || attribute.Name.LocalName is "DataType" or "Ignorable" or "Background" or "Foreground" or "FontFamily" or "FontSize" or "Padding"
                || attribute.Name.NamespaceName.Contains("expression/blend", StringComparison.Ordinal);
            if (keep && attribute.Name.LocalName != "Class")
            {
                control.Add(new XAttribute(attribute));
            }
        }

        foreach (var node in window.Nodes())
        {
            if (node is XElement element && element.Name.LocalName.StartsWith(window.Name.LocalName + ".", StringComparison.Ordinal))
            {
                var property = element.Name.LocalName[(window.Name.LocalName.Length + 1)..];
                if (property is "Styles" or "Resources" or "DataTemplates")
                {
                    control.Add(new XElement(XName.Get($"ContentControl.{property}", AvaloniaNamespace), element.Nodes()));
                }

                continue;
            }

            control.Add(node);
        }

        return control.ToString();
    }

    /// <summary>The types of an assembly that load, skipping those whose dependencies are missing.</summary>
    internal static Type[] LoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return [.. ex.Types.OfType<Type>()];
        }
    }

    /// <summary>An exception thrown by the app's code on the UI thread ends the app instead of TabStudio.</summary>
    private void OnUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (e.Exception.ToString().Contains(_assemblyName, StringComparison.Ordinal))
        {
            e.Handled = true;
            Services.CrashLog.Claimed.AddOrUpdate(e.Exception, this);
            Crashed?.Invoke(this, e.Exception);
        }
    }
}
