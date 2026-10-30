using TabStudio.Core.Workspaces;

namespace TabStudio.Core.Compatibility;

/// <summary>
/// Whether a legacy .NET Framework project runs on the tablet, with .NET, like the SDK-style project it would be
/// upgraded to, or needs .NET Framework on Windows.
/// </summary>
/// <remarks>
/// <para>
/// Most .NET Framework tutorials and student projects are a WPF, Windows Forms or console program that uses nothing
/// but the framework. TabStudio reads their items as MSBuild does (see <see cref="ProjectParser"/>) and runs them on
/// the same paths as SDK-style projects: WPF on OpenSilver, Windows Forms on TabStudio's Forms layer, and console
/// programs in-process. A project runs there when every assembly it references is one that path provides.
/// </para>
/// <para>
/// What only Windows has keeps it on the Windows Bridge: a framework assembly no path provides (such as
/// <c>System.Speech</c>), a DLL referenced by file rather than through a NuGet package, a COM reference, Windows Forms
/// inside a WPF app, or P/Invoke.
/// </para>
/// </remarks>
public static class NetFrameworkCompatibility
{
    /// <summary>The run path's name for a .NET Framework project that runs on the tablet, before what the path itself says.</summary>
    public const string RunsHereSummary = "A .NET Framework project, run with .NET on this tablet.";

    /// <summary>
    /// The .NET Framework assemblies every path provides: .NET's own, under the same names, and those whose
    /// types .NET has elsewhere or that projects list without using, such as <c>System.Deployment</c> (ClickOnce).
    /// </summary>
    private static readonly HashSet<string> BaseAssemblies = new(StringComparer.OrdinalIgnoreCase)
    {
        "mscorlib", "netstandard", "System", "System.Core", "System.Data", "System.Data.DataSetExtensions", "System.Xml",
        "System.Xml.Linq", "Microsoft.CSharp", "Microsoft.VisualBasic", "System.Net", "System.Net.Http", "System.Numerics",
        "System.Runtime", "System.Runtime.Serialization", "System.ComponentModel.DataAnnotations", "System.Transactions",
        "System.IO.Compression", "System.IO.Compression.FileSystem", "System.Deployment", "System.Configuration",
        "System.Collections", "System.Collections.Immutable", "System.Linq", "System.Threading", "System.Threading.Tasks",
        "System.ValueTuple", "System.Memory", "System.Buffers", "System.Security", "System.Web.Extensions", "System.Json",

        // .NET has System.Web's HttpUtility; package installs add the reference to console apps that never use it.
        "System.Web",
    };

    /// <summary>What the WPF path, OpenSilver, provides besides the base.</summary>
    private static readonly HashSet<string> WpfAssemblies = new(StringComparer.OrdinalIgnoreCase)
    {
        "PresentationFramework", "PresentationCore", "WindowsBase", "System.Xaml",
    };

    /// <summary>What the Windows Forms path, TabStudio's Forms layer, provides besides the base.</summary>
    private static readonly HashSet<string> WindowsFormsAssemblies = new(StringComparer.OrdinalIgnoreCase)
    {
        "System.Windows.Forms", "System.Drawing",
    };

    /// <summary>What a console program's path provides besides the base: nothing.</summary>
    private static readonly HashSet<string> NoAssemblies = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether a project is a legacy .NET Framework program: an executable in the legacy format, not a test project.</summary>
    /// <param name="project">The project.</param>
    public static bool IsLegacyProgram(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return ProjectParser.IsLegacyFrameworkProject(project) && project.IsExecutable && !project.IsTestProject;
    }

    /// <summary>
    /// Why a legacy .NET Framework program cannot run on the tablet, in phrases such as
    /// <c>references System.Speech</c>; empty when it runs here. A project that is not a legacy C# WPF, Windows Forms or
    /// console program always has a reason.
    /// </summary>
    /// <param name="project">The project.</param>
    public static IReadOnlyList<string> WindowsOnlyReasons(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!ProjectParser.IsLegacyFrameworkProject(project))
        {
            return ["targets the .NET Framework"];
        }

        if (!project.IsRoslynLanguage)
        {
            return [$"is written in {(project.Language == ProjectLanguage.FSharp ? "F#" : "a language TabStudio does not compile")}, and TabStudio compiles C# and Visual Basic"];
        }

        if (project.IsVisualBasic && project.Kind == ProjectKind.Wpf)
        {
            return ["is a WPF app in Visual Basic, and OpenSilver, which runs WPF here, compiles C#"];
        }

        var provided = project.Kind switch
        {
            ProjectKind.Wpf => WpfAssemblies,
            ProjectKind.WinForms => WindowsFormsAssemblies,
            ProjectKind.ConsoleApp or ProjectKind.ClassLibrary => NoAssemblies,
            _ => null,
        };
        if (provided is null)
        {
            return ["is a kind of .NET Framework project that runs on Windows only"];
        }

        var packages = project.PackageReferences.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var reasons = new List<string>();
        var windowsOnly = project.AssemblyReferences
            .Where(r => !IsProvided(r, provided, packages))
            .ToList();
        if (project.Kind == ProjectKind.Wpf && windowsOnly.RemoveAll(r => WindowsFormsAssemblies.Contains(r.Name)) > 0)
        {
            reasons.Add("uses Windows Forms inside WPF");
        }

        var byFile = windowsOnly.Where(r => r.HintPath is not null).ToList();
        var byName = windowsOnly.Except(byFile).ToList();
        if (byName.Count > 0)
        {
            reasons.Add($"references {List(byName.Select(r => r.Name))}, which {(byName.Count == 1 ? "is" : "are")} part of the .NET Framework on Windows only");
        }

        if (byFile.Count > 0)
        {
            reasons.Add($"references {List(byFile.Select(r => FileName(r.HintPath!)))}, built for the .NET Framework");
        }

        if (project.ComReferences.Count > 0)
        {
            reasons.Add($"uses the COM component{(project.ComReferences.Count == 1 ? string.Empty : "s")} {List(project.ComReferences)}");
        }

        if (project.PlatformInvokeFiles.Count > 0)
        {
            var files = project.PlatformInvokeFiles;
            reasons.Add(files.Count <= 3
                ? $"calls Windows through P/Invoke in {List(files)}"
                : $"calls Windows through P/Invoke in {string.Join(", ", files.Take(2))} and {files.Count - 2} more files");
        }

        return reasons;
    }

    /// <summary>Whether a legacy .NET Framework program runs on the tablet with .NET.</summary>
    /// <param name="project">The project.</param>
    public static bool RunsHere(ProjectInfo project) => IsLegacyProgram(project) && WindowsOnlyReasons(project).Count == 0;

    /// <summary>
    /// Whether a project is a legacy .NET Framework program that only Windows runs, which TabStudio does not compile
    /// against .NET: its errors would only be those of the wrong framework.
    /// </summary>
    /// <param name="project">The project.</param>
    public static bool NeedsWindows(ProjectInfo project) => IsLegacyProgram(project) && WindowsOnlyReasons(project).Count > 0;

    /// <summary>
    /// Why a legacy .NET Framework program needs Windows, as a phrase after "it", such as
    /// <c>references System.Speech, which is part of the .NET Framework on Windows only, and uses the COM component WMPLib</c>.
    /// </summary>
    /// <param name="project">The project.</param>
    public static string WhyWindows(ProjectInfo project) => List(WindowsOnlyReasons(project));

    /// <summary>
    /// The sentence that says why a legacy .NET Framework program needs Windows, such as
    /// <c>MediaPlayer is a .NET Framework project that needs Windows: it uses the COM component WMPLib.</c>
    /// </summary>
    /// <param name="project">The project.</param>
    public static string NeedsWindowsMessage(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return $"{project.Name} is a .NET Framework project that needs Windows: it {WhyWindows(project)}.";
    }

    /// <summary>Whether a reference is to an assembly the run path provides, or to a package NuGet restores for .NET.</summary>
    private static bool IsProvided(AssemblyReferenceInfo reference, HashSet<string> provided, HashSet<string> packages)
    {
        if (reference.HintPath is { } hint)
        {
            // packages.config projects reference a package's DLL by file: ..\packages\Newtonsoft.Json.13.0.3\lib\net45\Newtonsoft.Json.dll.
            return PackageOf(hint) is { } package && packages.Any(p => package.StartsWith(p + ".", StringComparison.OrdinalIgnoreCase));
        }

        return BaseAssemblies.Contains(reference.Name)
            || provided.Contains(reference.Name)
            || packages.Contains(reference.Name)
            || reference.Name.StartsWith("System.Runtime.", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The package folder a <c>HintPath</c> goes through, such as <c>Newtonsoft.Json.13.0.3</c>, or <see langword="null"/>.</summary>
    private static string? PackageOf(string hintPath)
    {
        var parts = hintPath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var index = Array.FindIndex(parts, p => string.Equals(p, "packages", StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < parts.Length ? parts[index + 1] : null;
    }

    private static string FileName(string path) => path.Replace('\\', '/').Split('/')[^1];

    /// <summary>A list in prose: <c>A</c>, <c>A and B</c>, <c>A, B and C</c>.</summary>
    private static string List(IEnumerable<string> items)
    {
        var all = items.ToList();
        return all.Count switch
        {
            0 => string.Empty,
            1 => all[0],
            _ => string.Join(", ", all.Take(all.Count - 1)) + " and " + all[^1],
        };
    }
}
