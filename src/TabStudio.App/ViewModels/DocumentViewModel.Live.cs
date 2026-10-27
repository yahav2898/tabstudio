using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TabStudio.App.Services.Live;
using TabStudio.App.ViewModels.Live;

namespace TabStudio.App.ViewModels;

/// <summary>
/// A document shared in a TabStudio Live session: the binder that keeps its text in step with everyone's, whose undo
/// is this participant's own, and the avatars of the participants who show it, for its tab.
/// </summary>
public sealed partial class DocumentViewModel
{
    /// <summary>The binder to the shared document, while the document is shared in a session.</summary>
    public LiveDocumentBinder? Live { get; private set; }

    /// <summary>The other participants who show this document, for the avatars on its tab.</summary>
    public ObservableCollection<LiveAvatarViewModel> LiveAvatars { get; } = [];

    /// <summary>Whether another participant shows this document.</summary>
    [ObservableProperty]
    public partial bool HasLiveAvatars { get; private set; }

    /// <summary>Whether this participant may only watch this document: its editor takes no typing.</summary>
    [ObservableProperty]
    public partial bool IsLiveReadOnly { get; private set; }

    /// <summary>Raised when the document starts or stops being shared, so the editor shows or hides the others' carets.</summary>
    public event EventHandler? LiveChanged;

    /// <summary>Starts or stops sharing the document through a binder.</summary>
    /// <param name="binder">The binder, or <see langword="null"/> when the session ended.</param>
    internal void SetLive(LiveDocumentBinder? binder)
    {
        Live = binder;
        IsLiveReadOnly = binder?.IsReadOnly ?? false;
        if (binder is null)
        {
            ShowLiveAvatars([]);
            CanUndo = Document.UndoStack.CanUndo;
            CanRedo = Document.UndoStack.CanRedo;
        }

        LiveChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The binder's undo changed: what Undo and Redo can do now.</summary>
    internal void SetLiveUndo(bool canUndo, bool canRedo)
    {
        CanUndo = canUndo;
        CanRedo = canRedo;
    }

    /// <summary>Whether this participant may edit changed.</summary>
    internal void SetLiveReadOnly(bool isReadOnly) => IsLiveReadOnly = isReadOnly;

    /// <summary>The shared text changed: the document has changes the file does not.</summary>
    internal void MarkLiveEdited() => IsDirty = true;

    /// <summary>The host saved the document: it matches its file again.</summary>
    internal void MarkLiveSaved()
    {
        Document.UndoStack.MarkAsOriginalFile();
        IsDirty = false;
    }

    /// <summary>Shows the participants who show this document on its tab.</summary>
    internal void ShowLiveAvatars(IReadOnlyList<LiveAvatarViewModel> avatars)
    {
        if (LiveAvatars.SequenceEqual(avatars))
        {
            return;
        }

        LiveAvatars.Clear();
        foreach (var avatar in avatars)
        {
            LiveAvatars.Add(avatar);
        }

        HasLiveAvatars = LiveAvatars.Count > 0;
    }
}
