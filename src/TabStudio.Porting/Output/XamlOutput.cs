using TabStudio.Porting.Wpf;
using TabStudio.Porting.Xaml;

namespace TabStudio.Porting.Output;

/// <summary>Writes converted XAML and gives its port notes the lines of their TODO comments.</summary>
internal static class XamlOutput
{
    /// <summary>The file of the new project for converted XAML.</summary>
    /// <param name="xaml">The converted XAML.</param>
    public static PortedFile Write(ConvertedXaml xaml)
    {
        var text = XamlWriter.Write(xaml.Document, xaml.Format, out var lines);
        var notes = xaml.Report.Notes;
        for (var i = 0; i < notes.Count; i++)
        {
            if (notes[i].XamlAnchor is { } anchor)
            {
                notes[i] = notes[i] with { Line = lines.TryGetValue(anchor, out var line) ? line : 1, XamlAnchor = null };
            }
        }

        return new PortedFile(xaml.Path, text, null, xaml.SourcePath, PortedFileKind.Xaml, xaml.Report.Conversions());
    }
}
