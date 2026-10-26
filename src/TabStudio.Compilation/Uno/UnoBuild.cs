using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using TabStudio.Core.Workspaces;
using TsProject = TabStudio.Core.Workspaces.ProjectInfo;

namespace TabStudio.Compilation.Uno;

/// <summary>
/// How TabStudio builds Uno Platform and WinUI 3 projects: as Uno.Sdk builds an app for Uno's Skia renderer on the
/// desktop, which is what TabStudio's host runs, with TabStudio's host in place of the desktop's. The values follow
/// what Uno.Sdk hands the C# compiler and Uno's XAML generator for <c>net10.0-desktop</c>, recorded from a real build.
/// </summary>
/// <remarks>
/// <para>
/// Uno's XAML generator (<c>Uno.UI.SourceGenerators</c>, from the Uno.WinUI package) runs inside TabStudio's Roslyn
/// as it runs inside the SDK's: the project's <c>.xaml</c> files are additional files, with the item group each
/// belongs to (<c>ApplicationDefinition</c> for <c>App.xaml</c>, <c>Page</c> for the others, <c>PRIResource</c> for
/// <c>.resw</c> files), and the build properties it reads come in the generated analyzer configuration.
/// </para>
/// <para>
/// Each platform's head folder (<c>Platforms/</c>) and the files named for another platform (<c>*.Android.cs</c>,
/// <c>*.wasm.cs</c>, …) are left out, as Uno.Sdk leaves them out of a desktop build; the desktop head's
/// <c>Program.cs</c> is too, because TabStudio's host starts the app instead.
/// </para>
/// </remarks>
public static class UnoBuild
{
    /// <summary>The target framework the generator sees: Uno's desktop target, which renders with Skia.</summary>
    public const string TargetFramework = "net10.0-desktop";

    /// <summary>The item group of an app's <c>App.xaml</c>.</summary>
    public const string ApplicationDefinition = "ApplicationDefinition";

    /// <summary>The item group of the other <c>.xaml</c> files.</summary>
    public const string Page = "Page";

    /// <summary>The item group of <c>.resw</c> string resources.</summary>
    public const string PriResource = "PRIResource";

    /// <summary>
    /// The conditional compilation symbols of a desktop Uno app, besides the SDK's, as Uno.Sdk and Uno's packages define
    /// them. <c>__UNO_SKIA_TABSTUDIO__</c> is TabStudio's, in the manner of each desktop host's own.
    /// </summary>
    public static IReadOnlyList<string> Symbols { get; } =
    [
        "DESKTOP", "DESKTOP1_0", "DESKTOP1_0_OR_GREATER",
        "HAS_UNO", "__UNO__", "HAS_UNO_WINUI", "__UNO_WINUI__", "WINUI_WINDOWING",
        "UNO_HAS_FRAMEWORKELEMENT_MEASUREOVERRIDE", "UNO_HAS_NO_IDEPENDENCYOBJECT", "UNO_REFERENCE_API",
        "HAS_UNO_SKIA", "__DESKTOP__", "__UNO_SKIA__", "HAS_UNO_SKIA_TABSTUDIO", "__UNO_SKIA_TABSTUDIO__",
        "DISABLE_XAML_GENERATED_BREAK_ON_UNHANDLED_EXCEPTION",
    ];

    /// <summary>The global usings Uno.Sdk adds to projects with <c>ImplicitUsings</c> on (<c>Uno.Implicit.Namespaces.targets</c>).</summary>
    public static IReadOnlyList<string> ImplicitUsings { get; } =
    [
        "System.Windows.Input", "Windows.Networking.Connectivity", "Windows.Storage", "Microsoft.UI.Xaml",
        "Microsoft.UI.Xaml.Controls", "Microsoft.UI.Xaml.Media", "Microsoft.UI.Xaml.Navigation", "Uno.UI",
        "Windows.ApplicationModel",
    ];

    /// <summary>File name endings Uno.Sdk compiles only for other platforms (<c>Uno.CrossTargeting.targets</c>).</summary>
    private static readonly string[] OtherPlatformSuffixes =
    [
        ".wasm.cs", ".browserwasm.cs", ".reference.cs", ".iOS.cs", ".iOSmacOS.cs", ".tvOS.cs", ".UIKit.cs", ".Apple.cs",
        ".Android.cs", ".WinAppSDK.cs",
    ];

    /// <summary>XAML namespaces the generator leaves out and keeps for the Skia renderer, as for Uno's desktop target.</summary>
    private const string ExcludedXamlNamespaces = "android,ios,macos,wasm,not_skia,not_netstdref,androidskia,iosskia,tvosskia,wasmskia,win,not_mux";

    private const string IncludedXamlNamespaces = "not_android,not_ios,not_macos,not_wasm,skia,netstdref,not_androidskia,not_iosskia,not_tvosskia,not_wasmskia,xamarin,not_win,legacy";

    /// <summary>Whether a project builds against Uno Platform.</summary>
    /// <param name="project">The project, or <see langword="null"/>.</param>
    public static bool UsesUno(TsProject? project) => project is not null && UnoProjects.UsesUno(project);

    /// <summary>Whether a source file of an Uno project is compiled for the Skia renderer.</summary>
    /// <param name="project">The project.</param>
    /// <param name="path">Full path of a <c>.cs</c> file.</param>
    public static bool IsCompiled(TsProject project, string path)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(path);
        if (!UsesUno(project))
        {
            return true;
        }

        return !IsInPlatformsFolder(project, path)
            && !OtherPlatformSuffixes.Any(suffix => path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The item group of an additional file of an Uno project, or <see langword="null"/> when the generator does not read it.</summary>
    /// <param name="project">The project.</param>
    /// <param name="path">Full path of the file.</param>
    public static string? ItemGroupOf(TsProject project, string path)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(path);
        if (path.EndsWith(".resw", StringComparison.OrdinalIgnoreCase))
        {
            return PriResource;
        }

        if (!path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var relative = Path.GetRelativePath(project.DirectoryPath, path);
        return string.Equals(relative, "App.xaml", StringComparison.OrdinalIgnoreCase) ? ApplicationDefinition : Page;
    }

    /// <summary>The XAML and string resource files of an Uno project, which become additional files.</summary>
    /// <param name="project">The project.</param>
    /// <param name="allProjectDirectories">The folders of every project, whose files belong to them.</param>
    public static IReadOnlyList<string> EnumerateGeneratorInputs(TsProject project, IReadOnlyList<string> allProjectDirectories)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(allProjectDirectories);
        if (!UsesUno(project))
        {
            return [];
        }

        return ProjectSourceFiles.Enumerate(project, allProjectDirectories, ".xaml")
            .Concat(ProjectSourceFiles.Enumerate(project, allProjectDirectories, ".resw"))
            .Where(path => !IsInPlatformsFolder(project, path))
            .ToList();
    }

    /// <summary>Whether a file is in the project's <c>Platforms/</c> folder, which holds each platform's head.</summary>
    private static bool IsInPlatformsFolder(TsProject project, string path)
    {
        var relative = Path.GetRelativePath(project.DirectoryPath, path);
        return string.Equals(relative.Split(Path.DirectorySeparatorChar, 2)[0], "Platforms", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Adds Uno's symbols to a project's parse options.</summary>
    /// <param name="project">The project.</param>
    /// <param name="options">The options without them.</param>
    public static CSharpParseOptions WithSymbols(TsProject? project, CSharpParseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return UsesUno(project) ? options.WithPreprocessorSymbols(options.PreprocessorSymbolNames.Concat(Symbols).Distinct(StringComparer.Ordinal).ToArray()) : options;
    }

    /// <summary>
    /// The build properties Uno's XAML generator reads, over the project's own, as Uno.Sdk sets them for a desktop
    /// build; and TabStudio's: <c>UnoEnableAlcAppSupport</c>, because the preview runs each build of an app in a load
    /// context of its own.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <param name="preprocessorSymbols">The project's symbols, for <c>DefineConstantsProperty</c>.</param>
    public static IReadOnlyDictionary<string, string> GeneratorProperties(TsProject project, IEnumerable<string> preprocessorSymbols)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(preprocessorSymbols);
        var intermediate = $"obj/Debug/{TargetFramework}/";
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["TargetFramework"] = TargetFramework,
            ["MSBuildProjectFullPath"] = project.FilePath,
            ["RootNamespace"] = project.GetProperty("RootNamespace") ?? project.Name,
            ["AssemblyName"] = project.GetProperty("AssemblyName") ?? project.Name,
            ["DefineConstantsProperty"] = string.Join(',', preprocessorSymbols),
            ["Configuration"] = "Debug",
            ["Platform"] = "AnyCPU",
            ["OutputType"] = project.OutputType ?? "Library",
            ["IntermediateOutputPath"] = intermediate,
            ["BaseIntermediateOutputPath"] = "obj/",
            ["OutputPath"] = $"bin/Debug/{TargetFramework}/",
            ["AppendRuntimeIdentifierToOutputPath"] = "true",
            ["BuildingProject"] = "true",
            ["IsUnoHead"] = project.IsExecutable ? "true" : string.Empty,
            ["UnoRuntimeIdentifier"] = "Skia",
            ["DefaultLanguage"] = project.GetProperty("DefaultLanguage") ?? "en",
            ["UnoPlatformDefaultSymbolsFontFamily"] = "ms-appx:///Uno.Fonts.Fluent/Fonts/uno-fluentui-assets.ttf",
            ["ExcludeXamlNamespacesProperty"] = ExcludedXamlNamespaces,
            ["IncludeXamlNamespacesProperty"] = IncludedXamlNamespaces,
            ["UnoEnableImplicitXamlNamespaces"] = UnoProjects.UsesUnoSdk(project) ? "true" : string.Empty,
            ["UnoGlobalXamlNamespaceUri"] = "http://schemas.microsoft.com/winfx/2006/xaml/presentation/global",
            ["UnoEnableAlcAppSupport"] = "true",
            ["UnoDefaultFont"] = project.IsExecutable && UsesOpenSans(project) ? "OpenSans" : string.Empty,
            ["UseHostCompilerIfAvailable"] = "true",
            ["UnoPlatformTelemetryOptOut"] = "true",
        };
    }

    /// <summary>The generated analyzer configuration's section for one additional file, with the metadata the generator reads.</summary>
    /// <param name="path">Full path of the file.</param>
    /// <param name="itemGroup">Its item group.</param>
    public static string FileSection(string path, string itemGroup)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(itemGroup);
        var section = new StringBuilder();
        section.Append('[').Append(path).Append("]\n");
        section.Append("build_metadata.AdditionalFiles.SourceItemGroup = ").Append(itemGroup).Append('\n');
        section.Append("build_metadata.AdditionalFiles.UnoGenerateCodeBehind = \n");
        section.Append("build_metadata.AdditionalFiles.Link = \n");
        section.Append("build_metadata.AdditionalFiles.TargetPath = \n");
        section.Append("build_metadata.AdditionalFiles.FullPath = ").Append(path).Append('\n');
        section.Append("build_metadata.AdditionalFiles.Identity = ").Append(path).Append('\n');
        section.Append("build_metadata.AdditionalFiles.XamlRuntime = ").Append(itemGroup == ApplicationDefinition ? "WinUI" : string.Empty).Append('\n');
        return section.ToString();
    }

    /// <summary>
    /// What the build-time packages of an Uno.Sdk app would generate, which TabStudio does not restore: Uno.Resizetizer's
    /// <c>SetWindowIcon</c>, which only sets the icon of a Windows window, and Hot Design's <c>UseStudio</c>, Uno's
    /// visual designer for Visual Studio and VS Code, which the template calls in Debug builds.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <returns>The source, or <see langword="null"/> when the project does not need it.</returns>
    public static string? CreateBuildToolsSource(TsProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!UnoProjects.UsesUnoSdk(project) || !project.IsExecutable)
        {
            return null;
        }

        var rootNamespace = project.GetProperty("RootNamespace") is { Length: > 0 } root ? root : project.Name.Replace(' ', '_');
        var source = new StringBuilder("// <auto-generated/>\n");
        source.Append("// What Uno.Resizetizer and Uno.UI.HotDesign generate at build time, as TabStudio builds the app for its preview.\n");
        source.Append("#nullable enable\n");
        source.Append("using System.ComponentModel;\n\n");
        source.Append("namespace ").Append(EscapeNamespace(rootNamespace)).Append("\n{\n");
        source.Append("    [EditorBrowsable(EditorBrowsableState.Never)]\n");
        source.Append("    public static partial class WindowExtensions\n    {\n");
        source.Append("        /// <summary>Sets the window's icon on Windows; elsewhere, the platform shows the app's icon.</summary>\n");
        source.Append("        public static void SetWindowIcon(this global::Microsoft.UI.Xaml.Window window)\n        {\n        }\n    }\n}\n\n");
        source.Append("namespace Uno.Resizetizer\n{\n");
        source.Append("    [EditorBrowsable(EditorBrowsableState.Never)]\n");
        source.Append("    public static partial class __LegacyResizetizerSupport__\n    {\n    }\n}\n\n");
        source.Append("[EditorBrowsable(EditorBrowsableState.Never)]\n");
        source.Append("internal static class __TabStudioHotDesignSupport__\n{\n");
        source.Append("    /// <summary>Hot Design, Uno's visual designer, runs in Visual Studio and VS Code; TabStudio has its own designer.</summary>\n");
        source.Append("    internal static global::Microsoft.UI.Xaml.Window UseStudio(this global::Microsoft.UI.Xaml.Window window, bool isEnabled = true) => window;\n}\n");
        return source.ToString();
    }

    /// <summary>The source of Uno.Sdk's implicit global usings, for a project that turns <c>ImplicitUsings</c> on.</summary>
    /// <param name="project">The project.</param>
    public static string? CreateImplicitUsingsSource(TsProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!UnoProjects.UsesUnoSdk(project) || !project.ImplicitUsings)
        {
            return null;
        }

        var source = new StringBuilder("// <auto-generated/>\n");
        foreach (var name in ImplicitUsings.Where(name => !project.RemovedUsings.Contains(name, StringComparer.Ordinal)))
        {
            source.Append("global using ").Append(name).Append(";\n");
        }

        return source.ToString();
    }

    private static bool UsesOpenSans(TsProject project) =>
        project.GetProperty("UnoDefaultFont") is not { Length: > 0 } font || font.Trim().Equals("OpenSans", StringComparison.OrdinalIgnoreCase);

    /// <summary>A root namespace as C# writes it: <c>MyApp._1</c> for a project named <c>MyApp.1</c>.</summary>
    private static string EscapeNamespace(string name) =>
        string.Join('.', name.Split('.').Select(part => part.Length > 0 && char.IsDigit(part[0]) ? "_" + part : part));
}
