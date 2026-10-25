using System.Reflection;
using Avalonia;
using TabStudio.Compilation.Building;
using TabStudio.Compilation.Running;

namespace TabStudio.App.Previews;

/// <summary>
/// One build of an Avalonia project, loaded for the XAML designer: its views, view models and controls, so the
/// XAML being edited can use them. Nothing of the app runs except what the designed XAML creates.
/// </summary>
internal sealed class XamlDesignBuild
{
    private readonly Dictionary<string, Type> _types;

    private XamlDesignBuild(LoadedProgram program, Type[] types, DateTime builtAt)
    {
        Program = program;
        BuiltAt = builtAt;
        _types = types.Where(t => t.FullName is not null).GroupBy(t => t.FullName!, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
    }

    /// <summary>The loaded build.</summary>
    public LoadedProgram Program { get; }

    /// <summary>The project's own assembly.</summary>
    public Assembly Assembly => Program.EntryAssembly;

    /// <summary>When the build was made.</summary>
    public DateTime BuiltAt { get; }

    /// <summary>Loads a build, which stays loaded: the XAML compiled from it references its types.</summary>
    /// <param name="build">A successful build of the project.</param>
    /// <param name="packageAssemblies">The project's package assemblies.</param>
    public static XamlDesignBuild Load(BuildResult build, IReadOnlyList<string> packageAssemblies)
    {
        ArgumentNullException.ThrowIfNull(build);
        var entry = build.EntryAssembly ?? throw new ArgumentException("The build failed.", nameof(build));
        AppXamlLoader.Install();
        var program = LoadedProgram.Load(entry, build.Dependencies, packageAssemblies, AvaloniaAppHost.IsSharedWithHost, unloadable: false);
        program.LoadAll();
        return new XamlDesignBuild(program, AvaloniaAppHost.LoadableTypes(program.EntryAssembly), DateTime.Now);
    }

    /// <summary>A type of the project, such as <c>HelloAvalonia.ViewModels.MainViewModel</c>.</summary>
    /// <param name="fullName">The type's full name.</param>
    public Type? FindType(string fullName) => _types.GetValueOrDefault(fullName);

    /// <summary>
    /// Starts a new instance of the app's <c>App</c> for its theme, without its startup code: only <c>Initialize</c>,
    /// which loads <c>App.axaml</c>. UI thread only.
    /// </summary>
    /// <returns>The app, or <see langword="null"/> when it has no <c>App</c> or it could not start.</returns>
    public Application? CreateApp()
    {
        var appType = _types.Values.FirstOrDefault(t => typeof(Application).IsAssignableFrom(t) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) is not null);
        if (appType is null)
        {
            return null;
        }

        try
        {
            var app = (Application)Activator.CreateInstance(appType)!;
            app.Initialize();
            return app;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The views are still shown, in Avalonia's default look.
            return null;
        }
    }
}
