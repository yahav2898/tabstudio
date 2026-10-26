namespace TabStudio.Porting;

/// <summary>What a file of the new project is.</summary>
public enum PortedFileKind
{
    /// <summary>An Avalonia XAML file (<c>.axaml</c>).</summary>
    Xaml,

    /// <summary>C# code.</summary>
    Code,

    /// <summary>The project file.</summary>
    Project,

    /// <summary>Code or styles the port adds so the app runs the same on Avalonia, such as message boxes.</summary>
    Support,

    /// <summary>A file copied as it is, such as an image.</summary>
    Resource,
}

/// <summary>One kind of change the converter made in a file, and how often.</summary>
/// <param name="Description">What changed, such as <c>Visibility → IsVisible</c>.</param>
/// <param name="Count">How many times.</param>
public sealed record PortConversion(string Description, int Count);

/// <summary>A file of the new project.</summary>
/// <param name="Path">The file relative to the new project's folder, with <c>/</c>.</param>
/// <param name="Text">The file's text, or <see langword="null"/> for a file copied from <paramref name="CopyFrom"/>.</param>
/// <param name="CopyFrom">For a copied file, its full path in the original project.</param>
/// <param name="SourcePath">The original file it was converted from, relative to the original project, or <see langword="null"/> for a new file.</param>
/// <param name="Kind">What the file is.</param>
/// <param name="Conversions">What the converter changed in it.</param>
public sealed record PortedFile(
    string Path,
    string? Text,
    string? CopyFrom,
    string? SourcePath,
    PortedFileKind Kind,
    IReadOnlyList<PortConversion> Conversions)
{
    /// <summary>How many changes the converter made in the file.</summary>
    public int ConversionCount => Conversions.Sum(c => c.Count);
}
