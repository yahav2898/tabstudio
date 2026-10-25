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
/// <c>.resx</c> files are not embedded: they are compiled into <c>.resources</c> by a build step TabStudio does not
/// have. A <c>LogicalName</c> or <c>Link</c> on an item is not read; the resource is named after its path.
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
            if (!string.Equals(Path.GetExtension(path), ".resx", StringComparison.OrdinalIgnoreCase))
            {
                var file = path;
                resources.Add(new ResourceDescription(EmbeddedResourceName(project, path), () => File.OpenRead(file), isPublic: true));
            }
        }

        var avalonia = ProjectSourceFiles.EnumerateItems(project, project.AvaloniaResourceIncludes, project.AvaloniaResourceRemoves);
        if (avalonia.Count > 0)
        {
            resources.Add(new ResourceDescription(AvaloniaResourcesName, () => PackAvaloniaResources(project, avalonia), isPublic: true));
        }

        return resources;
    }

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
