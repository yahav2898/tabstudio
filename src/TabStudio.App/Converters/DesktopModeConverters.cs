using Avalonia.Data.Converters;
using Avalonia.Media;
using TabStudio.App.Theming;

namespace TabStudio.App.Converters;

/// <summary>Value converters of desktop mode's companion.</summary>
public static class DesktopModeConverters
{
    /// <summary>The icon of a Toolbox item, by the name its framework file gives it, or a plain control icon.</summary>
    public static IValueConverter ToolboxIcon { get; } = new FuncValueConverter<string?, Geometry?>(icon =>
        Find("Icon.Toolbox." + icon) ?? Find("Icon.Toolbox.Control"));

    /// <summary>
    /// Whether the editor's floating touch buttons show: while editing or debugging (the first two values), except
    /// where the keyboard is preferred (the third), as on the external display in desktop mode, where a keyboard and
    /// a mouse are used and the tablet's companion has the big buttons.
    /// </summary>
    public static IMultiValueConverter TouchBarsVisible { get; } = new FuncMultiValueConverter<bool, bool>(values =>
    {
        var (editing, debugging, prefersKeyboard) = values.ToList() switch
        {
            [var first, var second, var third] => (first, second, third),
            _ => (false, false, false),
        };
        return (editing || debugging) && !prefersKeyboard;
    });

    /// <summary>Whether a Toolbox item's icon is in the theme, for tests of the framework files.</summary>
    /// <param name="icon">The icon's name.</param>
    public static bool HasToolboxIcon(string icon) => Find("Icon.Toolbox." + icon) is not null;

    private static Geometry? Find(string key) => ThemeBrushes.Find<Geometry>(key);
}
