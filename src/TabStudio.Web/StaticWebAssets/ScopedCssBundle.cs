using System.Globalization;
using System.Text;
using TabStudio.Compilation.Razor;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;

namespace TabStudio.Web.StaticWebAssets;

/// <summary>A project's scoped CSS, rewritten and joined into one stylesheet, and the problems found.</summary>
/// <param name="Css">The stylesheet; empty when the project has no scoped CSS and imports nothing.</param>
/// <param name="Errors">Problems, on the scoped CSS files; a file with a problem is left out of the bundle, as the SDK fails the build.</param>
public sealed record ScopedCssBundleResult(string Css, IReadOnlyList<BuildDiagnostic> Errors)
{
    /// <summary>Whether there is anything to serve.</summary>
    public bool HasContent => Css.Length > 0;
}

/// <summary>
/// Joins the scoped CSS of a project into the stylesheet the SDK's <c>ConcatenateCssFiles</c> task writes: an app's
/// <c>&lt;Project&gt;.styles.css</c>, which imports the bundles of the Razor class libraries it references, or a
/// library's <c>&lt;Project&gt;.bundle.scp.css</c>.
/// </summary>
public static class ScopedCssBundle
{
    /// <summary>The scoped CSS files of a project: each <c>Name.razor.css</c> next to <c>Name.razor</c>, and each <c>Name.cshtml.css</c> next to <c>Name.cshtml</c>.</summary>
    /// <param name="project">The project.</param>
    /// <param name="otherProjectDirectories">Folders of other projects, which are never included.</param>
    public static IReadOnlyList<string> FindFiles(ProjectInfo project, IEnumerable<string>? otherProjectDirectories = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!RazorProjects.UsesRazor(project) || !RazorProjects.IsScopedCssEnabled(project))
        {
            return [];
        }

        return
        [
            .. ProjectSourceFiles.Enumerate(project, otherProjectDirectories, ".css")
                .Where(css => RazorProjects.IsRazorFile(css[..^".css".Length]) && File.Exists(css[..^".css".Length]))
                .Order(StringComparer.OrdinalIgnoreCase),
        ];
    }

    /// <summary>Rewrites a project's scoped CSS files with their scopes and joins them.</summary>
    /// <param name="project">The project.</param>
    /// <param name="scopedFiles">Its scoped CSS files, from <see cref="FindFiles"/>.</param>
    /// <param name="basePath">The base path its assets are served under: empty for an app, <c>_content/Library</c> for a library.</param>
    /// <param name="imports">The routes of the referenced libraries' bundles, which an app's stylesheet imports first.</param>
    public static ScopedCssBundleResult Create(ProjectInfo project, IReadOnlyList<string> scopedFiles, string basePath, IReadOnlyList<string> imports)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(scopedFiles);
        ArgumentNullException.ThrowIfNull(basePath);
        ArgumentNullException.ThrowIfNull(imports);

        var bundle = new StringBuilder();
        var errors = new List<BuildDiagnostic>();
        if (imports.Count > 0)
        {
            foreach (var import in imports.Order(StringComparer.OrdinalIgnoreCase))
            {
                bundle.Append(CultureInfo.InvariantCulture, $"@import '{StaticWebAsset.NormalizeRoute(import)}';\n");
            }

            bundle.Append('\n');
        }

        var header = StaticWebAsset.NormalizeRoute(basePath);
        foreach (var file in scopedFiles.Order(StringComparer.OrdinalIgnoreCase))
        {
            var result = ScopedCss.Rewrite(ReadText(file), CssScope.Compute(project, file));
            if (result.Errors.Count > 0)
            {
                errors.AddRange(result.Errors.Select(e => ScopedCss.ToDiagnostic(file, e, project.Name)));
                continue;
            }

            var relative = Path.GetRelativePath(project.DirectoryPath, file).Replace(Path.DirectorySeparatorChar, '/');
            bundle.Append(CultureInfo.InvariantCulture, $"/* {header}/{relative[..^".css".Length]}.rz.scp.css */\n");
            // Line by line, as the SDK reads the rewritten file: a final line break adds no empty line.
            var lines = result.Css.ReplaceLineEndings("\n").Split('\n');
            var count = lines[^1].Length == 0 ? lines.Length - 1 : lines.Length;
            for (var i = 0; i < count; i++)
            {
                bundle.Append(lines[i]).Append('\n');
            }
        }

        return new ScopedCssBundleResult(bundle.ToString(), errors);
    }

    /// <summary>The file name of an app's bundle, the one its pages link to: <c>&lt;Assembly&gt;.styles.css</c>.</summary>
    /// <param name="project">The app.</param>
    public static string AppBundleName(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return (project.GetProperty("AssemblyName") ?? project.Name) + ".styles.css";
    }

    /// <summary>The file name of a library's bundle, which apps import: <c>&lt;Assembly&gt;.bundle.scp.css</c>.</summary>
    /// <param name="project">The library.</param>
    public static string LibraryBundleName(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return (project.GetProperty("AssemblyName") ?? project.Name) + ".bundle.scp.css";
    }

    private static string ReadText(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }
}
