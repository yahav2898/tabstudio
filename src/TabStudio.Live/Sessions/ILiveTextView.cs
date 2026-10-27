using TabStudio.Live.Text;

namespace TabStudio.Live.Sessions;

/// <summary>The editor that shows a <see cref="LiveDocument"/> on this device. Called on the session's thread.</summary>
public interface ILiveTextView
{
    /// <summary>Shows edits other devices made, or an undo of this device's, without taking them as new edits.</summary>
    /// <param name="changes">The changes, in the order to apply them.</param>
    void ApplyRemote(IReadOnlyList<TextChange> changes);

    /// <summary>Replaces the whole text, after the document was loaded again from the host.</summary>
    /// <param name="text">The text.</param>
    void Reload(string text);
}
