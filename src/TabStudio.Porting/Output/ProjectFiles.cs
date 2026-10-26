using System.Globalization;
using System.Reflection;
using System.Security;
using System.Text;

namespace TabStudio.Porting.Output;

/// <summary>
/// The files every ported project gets: the project file and Program.cs as TabStudio's Avalonia template writes
/// them, the support code the app uses, and the original project's images and fonts.
/// </summary>
internal static class ProjectFiles
{
    /// <summary>Properties of the original project that mean the same in the new one.</summary>
    private static readonly string[] KeptProperties =
    [
        "LangVersion", "Version", "AssemblyVersion", "FileVersion", "Company", "Product", "Copyright", "Authors", "Description",
        "ApplicationIcon",
    ];

    /// <summary>Packages that exist only for WPF or Windows Forms, which the new project leaves out.</summary>
    private static readonly string[] WindowsOnlyPackages =
    [
        "Microsoft.Xaml.Behaviors.Wpf", "MaterialDesignThemes", "MaterialDesignColors", "MahApps.Metro", "Extended.Wpf.Toolkit",
        "ModernWpfUI", "WPF-UI", "Microsoft.Toolkit.Wpf", "Microsoft.Windows.Compatibility", "System.Windows.Forms",
        "Microsoft.WindowsAPICodePack", "DevExpress", "Telerik", "Syncfusion", "Microsoft.Web.WebView2",
    ];

    /// <summary>The new project file.</summary>
    /// <param name="context">The port.</param>
    public static PortedFile ProjectFile(PortContext context)
    {
        var source = context.Source;
        var report = context.Report(context.Options.ProjectName + ".csproj");
        var framework = source.GetProperty("TargetFramework") ?? source.GetProperty("TargetFrameworks") ?? "net10.0";
        var text = new StringBuilder();
        text.Append("<Project Sdk=\"Microsoft.NET.Sdk\">\n\n");
        text.Append("  <PropertyGroup>\n");
        text.Append("    <OutputType>WinExe</OutputType>\n");
        text.Append("    <TargetFramework>net10.0</TargetFramework>\n");
        if (context.RootNamespace != context.Options.ProjectName)
        {
            text.Append(CultureInfo.InvariantCulture, $"    <RootNamespace>{Escape(context.RootNamespace)}</RootNamespace>\n");
        }

        text.Append(CultureInfo.InvariantCulture, $"    <ImplicitUsings>{(source.ImplicitUsings ? "enable" : "disable")}</ImplicitUsings>\n");
        text.Append(CultureInfo.InvariantCulture, $"    <Nullable>{(source.NullableEnabled ? "enable" : "disable")}</Nullable>\n");
        foreach (var name in KeptProperties)
        {
            if (source.GetProperty(name) is { Length: > 0 } value)
            {
                text.Append(CultureInfo.InvariantCulture, $"    <{name}>{Escape(value)}</{name}>\n");
            }
        }

        text.Append(CultureInfo.InvariantCulture, $"    <!-- Ported from {(source.Kind == PortSourceKind.Wpf ? "WPF" : "Windows Forms")}: bindings find their paths as the app runs, as they did there. -->\n");
        text.Append("    <AvaloniaUseCompiledBindingsByDefault>false</AvaloniaUseCompiledBindingsByDefault>\n");
        text.Append("  </PropertyGroup>\n\n");

        text.Append("  <ItemGroup>\n");
        text.Append("    <AvaloniaResource Include=\"Assets\\**\" />\n");
        foreach (var resource in source.Resources.Where(r => !r.StartsWith("Assets/", StringComparison.Ordinal)))
        {
            text.Append(CultureInfo.InvariantCulture, $"    <AvaloniaResource Include=\"{Escape(resource.Replace('/', '\\'))}\" />\n");
        }

        text.Append("  </ItemGroup>\n\n");

        text.Append("  <ItemGroup>\n");
        var version = context.Options.AvaloniaVersion;
        text.Append(CultureInfo.InvariantCulture, $"    <PackageReference Include=\"Avalonia\" Version=\"{version}\" />\n");
        text.Append(CultureInfo.InvariantCulture, $"    <PackageReference Include=\"Avalonia.Themes.Fluent\" Version=\"{version}\" />\n");
        if (context.UsesDataGrid)
        {
            text.Append(CultureInfo.InvariantCulture, $"    <PackageReference Include=\"Avalonia.Controls.DataGrid\" Version=\"{context.Options.DataGridVersion}\" />\n");
            report.Converted("DataGrid → the Avalonia.Controls.DataGrid package");
        }

        if (context.UsesConfiguration)
        {
            text.Append("    <PackageReference Include=\"System.Configuration.ConfigurationManager\" Version=\"10.0.0\" />\n");
        }

        foreach (var package in source.Packages)
        {
            if (WindowsOnlyPackages.Any(p => package.Name.StartsWith(p, StringComparison.OrdinalIgnoreCase)) || package.Name.Contains("Wpf", StringComparison.OrdinalIgnoreCase) || package.Name.Contains("WinForms", StringComparison.OrdinalIgnoreCase))
            {
                report.Note(1, $"The package {package.Name}", "It is made for WPF or Windows Forms, so the new project leaves it out; use its Avalonia counterpart, if it has one");
                continue;
            }

            if (package.Name.StartsWith("Avalonia", StringComparison.Ordinal))
            {
                continue;
            }

            text.Append(package.Version is { } packageVersion
                ? $"    <PackageReference Include=\"{Escape(package.Name)}\" Version=\"{Escape(packageVersion)}\" />\n"
                : $"    <PackageReference Include=\"{Escape(package.Name)}\" />\n");
        }

        text.Append("  </ItemGroup>\n\n");
        text.Append("</Project>\n");

        report.Converted($"{(source.Kind == PortSourceKind.Wpf ? "UseWPF" : "UseWindowsForms")} project → Avalonia project");
        if (!string.Equals(framework, "net10.0", StringComparison.OrdinalIgnoreCase))
        {
            report.Converted($"{framework} → net10.0");
        }

        return new PortedFile(context.Options.ProjectName + ".csproj", text.ToString(), null, null, PortedFileKind.Project, report.Conversions());
    }

    /// <summary>Program.cs as TabStudio's Avalonia template writes it.</summary>
    /// <param name="context">The port.</param>
    public static PortedFile Program(PortContext context)
    {
        var text = $$"""
            using Avalonia;

            namespace {{context.RootNamespace}};

            internal static class Program
            {
                // TabStudio shows this app beside the code. To run it on a PC, add the Avalonia.Desktop package
                // and call UsePlatformDetect() before StartWithClassicDesktopLifetime.
                [STAThread]
                public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

                public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().LogToTrace();
            }

            """;
        return new PortedFile("Program.cs", text, null, null, PortedFileKind.Code, [new PortConversion("Avalonia's entry point, as TabStudio's Avalonia template has it", 1)]);
    }

    /// <summary>The support code the app uses.</summary>
    /// <param name="context">The port.</param>
    public static IEnumerable<PortedFile> Support(PortContext context)
    {
        var needs = context.Support;
        var files = new List<(string Resource, string Path)>();
        if ((needs & SupportNeeds.Windows) != 0)
        {
            files.Add(("WindowView.cs", "WindowView.cs"));
            files.Add(("ScreenHost.cs", "ScreenHost.cs"));
        }

        if ((needs & (SupportNeeds.WpfWindow | SupportNeeds.WpfMessageBox)) != 0 && context.Source.Kind == PortSourceKind.Wpf)
        {
            files.Add(("AppWindow.cs", "AppWindow.cs"));
        }

        if ((needs & SupportNeeds.Form) != 0)
        {
            files.Add(("AppForm.cs", "AppForm.cs"));
        }

        if ((needs & (SupportNeeds.WpfMessageBox | SupportNeeds.FormsMessageBox)) != 0)
        {
            files.Add(("MessageDialog.cs", "MessageDialog.cs"));
            files.Add(((needs & SupportNeeds.WpfMessageBox) != 0 ? "MessageBox.Wpf.cs" : "MessageBox.Forms.cs", "MessageBox.cs"));
        }

        if ((needs & SupportNeeds.CommandManager) != 0)
        {
            files.Add(("CommandManager.cs", "CommandManager.cs"));
        }

        if ((needs & SupportNeeds.GridRows) != 0)
        {
            files.Add(("GridRows.cs", "GridRows.cs"));
        }

        if ((needs & SupportNeeds.Styles) != 0)
        {
            files.Add(("PortingStyles.axaml", "PortingStyles.axaml"));
        }

        foreach (var (resource, path) in files)
        {
            var text = Read(resource).Replace("$rootnamespace$", context.RootNamespace, StringComparison.Ordinal);
            yield return new PortedFile("Porting/" + path, text, null, null, PortedFileKind.Support, []);
        }
    }

    /// <summary>The original project's images, fonts and icons, copied.</summary>
    /// <param name="context">The port.</param>
    public static IEnumerable<PortedFile> Resources(PortContext context)
    {
        foreach (var resource in context.Source.Resources)
        {
            var from = context.Source.Directory is { } directory ? Path.Combine(directory, resource.Replace('/', Path.DirectorySeparatorChar)) : null;
            yield return new PortedFile(resource, null, from, resource, PortedFileKind.Resource, []);
        }
    }

    /// <summary>A support file, from the library's resources.</summary>
    /// <param name="name">The file, such as <c>WindowView.cs</c>.</param>
    internal static string Read(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Support/Porting/" + name)
            ?? throw new InvalidOperationException($"The support file {name} is missing from the library.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string Escape(string value) => SecurityElement.Escape(value) ?? value;
}
