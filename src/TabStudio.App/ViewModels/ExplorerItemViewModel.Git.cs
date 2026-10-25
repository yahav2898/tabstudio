using CommunityToolkit.Mvvm.ComponentModel;

namespace TabStudio.App.ViewModels;

/// <summary>The source control glyph at the end of a Solution Explorer row.</summary>
public enum ExplorerGitGlyph
{
    /// <summary>Nothing changed.</summary>
    None,

    /// <summary>The file changed.</summary>
    Modified,

    /// <summary>The file is new.</summary>
    Added,

    /// <summary>The file has a merge conflict.</summary>
    Conflict,

    /// <summary>A folder, project or solution with changed files inside.</summary>
    ContainsChanges,
}

/// <summary>The row's source control glyph.</summary>
public sealed partial class ExplorerItemViewModel
{
    /// <summary>The source control glyph: a colored letter for a changed file, a dot for a folder with changes inside.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGitGlyph), nameof(HasGitLetter), nameof(HasGitDot), nameof(GitGlyphText), nameof(GitGlyphTip))]
    public partial ExplorerGitGlyph GitGlyph { get; set; }

    /// <summary>Whether the row shows a source control glyph.</summary>
    public bool HasGitGlyph => GitGlyph != ExplorerGitGlyph.None;

    /// <summary>Whether the row shows a status letter.</summary>
    public bool HasGitLetter => GitGlyph is ExplorerGitGlyph.Modified or ExplorerGitGlyph.Added or ExplorerGitGlyph.Conflict;

    /// <summary>Whether the row shows the dot of a folder with changes inside.</summary>
    public bool HasGitDot => GitGlyph == ExplorerGitGlyph.ContainsChanges;

    /// <summary>The status letter: M, A or !.</summary>
    public string GitGlyphText => GitGlyph switch
    {
        ExplorerGitGlyph.Modified => "M",
        ExplorerGitGlyph.Added => "A",
        ExplorerGitGlyph.Conflict => "!",
        _ => string.Empty,
    };

    /// <summary>The glyph's meaning, for its tooltip.</summary>
    public string? GitGlyphTip => GitGlyph switch
    {
        ExplorerGitGlyph.Modified => "Modified",
        ExplorerGitGlyph.Added => "Added",
        ExplorerGitGlyph.Conflict => "Merge conflict",
        ExplorerGitGlyph.ContainsChanges => "Contains changes",
        _ => null,
    };
}
