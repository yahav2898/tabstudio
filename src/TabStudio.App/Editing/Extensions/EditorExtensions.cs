using Avalonia.Styling;
using TabStudio.App.Theming;
using TabStudio.Extensions.Contributions;
using TabStudio.Extensions.Languages;
using TabStudio.Extensions.Snippets;
using TabStudio.Extensions.Themes;

namespace TabStudio.App.Editing.Extensions;

/// <summary>
/// What installed extensions contribute to the editor, for every editor in the app: languages and their grammars,
/// language configurations and snippets, the chosen color theme and the chosen file icon theme. The extensions service
/// publishes it; editors, file types and TextMate highlighting read it.
/// </summary>
/// <remarks>
/// Extensions are installed once for the whole app, so this is app-wide state, as <see cref="FileTypes"/> and
/// <see cref="TextMateHighlighting"/> are. Publish and read it on the UI thread.
/// </remarks>
public static class EditorExtensions
{
    private static object? _owner;
    private static EditorColorTheme? _chosenTheme;

    /// <summary>Raised after the contributions or a theme changed.</summary>
    public static event EventHandler? Changed;

    /// <summary>What the enabled extensions contribute.</summary>
    public static ContributionSet Contributions { get; private set; } = ContributionSet.Empty;

    /// <summary>
    /// The editor's color theme: the one chosen from an extension, or else TabStudio's own for the IDE's theme, which is
    /// <see cref="BuiltInEditorThemes.Light"/> while the IDE is light and <see langword="null"/> (the editor's built-in
    /// dark colors) while it is dark.
    /// </summary>
    public static EditorColorTheme? Theme => _chosenTheme ?? BuiltInTheme;

    /// <summary>TabStudio's own editor theme for the IDE's theme, used when no extension's theme is chosen.</summary>
    public static EditorColorTheme? BuiltInTheme { get; private set; }

    /// <summary>The chosen file icon theme, or <see langword="null"/> for TabStudio's badges.</summary>
    public static FileIconTheme? IconTheme { get; private set; }

    /// <summary>Publishes what the extensions contribute, replacing what was published before.</summary>
    /// <param name="owner">The publisher; only it can withdraw what it published.</param>
    /// <param name="contributions">The contributions of the enabled extensions.</param>
    /// <param name="theme">The chosen color theme, or <see langword="null"/> for TabStudio's own.</param>
    /// <param name="iconTheme">The chosen file icon theme, or <see langword="null"/>.</param>
    public static void Publish(object owner, ContributionSet contributions, EditorColorTheme? theme, FileIconTheme? iconTheme)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(contributions);
        var before = Theme;
        _owner = owner;
        Contributions = contributions;
        _chosenTheme = theme;
        IconTheme = iconTheme;
        FileTypes.Contributed = ContributedFileTypes.For;
        TextMateHighlighting.Contributions = ExtensionTextMate.Instance;
        OnThemeChanged(before);
        Changed?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// Sets TabStudio's own editor theme for the IDE's theme: <see cref="BuiltInEditorThemes.Light"/> while the IDE is
    /// light, <see langword="null"/> while it is dark. An extension's chosen theme still wins.
    /// </summary>
    /// <param name="theme">The theme, or <see langword="null"/> for the editor's built-in dark colors.</param>
    public static void UseBuiltInTheme(EditorColorTheme? theme)
    {
        if (ReferenceEquals(BuiltInTheme, theme))
        {
            return;
        }

        var before = Theme;
        BuiltInTheme = theme;
        TextMateHighlighting.Contributions ??= ExtensionTextMate.Instance;
        OnThemeChanged(before);
        Changed?.Invoke(null, EventArgs.Empty);
    }

    private static void OnThemeChanged(EditorColorTheme? before)
    {
        // Margins, squiggles and the debugger's lines take their colors from the editor's theme, dark or light.
        ThemeBrushes.SetEditorVariant(Theme is { } theme ? (theme.Source.IsDark ? ThemeVariant.Dark : ThemeVariant.Light) : null);
        if (!ReferenceEquals(before, Theme))
        {
            TextMateHighlighting.RefreshTheme();
        }
    }

    /// <summary>Withdraws what an owner published, as when the app's services are disposed.</summary>
    /// <param name="owner">The publisher.</param>
    public static void Withdraw(object owner)
    {
        if (ReferenceEquals(_owner, owner))
        {
            Publish(owner, ContributionSet.Empty, null, null);
            _owner = null;
        }
    }

    /// <summary>The VS Code language identifier of a file: the one its extension gives it, or TabStudio's own for its type.</summary>
    /// <param name="type">The file's type.</param>
    /// <param name="path">The file's path.</param>
    public static string? LanguageIdOf(FileType type, string path)
    {
        ArgumentNullException.ThrowIfNull(type);
        return type.LanguageId ?? BuiltInLanguages.For(path);
    }

    /// <summary>The language configuration an extension contributes for a file's language, or <see langword="null"/>.</summary>
    /// <param name="type">The file's type.</param>
    /// <param name="path">The file's path.</param>
    public static LanguageConfiguration? ConfigurationFor(FileType type, string path) =>
        LanguageIdOf(type, path) is { } language ? Contributions.FindConfiguration(language) : null;

    /// <summary>The snippets extensions contribute for a file's language.</summary>
    /// <param name="type">The file's type.</param>
    /// <param name="path">The file's path.</param>
    public static IReadOnlyList<SnippetDefinition> SnippetsFor(FileType type, string path) =>
        LanguageIdOf(type, path) is { } language ? Contributions.FindSnippets(language) : [];
}
