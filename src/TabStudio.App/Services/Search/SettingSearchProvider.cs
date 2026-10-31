using TabStudio.App.Services.Appearance;

namespace TabStudio.App.Services.Search;

/// <summary>A setting the search can change.</summary>
/// <param name="Title">What it sets, such as "Color theme: Light".</param>
/// <param name="Description">What it does.</param>
/// <param name="Keywords">Other words for it, such as "dark mode".</param>
/// <param name="IsCurrent">Whether it is the setting in use.</param>
/// <param name="Apply">Changes the setting.</param>
public sealed record SettingEntry(string Title, string Description, IReadOnlyList<string> Keywords, Func<bool> IsCurrent, Func<Task> Apply);

/// <summary>Finds settings, such as the IDE's theme, and changes them in one tap.</summary>
/// <param name="settings">The settings.</param>
public sealed class SettingSearchProvider(IEnumerable<SettingEntry> settings) : ISearchProvider
{
    /// <inheritdoc />
    public SearchKind Kind => SearchKind.Setting;

    /// <inheritdoc />
    public Task<IReadOnlyList<SearchItem>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        IReadOnlyList<SearchItem> items = [.. settings.Select(setting =>
            new SearchItem(SearchKind.Setting, setting.Title, setting.Description, setting.Apply)
            {
                IconKey = "Icon.Gear",
                Keywords = setting.Keywords,
                Note = setting.IsCurrent() ? "Current" : null,
            })];
        return Task.FromResult(items);
    }

    /// <summary>The IDE's text size as settings: Follow the system, Small, Default, Large, Larger and Very large.</summary>
    /// <param name="appearance">The IDE's appearance, which keeps the text size.</param>
    public static IEnumerable<SettingEntry> TextSizes(AppearanceService appearance)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        string[] keywords = ["text size", "font size", "zoom", "larger text", "accessibility", "font scale"];
        foreach (var size in Accessibility.TextSizes.All)
        {
            var description = size == Accessibility.TextSize.System
                ? "Android's font size, from Settings > Display, changing with it"
                : $"{Math.Round(Accessibility.TextSizes.Factor(size, 1) * 100)}% text in the whole IDE and the editor";
            yield return new SettingEntry(
                $"Text size: {Accessibility.TextSizes.DisplayName(size)}",
                description,
                keywords,
                () => appearance.TextSize == size,
                () =>
                {
                    appearance.SetTextSize(size);
                    return Task.CompletedTask;
                });
        }
    }

    /// <summary>The IDE's theme as three settings: Dark, Light and Follow the system.</summary>
    /// <param name="appearance">The IDE's theme.</param>
    /// <param name="apply">
    /// Uses a theme, such as <see cref="Extensions.ExtensionsService.UseTabStudioTheme"/>, which also leaves an
    /// extension's color theme; <see langword="null"/> sets the IDE's theme alone.
    /// </param>
    public static IEnumerable<SettingEntry> Appearance(AppearanceService appearance, Action<IdeTheme>? apply = null)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        string[] keywords = ["theme", "appearance", "dark mode", "light mode", "colors"];
        foreach (var theme in new[] { IdeTheme.Dark, IdeTheme.Light, IdeTheme.System })
        {
            var description = theme switch
            {
                IdeTheme.Light => "TabStudio's light colors for the whole IDE, after Visual Studio's Light",
                IdeTheme.System => "Dark or light as the system is, changing with it",
                _ => "TabStudio's dark colors for the whole IDE",
            };
            yield return new SettingEntry(
                $"Color theme: {AppearanceService.DisplayName(theme)}",
                description,
                keywords,
                () => appearance.Theme == theme,
                () =>
                {
                    if (apply is null)
                    {
                        appearance.SetTheme(theme);
                    }
                    else
                    {
                        apply(theme);
                    }

                    return Task.CompletedTask;
                });
        }
    }
}
