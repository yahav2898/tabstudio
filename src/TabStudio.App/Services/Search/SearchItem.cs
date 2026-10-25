using TabStudio.Compilation.Features;

namespace TabStudio.App.Services.Search;

/// <summary>What a search result is, which also orders the groups of results when they score alike.</summary>
public enum SearchKind
{
    /// <summary>A file of the open solution or folder.</summary>
    File,

    /// <summary>A command from the menus, with its key gesture.</summary>
    Command,

    /// <summary>A setting, such as the color theme.</summary>
    Setting,

    /// <summary>A type or member in the code.</summary>
    Symbol,

    /// <summary>A project template.</summary>
    Template,

    /// <summary>A solution or folder opened recently.</summary>
    Workspace,
}

/// <summary>Something the search can find and open.</summary>
/// <param name="Kind">What it is.</param>
/// <param name="Title">Its name, which the query is matched against first.</param>
/// <param name="Detail">Where it is or what it does, such as a folder or a menu path; matched with less weight.</param>
/// <param name="Open">Opens it: the file, the command, the setting.</param>
public sealed record SearchItem(SearchKind Kind, string Title, string? Detail, Func<Task> Open)
{
    /// <summary>Identifies it across searches, for remembering what was opened.</summary>
    public string Id => $"{Kind}:{Title}:{Detail}";

    /// <summary>A command's key gesture, such as "Ctrl+Shift+B", shown as key caps.</summary>
    public string? Gesture { get; init; }

    /// <summary>The resource key of its icon, such as <c>Icon.File</c>; symbols use <see cref="Symbol"/>.</summary>
    public string? IconKey { get; init; }

    /// <summary>A symbol's glyph, as IntelliSense draws it.</summary>
    public SymbolGlyph? Symbol { get; init; }

    /// <summary>More words it answers to, such as a setting's synonyms or a template's tags.</summary>
    public IReadOnlyList<string> Keywords { get; init; } = [];

    /// <summary>A short note on the right, such as "Current" for the setting in use.</summary>
    public string? Note { get; init; }

    /// <summary>Whether it can be opened now; a command that does not apply shows, dimmed, but does not run.</summary>
    public bool IsEnabled { get; init; } = true;
}
