using Avalonia.Data.Converters;
using Avalonia.Media;
using TabStudio.App.Theming;

namespace TabStudio.App.Converters;

/// <summary>Value converters of the shell's new pieces: the search, the activity bar and the panel states.</summary>
public static class ShellConverters
{
    /// <summary>The status bar's branch: its name, and the commits waiting to be pulled, such as "main ↓2".</summary>
    public static IMultiValueConverter BranchLabel { get; } = new FuncMultiValueConverter<object?, string>(values =>
    {
        var list = values.ToList();
        var name = list.Count > 0 ? list[0] as string ?? string.Empty : string.Empty;
        return list.Count > 1 && list[1] is int incoming && incoming > 0
            ? $"{name} ↓{incoming.ToString(System.Globalization.CultureInfo.CurrentCulture)}"
            : name;
    });

    /// <summary>An icon by its resource key, such as <c>Icon.File</c>.</summary>
    public static IValueConverter IconByKey { get; } = new FuncValueConverter<string?, Geometry?>(key =>
        key is null ? null : ThemeBrushes.Find<Geometry>(key));
}
