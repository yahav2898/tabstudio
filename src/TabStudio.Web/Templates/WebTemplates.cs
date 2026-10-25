using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace TabStudio.Web.Templates;

/// <summary>Where the components of a Blazor Web App run, as Visual Studio's "Interactive render mode" sets it.</summary>
public enum BlazorInteractivity
{
    /// <summary>On the server, over a live connection to the browser: Interactive Server, the default.</summary>
    Server,

    /// <summary>In the browser, on .NET for WebAssembly, from a client project the app references.</summary>
    WebAssembly,

    /// <summary>On the server at first, then in the browser once the client has downloaded.</summary>
    Auto,

    /// <summary>Rendered on the server only, without interactivity.</summary>
    None,
}

/// <summary>
/// The .NET 10 templates for web apps with Razor, with the content <c>dotnet new</c> writes for them: Blazor Web App,
/// Blazor WebAssembly Standalone App, ASP.NET Core Web App (Razor Pages) and ASP.NET Core Web App
/// (Model-View-Controller), each in the folders Visual Studio creates, with Bootstrap (and jQuery for Razor Pages and
/// MVC) in <c>wwwroot/lib</c>.
/// </summary>
/// <remarks>
/// The files are those of <c>dotnet new</c> from the .NET SDK 10.0.1xx, with the template's name (<c>BlazorApp1</c>,
/// <c>WebApplication1</c>) replaced by the project's, new ports in <c>launchSettings.json</c> as <c>dotnet new</c>
/// picks them, and the current year in the layout's footer.
/// </remarks>
public static class WebTemplates
{
    /// <summary>Blazor Web App (<c>dotnet new blazor</c>).</summary>
    public const string BlazorWebAppId = "blazor";

    /// <summary>Blazor WebAssembly Standalone App (<c>dotnet new blazorwasm</c>).</summary>
    public const string BlazorWebAssemblyId = "blazorwasm";

    /// <summary>ASP.NET Core Web App (Razor Pages) (<c>dotnet new webapp</c>).</summary>
    public const string RazorPagesId = "webapp";

    /// <summary>ASP.NET Core Web App (Model-View-Controller) (<c>dotnet new mvc</c>).</summary>
    public const string MvcId = "mvc";

    private const string ResourcePrefix = "Templates/";
    private const string LibrariesResource = "Templates/lib.zip";

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".razor", ".cshtml", ".csproj", ".json", ".css", ".js", ".html", ".md", ".txt",
    };

    /// <summary>The templates, in the order Visual Studio lists them.</summary>
    public static IReadOnlyList<string> Ids { get; } = [BlazorWebAppId, BlazorWebAssemblyId, RazorPagesId, MvcId];

    /// <summary>
    /// The template of a Blazor Web App with its components running where an interactivity says, such as
    /// <c>blazor-auto</c>; <see cref="BlazorWebAppId"/> for Interactive Server.
    /// </summary>
    /// <param name="interactivity">Where the components run.</param>
    public static string BlazorWebAppIdFor(BlazorInteractivity interactivity) => interactivity switch
    {
        BlazorInteractivity.WebAssembly => BlazorWebAppId + "-webassembly",
        BlazorInteractivity.Auto => BlazorWebAppId + "-auto",
        BlazorInteractivity.None => BlazorWebAppId + "-none",
        _ => BlazorWebAppId,
    };

    /// <summary>Whether an id is one of these templates, or a Blazor Web App with another interactivity.</summary>
    /// <param name="templateId">The id.</param>
    public static bool IsTemplate(string templateId) =>
        Ids.Contains(templateId, StringComparer.Ordinal)
        || Enum.GetValues<BlazorInteractivity>().Any(i => string.Equals(BlazorWebAppIdFor(i), templateId, StringComparison.Ordinal));

    /// <summary>
    /// Creates the projects of a template in a solution folder, as <c>dotnet new</c> creates them: the app in
    /// <c>&lt;folder&gt;/&lt;name&gt;</c>, and a Blazor Web App's WebAssembly client in <c>&lt;folder&gt;/&lt;name&gt;.Client</c>.
    /// </summary>
    /// <param name="templateId">One of <see cref="Ids"/>, or a Blazor Web App's from <see cref="BlazorWebAppIdFor"/>.</param>
    /// <param name="solutionDirectory">The solution folder.</param>
    /// <param name="name">The project name, which is also its namespace.</param>
    /// <returns>The project files, the app first.</returns>
    /// <exception cref="ArgumentException">The template is not one of these.</exception>
    public static IReadOnlyList<string> Create(string templateId, string solutionDirectory, string name)
    {
        ArgumentNullException.ThrowIfNull(templateId);
        ArgumentException.ThrowIfNullOrEmpty(solutionDirectory);
        ArgumentException.ThrowIfNullOrEmpty(name);

        // The folder of each template's files, the name dotnet new replaces, and whether it has a solution folder.
        var (content, sourceName, hasSolutionFolder) = templateId switch
        {
            BlazorWebAppId => ("blazor-server", "BlazorApp1", false),
            BlazorWebAppId + "-webassembly" => ("blazor-webassembly", "BlazorApp1", true),
            BlazorWebAppId + "-auto" => ("blazor-auto", "BlazorApp1", true),
            BlazorWebAppId + "-none" => ("blazor-none", "BlazorApp1", false),
            BlazorWebAssemblyId => ("blazorwasm", "BlazorApp1", false),
            RazorPagesId => ("webapp", "WebApplication1", false),
            MvcId => ("mvc", "WebApplication1", false),
            _ => throw new ArgumentException($"Unknown web template '{templateId}'.", nameof(templateId)),
        };

        var tokens = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [sourceName] = name,
            ["$httpPort$"] = RandomNumberGenerator.GetInt32(5000, 5300).ToString(CultureInfo.InvariantCulture),
            ["$httpsPort$"] = RandomNumberGenerator.GetInt32(7000, 7300).ToString(CultureInfo.InvariantCulture),
            ["$copyrightYear$"] = DateTime.Now.Year.ToString(CultureInfo.InvariantCulture),
        };

        var assembly = typeof(WebTemplates).Assembly;
        var prefix = ResourcePrefix + content + "/";
        var projects = new List<string>();
        foreach (var resource in assembly.GetManifestResourceNames().Select(r => (Name: r, Path: r.Replace('\\', '/'))).Where(r => r.Path.StartsWith(prefix, StringComparison.Ordinal)))
        {
            var relative = resource.Path[prefix.Length..];
            if (hasSolutionFolder)
            {
                relative = relative[(relative.IndexOf('/', StringComparison.Ordinal) + 1)..];
            }

            var target = Path.Combine(solutionDirectory, relative.Replace(sourceName, name, StringComparison.Ordinal).Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var source = assembly.GetManifestResourceStream(resource.Name)!;
            if (TextExtensions.Contains(Path.GetExtension(target)))
            {
                WriteText(source, target, tokens);
            }
            else
            {
                using var file = File.Create(target);
                source.CopyTo(file);
            }

            if (target.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                projects.Add(target);
            }
        }

        // The app is the project named after the solution; a Web App's client comes after it.
        var ordered = projects.OrderByDescending(p => string.Equals(Path.GetFileNameWithoutExtension(p), name, StringComparison.Ordinal)).ToList();
        if (ordered.Count == 0)
        {
            throw new InvalidOperationException($"The {templateId} template is missing from TabStudio.");
        }

        WriteLibraries(assembly, Path.Combine(Path.GetDirectoryName(ordered[0])!, "wwwroot", "lib"), bootstrapOnly: sourceName == "BlazorApp1");
        return ordered;
    }

    /// <summary>Writes a text file with the template's tokens replaced, keeping its byte order mark.</summary>
    private static void WriteText(Stream source, string target, IReadOnlyDictionary<string, string> tokens)
    {
        using var buffer = new MemoryStream();
        source.CopyTo(buffer);
        var bytes = buffer.ToArray();
        var preamble = Encoding.UTF8.Preamble;
        var hasBom = bytes.AsSpan().StartsWith(preamble);
        var text = Encoding.UTF8.GetString(hasBom ? bytes[preamble.Length..] : bytes);
        foreach (var (token, value) in tokens)
        {
            text = text.Replace(token, value, StringComparison.Ordinal);
        }

        File.WriteAllText(target, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: hasBom));
    }

    /// <summary>
    /// Writes the client libraries to <c>wwwroot/lib</c>: Bootstrap's <c>dist</c> folder for Blazor, and Bootstrap,
    /// jQuery and jQuery Validation for Razor Pages and MVC.
    /// </summary>
    private static void WriteLibraries(Assembly assembly, string libraryDirectory, bool bootstrapOnly)
    {
        using var stream = assembly.GetManifestResourceStream(LibrariesResource)
            ?? throw new InvalidOperationException("The templates' client libraries are missing from TabStudio.");
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var root = Path.GetFullPath(libraryDirectory) + Path.DirectorySeparatorChar;
        foreach (var entry in archive.Entries.Where(e => e.Name.Length > 0))
        {
            var path = entry.FullName.Replace('\\', '/');
            if (bootstrapOnly && !path.StartsWith("bootstrap/dist/", StringComparison.Ordinal))
            {
                continue;
            }

            var target = Path.GetFullPath(Path.Combine(libraryDirectory, path.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(root, StringComparison.Ordinal))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
        }
    }
}
