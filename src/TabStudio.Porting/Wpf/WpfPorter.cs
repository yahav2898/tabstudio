using System.Xml.Linq;
using TabStudio.Porting.Output;
using TabStudio.Porting.Xaml;

namespace TabStudio.Porting.Wpf;

/// <summary>Ports a WPF project: its XAML, then its code, then the application, which needs to know what the rest uses.</summary>
internal static class WpfPorter
{
    /// <summary>Converts the project's XAML and code.</summary>
    /// <param name="context">The port.</param>
    /// <returns>The converted files of the new project.</returns>
    public static List<PortedFile> Port(PortContext context)
    {
        var project = new WpfProject(context);
        var converted = new List<ConvertedXaml>();
        string? applicationPath = null;
        foreach (var path in project.Xaml.Keys.Order(StringComparer.Ordinal))
        {
            if (project.Xaml[path].Root?.Name == XamlNamespaces.Wpf + "Application")
            {
                applicationPath = path;
                continue;
            }

            converted.Add(WpfXamlConverter.Convert(project, path));
        }

        // The application last: its styles include the data grid's theme and the port's styles when the rest uses them.
        var application = applicationPath is null ? null : WpfXamlConverter.Convert(project, applicationPath);
        var files = new List<PortedFile>();
        foreach (var (path, text) in context.Source.Files)
        {
            if (path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase) && !project.Xaml.ContainsKey(path))
            {
                var report = context.Report(WpfProject.TargetPath(path));
                report.Note(1, "XAML that is not well-formed", "The converter could not read it; fix the XML, and convert it by hand");
                files.Add(new PortedFile(WpfProject.TargetPath(path), text, null, path, PortedFileKind.Xaml, []));
            }
            else if (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                if (IsOnlyThemeInfo(path, text))
                {
                    context.Report(context.Options.ProjectName + ".csproj").Converted("AssemblyInfo.cs with only ThemeInfo removed: Avalonia has no theme dictionaries per assembly");
                    continue;
                }

                var code = WpfCodeConverter.Convert(project, path, text);
                var target = WpfProject.TargetPath(path);
                files.Add(new PortedFile(target, code, null, path, PortedFileKind.Code, context.Report(target).Conversions()));
            }
        }

        if (application is not null)
        {
            WpfXamlConverter.FinishApplication(application.Document, context);
            converted.Add(application);
            if (!context.Source.Files.ContainsKey(applicationPath + ".cs"))
            {
                files.Add(ApplicationCode(context, project, application));
            }
        }
        else
        {
            files.AddRange(DefaultApplication(context));
        }

        files.InsertRange(0, converted.Select(XamlOutput.Write));
        return files;
    }

    /// <summary>Whether a file is the WPF template's AssemblyInfo.cs, which only declares where theme dictionaries are.</summary>
    private static bool IsOnlyThemeInfo(string path, string text) =>
        Path.GetFileName(path) == "AssemblyInfo.cs" && text.Contains("ThemeInfo", StringComparison.Ordinal)
        && !text.Split('\n').Any(l => l.TrimStart().StartsWith("[assembly:", StringComparison.Ordinal) && !l.Contains("ThemeInfo", StringComparison.Ordinal));

    /// <summary>App.axaml.cs for an App.xaml that had no code-behind.</summary>
    private static PortedFile ApplicationCode(PortContext context, WpfProject project, ConvertedXaml application)
    {
        var className = application.Document.Root?.Attribute(XamlNamespaces.Xaml + "Class")?.Value ?? context.RootNamespace + ".App";
        var stub = $"namespace {className[..Math.Max(0, className.LastIndexOf('.'))]}\n{{\n    public partial class {className[(className.LastIndexOf('.') + 1)..]} : System.Windows.Application\n    {{\n    }}\n}}\n";
        var path = application.SourcePath + ".cs";
        var code = WpfCodeConverter.Convert(project, path, stub);
        var target = WpfProject.TargetPath(path);
        return new PortedFile(target, code, null, null, PortedFileKind.Code, context.Report(target).Conversions());
    }

    /// <summary>App.axaml and App.axaml.cs for a project without an application definition.</summary>
    private static IEnumerable<PortedFile> DefaultApplication(PortContext context)
    {
        var ns = context.RootNamespace;
        var xaml = new XDocument(new XElement(XamlNamespaces.Avalonia + "Application",
            new XAttribute("xmlns", XamlNamespaces.Avalonia.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "x", XamlNamespaces.Xaml.NamespaceName),
            new XAttribute(XamlNamespaces.Xaml + "Class", ns + ".App"),
            new XAttribute("RequestedThemeVariant", "Light")));
        WpfXamlConverter.FinishApplication(xaml, context);
        var report = context.Report("App.axaml");
        report.Note(1, "No application definition", "The project had no App.xaml; open its first window in App.axaml.cs with WindowView.ShowMain");
        yield return new PortedFile("App.axaml", XamlWriter.Write(xaml, new XamlFormat(2, SpaceBeforeSlash: true), out _), null, null, PortedFileKind.Xaml, []);
        yield return new PortedFile("App.axaml.cs", $$"""
            using Avalonia;
            using Avalonia.Markup.Xaml;

            namespace {{ns}};

            public partial class App : Application
            {
                public override void Initialize() => AvaloniaXamlLoader.Load(this);
            }

            """, null, null, PortedFileKind.Code, []);
    }
}
