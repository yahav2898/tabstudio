using System.ComponentModel;

namespace Microsoft.VisualBasic.Devices;

/// <summary>
/// Sound output (<c>My.Computer.Audio</c>). TabStudio's app preview has none: <c>Play</c> throws
/// <see cref="NotSupportedException"/> after checking its arguments, and <see cref="Stop"/> does nothing.
/// </summary>
public class Audio
{
    /// <summary>Creates the audio object.</summary>
    public Audio()
    {
    }

    /// <summary>Plays a .wav file in the background. Not supported: the app preview has no sound output.</summary>
    /// <param name="location">The file.</param>
    /// <exception cref="NotSupportedException">Always, after the arguments are checked.</exception>
    public void Play(string location) => Play(location, AudioPlayMode.Background);

    /// <summary>Plays a .wav file. Not supported: the app preview has no sound output.</summary>
    /// <param name="location">The file.</param>
    /// <param name="playMode">How to play it.</param>
    /// <exception cref="NotSupportedException">Always, after the arguments are checked.</exception>
    public void Play(string location, AudioPlayMode playMode)
    {
        ValidateAudioPlayModeEnum(playMode, nameof(playMode));
        ArgumentException.ThrowIfNullOrEmpty(location);
        throw NoSound();
    }

    /// <summary>Plays a .wav file's bytes. Not supported: the app preview has no sound output.</summary>
    /// <param name="data">The file's bytes.</param>
    /// <param name="playMode">How to play it.</param>
    /// <exception cref="NotSupportedException">Always, after the arguments are checked.</exception>
    public void Play(byte[] data, AudioPlayMode playMode)
    {
        ArgumentNullException.ThrowIfNull(data);
        ValidateAudioPlayModeEnum(playMode, nameof(playMode));
        throw NoSound();
    }

    /// <summary>Plays a .wav stream. Not supported: the app preview has no sound output.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="playMode">How to play it.</param>
    /// <exception cref="NotSupportedException">Always, after the arguments are checked.</exception>
    public void Play(Stream stream, AudioPlayMode playMode)
    {
        ValidateAudioPlayModeEnum(playMode, nameof(playMode));
        ArgumentNullException.ThrowIfNull(stream);
        throw NoSound();
    }

    /// <summary>Stops the sound playing in the background: there is none, so it does nothing.</summary>
    public void Stop()
    {
        // Nothing plays in the app preview.
    }

    private static void ValidateAudioPlayModeEnum(AudioPlayMode value, string paramName)
    {
        if (value is < AudioPlayMode.WaitToComplete or > AudioPlayMode.BackgroundLoop)
        {
            throw new InvalidEnumArgumentException(paramName, (int)value, typeof(AudioPlayMode));
        }
    }

    private static NotSupportedException NoSound() =>
        Unsupported.Member("My.Computer.Audio.Play", "TabStudio's app preview has no sound output. Show the information on a form instead.");
}
