using TabStudio.Compilation.Running;

namespace TabStudio.Swarm.Device;

/// <summary>
/// What a device's app provides to the swarm: its screen, showing the apps the tablet sends, and its app preview's
/// answers to the tablet's requests about the screen. Console apps run in the library itself.
/// </summary>
/// <remarks>Members may be called from any thread.</remarks>
public interface ISwarmDeviceHost
{
    /// <summary>How the device's screen looks to the app now.</summary>
    SwarmScreenInfo Screen { get; }

    /// <summary>The device's system, such as <c>Android 14</c>, as the tablet shows it.</summary>
    string Platform { get; }

    /// <summary>
    /// Shows an Avalonia app full screen, or a web app in a web view, until the returned run is disposed. An Avalonia
    /// app with a <see cref="SwarmApp.Debugger"/> is connected to it, with <c>DebugSession.Attach</c>, once its assemblies
    /// are loaded and before its code runs.
    /// </summary>
    /// <param name="app">The app.</param>
    /// <param name="cancellationToken">Gives up starting it.</param>
    /// <returns>The running app.</returns>
    Task<ISwarmAppRun> StartAsync(SwarmApp app, CancellationToken cancellationToken);

    /// <summary>Replays an input on the app shown, as if a person made it.</summary>
    /// <param name="input">The input.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Whether the element was found and the input delivered.</returns>
    Task<bool> SendInputAsync(SwarmInput input, CancellationToken cancellationToken);

    /// <summary>Takes a picture of the app shown.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A PNG image, or <see langword="null"/> while no app is shown.</returns>
    Task<byte[]?> CaptureAsync(CancellationToken cancellationToken);

    /// <summary>Runs the layout checks on the app shown.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The problems found, most serious first.</returns>
    Task<IReadOnlyList<SwarmLayoutIssue>> InspectLayoutAsync(CancellationToken cancellationToken);

    /// <summary>Reads the text an element of the app shown displays, for the checks of recorded tests.</summary>
    /// <param name="elementPath">The element.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The text, or <see langword="null"/> when there is no such element.</returns>
    Task<string?> ReadTextAsync(string elementPath, CancellationToken cancellationToken);

    /// <summary>Shows a short message on the screen for a moment, such as an input mirrored from another device that could not be delivered here.</summary>
    /// <param name="text">The message.</param>
    void ShowNotice(string text);
}

/// <summary>An app a device shows for the swarm. Disposing it stops it.</summary>
public interface ISwarmAppRun : IAsyncDisposable
{
    /// <summary>Completes when the app ends by itself: when it crashes, with the exception.</summary>
    Task<RunResult> Completion { get; }
}
