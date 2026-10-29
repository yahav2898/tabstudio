namespace Microsoft.VisualBasic;

/// <summary>How <c>My.Computer.Audio.Play</c> plays a sound.</summary>
public enum AudioPlayMode
{
    /// <summary>Plays the sound and waits until it ends.</summary>
    WaitToComplete = 0,

    /// <summary>Plays the sound in the background.</summary>
    Background = 1,

    /// <summary>Plays the sound in the background until it is stopped.</summary>
    BackgroundLoop = 2,
}
