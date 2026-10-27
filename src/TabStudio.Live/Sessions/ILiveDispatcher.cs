namespace TabStudio.Live.Sessions;

/// <summary>
/// The one thread a session's state lives on: its documents, participants and presence. In the app it is the UI
/// thread, where the editors are, so a remote edit and the editor's text change together.
/// </summary>
public interface ILiveDispatcher
{
    /// <summary>Runs work on the session's thread, after the work posted before it.</summary>
    /// <param name="action">The work.</param>
    void Post(Action action);
}
