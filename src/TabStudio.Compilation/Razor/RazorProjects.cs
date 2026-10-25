using System.Text;
using TabStudio.Core.Workspaces;
using TsProject = TabStudio.Core.Workspaces.ProjectInfo;

namespace TabStudio.Compilation.Razor;

/// <summary>
/// What the Razor SDK (<c>Microsoft.NET.Sdk.Razor</c>, imported by the Web and Blazor WebAssembly SDKs) decides about a
/// project before the compiler runs, which TabStudio decides itself: which projects compile Razor, which files they
/// compile, and the language version and assembly attributes that come with them.
/// </summary>
/// <remarks>
/// It follows <c>Sdk.Razor.CurrentVersion.targets</c> and <c>Microsoft.NET.Sdk.Razor.SourceGenerators.targets</c> of the
/// .NET SDK 10.0.1xx, whose Razor compiler TabStudio runs (see <c>Razor/Compiler/NOTICE.txt</c>).
/// </remarks>
public static class RazorProjects
{
    /// <summary>The extension of Razor components.</summary>
    public const string ComponentExtension = ".razor";

    /// <summary>The extension of Razor views and pages.</summary>
    public const string ViewExtension = ".cshtml";

    /// <summary>The SDKs that import the Razor SDK.</summary>
    private static readonly string[] RazorSdks = ["Microsoft.NET.Sdk.Razor", "Microsoft.NET.Sdk.Web", "Microsoft.NET.Sdk.BlazorWebAssembly"];

    /// <summary>The MVC application part factory the SDK puts on assemblies that contain compiled views.</summary>
    private const string ApplicationPartFactory = "Microsoft.AspNetCore.Mvc.ApplicationParts.ConsolidatedAssemblyApplicationPartFactory, Microsoft.AspNetCore.Mvc.Razor";

    /// <summary>Whether a project compiles Razor: a C# project on the Razor, Web or Blazor WebAssembly SDK.</summary>
    /// <param name="project">The project.</param>
    public static bool UsesRazor(TsProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project.Language == ProjectLanguage.CSharp
            && RazorSdks.Any(sdk => string.Equals(project.Sdk, sdk, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Whether a file is a Razor component, view or page.</summary>
    /// <param name="path">A file name or path.</param>
    public static bool IsRazorFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var extension = Path.GetExtension(path);
        return string.Equals(extension, ComponentExtension, StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ViewExtension, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Whether a file is a Razor component.</summary>
    /// <param name="path">A file name or path.</param>
    public static bool IsComponent(string path) =>
        string.Equals(Path.GetExtension(path), ComponentExtension, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The Razor files of a project, sorted by path: its <c>.razor</c> components and <c>.cshtml</c> views and pages,
    /// found the way the SDK's default content items find them.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <param name="otherProjectDirectories">Folders of other projects in the workspace, which are never included.</param>
    public static IReadOnlyList<string> EnumerateRazorFiles(TsProject project, IEnumerable<string>? otherProjectDirectories = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!UsesRazor(project))
        {
            return [];
        }

        var others = otherProjectDirectories?.ToList() ?? [];
        return
        [
            .. ProjectSourceFiles.Enumerate(project, others, ComponentExtension)
                .Concat(ProjectSourceFiles.Enumerate(project, others, ViewExtension))
                .Order(StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// The Razor language version: the project's <c>RazorLangVersion</c>, or the one the SDK picks for its target
    /// framework, which is 9.0 for .NET 9 and 10.
    /// </summary>
    /// <param name="project">The project.</param>
    public static string LanguageVersion(TsProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (project.GetProperty("RazorLangVersion") is { Length: > 0 } explicitVersion)
        {
            return explicitVersion.Trim();
        }

        var framework = project.TargetFrameworks.Count > 0 ? project.TargetFrameworks[0] : ReferenceAssemblies.TargetFramework;
        return FrameworkMajorVersion(framework) switch
        {
            >= 9 => "9.0",
            8 => "8.0",
            7 => "7.0",
            6 => "6.0",
            5 => "5.0",
            _ => "3.0",
        };
    }

    /// <summary>
    /// The path of a Razor file relative to its project, with <c>/</c> separators, which decides the namespace and
    /// route of what it compiles to. The SDK passes it as the file's <c>TargetPath</c> metadata.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <param name="filePath">Full path of the file.</param>
    public static string TargetPath(TsProject project, string filePath)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(filePath);
        return Path.GetRelativePath(project.DirectoryPath, filePath).Replace(Path.DirectorySeparatorChar, '/');
    }

    /// <summary>
    /// The <c>TargetPath</c> as the compiler receives it: base64 of its UTF-8 bytes, which is how the SDK's
    /// <c>EncodeRazorInputItem</c> task keeps paths with special characters intact in the analyzer configuration.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <param name="filePath">Full path of the file.</param>
    public static string EncodedTargetPath(TsProject project, string filePath) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(TargetPath(project, filePath)));

    /// <summary>
    /// The scope of a component's or view's scoped CSS file (<c>Counter.razor.css</c> for <c>Counter.razor</c>), or
    /// <see langword="null"/> when it has none.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <param name="filePath">Full path of the Razor file.</param>
    public static string? CssScopeOf(TsProject project, string filePath)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(filePath);
        var css = filePath + ".css";
        return IsScopedCssEnabled(project) && File.Exists(css) ? CssScope.Compute(project, css) : null;
    }

    /// <summary>Whether the project has scoped CSS, which is on unless <c>ScopedCssEnabled</c> is <c>false</c>.</summary>
    /// <param name="project">The project.</param>
    public static bool IsScopedCssEnabled(TsProject project) =>
        !string.Equals(project.GetProperty("ScopedCssEnabled"), "false", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The source of the assembly attributes and types the Razor SDK adds to a project with Razor files, or
    /// <see langword="null"/> when it adds none.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item>Views and pages: <c>ProvideApplicationPartFactoryAttribute</c>, so MVC finds the views compiled into the assembly.</item>
    ///   <item>Apps on the Web SDK: an <c>ApplicationPartAttribute</c> for each referenced project that may hold controllers or views.</item>
    ///   <item>Components on .NET 10: the embedded <c>ValidatableTypeAttribute</c> and its global using, for Blazor form validation.</item>
    /// </list>
    /// </remarks>
    /// <param name="project">The project.</param>
    /// <param name="razorFiles">Its Razor files, from <see cref="EnumerateRazorFiles"/>.</param>
    /// <param name="referencedProjects">The projects it references.</param>
    public static string? CreateSdkSource(TsProject project, IReadOnlyList<string> razorFiles, IEnumerable<TsProject> referencedProjects)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(razorFiles);
        ArgumentNullException.ThrowIfNull(referencedProjects);

        var hasViews = razorFiles.Any(f => !IsComponent(f));
        var hasComponents = razorFiles.Any(IsComponent);
        var applicationParts = IsWebApp(project)
            ? referencedProjects.Where(MayHoldApplicationParts).Select(p => p.GetProperty("AssemblyName") ?? p.Name).ToList()
            : [];
        var embeddedValidation = hasComponents && FrameworkMajorVersion(TargetFramework(project)) >= 10
            && !string.Equals(project.GetProperty("GenerateEmbeddedValidatableTypeAttribute"), "false", StringComparison.OrdinalIgnoreCase);
        if (!hasViews && applicationParts.Count == 0 && !embeddedValidation)
        {
            return null;
        }

        var source = new StringBuilder("// <auto-generated/>\n");
        if (embeddedValidation && !string.Equals(project.GetProperty("IncludeEmbeddedValidationGlobalUsing"), "false", StringComparison.OrdinalIgnoreCase))
        {
            source.Append("global using global::Microsoft.Extensions.Validation.Embedded;\n");
        }

        if (hasViews)
        {
            source.Append("[assembly: global::Microsoft.AspNetCore.Mvc.ApplicationParts.ProvideApplicationPartFactoryAttribute(\"")
                .Append(ApplicationPartFactory).Append("\")]\n");
        }

        foreach (var part in applicationParts)
        {
            source.Append("[assembly: global::Microsoft.AspNetCore.Mvc.ApplicationParts.ApplicationPartAttribute(\"").Append(part).Append("\")]\n");
        }

        if (embeddedValidation)
        {
            source.Append("""
                namespace Microsoft.CodeAnalysis
                {
                    internal sealed partial class EmbeddedAttribute : global::System.Attribute
                    {
                    }
                }
                namespace Microsoft.Extensions.Validation.Embedded
                {
                    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
                    [global::System.AttributeUsage(global::System.AttributeTargets.Class)]
                    internal sealed class ValidatableTypeAttribute : global::System.Attribute
                    {
                    }
                }

                """.ReplaceLineEndings("\n"));
        }

        return source.ToString();
    }

    /// <summary>
    /// The build properties the Razor compiler reads, as the SDK's <c>CompilerVisibleProperty</c> items pass them.
    /// </summary>
    /// <param name="project">The project.</param>
    public static IReadOnlyDictionary<string, string> CompilerProperties(TsProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["RazorLangVersion"] = LanguageVersion(project),
            ["SupportLocalizedComponentNames"] = project.GetProperty("SupportLocalizedComponentNames") ?? string.Empty,
            ["GenerateRazorMetadataSourceChecksumAttributes"] = project.GetProperty("GenerateRazorMetadataSourceChecksumAttributes") ?? string.Empty,
            ["_RazorSourceGeneratorDebug"] = string.Empty,
        };
    }

    /// <summary>Whether a project is an app on the Web SDK, which discovers MVC application parts.</summary>
    private static bool IsWebApp(TsProject project) =>
        project.IsExecutable && string.Equals(project.Sdk, "Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether a referenced project may hold MVC controllers, views or pages: it uses the Razor SDK or ASP.NET Core.
    /// The SDK looks for assemblies that reference MVC; an attribute for one that does not is harmless.
    /// </summary>
    private static bool MayHoldApplicationParts(TsProject project) =>
        project.Language == ProjectLanguage.CSharp
        && !string.Equals(project.Sdk, "Microsoft.NET.Sdk.BlazorWebAssembly", StringComparison.OrdinalIgnoreCase)
        && (UsesRazor(project) || project.FrameworkReferences.Contains(ProjectParser.AspNetCoreFramework, StringComparer.OrdinalIgnoreCase));

    private static string TargetFramework(TsProject project) =>
        project.TargetFrameworks.Count > 0 ? project.TargetFrameworks[0] : ReferenceAssemblies.TargetFramework;

    /// <summary>The major version of a framework such as <c>net10.0</c>, or 0 when it is not a .NET 5+ framework.</summary>
    private static int FrameworkMajorVersion(string framework)
    {
        if (!framework.StartsWith("net", StringComparison.OrdinalIgnoreCase) || framework.Length < 4 || !char.IsDigit(framework[3]))
        {
            return 0;
        }

        var end = framework.IndexOfAny(['.', '-'], 3);
        return int.TryParse(end < 0 ? framework[3..] : framework[3..end], out var major) && major >= 5 ? major : 0;
    }
}
