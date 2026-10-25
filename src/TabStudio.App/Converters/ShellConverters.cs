using Avalonia.Data.Converters;
using Avalonia.Media;
using TabStudio.App.Theming;

namespace TabStudio.App.Converters;

/// <summary>Value converters of the shell's new pieces: the search, the activity bar and the panel states.</summary>
public static class ShellConverters
{
    /// <summary>An icon by its resource key, such as <c>Icon.File</c>.</summary>
    public static IValueConverter IconByKey { get; } = new FuncValueConverter<string?, Geometry?>(key =>
        key is null ? null : ThemeBrushes.Find<Geometry>(key));
}
