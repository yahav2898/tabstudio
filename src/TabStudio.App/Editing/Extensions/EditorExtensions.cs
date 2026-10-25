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

    /// <summary>Raised after the contributions or a theme changed.</summary>
    public static event EventHandler? Changed;

    /// <summary>What the enabled extensions contribute.</summary>
    public static ContributionSet Contributions { get; private set; } = ContributionSet.Empty;

    /// <summary>The chosen color theme, or <see langword="null"/> for TabStudio's own.</summary>
    public static EditorColorTheme? Theme { get; private set; }

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
        var themeChanged = !ReferenceEquals(Theme, theme);
        _owner = owner;
        Contributions = contributions;
        Theme = theme;
        IconTheme = iconTheme;
        FileTypes.Contributed = ContributedFileTypes.For;
        TextMateHighlighting.Contributions = ExtensionTextMate.Instance;
        if (themeChanged)
        {
            TextMateHighlighting.RefreshTheme();
        }

        Changed?.Invoke(null, EventArgs.Empty);
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
