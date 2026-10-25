using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace TabStudio.App.Converters;

/// <summary>Value converters of desktop mode's companion.</summary>
public static class DesktopModeConverters
{
    /// <summary>The icon of a Toolbox item, by the name its framework file gives it, or a plain control icon.</summary>
    public static IValueConverter ToolboxIcon { get; } = new FuncValueConverter<string?, Geometry?>(icon =>
        Find("Icon.Toolbox." + icon) ?? Find("Icon.Toolbox.Control"));

    /// <summary>Whether a Toolbox item's icon is in the theme, for tests of the framework files.</summary>
    /// <param name="icon">The icon's name.</param>
    public static bool HasToolboxIcon(string icon) => Find("Icon.Toolbox." + icon) is not null;

    private static Geometry? Find(string key) =>
        Application.Current?.TryGetResource(key, Application.Current.ActualThemeVariant, out var value) == true ? value as Geometry : null;
}
