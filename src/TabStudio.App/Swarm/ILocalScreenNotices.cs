namespace TabStudio.App.Swarm;

/// <summary>
/// A screen on this tablet that shows its notices (<see cref="IAppScreenNotices"/>) in TabStudio itself: on its tile in
/// the Device Matrix, and on the pane that shows it, such as the app preview.
/// </summary>
public interface ILocalScreenNotices
{
    /// <summary>Raised on the UI thread for every notice shown on the screen.</summary>
    event EventHandler<string>? NoticeShown;
}
