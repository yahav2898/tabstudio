using Avalonia.Data.Converters;
using Avalonia.Media;
using TabStudio.App.Theming;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Converters;

/// <summary>Colors of source control states, from the app's theme: amber for modified, green for added, red for deleted and conflicts.</summary>
public static class SourceControlConverters
{
    /// <summary>The color of a changed file's status letter.</summary>
    public static IValueConverter StatusBrush { get; } = new FuncValueConverter<GitFileStatus, IBrush?>(status => Find(status switch
    {
        GitFileStatus.Added => "Brush.Success",
        GitFileStatus.Modified => "Brush.Warning",
        _ => "Brush.Error",
    }));

    /// <summary>The color of a Solution Explorer row's glyph.</summary>
    public static IValueConverter GlyphBrush { get; } = new FuncValueConverter<ExplorerGitGlyph, IBrush?>(glyph => Find(glyph switch
    {
        ExplorerGitGlyph.Added => "Brush.Success",
        ExplorerGitGlyph.Conflict => "Brush.Error",
        _ => "Brush.Warning",
    }));

    private static IBrush? Find(string key) => ThemeBrushes.Find<IBrush>(key);
}
