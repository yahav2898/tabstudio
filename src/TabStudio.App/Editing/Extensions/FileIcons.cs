using Avalonia.Media;
using Avalonia.Media.Imaging;
using TabStudio.App.Editing.Extensions.Svg;
using TabStudio.Extensions.Themes;

namespace TabStudio.App.Editing.Extensions;

/// <summary>
/// The icons the chosen file icon theme gives files and folders, for the Solution Explorer and the document tabs:
/// SVG and PNG images, drawn once and kept. Without a theme, or for a theme whose icons are font characters, TabStudio
/// shows its own badges.
/// </summary>
/// <remarks>Call on the UI thread: the images are drawn with brushes that belong to it.</remarks>
public static class FileIcons
{
    private const int MaximumImages = 2000;

    private static readonly Dictionary<string, IImage?> Images = new(StringComparer.Ordinal);
    private static FileIconTheme? _imagesTheme;

    /// <summary>The icon of a file, or <see langword="null"/> for TabStudio's badge.</summary>
    /// <param name="fileName">The file's name or path.</param>
    public static IImage? ForFile(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        if (Theme() is not { } theme)
        {
            return null;
        }

        var type = FileTypes.For(fileName);
        return Image(theme.ForFile(Path.GetFileName(fileName), EditorExtensions.LanguageIdOf(type, fileName)));
    }

    /// <summary>The icon of a folder, or <see langword="null"/> for TabStudio's folder icon.</summary>
    /// <param name="folderName">The folder's name.</param>
    /// <param name="expanded">Whether it is shown expanded.</param>
    public static IImage? ForFolder(string folderName, bool expanded)
    {
        ArgumentNullException.ThrowIfNull(folderName);
        return Theme() is { } theme ? Image(theme.ForFolder(folderName, expanded)) : null;
    }

    /// <summary>Draws an icon image file: an SVG, or a bitmap such as a PNG.</summary>
    /// <param name="path">The image file.</param>
    /// <returns>The image, or <see langword="null"/> when it cannot be read or drawn.</returns>
    internal static IImage? Load(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > 1024 * 1024)
            {
                return null;
            }

            return path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
                ? SvgDrawingReader.Read(File.ReadAllText(path))
                : new Bitmap(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>The chosen theme, forgetting the images of the one before when it changed.</summary>
    private static FileIconTheme? Theme()
    {
        var theme = EditorExtensions.IconTheme;
        if (!ReferenceEquals(theme, _imagesTheme))
        {
            Images.Clear();
            _imagesTheme = theme;
        }

        return theme;
    }

    private static IImage? Image(FileIcon? icon)
    {
        if (icon?.ImagePath is not { } path)
        {
            return null;
        }

        if (!Images.TryGetValue(path, out var image))
        {
            if (Images.Count >= MaximumImages)
            {
                Images.Clear();
            }

            image = Load(path);
            Images[path] = image;
        }

        return image;
    }
}
