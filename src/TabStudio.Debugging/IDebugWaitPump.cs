namespace TabStudio.Debugging;

/// <summary>
/// Keeps a host's UI alive while the debugged program is paused on the host's own UI thread, as an Avalonia app shown
/// in TabStudio is: instead of blocking that thread, the debugger lets the host run its message loop until the program
/// continues, so the screen keeps drawing and the host can say that the app is paused.
/// </summary>
/// <remarks>
/// The program's own code is still paused: a host that pumps must keep input away from the app until it continues.
/// Code of the program that runs while the loop is pumped, such as a timer of the app, pauses again at its first
/// statement, inside the pump.
/// </remarks>
public interface IDebugWaitPump
{
    /// <summary>Whether the calling thread is one the host keeps alive while it waits, such as its UI thread.</summary>
    bool CanPumpCurrentThread { get; }

    /// <summary>
    /// Runs the host's message loop on the calling thread for as long as <paramref name="isWaiting"/> returns
    /// <see langword="true"/>. The debugger calls <see cref="Wake"/> whenever the answer may have changed.
    /// </summary>
    /// <param name="isWaiting">Whether the thread must still wait. Thread-safe; cheap to call.</param>
    void PumpWhile(Func<bool> isWaiting);

    /// <summary>Tells a thread inside <see cref="PumpWhile"/> to ask again whether it must still wait. Any thread may call it.</summary>
    void Wake();
}
