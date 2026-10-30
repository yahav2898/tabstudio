using System.Text;
using Microsoft.CodeAnalysis;
using TabStudio.Core.Workspaces;
using TsProject = TabStudio.Core.Workspaces.ProjectInfo;

namespace TabStudio.Compilation;

/// <summary>
/// The files a project compiles into its assembly as resources: its <c>EmbeddedResource</c> items, named as the
/// SDK names them, and an Avalonia app's <c>AvaloniaResource</c> items, packed the way Avalonia's build packs them.
/// </summary>
/// <remarks>
/// A <c>.resx</c> item is compiled into <c>.resources</c> with its strings, as MSBuild's <c>GenerateResource</c>
/// does, so <c>Properties.Resources</c> and a form's <c>ComponentResourceManager</c> find their resources; entries of
/// other types, such as images, are left out and read as <see langword="null"/>. A <c>.resx</c> beside the code file of
/// a class, such as <c>Form1.resx</c> beside <c>Form1.cs</c>, is named after the class. Culture-specific <c>.resx</c>
/// files, which build satellite assemblies, are not compiled. An item's <c>LogicalName</c> names its resource as it is;
/// a <c>Link</c> is not read, and the resource is named after its path.
/// </remarks>
public static class ManifestResources
{
    /// <summary>The resource Avalonia reads <c>avares://</c> files from.</summary>
    public const string AvaloniaResourcesName = "!AvaloniaResources";

    /// <summary>The version of the index format, as Avalonia 11 writes and reads it.</summary>
    private const int AvaloniaIndexVersion = 2;

    /// <summary>The resources to compile into a project's assembly, read from disk when the assembly is emitted.</summary>
    /// <param name="project">The project.</param>
    public static IReadOnlyList<ResourceDescription> For(TsProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var resources = new List<ResourceDescription>();
        foreach (var path in ProjectSourceFiles.EnumerateItems(project, project.EmbeddedResourceIncludes, project.EmbeddedResourceRemoves))
        {
            var file = path;

            // An item's LogicalName is the resource's name as it is, as CommunityToolkit.Mvvm's generators embed the
            // sources they read: <EmbeddedResource Include="...\ObservableObject.cs"><LogicalName>ObservableObject.cs</LogicalName>.
            var logicalName = LogicalNameOf(project, path);
            if (!string.Equals(Path.GetExtension(path), ".resx", StringComparison.OrdinalIgnoreCase))
            {
                resources.Add(new ResourceDescription(logicalName ?? EmbeddedResourceName(project, path), () => File.OpenRead(file), isPublic: true));
            }
            else if (!IsCultureSpecific(path))
            {
                // A Windows Forms .resx file holding images, icons or image lists compiles for the Forms layer's reader.
                var forms = WindowsForms.WindowsFormsProject.UsesWindowsForms(project);
                resources.Add(new ResourceDescription(logicalName ?? ResxResourceName(project, path), () => (forms ? WindowsForms.WindowsFormsResources.TryCompile(file) : null) ?? CompileResx(file), isPublic: true));
            }
        }

        var avalonia = ProjectSourceFiles.EnumerateItems(project, project.AvaloniaResourceIncludes, project.AvaloniaResourceRemoves);
        if (avalonia.Count > 0)
        {
            resources.Add(new ResourceDescription(AvaloniaResourcesName, () => PackAvaloniaResources(project, avalonia), isPublic: true));
        }

        return resources;
    }

    /// <summary>The <c>LogicalName</c> the project's items give a resource file, the last one winning; <see langword="null"/> when none does.</summary>
    private static string? LogicalNameOf(TsProject project, string path) =>
        project.EmbeddedResourceItems
            .Where(item => item.Metadata.TryGetValue("LogicalName", out var name) && name.Length > 0 && !name.Contains("%(", StringComparison.Ordinal)
                && new GlobPattern(project.DirectoryPath, item.Include).IsMatch(path))
            .Select(item => item.Metadata["LogicalName"])
            .LastOrDefault();

    /// <summary>
    /// The SDK's name for an embedded resource: the root namespace, then the folders, each made a valid identifier,
    /// then the file name as it is, such as <c>App.Data.Sample_Files.notes.txt</c>.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <param name="path">The file's full path.</param>
    public static string EmbeddedResourceName(TsProject project, string path)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(path);

        var rootNamespace = project.GetProperty("RootNamespace") ?? project.Name;
        var relative = Path.GetRelativePath(project.DirectoryPath, path).Replace('\\', '/');
        var parts = relative.Split('/');
        var name = new StringBuilder(rootNamespace);
        for (var i = 0; i < parts.Length; i++)
        {
            if (name.Length > 0)
            {
                name.Append('.');
            }

            name.Append(i == parts.Length - 1 ? parts[i] : Identifier(parts[i]));
        }

        return name.ToString();
    }

    /// <summary>
    /// The name MSBuild gives a compiled <c>.resx</c>: the class of the code file beside it, such as
    /// <c>Calculator.Form1.resources</c> for <c>Form1.resx</c> beside <c>Form1.cs</c>, or else the root namespace and the
    /// file's path, such as <c>Calculator.Properties.Resources.resources</c>.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <param name="path">The <c>.resx</c> file's full path.</param>
    public static string ResxResourceName(TsProject project, string path)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(path);
        var code = Path.ChangeExtension(path, ".cs");
        if (File.Exists(code) && ClassOf(File.ReadAllText(code)) is { } type)
        {
            return type + ".resources";
        }

        var name = EmbeddedResourceName(project, path);
        return name[..^".resx".Length] + ".resources";
    }

    /// <summary>
    /// The full name of the first class a C# file declares, as MSBuild's <c>CreateCSharpManifestResourceName</c> reads
    /// it: its namespace, block or file-scoped, and its name.
    /// </summary>
    private static string? ClassOf(string code)
    {
        var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(code);
        var type = tree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().FirstOrDefault();
        if (type is null)
        {
            return null;
        }

        var names = new List<string> { type.Identifier.ValueText };
        for (var node = type.Parent; node is not null; node = node.Parent)
        {
            switch (node)
            {
                case Microsoft.CodeAnalysis.CSharp.Syntax.BaseNamespaceDeclarationSyntax ns:
                    names.Insert(0, ns.Name.ToString());
                    break;
                case Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax outer:
                    names.Insert(0, outer.Identifier.ValueText);
                    break;
            }
        }

        return string.Join('.', names);
    }

    /// <summary>Whether a <c>.resx</c> is for one culture, as <c>Strings.fr-FR.resx</c> is, which builds a satellite assembly.</summary>
    private static bool IsCultureSpecific(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var dot = name.LastIndexOf('.');
        if (dot < 0)
        {
            return false;
        }

        var suffix = name[(dot + 1)..];
        if (!System.Text.RegularExpressions.Regex.IsMatch(suffix, "^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8}){0,2}$", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1)))
        {
            return false;
        }

        try
        {
            return System.Globalization.CultureInfo.GetCultureInfo(suffix, predefinedOnly: true).Name.Length > 0;
        }
        catch (System.Globalization.CultureNotFoundException)
        {
            return false;
        }
    }

    /// <summary>A <c>.resx</c> compiled into the binary <c>.resources</c> format, with its string entries.</summary>
    /// <param name="path">The <c>.resx</c> file.</param>
    public static MemoryStream CompileResx(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var compiled = new MemoryStream();
        byte[] bytes;
        using (var writer = new System.Resources.ResourceWriter(compiled))
        {
            foreach (var (name, value) in ReadResxStrings(path))
            {
                writer.AddResource(name, value);
            }

            writer.Generate();
            bytes = compiled.ToArray();
        }

        return new MemoryStream(bytes);
    }

    /// <summary>The string entries of a <c>.resx</c>: its <c>data</c> elements with no <c>type</c> or <c>mimetype</c>.</summary>
    /// <param name="path">The <c>.resx</c> file.</param>
    public static IReadOnlyList<(string Name, string Value)> ReadResxStrings(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        System.Xml.Linq.XDocument document;
        try
        {
            document = System.Xml.Linq.XDocument.Load(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            return [];
        }

        return document.Root?.Elements("data")
            .Where(d => d.Attribute("type") is null && d.Attribute("mimetype") is null && d.Attribute("name")?.Value is { Length: > 0 })
            .Select(d => (d.Attribute("name")!.Value, d.Element("value")?.Value ?? string.Empty))
            .DistinctBy(d => d.Item1, StringComparer.Ordinal)
            .ToList() ?? [];
    }

    /// <summary>
    /// Avalonia's resource blob: the index's length, the index (format version, count, and for each file its
    /// <c>/</c>-rooted path, offset and size), then the files' bytes one after another.
    /// </summary>
    private static MemoryStream PackAvaloniaResources(TsProject project, IReadOnlyList<string> files)
    {
        var contents = files.Select(File.ReadAllBytes).ToList();
        using var index = new MemoryStream();
        using (var writer = new BinaryWriter(index, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(AvaloniaIndexVersion);
            writer.Write(files.Count);
            var offset = 0;
            for (var i = 0; i < files.Count; i++)
            {
                writer.Write("/" + Path.GetRelativePath(project.DirectoryPath, files[i]).Replace('\\', '/'));
                writer.Write(offset);
                writer.Write(contents[i].Length);
                offset += contents[i].Length;
            }
        }

        var blob = new MemoryStream();
        using (var writer = new BinaryWriter(blob, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((int)index.Length);
            writer.Write(index.GetBuffer(), 0, (int)index.Length);
            foreach (var content in contents)
            {
                writer.Write(content);
            }
        }

        blob.Position = 0;
        return blob;
    }

    /// <summary>A folder name as the SDK puts it into a resource name: letters, digits and underscores, not starting with a digit.</summary>
    private static string Identifier(string folder)
    {
        var text = new StringBuilder(folder.Length + 1);
        foreach (var c in folder)
        {
            text.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        }

        if (text.Length == 0 || char.IsDigit(text[0]))
        {
            text.Insert(0, '_');
        }

        return text.ToString();
    }
}
