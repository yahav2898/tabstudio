using System.Text;
using TabStudio.App.Editing;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.ViewModels.Extensions;
using TabStudio.Extensions.Snippets;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Extensions and documents: a file's type follows the installed extensions (a file an extension claims gets its
/// language, grammar and icon as soon as the extension is installed), and the Extensions window opens as a tab.
/// </summary>
public sealed partial class DocumentViewModel
{
    private static readonly FileType ExtensionsPageType = new("Extensions", "EXT", BadgeKind.Markup, null, null, null);

    private DocumentViewModel(ExtensionsViewModel page, string tabPath, DeviceProfile profile)
        : this(tabPath, new TextFileContent(string.Empty, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)), language: null, profile)
    {
        ExtensionsPage = page;
        FileType = ExtensionsPageType;
        Document.UndoStack.SizeLimit = 0;
    }

    /// <summary>Raised after <see cref="FileType"/> changed, because an extension that claims the file was installed or removed.</summary>
    public event EventHandler? FileTypeChanged;

    /// <summary>Raised when a snippet should be inserted at the caret, as Insert Snippet does; the editor inserts it.</summary>
    public event EventHandler<SnippetDefinition>? SnippetInsertRequested;

    /// <summary>For the Extensions tab, the Extensions window; <see langword="null"/> for a file.</summary>
    public ExtensionsViewModel? ExtensionsPage { get; }

    /// <summary>Whether this is the Extensions tab rather than a file.</summary>
    public bool IsExtensionsPage => ExtensionsPage is not null;

    /// <summary>Creates the Extensions tab. Call on the UI thread.</summary>
    /// <param name="page">The Extensions window.</param>
    /// <param name="tabPath">The path the tab is known by; it never exists on disk, so sessions do not reopen it.</param>
    /// <param name="profile">The device profile.</param>
    public static DocumentViewModel ForExtensionsPage(ExtensionsViewModel page, string tabPath, DeviceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentException.ThrowIfNullOrEmpty(tabPath);
        ArgumentNullException.ThrowIfNull(profile);
        return new DocumentViewModel(page, tabPath, profile);
    }

    /// <summary>Asks the editor to insert a snippet at the caret, around the selection, and start its tab stops.</summary>
    /// <param name="snippet">The snippet.</param>
    public void InsertSnippet(SnippetDefinition snippet)
    {
        ArgumentNullException.ThrowIfNull(snippet);
        SnippetInsertRequested?.Invoke(this, snippet);
    }

    /// <summary>
    /// Looks up the file's type again, after the installed extensions changed, and tells the editor when it is
    /// different. C# files, notebooks, comparisons, logs and the Extensions tab keep theirs.
    /// </summary>
    public void RefreshFileType()
    {
        if (_disposed || IsExtensionsPage || IsCiLog || IsComparison || IsNotebook || FileType.IsCSharp)
        {
            return;
        }

        var type = FileTypes.For(FilePath);
        if (type == FileType)
        {
            return;
        }

        FileType = type;
        OnPropertyChanged(nameof(FileType));
        FileTypeChanged?.Invoke(this, EventArgs.Empty);
    }
}
