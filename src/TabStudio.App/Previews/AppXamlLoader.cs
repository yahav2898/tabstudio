using System.Reflection;
using System.Runtime.Loader;
using Avalonia.Markup.Xaml;
using TabStudio.Compilation.Xaml;

namespace TabStudio.App.Previews;

/// <summary>
/// Loads the XAML of Avalonia apps built in TabStudio, for the app preview and the XAML designer alike: with the
/// types of the app's own build, its <c>avares://</c> files, and Avalonia's run-time XAML loader.
/// </summary>
/// <remarks>
/// The generated code of an app calls the loader in <see cref="AppContext"/> under
/// <see cref="AvaloniaXamlGenerator.LoaderKey"/> from <c>InitializeComponent</c>; <see cref="Install"/> puts this one
/// there. While the designer creates a view of its own XAML (<see cref="SuppressCompiledXaml"/>), the XAML compiled
/// into the app is not loaded, so the view gets only the XAML being designed.
/// </remarks>
internal static class AppXamlLoader
{
    [ThreadStatic]
    private static int _suppressed;

    /// <summary>Makes the generated code of apps load their XAML with this loader.</summary>
    public static void Install() =>
        AppContext.SetData(AvaloniaXamlGenerator.LoaderKey, new Action<object, string, string>(LoadCompiled));

    /// <summary>
    /// Loads XAML: into <paramref name="root"/> when given, otherwise into a new object of the XAML's root type.
    /// Types are resolved from the app's build, including <c>clr-namespace:</c> types found by name, and
    /// addresses such as <c>/Assets/logo.png</c> from the file's <c>avares://</c> address, as in a built app.
    /// </summary>
    /// <param name="xaml">The XAML.</param>
    /// <param name="path">The file's path in the app, such as <c>/Views/MainView.axaml</c>.</param>
    /// <param name="assembly">The app's assembly the XAML belongs to.</param>
    /// <param name="root">The object to load into, such as the view whose class the XAML belongs to.</param>
    /// <param name="designMode">Whether to honor <c>Design.*</c> and <c>d:</c> properties, as a designer does.</param>
    public static object Load(string xaml, string path, Assembly assembly, object? root = null, bool designMode = false)
    {
        RuntimeXamlTypes.PrepareFor(assembly);
        RuntimeXamlTypes.CountLoad();
        RuntimeAssets.UseFor(assembly);
        var address = new Uri($"avares://{assembly.GetName().Name}{path}");
        using var scope = AssemblyLoadContext.EnterContextualReflection(assembly);
        return AvaloniaRuntimeXamlLoader.Load(xaml, assembly, root, address, designMode);
    }

    /// <summary>
    /// Until disposed, <c>InitializeComponent</c> on this thread does not load the XAML compiled into the app, so a
    /// designer can create a view and load the XAML being edited into it instead.
    /// </summary>
    public static IDisposable SuppressCompiledXaml()
    {
        _suppressed++;
        return new Suppression();
    }

    /// <summary>What the generated <c>InitializeComponent</c> and <c>AvaloniaXamlLoader.Load(this)</c> call.</summary>
    private static void LoadCompiled(object root, string xaml, string path)
    {
        if (_suppressed == 0)
        {
            Load(xaml, path, root.GetType().Assembly, root);
        }
    }

    private sealed class Suppression : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _suppressed--;
            }
        }
    }
}
