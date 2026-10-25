using Microsoft.CodeAnalysis;
using TabStudio.Compilation.Wpf;
using TabStudio.Compilation.Wpf.Xaml;
using TabStudio.Core.Workspaces;
using TabStudio.Wpf.Building;
using RoslynCompilation = Microsoft.CodeAnalysis.Compilation;

namespace TabStudio.Wpf.Browser;

/// <summary>A window class of a WPF app, from the XAML file it is declared in.</summary>
/// <param name="RelativePath">The XAML file's path in the project, such as <c>MainWindow.xaml</c>.</param>
/// <param name="ClassName">The class's full name, such as <c>WpfApp1.MainWindow</c>.</param>
/// <param name="CanCreate">Whether it has a public parameterless constructor, so a <c>StartupUri</c> can open it.</param>
/// <param name="Background">The background its XAML sets, as a CSS color, which OpenSilver's window does not paint.</param>
public sealed record WpfWindowClass(string RelativePath, string ClassName, bool CanCreate, string? Background);

/// <summary>
/// How a WPF app starts, as WPF starts it, for the browser host: the application class of its <c>App.xaml</c>, the
/// window its <c>StartupUri</c> opens, and every window class its XAML declares.
/// </summary>
/// <param name="ApplicationClass">The application class's full name, or <see langword="null"/> when the app has no <c>ApplicationDefinition</c>.</param>
/// <param name="StartupUri">The <c>StartupUri</c> of <c>App.xaml</c>, relative to it, or <see langword="null"/>.</param>
/// <param name="Windows">The window classes.</param>
public sealed record WpfStartupInfo(string? ApplicationClass, string? StartupUri, IReadOnlyList<WpfWindowClass> Windows)
{
    /// <summary>Reads a project's XAML, with the types of its build.</summary>
    /// <param name="project">The WPF project.</param>
    /// <param name="compilation">A compilation that references the project's assembly and OpenSilver.</param>
    /// <param name="appAssemblyName">The project's assembly.</param>
    public static WpfStartupInfo Read(ProjectInfo project, RoslynCompilation compilation, string appAssemblyName)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(compilation);
        var window = compilation.GetTypeByMetadataName("System.Windows.Window");
        var app = compilation.SourceModule.ReferencedAssemblySymbols.FirstOrDefault(a => a.Name == appAssemblyName);
        string? applicationClass = null;
        string? startupUri = null;
        var windows = new List<WpfWindowClass>();
        foreach (var item in WpfProject.XamlItems(project))
        {
            XamlDocument document;
            try
            {
                document = XamlDocument.Parse(File.ReadAllText(item.FilePath));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            if (document.Root is not { } root || root.Attribute(XamlNamespaces.Language, "Class")?.Value.Trim() is not { Length: > 0 } className)
            {
                continue;
            }

            if (item.Kind == XamlItemKind.ApplicationDefinition)
            {
                applicationClass = className;
                startupUri = root.Attribute("StartupUri")?.Value.Trim() is { Length: > 0 } uri ? uri : null;
                continue;
            }

            if (app?.GetTypeByMetadataName(className) is { } type && XamlTypeSystem.DerivesFrom(type, window))
            {
                var canCreate = !type.IsAbstract && type.InstanceConstructors.Any(c => c.Parameters.Length == 0 && c.DeclaredAccessibility == Accessibility.Public);
                windows.Add(new WpfWindowClass(item.RelativePath, className, canCreate, WindowXamlSettings.BackgroundColor(root)));
            }
        }

        return new WpfStartupInfo(applicationClass, startupUri, windows);
    }

    /// <summary>The window a <c>StartupUri</c> opens: a path relative to <c>App.xaml</c>, or a <c>pack://</c> or component address.</summary>
    /// <param name="startupUri">The address.</param>
    public WpfWindowClass? WindowAt(string startupUri)
    {
        ArgumentNullException.ThrowIfNull(startupUri);
        var path = startupUri.Replace('\\', '/');
        foreach (var prefix in new[] { "pack://application:,,,/", "pack://application:,,," })
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                path = path[prefix.Length..];
            }
        }

        var component = path.IndexOf(";component/", StringComparison.OrdinalIgnoreCase);
        if (component >= 0)
        {
            path = path[(component + ";component/".Length)..];
        }

        path = path.TrimStart('/');
        return Windows.FirstOrDefault(w => string.Equals(w.RelativePath, path, StringComparison.OrdinalIgnoreCase));
    }
}
