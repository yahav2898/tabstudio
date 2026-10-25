namespace TabStudio.App.Swarm;

/// <summary>
/// A screen that can tell what an element shows, for the checks of recorded UI tests ("this text shows"). An addition
/// to <see cref="IAppScreen"/>, which every screen of the registry may implement: the swarm's device screens do, and the
/// preview and virtual devices should, through the same element paths as their input.
/// </summary>
public interface IAppScreenElements
{
    /// <summary>Reads the text an element shows: a text block's or text box's text, or the texts inside it.</summary>
    /// <param name="elementPath">The element, in <see cref="AppInput.ElementPath"/>'s form.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The text, or <see langword="null"/> when there is no such element or the app is not running.</returns>
    Task<string?> ReadTextAsync(string elementPath, CancellationToken cancellationToken);
}

/// <summary>
/// A screen that can show a short message for a moment, such as an input mirrored from another screen that could not
/// be delivered there. An addition to <see cref="IAppScreen"/>: the swarm's device screens show it on the device, and the
/// preview and virtual devices may show it on their tile.
/// </summary>
public interface IAppScreenNotices
{
    /// <summary>Shows a short message on the screen for a few seconds.</summary>
    /// <param name="text">The message.</param>
    void ShowNotice(string text);
}

/// <summary>
/// A screen that can start the app again, fresh, as a recorded UI test starts it before its first step. An addition to
/// <see cref="IAppScreen"/>: the swarm's device screens do it on the device, and the preview and virtual devices should.
/// A screen without it replays recorded tests on the app as it is.
/// </summary>
public interface IAppScreenRestart
{
    /// <summary>Starts the app again and waits until it shows.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Whether the app started again.</returns>
    Task<bool> RestartAppAsync(CancellationToken cancellationToken);
}
