namespace TabStudio.Agent.Local;

/// <summary>The background inference policy, independent of Android APIs for testing.</summary>
public static class InferencePolicy
{
    /// <summary>Why completions pause; an unknown battery level does not imply low battery.</summary>
    public static string? PauseReason(bool hot, int batteryPercent, bool charging) => hot
        ? "Inline completions paused: device is hot."
        : batteryPercent is >= 0 and <= 15 && !charging ? "Inline completions paused: battery is low." : null;
}
