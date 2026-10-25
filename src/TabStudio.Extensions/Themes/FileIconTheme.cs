using System.Text.Json;
using TabStudio.Extensions.Installation;
using TabStudio.Extensions.Json;

namespace TabStudio.Extensions.Themes;

/// <summary>An icon of a file icon theme: an image file, or a character of an icon font.</summary>
/// <param name="ImagePath">The full path of the image (SVG or PNG), or <see langword="null"/> for a font character.</param>
/// <param name="FontCharacter">The character of the theme's icon font, or <see langword="null"/>.</param>
/// <param name="FontColor">The color of the font character, or <see langword="null"/>.</param>
public sealed record FileIcon(string? ImagePath, string? FontCharacter, string? FontColor);

/// <summary>
/// A VS Code file icon theme (<c>contributes.iconThemes</c>): which icon a file or folder gets, by file name, extension,
/// language and folder name, as VS Code resolves them.
/// </summary>
public sealed class FileIconTheme
{
    private readonly Dictionary<string, FileIcon> _definitions;
    private readonly Associations _associations;

    private FileIconTheme(string name, Dictionary<string, FileIcon> definitions, Associations associations)
    {
        Name = name;
        _definitions = definitions;
        _associations = associations;
    }

    /// <summary>The theme's name.</summary>
    public string Name { get; }

    /// <summary>Whether the theme's icons are images (SVG or PNG), rather than characters of an icon font.</summary>
    public bool UsesImages => _definitions.Values.Any(d => d.ImagePath is not null);

    /// <summary>Reads an icon theme file.</summary>
    /// <param name="path">The file.</param>
    /// <param name="name">The theme's name.</param>
    /// <param name="root">The extension folder; icons outside it are ignored.</param>
    /// <exception cref="JsonException">The file is not JSON.</exception>
    public static FileIconTheme Read(string path, string name, string root)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentException.ThrowIfNullOrEmpty(root);
        using var document = Jsonc.Parse(Jsonc.ReadText(path));
        var theme = document.RootElement;
        var folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
        var definitions = new Dictionary<string, FileIcon>(StringComparer.Ordinal);
        if (theme.GetObject("iconDefinitions") is { } icons)
        {
            foreach (var icon in icons.EnumerateObject().Where(i => i.Value.ValueKind == JsonValueKind.Object))
            {
                var image = icon.Value.GetString("iconPath") is { Length: > 0 } relative
                    ? InstalledExtension.ResolveInFolder(root, Path.GetRelativePath(root, Path.GetFullPath(Path.Combine(folder, relative))))
                    : null;
                var character = icon.Value.GetString("fontCharacter");
                if (image is not null || character is not null)
                {
                    definitions[icon.Name] = new FileIcon(image, character, icon.Value.GetString("fontColor"));
                }
            }
        }

        return new FileIconTheme(name, definitions, Associations.Read(theme));
    }

    /// <summary>The icon of a file.</summary>
    /// <param name="fileName">The file's name.</param>
    /// <param name="languageId">The file's language, or <see langword="null"/>.</param>
    public FileIcon? ForFile(string fileName, string? languageId)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        var name = fileName.ToLowerInvariant();
        if (_associations.FileNames.TryGetValue(name, out var byName))
        {
            return Icon(byName);
        }

        // The longest extension first: "spec.ts" before "ts".
        for (var dot = name.IndexOf('.', StringComparison.Ordinal); dot >= 0; dot = name.IndexOf('.', dot + 1))
        {
            if (_associations.FileExtensions.TryGetValue(name[(dot + 1)..], out var byExtension))
            {
                return Icon(byExtension);
            }
        }

        if (languageId is not null && _associations.LanguageIds.TryGetValue(languageId, out var byLanguage))
        {
            return Icon(byLanguage);
        }

        return Icon(_associations.File);
    }

    /// <summary>The icon of a folder.</summary>
    /// <param name="folderName">The folder's name.</param>
    /// <param name="expanded">Whether it is shown expanded.</param>
    public FileIcon? ForFolder(string folderName, bool expanded)
    {
        ArgumentNullException.ThrowIfNull(folderName);
        var name = folderName.ToLowerInvariant();
        if (expanded && _associations.FolderNamesExpanded.TryGetValue(name, out var open))
        {
            return Icon(open);
        }

        if (_associations.FolderNames.TryGetValue(name, out var byName))
        {
            return Icon(expanded ? _associations.FolderNamesExpanded.GetValueOrDefault(name) ?? byName : byName);
        }

        return Icon(expanded ? _associations.FolderExpanded ?? _associations.Folder : _associations.Folder);
    }

    private FileIcon? Icon(string? id) => id is not null && _definitions.TryGetValue(id, out var icon) ? icon : null;

    /// <summary>The theme's associations from names to icon ids.</summary>
    private sealed record Associations(
        string? File,
        string? Folder,
        string? FolderExpanded,
        Dictionary<string, string> FileNames,
        Dictionary<string, string> FileExtensions,
        Dictionary<string, string> LanguageIds,
        Dictionary<string, string> FolderNames,
        Dictionary<string, string> FolderNamesExpanded)
    {
        public static Associations Read(JsonElement theme) => new(
            theme.GetString("file"),
            theme.GetString("folder"),
            theme.GetString("folderExpanded"),
            Map(theme, "fileNames", lower: true),
            Map(theme, "fileExtensions", lower: true),
            Map(theme, "languageIds", lower: false),
            Map(theme, "folderNames", lower: true),
            Map(theme, "folderNamesExpanded", lower: true));

        private static Dictionary<string, string> Map(JsonElement theme, string name, bool lower)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (key, value) in theme.GetStringMap(name))
            {
                result[lower ? key.ToLowerInvariant() : key] = value;
            }

            return result;
        }
    }
}
