using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using TabStudio.App.Editing.Extensions.Svg;
using TabStudio.Extensions.Themes;

namespace TabStudio.App.Editing.Extensions;

/// <summary>
/// The icons the chosen file icon theme gives files and folders, for the Solution Explorer and the document tabs:
/// SVG and PNG images, drawn once and kept. Without a theme, a file of a language an extension contributes shows the
/// language's own icon when it has one, and other files show TabStudio's badges.
/// </summary>
/// <remarks>Call on the UI thread: the images are drawn with brushes that belong to it.</remarks>
public static class FileIcons
{
    private const int MaximumImages = 2000;

    private static readonly Dictionary<string, IImage?> Images = new(StringComparer.Ordinal);
    private static FileIconTheme? _imagesTheme;
    private static DrawingImage? _extensionsPage;

    /// <summary>
    /// The icon of a file: the icon theme's, or without one, the icon of the language an extension contributes for
    /// it; <see langword="null"/> for TabStudio's badge.
    /// </summary>
    /// <param name="fileName">The file's name or path.</param>
    public static IImage? ForFile(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        var type = FileTypes.For(fileName);
        if (Theme() is { } theme)
        {
            return Image(theme.ForFile(Path.GetFileName(fileName), EditorExtensions.LanguageIdOf(type, fileName))?.ImagePath);
        }

        return Image(type.IconPath);
    }

    /// <summary>The icon of the Extensions tab: TabStudio's extensions symbol.</summary>
    public static IImage? ExtensionsPage => _extensionsPage ??= SymbolImage("Icon.Extensions", "Brush.AccentText");

    /// <summary>The icon of a folder, or <see langword="null"/> for TabStudio's folder icon.</summary>
    /// <param name="folderName">The folder's name.</param>
    /// <param name="expanded">Whether it is shown expanded.</param>
    public static IImage? ForFolder(string folderName, bool expanded)
    {
        ArgumentNullException.ThrowIfNull(folderName);
        return Theme() is { } theme ? Image(theme.ForFolder(folderName, expanded)?.ImagePath) : null;
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

    private static IImage? Image(string? path)
    {
        if (path is null)
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

    /// <summary>One of TabStudio's stroke icons (Icons.axaml) as an image, as <c>StrokeIcon</c> draws it.</summary>
    private static DrawingImage? SymbolImage(string geometryKey, string brushKey)
    {
        if (Application.Current is not { } application
            || !application.TryFindResource(geometryKey, out var geometry) || geometry is not Geometry symbol
            || !application.TryFindResource(brushKey, out var brush) || brush is not IBrush stroke)
        {
            return null;
        }

        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing { Brush = Brushes.Transparent, Geometry = new RectangleGeometry(new Rect(0, 0, 24, 24)) });
        group.Children.Add(new GeometryDrawing { Geometry = symbol, Pen = new Pen(stroke, 1.8, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round) });
        return new DrawingImage(group);
    }
}
