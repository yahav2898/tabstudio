using System.Collections;
using Microsoft.Build.Framework;

namespace TabStudio.Wpf.Compiler;

/// <summary>
/// An MSBuild item as a task sees it: a file with its well-known metadata, such as <c>FullPath</c> and
/// <c>Extension</c>, computed from the paths it was made with rather than from the process's current folder.
/// </summary>
internal sealed class XamlTaskItem : ITaskItem
{
    private static readonly string[] WellKnownNames =
        ["FullPath", "RootDir", "Filename", "Extension", "RelativeDir", "Directory", "RecursiveDir", "Identity"];

    private readonly Dictionary<string, string> _metadata = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _fullPath;

    /// <summary>Creates an item.</summary>
    /// <param name="identity">The item as the project writes it, relative to the project folder, such as <c>Views/CustomerView.xaml</c>.</param>
    /// <param name="fullPath">The file to read.</param>
    public XamlTaskItem(string identity, string fullPath)
    {
        ItemSpec = identity;
        _fullPath = fullPath;
    }

    /// <inheritdoc />
    public string ItemSpec { get; set; }

    /// <inheritdoc />
    public ICollection MetadataNames => WellKnownNames.Concat(_metadata.Keys).ToList();

    /// <inheritdoc />
    public int MetadataCount => WellKnownNames.Length + _metadata.Count;

    /// <inheritdoc />
    public string GetMetadata(string metadataName)
    {
        ArgumentNullException.ThrowIfNull(metadataName);
        var relativeDirectory = Path.GetDirectoryName(ItemSpec) is { Length: > 0 } directory ? directory + Path.DirectorySeparatorChar : string.Empty;
        return metadataName.ToUpperInvariant() switch
        {
            "IDENTITY" => ItemSpec,
            "FULLPATH" => _fullPath,
            "ROOTDIR" => Path.GetPathRoot(_fullPath) ?? string.Empty,
            "FILENAME" => Path.GetFileNameWithoutExtension(ItemSpec),
            "EXTENSION" => Path.GetExtension(ItemSpec),
            "RELATIVEDIR" => relativeDirectory,
            "DIRECTORY" => Path.GetDirectoryName(_fullPath) is { } full ? full[(Path.GetPathRoot(full)?.Length ?? 0)..] + Path.DirectorySeparatorChar : string.Empty,
            "RECURSIVEDIR" => string.Empty,
            _ => _metadata.GetValueOrDefault(metadataName, string.Empty),
        };
    }

    /// <inheritdoc />
    public void SetMetadata(string metadataName, string metadataValue)
    {
        ArgumentNullException.ThrowIfNull(metadataName);
        _metadata[metadataName] = metadataValue ?? string.Empty;
    }

    /// <inheritdoc />
    public void RemoveMetadata(string metadataName)
    {
        ArgumentNullException.ThrowIfNull(metadataName);
        _metadata.Remove(metadataName);
    }

    /// <inheritdoc />
    public void CopyMetadataTo(ITaskItem destinationItem)
    {
        ArgumentNullException.ThrowIfNull(destinationItem);
        foreach (var (name, value) in _metadata)
        {
            destinationItem.SetMetadata(name, value);
        }
    }

    /// <inheritdoc />
    public IDictionary CloneCustomMetadata() => new Dictionary<string, string>(_metadata, StringComparer.OrdinalIgnoreCase);
}
