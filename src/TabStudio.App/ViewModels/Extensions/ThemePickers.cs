using TabStudio.App.Services.Extensions;
using TabStudio.Extensions.Manifests;

namespace TabStudio.App.ViewModels.Extensions;

/// <summary>
/// The color theme and file icon theme pickers, as VS Code's: the installed extensions' themes, grouped by kind, with
/// TabStudio's own first. Moving through the color themes previews each in the open editors; Escape goes back.
/// </summary>
public static class ThemePickers
{
    /// <summary>The value of the item that opens the Extensions window to find more themes.</summary>
    public const string BrowseMore = "browse-more";

    /// <summary>Lets the user pick the editor's color theme.</summary>
    /// <param name="service">The extensions.</param>
    /// <param name="quickPick">The picker.</param>
    /// <param name="only">Only this extension's themes, or <see langword="null"/> for all; with one theme it is used at once.</param>
    /// <param name="browseMore">Opens the Extensions window on themes, offered at the end; <see langword="null"/> leaves it out.</param>
    public static async Task PickColorThemeAsync(ExtensionsService service, QuickPickViewModel quickPick, ExtensionIdentifier? only = null, Action? browseMore = null)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(quickPick);
        var themes = service.Contributions.Themes.Where(t => only is null || t.Owner == only.Value).ToList();
        if (only is not null && themes.Count == 1)
        {
            service.SelectColorTheme(themes[0].Key);
            return;
        }

        var current = service.Settings.ColorTheme is { } key && themes.Any(t => t.Key == key) ? key : null;
        var items = new List<QuickPickItem>();
        if (only is null)
        {
            items.Add(new QuickPickItem("TabStudio Dark", current is null ? "current" : null, "TabStudio's own editor colors", null, "TabStudio"));
        }

        foreach (var group in themes.GroupBy(t => KindName(t.Kind)).OrderBy(g => KindOrder(g.Key)))
        {
            items.AddRange(group
                .OrderBy(t => t.Label, StringComparer.CurrentCultureIgnoreCase)
                .Select(t => new QuickPickItem(t.Label, t.Key == current ? "current" : null, OwnerName(service, t.Owner), t.Key, group.Key)));
        }

        if (browseMore is not null)
        {
            items.Add(new QuickPickItem("Browse Color Themes on Open VSX…", null, null, BrowseMore, "More"));
        }

        var selected = items.FirstOrDefault(i => Equals(i.Value, current));
        var picked = await quickPick.ShowAsync(
            "Color Theme",
            "Select a color theme for the editor (arrow keys preview it)",
            items,
            selected,
            item =>
            {
                if (item is not null && !Equals(item.Value, BrowseMore))
                {
                    service.PreviewColorTheme(item.Value as string);
                }
            });

        if (picked is null)
        {
            service.PreviewColorTheme(service.Settings.ColorTheme);
        }
        else if (Equals(picked.Value, BrowseMore))
        {
            service.PreviewColorTheme(service.Settings.ColorTheme);
            browseMore?.Invoke();
        }
        else
        {
            service.SelectColorTheme(picked.Value as string);
        }
    }

    /// <summary>Lets the user pick the file icon theme of the Solution Explorer and tabs.</summary>
    /// <param name="service">The extensions.</param>
    /// <param name="quickPick">The picker.</param>
    /// <param name="only">Only this extension's icon themes, or <see langword="null"/> for all; with one it is used at once.</param>
    /// <param name="browseMore">Opens the Extensions window on themes, offered at the end; <see langword="null"/> leaves it out.</param>
    public static async Task PickIconThemeAsync(ExtensionsService service, QuickPickViewModel quickPick, ExtensionIdentifier? only = null, Action? browseMore = null)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(quickPick);
        // Themes that draw their icons with a font cannot be shown, so they are not offered.
        var themes = service.Contributions.IconThemes.Where(t => (only is null || t.Owner == only.Value) && service.CanDraw(t)).ToList();
        if (only is not null && themes.Count == 0)
        {
            return;
        }

        if (only is not null && themes.Count == 1)
        {
            service.SelectIconTheme(themes[0].Key);
            return;
        }

        var current = service.Settings.IconTheme is { } key && themes.Any(t => t.Key == key) ? key : null;
        var items = new List<QuickPickItem>();
        if (only is null)
        {
            items.Add(new QuickPickItem("None", current is null ? "current" : null, "TabStudio's file badges", null, "TabStudio"));
        }

        items.AddRange(themes
            .OrderBy(t => t.Label, StringComparer.CurrentCultureIgnoreCase)
            .Select(t => new QuickPickItem(t.Label, t.Key == current ? "current" : null, OwnerName(service, t.Owner), t.Key, "File icon themes")));
        if (browseMore is not null)
        {
            items.Add(new QuickPickItem("Browse File Icon Themes on Open VSX…", null, null, BrowseMore, "More"));
        }

        var picked = await quickPick.ShowAsync(
            "File Icon Theme",
            "Select the icons of files in the Solution Explorer and on tabs",
            items,
            items.FirstOrDefault(i => Equals(i.Value, current)));
        if (picked is null)
        {
            return;
        }

        if (Equals(picked.Value, BrowseMore))
        {
            browseMore?.Invoke();
        }
        else
        {
            service.SelectIconTheme(picked.Value as string);
        }
    }

    private static string OwnerName(ExtensionsService service, ExtensionIdentifier owner) =>
        service.Store.Find(owner)?.Manifest.DisplayName ?? owner.ToString();

    private static string KindName(ColorThemeKind kind) => kind switch
    {
        ColorThemeKind.Light => "Light themes",
        ColorThemeKind.HighContrastDark or ColorThemeKind.HighContrastLight => "High contrast themes",
        _ => "Dark themes",
    };

    private static int KindOrder(string group) => group switch
    {
        "Dark themes" => 0,
        "Light themes" => 1,
        _ => 2,
    };
}
