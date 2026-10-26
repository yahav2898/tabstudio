using System.Text;

namespace TabStudio.Uno.Templates;

/// <summary>
/// The Uno Platform and WinUI 3 project templates, with the files their own tools write: the Uno Platform App
/// (<c>dotnet new unoapp -preset blank</c> from Uno.Templates 6.7, with the Uno.Sdk version in <c>global.json</c>)
/// and the Windows App SDK's Blank App, Packaged (WinUI 3 in Desktop) (<c>dotnet new winui</c>), each in the
/// folders Visual Studio creates.
/// </summary>
/// <remarks>
/// The template's name (<c>UnoApp1</c>, or the Windows App SDK's tokens) is replaced by the project's, as
/// <c>dotnet new</c> replaces it. The WinUI 3 template's package versions are the latest stable ones its
/// <c>dotnet new</c> pins: TabStudio builds and runs it on Uno Platform's implementation of WinUI.
/// </remarks>
public static class UnoTemplates
{
    /// <summary>Uno Platform App, the blank preset (<c>dotnet new unoapp -preset blank</c>).</summary>
    public const string UnoAppId = "unoapp";

    /// <summary>Blank App, Packaged (WinUI 3 in Desktop) (<c>dotnet new winui</c>).</summary>
    public const string WinUIAppId = "winui";

    private const string ResourcePrefix = "Templates/";

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".xaml", ".csproj", ".props", ".targets", ".json", ".md", ".txt", ".xml", ".resw", ".appxmanifest",
        ".manifest", ".pubxml", ".js", ".css", ".webmanifest", ".svg", ".plist", ".xcprivacy", ".conf", ".config",
        ".editorconfig", ".gitignore",
    };

    /// <summary>The templates, in the order Visual Studio lists them.</summary>
    public static IReadOnlyList<string> Ids { get; } = [UnoAppId, WinUIAppId];

    /// <summary>Whether an id is one of these templates.</summary>
    /// <param name="templateId">The id.</param>
    public static bool IsTemplate(string templateId) => Ids.Contains(templateId, StringComparer.Ordinal);

    /// <summary>
    /// Creates the project of a template in a solution folder, as <c>dotnet new</c> creates it: the app in
    /// <c>&lt;folder&gt;/&lt;name&gt;</c>, and for the Uno Platform App, its <c>global.json</c> and
    /// <c>Directory.*.props</c> files in the solution folder.
    /// </summary>
    /// <param name="templateId">One of <see cref="Ids"/>.</param>
    /// <param name="solutionDirectory">The solution folder.</param>
    /// <param name="name">The project name, which is also its namespace.</param>
    /// <returns>The project file.</returns>
    /// <exception cref="ArgumentException">The template is not one of these.</exception>
    public static string Create(string templateId, string solutionDirectory, string name)
    {
        ArgumentNullException.ThrowIfNull(templateId);
        ArgumentException.ThrowIfNullOrEmpty(solutionDirectory);
        ArgumentException.ThrowIfNullOrEmpty(name);

        var (content, root, tokens) = templateId switch
        {
            UnoAppId => ("unoapp", solutionDirectory, UnoTokens(name)),
            WinUIAppId => ("winui", Path.Combine(solutionDirectory, name), WinUITokens(name)),
            _ => throw new ArgumentException($"Unknown Uno Platform template '{templateId}'.", nameof(templateId)),
        };

        var assembly = typeof(UnoTemplates).Assembly;
        var prefix = ResourcePrefix + content + "/";
        string? project = null;
        foreach (var resource in assembly.GetManifestResourceNames().Select(r => (Name: r, Path: r.Replace('\\', '/'))).Where(r => r.Path.StartsWith(prefix, StringComparison.Ordinal)))
        {
            var relative = Replace(resource.Path[prefix.Length..], tokens);
            var target = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
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
                project = target;
            }
        }

        return project ?? throw new InvalidOperationException($"The {templateId} template is missing from TabStudio.");
    }

    /// <summary>What <c>dotnet new unoapp</c> replaces: the template's name, and its lower-case form in the app id.</summary>
    private static List<KeyValuePair<string, string>> UnoTokens(string name) =>
    [
        new("UnoApp1", name),
        new("unoapp1", name.ToLowerInvariant()),
    ];

    /// <summary>What <c>dotnet new winui</c> replaces, with its defaults for the publisher.</summary>
    private static List<KeyValuePair<string, string>> WinUITokens(string name)
    {
        var safeName = SafeName(name);
        return
        [
            new("ProjectTemplate", name),
            new("$safeprojectname$", safeName),
            new("$projectname$", name),
            new("$targetnametoken$", safeName),
            new("$targetentrypoint$", safeName + ".App"),
            new("$guid9$", Guid.NewGuid().ToString("D")),
            new("$XmlEscapedPublisherDistinguishedName$", "CN=AppPublisher"),
            new("$XmlEscapedPublisher$", "AppPublisher"),
        ];
    }

    /// <summary>A name as a namespace, as <c>dotnet new</c>'s <c>safe_name</c> form writes it.</summary>
    private static string SafeName(string name)
    {
        var safe = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            safe.Append(char.IsLetterOrDigit(c) || c is '_' or '.' ? c : '_');
        }

        return safe.Length > 0 && char.IsDigit(safe[0]) ? "_" + safe : safe.ToString();
    }

    private static string Replace(string text, IEnumerable<KeyValuePair<string, string>> tokens)
    {
        foreach (var (token, value) in tokens)
        {
            text = text.Replace(token, value, StringComparison.Ordinal);
        }

        return text;
    }

    /// <summary>Writes a text file with the template's tokens replaced, keeping its byte order mark.</summary>
    private static void WriteText(Stream source, string target, IEnumerable<KeyValuePair<string, string>> tokens)
    {
        using var buffer = new MemoryStream();
        source.CopyTo(buffer);
        var bytes = buffer.ToArray();
        var preamble = Encoding.UTF8.Preamble;
        var hasBom = bytes.AsSpan().StartsWith(preamble);
        var text = Encoding.UTF8.GetString(hasBom ? bytes[preamble.Length..] : bytes);
        File.WriteAllText(target, Replace(text, tokens), new UTF8Encoding(encoderShouldEmitUTF8Identifier: hasBom));
    }
}
